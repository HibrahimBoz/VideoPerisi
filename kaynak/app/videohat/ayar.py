"""ayar.toml okuma. Eksik alana varsayılan, bilinmeyen alana uyarı."""
import tomllib
import warnings
from dataclasses import dataclass, field, fields
from pathlib import Path

from .hatalar import AyarHatasi


@dataclass
class SttAyar:
    gpu_model: str = "large-v3"
    cpu_model: str = "medium"
    beam_size: int = 5
    vad: bool = True
    terimler_dosyasi: str = "terimler.txt"


@dataclass
class SesAyar:
    highpass_hz: int = 80
    afftdn_nf: int = -25
    lufs: float = -16.0
    tp: float = -1.5
    lra: int = 11
    agir_kuru_oran: float = 0.2


@dataclass
class GoruntuAyar:
    etkin: bool = False
    crf: int = 20
    nvenc: bool = True


@dataclass
class IzleAyar:
    yoklama_sn: int = 5
    sabit_tur: int = 3


@dataclass
class GenelAyar:
    ara_dosya_sakla: bool = False
    cpu_threads: int = 0


@dataclass
class Ayar:
    kok: Path
    stt: SttAyar = field(default_factory=SttAyar)
    ses: SesAyar = field(default_factory=SesAyar)
    goruntu: GoruntuAyar = field(default_factory=GoruntuAyar)
    izle: IzleAyar = field(default_factory=IzleAyar)
    genel: GenelAyar = field(default_factory=GenelAyar)


_BOLUMLER = {"stt": SttAyar, "ses": SesAyar, "goruntu": GoruntuAyar,
             "izle": IzleAyar, "genel": GenelAyar}


def yukle(kok: Path) -> Ayar:
    yol = kok / "ayar.toml"
    ayar = Ayar(kok=kok)
    if not yol.exists():
        return ayar
    try:
        with open(yol, "rb") as f:
            ham = tomllib.load(f)
    except tomllib.TOMLDecodeError as e:
        raise AyarHatasi(f"ayar.toml bozuk: {e}") from e
    for bolum, sinif in _BOLUMLER.items():
        veri = ham.get(bolum, {})
        geçerli = {f.name for f in fields(sinif)}
        for k in veri:
            if k not in geçerli:
                warnings.warn(f"ayar.toml: bilinmeyen alan [{bolum}] {k}")
        setattr(ayar, bolum, sinif(**{k: v for k, v in veri.items() if k in geçerli}))
    for k in ham:
        if k not in _BOLUMLER:
            warnings.warn(f"ayar.toml: bilinmeyen bölüm [{k}]")
    return ayar
