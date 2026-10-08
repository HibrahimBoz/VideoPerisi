namespace Portal.Testleri;

using System.Text.Json;
using Portal.Cekirdek;
using Xunit;

public class DersTanimiTestleri : IDisposable
{
    private readonly string _geciciKlasor;

    public DersTanimiTestleri()
    {
        _geciciKlasor = Path.Combine(Path.GetTempPath(), "VH_DersTanimiTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_geciciKlasor);
    }

    public void Dispose()
    {
        if (Directory.Exists(_geciciKlasor))
        {
            try
            {
                Directory.Delete(_geciciKlasor, true);
            }
            catch
            {
                // Temizleme hatasını yut
            }
        }
    }

    private string StandartDersJsonUret(
        int semaSurumu = 1,
        string ad = "fizik101",
        string referansKanal = "kamera1",
        string dosya1 = "kamera1/parca01.mp4",
        string? dosya2 = null,
        string tur2 = "bosluk")
    {
        return $$"""
        {
          "sema_surumu": {{semaSurumu}},
          "ad": "{{ad}}",
          "referans_kanal": "{{referansKanal}}",
          "kanallar": [
            {
              "ad": "kamera1",
              "ofset_sn": 0.0,
              "drift_ppm": 0.0,
              "guvenilir": true,
              "parcalar": [
                {
                  "dosya": "{{dosya1}}",
                  "sanal_bas": 0.0,
                  "sanal_son": 600.0,
                  "yerel_bas": 0.0,
                  "tur": "kayit",
                  "ofset_sn": 0.0,
                  "guven": 1.0
                }
              ]
            },
            {
              "ad": "kamera2",
              "ofset_sn": 1.5,
              "drift_ppm": 200.0,
              "guvenilir": false,
              "parcalar": [
                {
                  "dosya": {{(dosya2 != null ? $"\"{dosya2}\"" : "null")}},
                  "sanal_bas": 0.0,
                  "sanal_son": 100.0,
                  "yerel_bas": 0.0,
                  "tur": "{{tur2}}"
                },
                {
                  "dosya": "kamera2/parca02.mp4",
                  "sanal_bas": 100.0,
                  "sanal_son": 600.0,
                  "yerel_bas": 5.0,
                  "tur": "cakisma_kirpildi"
                }
              ]
            }
          ],
          "senkron": {
            "yontem": "ses_capraz_korelasyon",
            "drift_ppm": 200.0,
            "guven": 0.88,
            "guvenilir": true,
            "neden": "yüksek korelasyon"
          },
          "elle_isaretler": [
            {
              "kanal": "kamera2",
              "sanal_sn": 150.0,
              "ortak_an_id": "an_01"
            }
          ]
        }
        """;
    }

    [Fact]
    public void Okuyucu_GecerliJson_BasariylaAyrArrayveDersOlusturur()
    {
        string json = StandartDersJsonUret();
        string yol = Path.Combine(_geciciKlasor, "ders.json");
        File.WriteAllText(yol, json);

        var ders = DersTanimiOkuyucu.Oku(yol);

        Assert.Equal(1, ders.SemaSurumu);
        Assert.Equal("fizik101", ders.Ad);
        Assert.Equal("kamera1", ders.ReferansKanal);
        Assert.Equal(2, ders.Kanallar.Count);
        Assert.Equal("ses_capraz_korelasyon", ders.Senkron.Yontem);
        Assert.True(ders.Senkron.Guvenilir);
        Assert.Single(ders.ElleIsaretler);
        Assert.Equal("an_01", ders.ElleIsaretler[0].OrtakAnId);

        var refKanal = ders.ReferansKanali;
        Assert.NotNull(refKanal);
        Assert.Equal("kamera1", refKanal.Ad);
    }

    [Fact]
    public void Okuyucu_GoreliDosyaYollari_DersKlasoruneGoreMutlakYapilir()
    {
        string json = StandartDersJsonUret(dosya1: "alt/video.mp4");
        string yol = Path.Combine(_geciciKlasor, "ders.json");
        File.WriteAllText(yol, json);

        var ders = DersTanimiOkuyucu.Oku(yol);
        var parca = ders.Kanallar[0].Parcalar[0];

        string beklenenMutlak = Path.GetFullPath(Path.Combine(_geciciKlasor, "alt/video.mp4"));
        Assert.Equal(beklenenMutlak, parca.Dosya);
    }

    [Fact]
    public void Okuyucu_BoslukDiliminde_DosyaNullOlabilir()
    {
        string json = StandartDersJsonUret(dosya2: null, tur2: "bosluk");
        var ders = DersTanimiOkuyucu.MetindenOku(json, _geciciKlasor);

        var boslukDilimi = ders.Kanallar[1].Parcalar[0];
        Assert.Equal("bosluk", boslukDilimi.Tur);
        Assert.Null(boslukDilimi.Dosya);
        Assert.False(boslukDilimi.KayitVar);
    }

    [Fact]
    public void Okuyucu_KayitDiliminde_DosyaNullOldugunda_InvalidDataExceptionFirlatir()
    {
        string json = StandartDersJsonUret(dosya2: null, tur2: "kayit");
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json, _geciciKlasor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public void Okuyucu_BilinmeyenSemaSurumu_InvalidDataExceptionFirlatir(int surum)
    {
        string json = StandartDersJsonUret(semaSurumu: surum);
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json, _geciciKlasor));
    }

    [Fact]
    public void Okuyucu_EksikSemaSurumu_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "ad": "ders1",
          "referans_kanal": "k1",
          "kanallar": [],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true },
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_EksikAd_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "referans_kanal": "k1",
          "kanallar": [],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true },
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_EksikReferansKanal_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "ad": "ders1",
          "kanallar": [],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true },
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_EksikKanallar_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "ad": "ders1",
          "referans_kanal": "k1",
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true },
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_EksikSenkron_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "ad": "ders1",
          "referans_kanal": "k1",
          "kanallar": [],
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_TurkceKarakterler_SorunsuzOkunur()
    {
        string json = StandartDersJsonUret(ad: "Çözümlü Türkçe Dersi Öğrenimi - Şiir & Ağaç");
        string yol = Path.Combine(_geciciKlasor, "turkce_ders.json");
        File.WriteAllText(yol, json, System.Text.Encoding.UTF8);

        var ders = DersTanimiOkuyucu.Oku(yol);
        Assert.Equal("Çözümlü Türkçe Dersi Öğrenimi - Şiir & Ağaç", ders.Ad);
    }

    [Fact]
    public void Okuyucu_DosyaBulunamadiginda_FileNotFoundExceptionFirlatir()
    {
        string olmayan = Path.Combine(_geciciKlasor, "olmayan_ders.json");
        Assert.Throws<FileNotFoundException>(() => DersTanimiOkuyucu.Oku(olmayan));
    }

    [Fact]
    public void Okuyucu_BozukJson_InvalidDataExceptionFirlatir()
    {
        string bozuk = "{ sema_surumu: 1, ad: broken json... ";
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(bozuk));
    }

    [Fact]
    public void Okuyucu_SanalSonSanalBastanKucukse_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "ad": "ders1",
          "referans_kanal": "k1",
          "kanallar": [
            {
              "ad": "k1",
              "ofset_sn": 0.0,
              "drift_ppm": 0.0,
              "guvenilir": true,
              "parcalar": [
                {
                  "dosya": "video.mp4",
                  "sanal_bas": 100.0,
                  "sanal_son": 50.0,
                  "yerel_bas": 0.0,
                  "tur": "kayit"
                }
              ]
            }
          ],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true },
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_GecersizParcaTuru_InvalidDataExceptionFirlatir()
    {
        string json = StandartDersJsonUret(tur2: "bilinmeyen_tur");
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json, _geciciKlasor));
    }

    [Fact]
    public void Okuyucu_EksikElleIsaretler_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "ad": "ders1",
          "referans_kanal": "k1",
          "kanallar": [],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true }
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_KanalEksikOfset_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "ad": "ders1",
          "referans_kanal": "k1",
          "kanallar": [
            {
              "ad": "k1",
              "drift_ppm": 0.0,
              "guvenilir": true,
              "parcalar": []
            }
          ],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true },
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Okuyucu_SenkronGecersizNeden_InvalidDataExceptionFirlatir()
    {
        string json = """
        {
          "sema_surumu": 1,
          "ad": "ders1",
          "referans_kanal": "k1",
          "kanallar": [],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true, "neden": 123 },
          "elle_isaretler": []
        }
        """;
        Assert.Throws<InvalidDataException>(() => DersTanimiOkuyucu.MetindenOku(json));
    }

    [Fact]
    public void Esleme_Formul_PythonTanimiIleBirebirSabitOrneklerleDogrula()
    {
        // Python formülü: kanal_zamani = sanal*(1+drift_ppm*1e-6) + ofset_sn
        // Örnek 1: Driftsiz, 5 sn ofsetli
        var kanal1 = new Kanal("k1", 5.0, 0.0, true, Array.Empty<ParcaTanimi>());
        Assert.Equal(105.0, kanal1.KanalZamaniHesapla(100.0), precision: 6);
        Assert.Equal(100.0, kanal1.SanalZamanHesapla(105.0), precision: 6);

        // Örnek 2: 250 ppm drift, -2.0 sn ofset, 1000 sanal sn
        // 1000 * (1 + 0.000250) - 2.0 = 1000.25 - 2.0 = 998.25
        var kanal2 = new Kanal("k2", -2.0, 250.0, true, Array.Empty<ParcaTanimi>());
        Assert.Equal(998.25, kanal2.KanalZamaniHesapla(1000.0), precision: 6);
        Assert.Equal(1000.0, kanal2.SanalZamanHesapla(998.25), precision: 6);

        // Örnek 3: -100 ppm drift, 1.25 sn ofset, 500 sanal sn
        // 500 * (1 - 0.000100) + 1.25 = 499.95 + 1.25 = 501.20
        var kanal3 = new Kanal("k3", 1.25, -100.0, true, Array.Empty<ParcaTanimi>());
        Assert.Equal(501.20, kanal3.KanalZamaniHesapla(500.0), precision: 6);
        Assert.Equal(500.0, kanal3.SanalZamanHesapla(501.20), precision: 6);
    }

    [Fact]
    public void Esleme_SanalDenYerele_DriftsizTekParca_DogruEslenir()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("video1.mp4", 0.0, 600.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        var (dosya, yerelSn, kayitVar) = kanal.SanalDenYerele(123.45);
        Assert.True(kayitVar);
        Assert.Equal("video1.mp4", dosya);
        Assert.Equal(123.45, yerelSn, precision: 6);
    }

    [Fact]
    public void Esleme_SanalDenYerele_DriftVeOfsetli_DogruEslenir()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("video2.mp4", 0.0, 1000.0, 10.0, "kayit")
        };
        // 200 ppm drift, 3.0 sn ofset
        var kanal = new Kanal("k2", 3.0, 200.0, true, parcalar);

        // Sanal: 100 sn -> kanal zamanı = 100 * 1.0002 + 3.0 = 103.02
        // yerelSn = yerelBas (10) + (103.02 - 0) = 113.02
        var (dosya, yerelSn, kayitVar) = kanal.SanalDenYerele(100.0);
        Assert.True(kayitVar);
        Assert.Equal("video2.mp4", dosya);
        Assert.Equal(113.02, yerelSn, precision: 6);
    }

    [Fact]
    public void Esleme_ParcaSiniri_IleriParcaSecilir()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("parca1.mp4", 0.0, 100.0, 0.0, "kayit"),
            new("parca2.mp4", 100.0, 200.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        // 100.0 tam sınır noktasında parca2 seçilmeli ve yerel saniyesi 0 olmalı
        var (dosya, yerelSn, kayitVar) = kanal.SanalDenYerele(100.0);
        Assert.True(kayitVar);
        Assert.Equal("parca2.mp4", dosya);
        Assert.Equal(0.0, yerelSn, precision: 6);

        // 99.99'da parca1 seçilmeli
        var (d1, y1, k1) = kanal.SanalDenYerele(99.99);
        Assert.True(k1);
        Assert.Equal("parca1.mp4", d1);
        Assert.Equal(99.99, y1, precision: 4);
    }

    [Fact]
    public void Esleme_BoslukDiliminde_KayitVarFalseDoner()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("parca1.mp4", 0.0, 100.0, 0.0, "kayit"),
            new(null, 100.0, 120.0, 0.0, "bosluk"),
            new("parca2.mp4", 120.0, 200.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        // Boşluk dilimi içinde 110.0 sn
        var (dosya, yerelSn, kayitVar) = kanal.SanalDenYerele(110.0);
        Assert.False(kayitVar);
        Assert.Null(dosya);
    }

    [Fact]
    public void Esleme_KayitOncesiVeSonrasi_KayitVarFalseDoner()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("parca1.mp4", 10.0, 100.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        // Parça öncesi
        var (_, _, k1) = kanal.SanalDenYerele(5.0);
        Assert.False(k1);

        // Parça sonrası
        var (_, _, k2) = kanal.SanalDenYerele(105.0);
        Assert.False(k2);
    }

    [Fact]
    public void Esleme_CakismaKirpildi_YerelBasiDikkateAlir()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("video.mp4", 50.0, 150.0, 12.5, "cakisma_kirpildi")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        // Sanal: 70.0 -> ofset = 70 - 50 = 20 -> yerel = 12.5 + 20 = 32.5
        var (dosya, yerelSn, kayitVar) = kanal.SanalDenYerele(70.0);
        Assert.True(kayitVar);
        Assert.Equal("video.mp4", dosya);
        Assert.Equal(32.5, yerelSn, precision: 6);
    }

    [Fact]
    public void Esleme_GidisDonus_DriftliVeDriftsiz_BirbiriyleEslesir()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("kamera1/parca1.mp4", 0.0, 500.0, 0.0, "kayit"),
            new("kamera1/parca2.mp4", 500.0, 1000.0, 2.0, "kayit")
        };
        // 150 ppm drift, -1.25 sn ofset
        var kanal = new Kanal("k1", -1.25, 150.0, true, parcalar);

        double[] testNoktalari = [10.0, 250.0, 499.0, 500.0, 750.0, 999.0];
        foreach (double sanalGiris in testNoktalari)
        {
            var (dosya, yerelSn, kayitVar) = kanal.SanalDenYerele(sanalGiris);
            Assert.True(kayitVar);
            Assert.NotNull(dosya);

            double sanalDonus = kanal.YereldenSanala(dosya, yerelSn);
            Assert.Equal(sanalGiris, sanalDonus, precision: 5);
        }
    }

    [Fact]
    public void Esleme_YereldenSanala_GecersizDosyaVeyaSinirDisi_IstisnaFirlatir()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("kamera1/parca1.mp4", 0.0, 100.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        // Tanımsız dosya
        Assert.Throws<ArgumentException>(() => kanal.YereldenSanala("olmayan.mp4", 50.0));

        // Parça sınırları dışı yerel saniye
        Assert.Throws<ArgumentOutOfRangeException>(() => kanal.YereldenSanala("kamera1/parca1.mp4", 150.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => kanal.YereldenSanala("kamera1/parca1.mp4", -5.0));
    }

    [Fact]
    public void Esleme_ElleOfsetUygulandiginda_OfsetDegisir()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("video.mp4", 0.0, 1000.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        var (d1, y1, _) = kanal.SanalDenYerele(100.0, ekOfsetSn: 0.0);
        var (d2, y2, _) = kanal.SanalDenYerele(100.0, ekOfsetSn: 2.5);

        Assert.Equal(100.0, y1, precision: 6);
        Assert.Equal(102.5, y2, precision: 6);

        double s1 = kanal.YereldenSanala("video.mp4", 102.5, ekOfsetSn: 2.5);
        Assert.Equal(100.0, s1, precision: 6);
    }

    [Fact]
    public void Esleme_FarkliKlasorlerdeAyniAdliDosyalar_YanlisParcayaEslesmez()
    {
        var parcalar = new List<ParcaTanimi>
        {
            new("klasorA/video.mp4", 0.0, 100.0, 0.0, "kayit"),
            new("klasorB/video.mp4", 100.0, 200.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        // klasorA/video.mp4 için sanal zaman 50 olmalı
        double sanalA = kanal.YereldenSanala("klasorA/video.mp4", 50.0);
        Assert.Equal(50.0, sanalA, precision: 6);

        // klasorB/video.mp4 için sanal zaman 150 olmalı (farklı klasördeki aynı adlı dosya doğru eşleşmeli)
        double sanalB = kanal.YereldenSanala("klasorB/video.mp4", 50.0);
        Assert.Equal(150.0, sanalB, precision: 6);

        // Olmayan klasördeki aynı adlı dosya için hata fırlatmalı
        Assert.Throws<ArgumentException>(() => kanal.YereldenSanala("klasorC/video.mp4", 50.0));
    }

    [Fact]
    public void Okuyucu_SiralamasizParcalar_SiralayarakIkiliAramayiDogruCalistirir()
    {
        // JSON içerisinde parçalar kasıtlı olarak ters sırada (200..300 önce, 0..100 sonra)
        string json = """
        {
          "sema_surumu": 1,
          "ad": "siralamasiz_test",
          "referans_kanal": "k1",
          "kanallar": [
            {
              "ad": "k1",
              "ofset_sn": 0.0,
              "drift_ppm": 0.0,
              "guvenilir": true,
              "parcalar": [
                {
                  "dosya": "video2.mp4",
                  "sanal_bas": 200.0,
                  "sanal_son": 300.0,
                  "yerel_bas": 0.0,
                  "tur": "kayit"
                },
                {
                  "dosya": "video1.mp4",
                  "sanal_bas": 0.0,
                  "sanal_son": 100.0,
                  "yerel_bas": 0.0,
                  "tur": "kayit"
                },
                {
                  "dosya": "video1_5.mp4",
                  "sanal_bas": 100.0,
                  "sanal_son": 200.0,
                  "yerel_bas": 0.0,
                  "tur": "kayit"
                }
              ]
            }
          ],
          "senkron": { "yontem": "a", "drift_ppm": 0, "guven": 1, "guvenilir": true },
          "elle_isaretler": []
        }
        """;

        var ders = DersTanimiOkuyucu.MetindenOku(json);
        var kanal = ders.Kanallar[0];

        // Okuyucu parçaları sanal başlangıca göre sıralamış olmalı
        Assert.Equal(0.0, kanal.Parcalar[0].SanalBas);
        Assert.Equal(100.0, kanal.Parcalar[1].SanalBas);
        Assert.Equal(200.0, kanal.Parcalar[2].SanalBas);

        // İkili arama her aralıkta doğru parçayı bulmalı
        var (d1, y1, k1) = kanal.SanalDenYerele(50.0);
        Assert.True(k1);
        Assert.Contains("video1.mp4", d1);
        Assert.Equal(50.0, y1, precision: 4);

        var (d2, y2, k2) = kanal.SanalDenYerele(150.0);
        Assert.True(k2);
        Assert.Contains("video1_5.mp4", d2);
        Assert.Equal(50.0, y2, precision: 4);

        var (d3, y3, k3) = kanal.SanalDenYerele(250.0);
        Assert.True(k3);
        Assert.Contains("video2.mp4", d3);
        Assert.Equal(50.0, y3, precision: 4);
    }

    [Fact]
    public void Esleme_YereldenSanala_SiralamasizYerelParcalar_DogruEslenir()
    {
        // Aynı dosyaya ait parçalar yerel olarak sırasız verilse bile YereldenSanala doğru çalışmalı
        var parcalar = new List<ParcaTanimi>
        {
            new("tek_video.mp4", 100.0, 200.0, 100.0, "kayit"),
            new("tek_video.mp4", 0.0, 100.0, 0.0, "kayit")
        };
        var kanal = new Kanal("k1", 0.0, 0.0, true, parcalar);

        double sanal1 = kanal.YereldenSanala("tek_video.mp4", 25.0);
        Assert.Equal(25.0, sanal1, precision: 5);

        double sanal2 = kanal.YereldenSanala("tek_video.mp4", 125.0);
        Assert.Equal(125.0, sanal2, precision: 5);
    }
}
