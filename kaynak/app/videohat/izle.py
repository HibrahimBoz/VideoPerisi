"""is\\gelen klasörünü yoklayarak izler; hazır dosyaları sırayla (tek iş) işler."""
import hashlib
import json
import logging
import os
import shutil
import time
from pathlib import Path

from . import yollar
from .ayar import Ayar
from .hat import Bayraklar, isle_tek

UZANTILAR = {".mp4", ".mkv", ".mov", ".avi", ".m4a", ".wav", ".mp3"}
log = logging.getLogger("videohat")


def parmak_izi(yol: Path) -> str:
    """sha1(boyut + ilk 1 MB + son 1 MB)."""
    boyut = yol.stat().st_size
    h = hashlib.sha1(str(boyut).encode())
    with open(yol, "rb") as f:
        h.update(f.read(1 << 20))
        if boyut > (1 << 20):
            f.seek(max(0, boyut - (1 << 20)))
            h.update(f.read(1 << 20))
    return h.hexdigest()


def _kilitli_mi(yol: Path) -> bool:
    """Kopyalama sürüyorsa Windows aynı ada rename'e izin vermez."""
    try:
        os.rename(yol, yol)
        return False
    except OSError:
        return True


def _benzersiz(dizin: Path, ad: str) -> Path:
    hedef = dizin / ad
    n = 1
    while hedef.exists():
        hedef = dizin / f"{Path(ad).stem}_{n}{Path(ad).suffix}"
        n += 1
    return hedef


def _durum_oku(yol: Path) -> set[str]:
    if not yol.exists():
        return set()
    kayitlar = set()
    for satir in yol.read_text(encoding="utf-8").splitlines():
        try:
            kayitlar.add(json.loads(satir)["parmak"])
        except (ValueError, KeyError):
            continue
    return kayitlar


def _geri_al(isleniyor: Path, gelen: Path) -> None:
    """Açılışta isleniyor\\'da kalanları gelen\\'e geri koy."""
    for p in isleniyor.iterdir():
        if p.is_file():
            hedef = _benzersiz(gelen, p.name)
            shutil.move(str(p), str(hedef))
            log.warning("Yarım kalan iş geri alındı: %s", hedef.name)


def tek_tur(ayar: Ayar, gorulen: dict[Path, tuple[int, float, int]], bayrak: Bayraklar | None = None) -> int:
    """Bir yoklama turu. `gorulen`: yol → (boyut, mtime, ardışık_sabit_tur). İşlenen dosya sayısını döndürür."""
    gelen = yollar.is_dizini("gelen")
    isleniyor = yollar.is_dizini("isleniyor")
    bitti = yollar.is_dizini("bitti")
    hata = yollar.is_dizini("hata")
    durum_yolu = yollar.kok() / "is" / "durum.jsonl"
    bayrak = bayrak or Bayraklar()

    mevcut = {p for p in gelen.iterdir() if p.is_file() and p.suffix.lower() in UZANTILAR}
    for p in list(gorulen):
        if p not in mevcut:
            del gorulen[p]
    hazirlar = []
    for p in sorted(mevcut):
        try:
            st = p.stat()
        except OSError:
            continue
        onceki = gorulen.get(p)
        if onceki and onceki[0] == st.st_size and onceki[1] == st.st_mtime:
            sabit = onceki[2] + 1
        else:
            sabit = 1
        gorulen[p] = (st.st_size, st.st_mtime, sabit)
        if sabit >= ayar.izle.sabit_tur and not _kilitli_mi(p):
            hazirlar.append(p)

    islenen = 0
    for p in hazirlar:  # tek iş: sırayla
        gorulen.pop(p, None)
        try:
            parmak = parmak_izi(p)
        except OSError as e:
            log.error("Okunamadı: %s (%s)", p.name, e)
            continue
        if parmak in _durum_oku(durum_yolu):
            hedef = _benzersiz(bitti, f"{p.stem}_yinelenen{p.suffix}")
            shutil.move(str(p), str(hedef))
            log.info("Yinelenen atlandı: %s → %s", p.name, hedef.name)
            continue
        calisan = _benzersiz(isleniyor, p.name)
        shutil.move(str(p), str(calisan))
        cikis_kok = bitti / calisan.stem
        cikis_kok.mkdir(parents=True, exist_ok=True)
        log.info("İş başladı: %s", calisan.name)
        s = isle_tek(calisan, cikis_kok, ayar, bayrak)
        if s.basarili:
            shutil.move(str(calisan), str(cikis_kok / calisan.name))
            with open(durum_yolu, "a", encoding="utf-8") as f:
                f.write(json.dumps({"parmak": parmak, "ad": calisan.name,
                                    "zaman": time.strftime("%Y-%m-%d %H:%M:%S")}, ensure_ascii=False) + "\n")
            log.info("İş bitti: %s sureler=%s", calisan.name,
                     {k: round(v, 1) for k, v in s.sureler.items()})
        else:
            hata_dir = _benzersiz(hata, calisan.stem)
            hata_dir.mkdir(parents=True, exist_ok=True)
            shutil.move(str(calisan), str(hata_dir / calisan.name))
            (hata_dir / "hata.txt").write_text(s.hata or "bilinmeyen hata", encoding="utf-8")
            shutil.rmtree(cikis_kok, ignore_errors=True)
            log.error("İş HATALI: %s: %s", calisan.name, s.hata)
        islenen += 1
    return islenen


def dongu(ayar: Ayar, tur_siniri: int | None = None) -> int:
    """Ctrl+C ile durur. `tur_siniri` yalnız testlerde (belirli tur sayısı sonra çık)."""
    for ad in ("gelen", "isleniyor", "bitti", "hata", "gunluk"):
        yollar.is_dizini(ad)
    _geri_al(yollar.is_dizini("isleniyor"), yollar.is_dizini("gelen"))
    log.info("İzleme başladı: %s (Ctrl+C ile durdurun)", yollar.is_dizini("gelen"))
    gorulen: dict[Path, tuple[int, float, int]] = {}
    tur = 0
    try:
        while tur_siniri is None or tur < tur_siniri:
            tek_tur(ayar, gorulen)
            tur += 1
            time.sleep(ayar.izle.yoklama_sn)
    except KeyboardInterrupt:
        log.info("İzleme durduruldu.")
    return 0
