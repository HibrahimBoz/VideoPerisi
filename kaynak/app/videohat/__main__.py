"""Komut satırı: python -m videohat {isle,izle,kontrol}. Çıkış: 0 ok, 1 iş hatası, 2 ortam hatası."""
import argparse
import logging
import os
import sys
from pathlib import Path

from . import yollar
from .hatalar import AyarHatasi, OrtamHatasi

VIDEO_UZANTILARI = {".mp4", ".mkv", ".mov", ".avi", ".m4a", ".wav", ".mp3"}


def _gunluk_kur(kok: Path) -> None:
    from datetime import date
    d = kok / "is" / "gunluk"
    d.mkdir(parents=True, exist_ok=True)
    bicim = logging.Formatter("%(asctime)s %(levelname)s %(message)s")
    log = logging.getLogger("videohat")
    log.setLevel(logging.INFO)
    if log.handlers:
        return
    for h in (logging.StreamHandler(sys.stdout),
              logging.FileHandler(d / f"{date.today():%Y-%m-%d}.log", encoding="utf-8")):
        h.setFormatter(bicim)
        log.addHandler(h)


def _topla(girdi: Path) -> list[Path]:
    if girdi.is_dir():
        return sorted(p for p in girdi.rglob("*") if p.suffix.lower() in VIDEO_UZANTILARI)
    return [girdi]


def _isle(args) -> int:
    from . import ayar as ayar_mod
    from .hat import Bayraklar, isle_tek
    kok = yollar.kok()
    ayar = ayar_mod.yukle(kok)
    log = logging.getLogger("videohat")
    bay = Bayraklar(agir_ses=args.agir_ses, goruntu=args.goruntu, upscale=args.upscale,
                    model=args.model, cihaz=args.cihaz)
    hatali = 0
    for yol in args.girdi:
        p = Path(yol)
        if not p.exists():
            log.error("Bulunamadı: %s", p)
            hatali += 1
            continue
        for video in _topla(p):
            cikis = Path(args.cikis) if args.cikis else video.parent / f"{video.stem}_videoperisi"
            cikis.mkdir(parents=True, exist_ok=True)
            log.info("İşleniyor: %s", video)
            s = isle_tek(video, cikis, ayar, bay)
            if s.basarili:
                log.info("Bitti: %s (%s) süreler=%s", video.name, s.cihaz,
                         {k: round(v, 1) for k, v in s.sureler.items()})
            else:
                log.error("HATA: %s: %s", video.name, s.hata)
                hatali += 1
    return 1 if hatali else 0


def _isle_grup(args) -> int:
    from . import ayar as ayar_mod, grup
    from .hat import Bayraklar
    kok = yollar.kok()
    ayar = ayar_mod.yukle(kok)
    log = logging.getLogger("videohat")
    bay = Bayraklar(agir_ses=args.agir_ses, goruntu=args.goruntu)
    klasor = Path(args.klasor)
    if not klasor.exists() or not klasor.is_dir():
        log.error("Klasör bulunamadı: %s", klasor)
        return 1
    sonuc = grup.isle_grup(klasor, ayar, bay, referans_kanal_adi=args.referans)
    for u in sonuc.uyarilar:
        log.warning("Grup uyarısı: %s", u)
    if not sonuc.basarili:
        log.error("Grup işleme başarısız: %s", sonuc.hata)
        return 1
    log.info("Grup işleme bitti: %s (%s)", sonuc.ders_adi, sonuc.cikis_klasoru)
    return 0


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(prog="videohat")
    alt = ap.add_subparsers(dest="komut", required=True)
    i = alt.add_parser("isle", help="dosya veya klasör işle")
    i.add_argument("girdi", nargs="+")
    i.add_argument("--cikis", help="çıktı klasörü (varsayılan: videonun yanında <ad>_videoperisi)")
    i.add_argument("--agir-ses", action="store_true", help="DeepFilterNet ağır mod")
    i.add_argument("--goruntu", action="store_true", help="görüntü iyileştirme")
    i.add_argument("--upscale", action="store_true", help="Real-ESRGAN x2 (isteğe bağlı)")
    i.add_argument("--model", choices=["large-v3", "medium", "turbo"])
    i.add_argument("--cihaz", choices=["cpu", "cuda"])
    ig = alt.add_parser("isle-grup", help="çok kameralı ders klasörünü işle")
    ig.add_argument("klasor", help="ders klasörü")
    ig.add_argument("--referans", help="referans kamera/kanal adı")
    ig.add_argument("--goruntu", action="store_true", help="görüntü iyileştirme")
    ig.add_argument("--agir-ses", action="store_true", help="DeepFilterNet ağır mod")
    alt.add_parser("izle", help="is\\gelen klasörünü izle")
    alt.add_parser("kontrol", help="kendi kendine test")
    args = ap.parse_args(argv)
    try:
        kok = yollar.kok()
        _gunluk_kur(kok)
        if os.environ.get("VIDEOHAT_AG_KILIDI") == "1":
            from . import ag_kilidi
            ag_kilidi.kur()
        if args.komut == "isle":
            return _isle(args)
        if args.komut == "isle-grup":
            return _isle_grup(args)
        if args.komut == "izle":
            from . import ayar as ayar_mod, izle
            return izle.dongu(ayar_mod.yukle(kok))
        from . import kontrol
        return kontrol.calistir()
    except (OrtamHatasi, AyarHatasi) as e:
        logging.getLogger("videohat").error("Ortam/ayar hatası: %s", e)
        return 2
    except KeyboardInterrupt:
        return 0


if __name__ == "__main__":
    sys.exit(main())
