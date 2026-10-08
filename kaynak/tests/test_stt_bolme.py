import unittest
import sys
import tempfile
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "app"))
from videohat.stt import (  # noqa: E402
    Segment, _adaylari_sec, _ikinci_gecis_bolgeleri, _istem_yap,
    _parca_siniri_tekrar_temizle, bolumleri_kes, filtrele_halusinasyonlar,
)
from videohat.ayar import yukle  # noqa: E402


def kelime(start, end, text, probability=0.99):
    return SimpleNamespace(start=start, end=end, word=f" {text}", probability=probability)


def bolum(start, end, text="", words=None):
    return SimpleNamespace(start=start, end=end, text=text, words=words)


class SttBolmeTesti(unittest.TestCase):
    def test_uzun_segment_sessizligi_kelime_zamanindan_atar(self):
        words = [kelime(510 + i * 2, 511 + i * 2, "konuşma") for i in range(17)]
        sonuc = bolumleri_kes([bolum(4.9, 420.3, "uzun", words)])
        self.assertTrue(sonuc)
        self.assertGreaterEqual(sonuc[0].bas, 510)
        self.assertTrue(all(s.son - s.bas <= 7 for s in sonuc))

    def test_yedi_saniye_ve_seksen_dort_karakter_sinirinda_boler(self):
        words = [kelime(i * 0.5, i * 0.5 + 0.4, "abcdefgh") for i in range(22)]
        sonuc = bolumleri_kes([bolum(0, 20, words=words)])
        self.assertGreater(len(sonuc), 1)
        self.assertTrue(all(s.son - s.bas <= 7 for s in sonuc))
        self.assertTrue(all(len(s.metin) <= 84 for s in sonuc))

    def test_bir_saniyeden_buyuk_bosluk_boler(self):
        words = [kelime(0, 0.5, "Birinci"), kelime(1.6, 2.0, "ikinci.")]
        sonuc = bolumleri_kes([bolum(0, 2, words=words)])
        self.assertEqual(len(sonuc), 2)
        self.assertEqual(sonuc[0].son, 1.0)
        self.assertEqual(sonuc[1].bas, 1.6)

    def test_uc_ardisik_ayni_metinden_ilkini_tutar(self):
        sonuc = filtrele_halusinasyonlar([
            Segment(0, 1, "Aynı metin"), Segment(1, 2, "Aynı metin"),
            Segment(2, 3, "Aynı metin"),
        ])
        self.assertEqual(sonuc, [Segment(0, 1, "Aynı metin")])

    def test_bilinen_halisinasyon_filtrelenir(self):
        metinler = [
            "Altyazı M.K.",
            "İzlediğiniz için teşekkür ederim.",
            "İZLEDİĞİNİZ İÇİN TEŞEKKÜRLER",
        ]
        self.assertEqual(
            filtrele_halusinasyonlar([Segment(0, 2, metin) for metin in metinler]),
            [],
        )

    def test_ingilizce_sarki_filtresi_turkce_metinleri_korur(self):
        self.assertEqual(
            filtrele_halusinasyonlar([
                Segment(0, 2, "Let me see you bounce, let me see you bounce, ooo"),
            ]),
            [],
        )
        turkce = [
            Segment(0, 2, "bugun ders yok"),
            Segment(2, 4, "Bugün ders yok"),
            Segment(4, 6, "Let me see bir tane"),
        ]
        self.assertEqual(filtrele_halusinasyonlar(turkce), turkce)

    def test_dusuk_guvenli_kisa_segment_elenir(self):
        dusuk = [kelime(0, 1, "kutu,", 0.2), kelime(1, 2, "kutu", 0.2),
                 kelime(2, 3, "söyle", 0.2), kelime(3, 3.5, "bir", 0.2),
                 kelime(3.5, 4, "tane", 0.2),
                 kelime(5.1, 6.1, "yüksek", 0.9),
                 kelime(6.1, 7.1, "güven", 0.9),
                 kelime(8.2, 8.7, ".sorularlaislamiyet", 0.2),
                 kelime(8.7, 9.2, ".com", 0.2),
                 kelime(10.3, 10.8, "Erişim", 0.2),
                 kelime(10.8, 11.3, "www", 0.2),
                 kelime(20, 25, "güvensiz", 0.2)]
        sonuc = bolumleri_kes([bolum(0, 25, words=dusuk)])
        self.assertFalse(any(s.guven == 0.2 for s in sonuc))
        self.assertIn("yüksek güven", [s.metin for s in sonuc])

    def test_orta_guven_sadece_kisa_segmentte_elenir(self):
        kisa = [kelime(0, 1, "kısa", 0.4)]
        uzun = [kelime(5, 6, "bu", 0.4), kelime(6, 7, "uzun", 0.4), kelime(7, 8, "bir", 0.4),
                 kelime(8, 9, "cümle", 0.4), kelime(9, 10, "midir", 0.4)]
        sonuc = bolumleri_kes([bolum(0, 1, words=kisa), bolum(5, 10, words=uzun)])
        self.assertEqual([s.metin for s in sonuc], ["bu uzun bir cümle midir"])

    def test_sifira_yakin_tek_kelime_atilir(self):
        sonuc = bolumleri_kes([bolum(0, 0.2, words=[kelime(0, 0.2, "değerli")])])
        self.assertEqual(sonuc, [])

    def test_eski_stt_ayarlari_guven_esiklerini_varsayilan_alir(self):
        with tempfile.TemporaryDirectory() as gecici:
            kok = Path(gecici)
            (kok / "ayar.toml").write_text("[stt]\nbeam_size = 3\n", encoding="utf-8")
            ayar = yukle(kok)
        self.assertEqual(ayar.stt.guven_esik, 0.45)
        self.assertEqual(ayar.stt.guven_kisa_esik, 0.30)
        self.assertTrue(ayar.stt.baglam_tasi)
        self.assertTrue(ayar.stt.ikinci_gecis)
        self.assertEqual(ayar.stt.ikinci_gecis_esik, 0.60)
        self.assertEqual(ayar.stt.ikinci_gecis_en_cok_oran, 0.25)

    def test_kelimesiz_uzun_segment_kisaltilir(self):
        sonuc = bolumleri_kes([bolum(2, 40, "abcdefghij")])
        self.assertEqual(sonuc, [Segment(2, 3.0, "abcdefghij")])

    def test_parca_ofseti_sirayi_ve_cakismayi_duzeltir(self):
        words = [kelime(0, 1, "ikinci parça")]
        sonuc = bolumleri_kes([bolum(0, 1, words=words)], ofset=1200, onceki_son=1200.5)
        self.assertEqual(sonuc, [Segment(1200.5, 1201.5, "ikinci parça")])

    def test_parca_siniri_turkce_case_ve_ek_tekrari_birlestirir(self):
        sonuc = _parca_siniri_tekrar_temizle(
            [Segment(1198, 1200, "RADAR İZİ"), Segment(1200, 1202, "radar izi")],
            [1200],
        )
        self.assertEqual(len(sonuc), 1)
        self.assertEqual(sonuc[0].metin, "RADAR İZİ")
        ek = _parca_siniri_tekrar_temizle(
            [Segment(1198, 1200, "Sinyali tekrar"), Segment(1200, 1202, "tekrar")],
            [1200],
        )
        self.assertEqual([s.metin for s in ek], ["Sinyali tekrar"])

    def test_kisa_altyazi_bir_saniyeye_uzar_ama_sonrakine_tasmaz(self):
        sonuc = bolumleri_kes([
            bolum(0, 0.5, words=[kelime(0, 0.5, "çıkar.")]),
            bolum(0.7, 1.2, words=[kelime(0.7, 1.2, "sonraki")]),
        ])
        self.assertEqual(sonuc[0].son, 0.7)
        self.assertLessEqual(sonuc[0].son, sonuc[1].bas)
        tek = bolumleri_kes([bolum(8, 8.5, words=[kelime(8, 8.5, "çıkar.")])])
        self.assertEqual(tek[0].son, 9.0)

    def test_ikinci_gecis_bolgesi_guven_butcesi_ve_birlestirme(self):
        segments = [
            Segment(10, 14, "düşük güvenli alan", 0.4),
            Segment(15, 18, "ikinci düşük alan", 0.5),
            Segment(30, 34, "yüksek güven", 0.9),
        ]
        bolgeler = _ikinci_gecis_bolgeleri(segments, 100, 0.6, 0.25)
        self.assertEqual(bolgeler, [(9, 19)])
        self.assertLessEqual(sum(b - a for a, b in bolgeler), 25)

    def test_aday_secimi_guven_farki_kucukse_ilk_gecisi_korur(self):
        ilk = [Segment(1, 2, "ilk geçiş", 0.70)]
        temiz = [Segment(1, 2, "temiz yeniden", 0.72)]
        self.assertEqual(_adaylari_sec(ilk, temiz, []), ilk)
        daha_iyi = [Segment(1, 2, "daha güvenli", 0.80)]
        self.assertEqual(_adaylari_sec(ilk, daha_iyi, []), daha_iyi)
        halusinasyon = [Segment(1, 4, "abone ol", 0.99)]
        self.assertEqual(_adaylari_sec(halusinasyon, daha_iyi, []), daha_iyi)
        self.assertEqual(_adaylari_sec(halusinasyon, halusinasyon, []), [])

    def test_ilk_prompt_terimleri_ve_onceki_cumleyle_olusturulur(self):
        self.assertEqual(_istem_yap("", "radar, karıştırma"), "radar, karıştırma")
        self.assertEqual(
            _istem_yap("eski cümle. Son cümle burada.", "radar"),
            "Son cümle burada. radar",
        )


if __name__ == "__main__":
    unittest.main()
