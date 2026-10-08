"""Yerel faster-whisper modeliyle Türkçe konuşma tanıma.

Uzun kayıtlar (> TEK_PARCA_SINIRI_SN) ~PARCA_SN'lik parçalara bölünür: faster-whisper tüm sesin
özniteliklerini tek seferde bellekte tuttuğundan bellek kayıt süresiyle orantılı büyür (ölçüldü:
49 dk 3,1 GB, 3 saat 10,5 GB). Parçalı işlemde bellek kayıt süresinden bağımsızdır.
"""
import gc
import logging
import os
import re
import unicodedata
import wave
from dataclasses import dataclass, field
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
    guven: float | None = field(default=None, compare=False)


_MAKS_ALTYAZI_SN = 7.0
_MAKS_ALTYAZI_KARAKTER = 84
_MIN_ALTYAZI_SN = 0.4


def _metin_norm(metin: str) -> str:
    metin = metin.replace("İ", "i").replace("I", "ı")
    harfler = "".join(
        " " if unicodedata.category(c)[0] == "P" else c.lower()
        for c in metin
        if c != "\u0307"
    )
    return re.sub(r"\s+", " ", harfler).strip()


_HALUSINASYON_KALIPLARI = frozenset(_metin_norm(k) for k in {
    "altyazi m k", "altyazi mk", "altyazı m k", "altyazı mk",
    "abone ol",
    "izlediginiz icin tesekkurler", "izlediğiniz için teşekkürler",
    "izlediginiz icin tesekkur ederim", "izlediğiniz için teşekkür ederim",
    "bir sonraki videoda gorusmek uzere", "bir sonraki videoda görüşmek üzere",
})

_INGILIZCE_SIK_KELIMELER = frozenset({
    "the", "you", "me", "let", "see", "and", "to", "of", "in", "is",
    "it", "i", "we", "my", "oh", "yeah", "baby", "love", "your", "so",
    "be", "on", "for", "that", "this", "with", "are", "don't", "just",
    "can", "now", "go", "up", "down", "bounce", "a", "an", "he", "she",
    "they", "them", "do", "not", "what", "all", "no", "yes", "like",
    "was", "have", "here", "there", "come", "get", "make", "one", "out",
})

_TURKCE_KELIMELER = frozenset({
    "acaba", "ama", "ben", "biz", "bu", "bugun", "bunu", "da", "daha",
    "ders", "diye", "çok", "cok", "degil", "değil", "evet", "hangi",
    "hem", "her", "icin", "için", "iki", "iyi", "kadar", "mi", "mı",
    "mu", "mü", "ne", "neden", "nerede", "o", "olan", "olarak", "on",
    "onu", "sen", "siz", "sonra", "tamam", "var", "ve", "veya", "yani",
    "yok", "zaten", "şey", "bir", "şu", "ile", "gibi", "olsun",
})


def _alan(nesne, ad: str, varsayilan=None):
    if isinstance(nesne, dict):
        return nesne.get(ad, varsayilan)
    return getattr(nesne, ad, varsayilan)


def _sozcuk_metni(sozcukler) -> str:
    return " ".join(str(_alan(s, "word", "")).strip() for s in sozcukler).strip()


def _bolme_indeksi(sozcukler) -> int:
    cumle = [i for i, s in enumerate(sozcukler[:-1], 1)
             if str(_alan(s, "word", "")).strip().endswith((".", "?", "!"))]
    if cumle:
        return cumle[-1]
    virgul = [i for i, s in enumerate(sozcukler[:-1], 1)
              if str(_alan(s, "word", "")).strip().endswith(",")]
    if virgul:
        return virgul[-1]
    bosluklar = [
        (float(_alan(sozcukler[i], "start", 0.0))
         - float(_alan(sozcukler[i - 1], "end", 0.0)), i)
        for i in range(1, len(sozcukler))
    ]
    return max(bosluklar)[1] if bosluklar else len(sozcukler)


def _kelimeli_segmentler(sozcukler) -> list[Segment]:
    sonuc: list[Segment] = []
    mevcut = []
    for sozcuk in sozcukler:
        bas = float(_alan(sozcuk, "start", 0.0))
        son = float(_alan(sozcuk, "end", bas))
        if son - bas > _MAKS_ALTYAZI_SN:
            son = bas + _MAKS_ALTYAZI_SN
            if isinstance(sozcuk, dict):
                sozcuk = {**sozcuk, "end": son}
            else:
                from types import SimpleNamespace
                sozcuk = SimpleNamespace(
                    start=bas, end=son, word=_alan(sozcuk, "word", ""),
                    probability=_alan(sozcuk, "probability"),
                )
        while mevcut:
            onceki_son = float(_alan(mevcut[-1], "end", 0.0))
            if bas - onceki_son > 1.0:
                sonuc.append(_segment_yap(mevcut))
                mevcut = []
                break
            else:
                aday = mevcut + [sozcuk]
                aday_suresi = son - float(_alan(aday[0], "start", 0.0))
                aday_metni = _sozcuk_metni(aday)
                if aday_suresi > _MAKS_ALTYAZI_SN or len(aday_metni) > _MAKS_ALTYAZI_KARAKTER:
                    bol = _bolme_indeksi(mevcut)
                    sonuc.append(_segment_yap(mevcut[:bol]))
                    mevcut = mevcut[bol:]
                    continue
                break
        mevcut.append(sozcuk)
    if mevcut:
        sonuc.append(_segment_yap(mevcut))
    return sonuc


def _segment_yap(sozcukler) -> Segment:
    olasiliklar = [
        float(p) for s in sozcukler
        if (p := _alan(s, "probability")) is not None
    ]
    return Segment(
        float(_alan(sozcukler[0], "start", 0.0)),
        float(_alan(sozcukler[-1], "end", 0.0)),
        _sozcuk_metni(sozcukler),
        sum(olasiliklar) / len(olasiliklar) if olasiliklar else None,
    )


def _guven_birlestir(a: Segment, b: Segment) -> float | None:
    if a.guven is None:
        return b.guven
    if b.guven is None:
        return a.guven
    a_adet = len(_metin_norm(a.metin).split())
    b_adet = len(_metin_norm(b.metin).split())
    return (a.guven * a_adet + b.guven * b_adet) / (a_adet + b_adet)


def _ingilizce_halusinasyonu(metin: str) -> bool:
    if any(c in metin for c in "çğıöşüİÇĞÖŞÜ"):
        return False
    kelimeler = re.findall(r"\b[\w']+\b", metin.lower())
    if len(kelimeler) < 3 or any(k in _TURKCE_KELIMELER for k in kelimeler):
        return False
    return sum(k in _INGILIZCE_SIK_KELIMELER for k in kelimeler) / len(kelimeler) >= 0.6


def filtrele_halusinasyonlar(
    segmentler: list[Segment],
    guven_esik: float = 0.45,
    guven_kisa_esik: float = 0.30,
) -> list[Segment]:
    """Ayıkla zamanlaması/metni bariz hatalı ve bilinen halüsinasyonları."""
    gecerli = []
    for segment in segmentler:
        metin = segment.metin.strip()
        sure = segment.son - segment.bas
        if sure <= 0 or not metin:
            continue
        if sure > 3.0 and len(metin) <= 6:
            continue
        if _metin_norm(metin) in _HALUSINASYON_KALIPLARI:
            continue
        if _ingilizce_halusinasyonu(metin):
            continue
        kelime_sayisi = len(_metin_norm(metin).split())
        if kelime_sayisi == 1 and sure < 0.3:
            continue
        if segment.guven is not None and (
            segment.guven < guven_kisa_esik
            or (segment.guven < guven_esik and sure < 4.0)
        ):
            continue
        gecerli.append(Segment(segment.bas, segment.son, metin, segment.guven))

    sonuc = []
    i = 0
    while i < len(gecerli):
        j = i + 1
        while j < len(gecerli) and gecerli[j].metin == gecerli[i].metin:
            j += 1
        sonuc.extend(gecerli[i:i + 1] if j - i >= 3 else gecerli[i:j])
        i = j
    return sonuc


def bolumleri_kes(whisper_segmentleri_ya_da_sozcukler, ofset: float = 0.0,
                  onceki_son: float | None = None,
                  guven_esik: float = 0.45,
                  guven_kisa_esik: float = 0.30) -> list[Segment]:
    """Whisper kelime zamanlarından kısa, konuşmaya sıkı bağlı altyazılar üret."""
    sonuc: list[Segment] = []
    for bolum in whisper_segmentleri_ya_da_sozcukler:
        sozcukler = _alan(bolum, "words")
        if sozcukler:
            sonuc.extend(_kelimeli_segmentler(sozcukler))
            continue
        metin = str(_alan(bolum, "text", "")).strip()
        bas = float(_alan(bolum, "start", 0.0))
        son = float(_alan(bolum, "end", bas))
        if son - bas > 15.0:
            son = min(son, bas + len(metin) / 25.0)
        sonuc.append(Segment(bas, son, metin))

    sirali = []
    onceki = onceki_son
    for segment in sonuc:
        bas = segment.bas + ofset
        son = segment.son + ofset
        if onceki is not None:
            bas = max(bas, onceki)
        if son > bas:
            sirali.append(Segment(bas, son, segment.metin, segment.guven))
            onceki = son

    i = 0
    while i < len(sirali):
        if sirali[i].son - sirali[i].bas >= _MIN_ALTYAZI_SN:
            i += 1
            continue
        komsular = []
        if i > 0 and sirali[i].bas - sirali[i - 1].son <= 1.0:
            komsular.append(i - 1)
        if i + 1 < len(sirali) and sirali[i + 1].bas - sirali[i].son <= 1.0:
            komsular.append(i + 1)
        uygun = [
            j for j in komsular
            if max(sirali[i].son, sirali[j].son) - min(sirali[i].bas, sirali[j].bas)
            <= _MAKS_ALTYAZI_SN
            and len(sirali[i].metin) + len(sirali[j].metin) + 1 <= _MAKS_ALTYAZI_KARAKTER
        ]
        if uygun:
            j = uygun[0]
            once, kisa = (j, i) if j < i else (i, j)
            sirali[once] = Segment(
                min(sirali[once].bas, sirali[kisa].bas),
                max(sirali[once].son, sirali[kisa].son),
                sirali[once].metin + " " + sirali[kisa].metin,
                _guven_birlestir(sirali[once], sirali[kisa]),
            )
            del sirali[kisa]
            i = max(0, once - 1)
        else:
            i += 1
    return filtrele_halusinasyonlar(sirali, guven_esik, guven_kisa_esik)


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
    sonuc: list[Segment] = []
    sonraki_yuzde = 10

    model = None
    coz = None
    try:
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
                vad_parameters={
                    "min_silence_duration_ms": ayar.stt.vad_bosluk_ms,
                    "speech_pad_ms": ayar.stt.vad_konusma_pad_ms,
                    "max_speech_duration_s": ayar.stt.vad_max_konusma_sn,
                },
                hallucination_silence_threshold=ayar.stt.halusinasyon_esik_sn,
                condition_on_previous_text=False,
                # hotwords her 30 sn'lik pencerede isteme eklenir; initial_prompt yalnız İLK pencerede
                # etkilidir (condition_on_previous_text=False iken) ve uzun kayıtlarda ipucu kaybolur.
                hotwords=terimler or None,
                word_timestamps=True,
            )

        def ekle(bolumler, ofset: float) -> None:
            nonlocal sonraki_yuzde
            bolumler = list(bolumler)
            for bolum in bolumler:
                yuzde = (ofset + float(bolum.end)) / toplam * 100 if toplam > 0 else 0
                while yuzde >= sonraki_yuzde and sonraki_yuzde <= 100:   # her %10'da bir satır
                    log.info("Transkript ilerleme: %%%d (%d/%d dk)", sonraki_yuzde,
                             (ofset + bolum.end) // 60, toplam // 60)
                    sonraki_yuzde += 10
            onceki_son = sonuc[-1].son if sonuc else None
            sonuc.extend(bolumleri_kes(
                bolumler, ofset=ofset, onceki_son=onceki_son,
                guven_esik=ayar.stt.guven_esik,
                guven_kisa_esik=ayar.stt.guven_kisa_esik,
            ))

        if toplam <= TEK_PARCA_SINIRI_SN:
            bolumler, _ = coz(str(wav16))
            ekle(bolumler, 0.0)
            return filtrele_halusinasyonlar(
                sonuc, ayar.stt.guven_esik, ayar.stt.guven_kisa_esik,
            )

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
        return filtrele_halusinasyonlar(
            sonuc, ayar.stt.guven_esik, ayar.stt.guven_kisa_esik,
        )
    finally:
        if coz is not None:
            del coz
        if model is not None:
            del model
        gc.collect()
