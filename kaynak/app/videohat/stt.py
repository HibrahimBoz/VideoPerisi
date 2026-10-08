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


def _kisa_altyazilari_uzat(segmentler: list[Segment]) -> list[Segment]:
    sonuc = []
    for i, segment in enumerate(segmentler):
        if segment.son - segment.bas <= 0.9:
            sonraki_bas = segmentler[i + 1].bas if i + 1 < len(segmentler) else segment.bas + 1.0
            son = min(segment.bas + 1.0, sonraki_bas)
            segment = Segment(segment.bas, max(segment.son, son), segment.metin, segment.guven)
        sonuc.append(segment)
    return sonuc


def _parca_siniri_tekrar_temizle(
    segmentler: list[Segment], sinirlar: list[float],
) -> list[Segment]:
    sonuc = list(segmentler)
    for sinir in sinirlar:
        onceki = max((i for i, s in enumerate(sonuc) if s.bas < sinir), default=-1)
        sonraki = next((i for i, s in enumerate(sonuc) if s.bas >= sinir), -1)
        if onceki < 0 or sonraki < 0:
            continue
        a, b = sonuc[onceki], sonuc[sonraki]
        x, y = _metin_norm(a.metin), _metin_norm(b.metin)
        if not x or not y or not (x == y or x.endswith(y) or y.endswith(x)
                                  or x.startswith(y) or y.startswith(x)):
            continue
        metin = a.metin if len(x) >= len(y) else b.metin
        sonuc[onceki] = Segment(
            min(a.bas, b.bas), max(a.son, b.son), metin, _guven_birlestir(a, b),
        )
        del sonuc[sonraki]
    return sonuc


def _ikinci_gecis_bolgeleri(
    segmentler: list[Segment], toplam_sn: float, esik: float, en_cok_oran: float,
) -> list[tuple[float, float]]:
    butce = max(0.0, toplam_sn * min(1.0, max(0.0, en_cok_oran)))
    adaylar = []
    for segment in segmentler:
        sure = segment.son - segment.bas
        kelime_sayisi = len(_metin_norm(segment.metin).split())
        dusuk_guven = segment.guven is not None and segment.guven < esik
        dusuk_oran = sure >= 2.0 and kelime_sayisi / sure < 0.5
        if dusuk_guven or dusuk_oran:
            adaylar.append((segment.guven if segment.guven is not None else 0.0,
                            max(0.0, segment.bas - 1.0), min(toplam_sn, segment.son + 1.0)))
    adaylar.sort(key=lambda a: a[1])
    birlesmis: list[tuple[float, float, float]] = []
    for guven, bas, son in adaylar:
        if birlesmis and bas <= birlesmis[-1][2]:
            eski_guven, eski_bas, eski_son = birlesmis[-1]
            birlesmis[-1] = (min(eski_guven, guven), eski_bas, max(eski_son, son))
        else:
            birlesmis.append((guven, bas, son))
    birlesmis.sort()
    secilen = []
    harcanan = 0.0
    for _, bas, son in birlesmis:
        kalan = butce - harcanan
        if kalan <= 0:
            break
        sure = min(son - bas, kalan)
        if sure <= 0:
            continue
        secilen.append((bas, bas + sure))
        harcanan += sure
    return secilen


def _aday_ortalama_guveni(segmentler: list[Segment]) -> float:
    olasiliklar = [
        (s.guven, max(1, len(_metin_norm(s.metin).split())))
        for s in segmentler if s.guven is not None
    ]
    toplam = sum(adet for _, adet in olasiliklar)
    return sum(guven * adet for guven, adet in olasiliklar) / toplam if toplam else 0.0


def _adaylari_sec(
    ilk: list[Segment], temiz: list[Segment], ham: list[Segment],
    guven_esik: float = 0.45, guven_kisa_esik: float = 0.30,
    kisa_altyazi_uzat: bool = True,
) -> list[Segment]:
    """Geçerli adaylar içinden güveni en yüksek olanı seç; yakın farkta ilk geçişi koru."""
    adaylar = []
    for sira, aday in enumerate((ilk, temiz, ham)):
        filtreli = filtrele_halusinasyonlar(aday, guven_esik, guven_kisa_esik)
        if filtreli:
            if kisa_altyazi_uzat:
                filtreli = _kisa_altyazilari_uzat(filtreli)
            adaylar.append((sira, filtreli, _aday_ortalama_guveni(filtreli)))
    if not adaylar:
        return []
    ilk_aday = next((a for a in adaylar if a[0] == 0), None)
    en_iyi = max(adaylar, key=lambda a: (a[2], -a[0]))
    if ilk_aday and en_iyi[0] != 0 and en_iyi[2] - ilk_aday[2] < 0.03:
        return ilk_aday[1]
    return en_iyi[1]


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
                  guven_kisa_esik: float = 0.30,
                  filtrele: bool = True,
                  kisa_altyazi_uzat: bool = True) -> list[Segment]:
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
    if filtrele:
        sirali = filtrele_halusinasyonlar(sirali, guven_esik, guven_kisa_esik)
        return _kisa_altyazilari_uzat(sirali) if kisa_altyazi_uzat else sirali
    return sirali


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


def _istem_yap(onceki_metin: str, terimler: str) -> str | None:
    baglam = onceki_metin[-220:].strip()
    cumle_siniri = next(
        (m.end() for m in re.finditer(r"[.!?]\s+", baglam) if m.end() < len(baglam)),
        None,
    )
    if cumle_siniri is not None:
        baglam = baglam[cumle_siniri:].strip()
    kisimlar = [k for k in (baglam, terimler.strip()) if k]
    return " ".join(kisimlar) or None


def _wav_parca(wav16: Path, bas: float, son: float, sr: int):
    import numpy as np
    with wave.open(str(wav16), "rb") as w:
        w.setpos(int(bas * sr))
        return np.frombuffer(
            w.readframes(max(0, int((son - bas) * sr))), dtype=np.int16,
        ).astype(np.float32) / 32768.0


def yaz(
    wav16: Path,
    secim: CihazSecimi,
    ayar: Ayar,
    terimler: str,
    wav16_ham: Path | None = None,
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
    sinirlar: list[float] = []
    ikinci_gecis = ayar.stt.ikinci_gecis
    try:
        model = WhisperModel(
            str(yollar.model_dizini(secim.model_ad)),
            device=secim.device,
            compute_type=secim.compute_type,
            cpu_threads=cpu_threads,
            num_workers=1,
        )

        def coz(girdi, initial_prompt=None, ikinci=False):
            ayarlar = {
                "language": "tr",
                "beam_size": 10 if ikinci else ayar.stt.beam_size,
                "vad_filter": False if ikinci else ayar.stt.vad,
                "hallucination_silence_threshold": ayar.stt.halusinasyon_esik_sn,
                "condition_on_previous_text": False,
                "hotwords": terimler or None,
                "word_timestamps": True,
            }
            if ikinci:
                ayarlar["temperature"] = (0.0, 0.2, 0.4)
            else:
                ayarlar["vad_parameters"] = {
                    "min_silence_duration_ms": ayar.stt.vad_bosluk_ms,
                    "speech_pad_ms": ayar.stt.vad_konusma_pad_ms,
                    "max_speech_duration_s": ayar.stt.vad_max_konusma_sn,
                }
            if initial_prompt:
                ayarlar["initial_prompt"] = initial_prompt
            return model.transcribe(girdi, **ayarlar)

        def ekle(bolumler, ofset: float, filtrele=True) -> None:
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
                filtrele=filtrele,
                kisa_altyazi_uzat=ayar.stt.kisa_altyazi_uzat,
            ))

        if toplam <= TEK_PARCA_SINIRI_SN:
            prompt = _istem_yap("", terimler)
            bolumler, _ = coz(str(wav16), prompt)
            ekle(bolumler, 0.0, filtrele=not ikinci_gecis)
        else:
            import numpy as np
            parcalar = _parcalar(wav16, sr, kare)
            sinirlar = [b for _, b in parcalar[:-1]]
            log.info("Uzun kayıt (%d dk): %d parçada işlenecek (bellek için)", toplam // 60, len(parcalar))
            onceki_metin = ""
            with wave.open(str(wav16), "rb") as w:
                for i, (a, b) in enumerate(parcalar, 1):
                    w.setpos(int(a * sr))
                    x = np.frombuffer(w.readframes(int((b - a) * sr)), dtype=np.int16).astype(np.float32) / 32768.0
                    prompt = _istem_yap(
                        onceki_metin if ayar.stt.baglam_tasi else "", terimler,
                    )
                    bolumler, _ = coz(x, prompt)
                    ekle(bolumler, a, filtrele=not ikinci_gecis)
                    onceki_metin = " ".join(
                        str(_alan(bolum, "text", "")).strip() for bolum in bolumler
                    )[-220:]
                    del x
                    log.info("Parça %d/%d bitti (%d-%d dk)", i, len(parcalar), a // 60, b // 60)

        if ikinci_gecis:
            # Temiz ve ham kaynakların her biri yeniden çözüldüğünden ortak süre bütçesi bölünür.
            alan_orani = ayar.stt.ikinci_gecis_en_cok_oran / (2 if wav16_ham is not None else 1)
            bolgeler = _ikinci_gecis_bolgeleri(
                sonuc, toplam, ayar.stt.ikinci_gecis_esik,
                alan_orani,
            )
            toplam_bolge_sn = sum(b - a for a, b in bolgeler)
            if bolgeler:
                log.info("İkinci geçiş: %d bölge (toplam %.1f sn)", len(bolgeler), toplam_bolge_sn)
            iyilesti = 0
            for bas, son in bolgeler:
                ilk = [s for s in sonuc if s.bas < son and s.son > bas]
                temiz_bolumler, _ = coz(_wav_parca(wav16, bas, son, sr), ikinci=True)
                temiz = bolumleri_kes(
                    temiz_bolumler, ofset=bas, guven_esik=ayar.stt.guven_esik,
                    guven_kisa_esik=ayar.stt.guven_kisa_esik, filtrele=False,
                    kisa_altyazi_uzat=ayar.stt.kisa_altyazi_uzat,
                )
                temiz = [
                    Segment(max(bas, s.bas), min(son, s.son), s.metin, s.guven)
                    for s in temiz if s.bas < son and s.son > bas
                ]
                ham = []
                if wav16_ham is not None:
                    ham_yolu = Path(wav16_ham)
                    ham_sr, ham_kare = _wav_bilgi(ham_yolu)
                    if ham_sr != sr or abs(ham_kare / ham_sr - toplam) > 0.1:
                        raise IsHatasi("Ham 16 kHz WAV temiz sesle aynı biçim ve süreye sahip olmalı.")
                    ham_bolumler, _ = coz(_wav_parca(ham_yolu, bas, son, sr), ikinci=True)
                    ham = bolumleri_kes(
                        ham_bolumler, ofset=bas, guven_esik=ayar.stt.guven_esik,
                        guven_kisa_esik=ayar.stt.guven_kisa_esik, filtrele=False,
                        kisa_altyazi_uzat=ayar.stt.kisa_altyazi_uzat,
                    )
                    ham = [
                        Segment(max(bas, s.bas), min(son, s.son), s.metin, s.guven)
                        for s in ham if s.bas < son and s.son > bas
                    ]
                secilen = _adaylari_sec(
                    ilk, temiz, ham, ayar.stt.guven_esik, ayar.stt.guven_kisa_esik,
                    ayar.stt.kisa_altyazi_uzat,
                )
                secilen = [
                    Segment(max(bas, s.bas), min(son, s.son), s.metin, s.guven)
                    for s in secilen if s.bas < son and s.son > bas
                ]
                ilk_filtreli = filtrele_halusinasyonlar(
                    ilk, ayar.stt.guven_esik, ayar.stt.guven_kisa_esik,
                )
                if [(s.metin, s.guven) for s in secilen] != [
                    (s.metin, s.guven) for s in ilk_filtreli
                ]:
                    iyilesti += 1
                sonuc = [s for s in sonuc if s.son <= bas or s.bas >= son] + secilen
                sonuc.sort(key=lambda s: (s.bas, s.son))
            if bolgeler:
                log.info("İkinci geçiş: %d bölge iyileşti", iyilesti)

        sonuc.sort(key=lambda s: (s.bas, s.son))
        if ayar.stt.parca_siniri_tekrar_temizle:
            sonuc = _parca_siniri_tekrar_temizle(sonuc, sinirlar)
        cakismasiz = []
        for i, segment in enumerate(sonuc):
            if i + 1 < len(sonuc) and sonuc[i + 1].bas <= segment.bas:
                continue
            sonraki_bas = sonuc[i + 1].bas if i + 1 < len(sonuc) else segment.son
            son = min(segment.son, sonraki_bas)
            if son > segment.bas:
                cakismasiz.append(Segment(segment.bas, son, segment.metin, segment.guven))
        cakismasiz = filtrele_halusinasyonlar(
            cakismasiz, ayar.stt.guven_esik, ayar.stt.guven_kisa_esik,
        )
        return _kisa_altyazilari_uzat(cakismasiz) if ayar.stt.kisa_altyazi_uzat else cakismasiz
    finally:
        if coz is not None:
            del coz
        if model is not None:
            del model
        gc.collect()
