"""Yerel faster-whisper modeliyle Türkçe konuşma tanıma.

Uzun kayıtlar (> TEK_PARCA_SINIRI_SN) ~PARCA_SN'lik parçalara bölünür: faster-whisper tüm sesin
özniteliklerini tek seferde bellekte tuttuğundan bellek kayıt süresiyle orantılı büyür (ölçüldü:
49 dk 3,1 GB, 3 saat 10,5 GB). Parçalı işlemde bellek kayıt süresinden bağımsızdır.
"""
import logging
import os
import wave
from dataclasses import dataclass
from pathlib import Path

from . import yollar
from .ayar import Ayar
from .cihaz import CihazSecimi
from .hatalar import IsHatasi

PARCA_SN = 1200.0            # parça hedef uzunluğu (20 dk)
TEK_PARCA_SINIRI_SN = 1800.0  # bundan kısa kayıtlar tek parça (dosya yoluyla) işlenir
ARAMA_SN = 20.0              # kesim noktası için hedefin ±bu kadar saniyesinde en sessiz yer aranır
SON_PARCA_MIN_SN = 120.0     # kalan bundan kısaysa önceki parçaya katılır


@dataclass
class Segment:
    bas: float
    son: float
    metin: str


def _wav_bilgi(wav16: Path) -> tuple[int, int]:
    """(örnekleme_hızı, toplam_kare). 16 kHz mono 16-bit dışında IsHatasi."""
    with wave.open(str(wav16), "rb") as w:
        if w.getnchannels() != 1 or w.getsampwidth() != 2:
            raise IsHatasi("Transkript için 16-bit mono WAV bekleniyordu.")
        return w.getframerate(), w.getnframes()


def _sessiz_nokta(w: wave.Wave_read, merkez_sn: float, sr: int, toplam_sn: float) -> float:
    """merkez ± ARAMA_SN içinde en düşük enerjili 0,5 sn'lik pencerenin ortasını (sn) döndürür."""
    import numpy as np
    bas = max(0.0, merkez_sn - ARAMA_SN)
    bit = min(toplam_sn, merkez_sn + ARAMA_SN)
    w.setpos(int(bas * sr))
    x = np.frombuffer(w.readframes(int((bit - bas) * sr)), dtype=np.int16).astype(np.float32)
    pen = int(0.5 * sr)
    if x.size < 2 * pen:
        return merkez_sn
    kare = x[: x.size // pen * pen].reshape(-1, pen)
    i = int(np.argmin((kare ** 2).mean(axis=1)))
    return bas + (i * pen + pen / 2) / sr


def _parcalar(wav16: Path, sr: int, kare: int) -> list[tuple[float, float]]:
    """[(baş_sn, son_sn), ...] — sessiz noktalardan kesilmiş, ardışık, boşluksuz."""
    toplam = kare / sr
    kesimler = [0.0]
    with wave.open(str(wav16), "rb") as w:
        hedef = PARCA_SN
        while toplam - hedef > SON_PARCA_MIN_SN:
            k = _sessiz_nokta(w, hedef, sr, toplam)
            if k <= kesimler[-1] + 60:    # beklenmedik durum: ilerleme olmazsa sert kes
                k = hedef
            kesimler.append(k)
            hedef = k + PARCA_SN
    kesimler.append(toplam)
    return list(zip(kesimler[:-1], kesimler[1:]))


def yaz(
    wav16: Path,
    secim: CihazSecimi,
    ayar: Ayar,
    terimler: str,
) -> list[Segment]:
    from faster_whisper import WhisperModel

    log = logging.getLogger("videohat")
    wav16 = Path(wav16)
    sr, kare = _wav_bilgi(wav16)
    toplam = kare / sr

    cpu_threads = ayar.genel.cpu_threads or max(1, (os.cpu_count() or 1) // 2)
    model = WhisperModel(
        str(yollar.model_dizini(secim.model_ad)),
        device=secim.device,
        compute_type=secim.compute_type,
        cpu_threads=cpu_threads,
        num_workers=1,
    )

    def coz(girdi):
        return model.transcribe(
            girdi,
            language="tr",
            beam_size=ayar.stt.beam_size,
            vad_filter=ayar.stt.vad,
            vad_parameters={"min_silence_duration_ms": 500},
            condition_on_previous_text=False,
            # hotwords her 30 sn'lik pencerede isteme eklenir; initial_prompt yalnız İLK pencerede
            # etkilidir (condition_on_previous_text=False iken) ve uzun kayıtlarda ipucu kaybolur.
            hotwords=terimler or None,
            word_timestamps=False,
        )

    sonuc: list[Segment] = []
    sonraki_yuzde = 10

    def ekle(bolumler, ofset: float) -> None:
        nonlocal sonraki_yuzde
        for bolum in bolumler:
            metin = bolum.text.strip()
            if metin:
                sonuc.append(Segment(ofset + float(bolum.start), ofset + float(bolum.end), metin))
            yuzde = (ofset + float(bolum.end)) / toplam * 100 if toplam > 0 else 0
            while yuzde >= sonraki_yuzde and sonraki_yuzde <= 100:   # her %10'da bir satır
                log.info("Transkript ilerleme: %%%d (%d/%d dk)", sonraki_yuzde,
                         (ofset + bolum.end) // 60, toplam // 60)
                sonraki_yuzde += 10

    if toplam <= TEK_PARCA_SINIRI_SN:
        bolumler, _ = coz(str(wav16))
        ekle(bolumler, 0.0)
        return sonuc

    import numpy as np
    parcalar = _parcalar(wav16, sr, kare)
    log.info("Uzun kayıt (%d dk): %d parçada işlenecek (bellek için)", toplam // 60, len(parcalar))
    with wave.open(str(wav16), "rb") as w:
        for i, (a, b) in enumerate(parcalar, 1):
            w.setpos(int(a * sr))
            x = np.frombuffer(w.readframes(int((b - a) * sr)), dtype=np.int16).astype(np.float32) / 32768.0
            bolumler, _ = coz(x)
            ekle(bolumler, a)
            del x
            log.info("Parça %d/%d bitti (%d-%d dk)", i, len(parcalar), a // 60, b // 60)
    return sonuc
