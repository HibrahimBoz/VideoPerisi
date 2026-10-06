"""Yerel faster-whisper modeliyle Türkçe konuşma tanıma."""
import logging
import os
from dataclasses import dataclass
from pathlib import Path

from . import yollar
from .ayar import Ayar
from .cihaz import CihazSecimi


@dataclass
class Segment:
    bas: float
    son: float
    metin: str


def yaz(
    wav16: Path,
    secim: CihazSecimi,
    ayar: Ayar,
    terimler: str,
) -> list[Segment]:
    from faster_whisper import WhisperModel

    cpu_threads = ayar.genel.cpu_threads or max(1, (os.cpu_count() or 1) // 2)
    model = WhisperModel(
        str(yollar.model_dizini(secim.model_ad)),
        device=secim.device,
        compute_type=secim.compute_type,
        cpu_threads=cpu_threads,
        num_workers=1,
    )
    bolumler, bilgi = model.transcribe(
        str(Path(wav16)),
        language="tr",
        beam_size=ayar.stt.beam_size,
        vad_filter=ayar.stt.vad,
        vad_parameters={"min_silence_duration_ms": 500},
        condition_on_previous_text=False,
        # hotwords her 30 sn'lik pencerede isteme eklenir; initial_prompt yalnız İLK pencerede etkilidir
        # (condition_on_previous_text=False iken) ve uzun kayıtlarda terim ipucu kaybolur.
        hotwords=terimler or None,
        word_timestamps=False,
    )
    log = logging.getLogger("videohat")
    toplam = float(getattr(bilgi, "duration", 0) or 0)
    sonraki_yuzde = 10
    sonuc = []
    for bolum in bolumler:
        metin = bolum.text.strip()
        if metin:
            sonuc.append(Segment(float(bolum.start), float(bolum.end), metin))
        if toplam > 0:   # uzun kayıtlarda ilerleme: her %10'da bir satır
            yuzde = float(bolum.end) / toplam * 100
            while yuzde >= sonraki_yuzde and sonraki_yuzde <= 100:
                log.info("Transkript ilerleme: %%%d (%d/%d dk)", sonraki_yuzde, bolum.end // 60, toplam // 60)
                sonraki_yuzde += 10
    return sonuc
