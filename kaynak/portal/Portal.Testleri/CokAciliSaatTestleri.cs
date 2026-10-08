namespace Portal.Testleri;

using Portal.Cekirdek;
using Xunit;

public class CokAciliSaatTestleri
{
    private static DersTanimi BasitDersUret(double sureSn = 3600.0)
    {
        var kanallar = new List<Kanal>
        {
            new("kamera1", 0.0, 0.0, true, new List<ParcaTanimi>
            {
                new("kamera1.mp4", 0.0, sureSn, 0.0, "kayit")
            }),
            new("kamera2", 0.0, 0.0, false, new List<ParcaTanimi>
            {
                new("kamera2.mp4", 0.0, sureSn, 0.0, "kayit")
            })
        };

        return new DersTanimi(
            1,
            "test_dersi",
            "kamera1",
            kanallar,
            new SenkronBilgisi("ses", 0.0, 1.0, true),
            Array.Empty<ElleIsaret>()
        );
    }

    [Fact]
    public void KararTablosu_EsikSiniri_0_149_YokDoner()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders, izinVerilenSapmaEsigi: 0.15, sertAtlamaEsigi: 1.0);
        var kanal = ders.Kanallar[0];

        // Sanal zaman 100 sn, bildirilen 99.851 sn -> sapma = 0.149 sn <= 0.15
        saat.Ara(100.0);
        var karar = saat.Karar(kanal, 99.851);

        Assert.IsType<Eylem.Yok>(karar);
    }

    [Fact]
    public void KararTablosu_EsikSiniri_0_151_HafifDuzeltDoner()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders, izinVerilenSapmaEsigi: 0.15, sertAtlamaEsigi: 1.0);
        var kanal = ders.Kanallar[0];

        // Sanal zaman 100 sn, bildirilen 99.849 sn -> sapma = 0.151 sn > 0.15 ve <= 1.0
        saat.Ara(100.0);
        var karar = saat.Karar(kanal, 99.849);

        var hd = Assert.IsType<Eylem.HafifDuzelt>(karar);
        // Geride olduğu için hız katsayısı 1.03 olmalı
        Assert.Equal(1.03, hd.HizKatsayisi);
    }

    [Fact]
    public void KararTablosu_EsikSiniri_0_999_HafifDuzeltDoner()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders, izinVerilenSapmaEsigi: 0.15, sertAtlamaEsigi: 1.0);
        var kanal = ders.Kanallar[0];

        // Sanal zaman 100 sn, bildirilen 99.001 sn -> sapma = 0.999 sn <= 1.0
        saat.Ara(100.0);
        var karar = saat.Karar(kanal, 99.001);

        var hd = Assert.IsType<Eylem.HafifDuzelt>(karar);
        Assert.Equal(1.03, hd.HizKatsayisi);
    }

    [Fact]
    public void KararTablosu_EsikSiniri_1_001_AtlaDoner()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders, izinVerilenSapmaEsigi: 0.15, sertAtlamaEsigi: 1.0);
        var kanal = ders.Kanallar[0];

        // Sanal zaman 100 sn, bildirilen 98.999 sn -> sapma = 1.001 sn > 1.0
        saat.Ara(100.0);
        var karar = saat.Karar(kanal, 98.999);

        var atla = Assert.IsType<Eylem.Atla>(karar);
        Assert.Equal(100.0, atla.HedefYerelSn, precision: 3);
    }

    [Fact]
    public void Karar_SapmaYonu_GerideyseHiz103_IlerideyseHiz097()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);
        var kanal = ders.Kanallar[0];
        saat.Ara(50.0);

        // Geride: bildirilen = 49.7 (hedef = 50.0, sapma = 0.3) -> 1.03
        var kararGeri = saat.Karar(kanal, 49.7);
        var hdGeri = Assert.IsType<Eylem.HafifDuzelt>(kararGeri);
        Assert.Equal(1.03, hdGeri.HizKatsayisi);

        // Sıfırla ve ilerideki durumu test et
        saat.Ara(50.0);
        // İleride: bildirilen = 50.3 (hedef = 50.0, sapma = 0.3) -> 0.97
        var kararIleri = saat.Karar(kanal, 50.3);
        var hdIleri = Assert.IsType<Eylem.HafifDuzelt>(kararIleri);
        Assert.Equal(0.97, hdIleri.HizKatsayisi);
    }

    [Fact]
    public void Karar_KayitDisiAralik_GizleDoner()
    {
        var ders = BasitDersUret(sureSn: 100.0);
        var saat = new CokAciliSaat(ders);
        var kanal = ders.Kanallar[0];

        // Sanal zaman 150 sn (kayıt 100 sn'de bitiyor)
        saat.Ara(150.0);
        var karar = saat.Karar(kanal, 50.0);

        Assert.IsType<Eylem.Gizle>(karar);
    }

    [Fact]
    public void Karar_BoslukDilimi_GizleDoner()
    {
        var kanallar = new List<Kanal>
        {
            new("k1", 0.0, 0.0, true, new List<ParcaTanimi>
            {
                new("v1.mp4", 0.0, 50.0, 0.0, "kayit"),
                new(null, 50.0, 70.0, 0.0, "bosluk"),
                new("v2.mp4", 70.0, 120.0, 0.0, "kayit")
            })
        };
        var ders = new DersTanimi(1, "d", "k1", kanallar, new SenkronBilgisi("a", 0, 1, true), Array.Empty<ElleIsaret>());
        var saat = new CokAciliSaat(ders);

        saat.Ara(60.0); // Boşluk dilimi
        var karar = saat.Karar(kanallar[0], 25.0);

        Assert.IsType<Eylem.Gizle>(karar);
    }

    [Fact]
    public void Karar_Histerezis_EsikYarisinaIneneKadarHiziKorur()
    {
        var ders = BasitDersUret();
        DateTime t0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var saat = new CokAciliSaat(ders, izinVerilenSapmaEsigi: 0.15, sertAtlamaEsigi: 1.0);
        var kanal = ders.Kanallar[0];
        saat.Ara(100.0, t0);

        // 1. Düzeltme başlat: sapma = 0.20 sn (> 0.15)
        var karar1 = saat.Karar(kanal, 99.80, t0);
        var hd1 = Assert.IsType<Eylem.HafifDuzelt>(karar1);
        Assert.Equal(1.03, hd1.HizKatsayisi);
        Assert.True(saat.HisterezisAktifMi(kanal.Ad));

        // 2. 600 ms sonra (500 ms sınırı aşıldı): sapma 0.10 sn'ye indi
        // Normalde 0.10 <= 0.15 olduğu için Yok dönmeliydi, ancak eşiğin yarısı (0.075) aşılmadığı için hızı korumalı
        DateTime t1 = t0.AddMilliseconds(600);
        var karar2 = saat.Karar(kanal, 99.90, t1);
        var hd2 = Assert.IsType<Eylem.HafifDuzelt>(karar2);
        Assert.Equal(1.03, hd2.HizKatsayisi);
        Assert.True(saat.HisterezisAktifMi(kanal.Ad));

        // 3. 1200 ms sonra: sapma 0.05 sn'ye indi (<= 0.075)
        // Eşiğin yarısına indi -> histerezis sonlanmalı ve Yok dönmeli
        DateTime t2 = t0.AddMilliseconds(1200);
        var karar3 = saat.Karar(kanal, 99.95, t2);
        Assert.IsType<Eylem.Yok>(karar3);
        Assert.False(saat.HisterezisAktifMi(kanal.Ad));

        // 4. Histerezis bittikten sonra sapma 0.10 olursa (normal eşik altı), tekrar Yok dönmeli
        DateTime t3 = t0.AddMilliseconds(1800);
        var karar4 = saat.Karar(kanal, 99.90, t3);
        Assert.IsType<Eylem.Yok>(karar4);
    }

    [Fact]
    public void Karar_500msHizSiniri_AyniKanalIcin500msAralikBekler()
    {
        var ders = BasitDersUret();
        DateTime t0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var saat = new CokAciliSaat(ders);
        var kanal = ders.Kanallar[0];
        saat.Ara(100.0, t0);

        // İlk çağrı: düzeltme yapılır
        var k1 = saat.Karar(kanal, 99.70, t0);
        Assert.IsType<Eylem.HafifDuzelt>(k1);

        // 200 ms sonra: 500 ms dolmadığı için yeni düzeltme engellenir -> Yok döner
        var k2 = saat.Karar(kanal, 99.70, t0.AddMilliseconds(200));
        Assert.IsType<Eylem.Yok>(k2);

        // 550 ms sonra: 500 ms dolduğu için düzeltme yapılır
        var k3 = saat.Karar(kanal, 99.70, t0.AddMilliseconds(550));
        Assert.IsType<Eylem.HafifDuzelt>(k3);
    }

    [Fact]
    public void Karar_500msHizSiniri_FarkliKanallarBirbiriniEngellemez()
    {
        var ders = BasitDersUret();
        DateTime t0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var saat = new CokAciliSaat(ders);
        saat.Ara(100.0, t0);

        // Kanal 1 düzeltme alır
        var k1 = saat.Karar(ders.Kanallar[0], 99.70, t0);
        Assert.IsType<Eylem.HafifDuzelt>(k1);

        // 100 ms sonra Kanal 2 düzeltme ister: Kanal 1'in süresinden bağımsız düzeltme almalı
        var k2 = saat.Karar(ders.Kanallar[1], 99.70, t0.AddMilliseconds(100));
        Assert.IsType<Eylem.HafifDuzelt>(k2);
    }

    [Fact]
    public void AnaSaat_OynatDuraklat_ZamaniIlerletirVeDondurur()
    {
        var ders = BasitDersUret();
        DateTime simdi = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var saat = new CokAciliSaat(ders, zamanKaynagi: () => simdi);

        Assert.False(saat.Oynatiliyor);
        Assert.Equal(0.0, saat.SanalZaman);

        saat.Oynat(simdi);
        Assert.True(saat.Oynatiliyor);

        // 5 saniye ilerlet
        simdi = simdi.AddSeconds(5.0);
        Assert.Equal(5.0, saat.SimdiSanalZaman(simdi), precision: 3);

        // Duraklat
        saat.Duraklat(simdi);
        Assert.False(saat.Oynatiliyor);
        Assert.Equal(5.0, saat.SanalZaman, precision: 3);

        // Zaman akmaya devam etse de duraklatıldığı için konum değişmemeli
        simdi = simdi.AddSeconds(10.0);
        Assert.Equal(5.0, saat.SimdiSanalZaman(simdi), precision: 3);
    }

    [Fact]
    public void AnaSaat_Ara_SanalZamaniAyarlaVeKanalHafizasiniSifirla()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);
        var kanal = ders.Kanallar[0];

        // Kanalda histerezis başlat
        saat.Karar(kanal, -0.3);
        Assert.True(saat.HisterezisAktifMi(kanal.Ad));

        // Ara (Seek) yapıldığında konum değişmeli ve histerezis sıfırlanmalı
        saat.Ara(150.0);
        Assert.Equal(150.0, saat.SanalZaman);
        Assert.False(saat.HisterezisAktifMi(kanal.Ad));
    }

    [Fact]
    public void AnaSaat_HizAyarla_OynatmaHiziZamanHesabiniEtkiler()
    {
        var ders = BasitDersUret();
        DateTime simdi = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var saat = new CokAciliSaat(ders, zamanKaynagi: () => simdi);

        saat.Oynat(simdi);
        saat.HizAyarla(1.5, simdi);

        // 10 saniye sonra 10 * 1.5 = 15 saniye ilerlemeli
        simdi = simdi.AddSeconds(10.0);
        Assert.Equal(15.0, saat.SimdiSanalZaman(simdi), precision: 3);
    }

    [Fact]
    public void SesKanali_VarsayilanReferansKanal_DigerleriSessiz()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);

        Assert.Equal("kamera1", saat.AktifSesKanali);
        Assert.False(saat.KanalSessizMi("kamera1"));
        Assert.True(saat.KanalSessizMi("kamera2"));
        Assert.False(saat.Sessiz("kamera1"));
        Assert.True(saat.Sessiz("kamera2"));
    }

    [Fact]
    public void SesKanali_Degistirildiginde_YeniKanalAktifDigerleriSessiz()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);

        saat.SesKanaliSec("kamera2");
        Assert.Equal("kamera2", saat.AktifSesKanali);
        Assert.True(saat.KanalSessizMi("kamera1"));
        Assert.False(saat.KanalSessizMi("kamera2"));
    }

    [Fact]
    public void GorunumModu_IzgaraOdakTek_GecislerdeOynatmaDurumuVeKonumuKorunur()
    {
        var ders = BasitDersUret();
        DateTime simdi = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var saat = new CokAciliSaat(ders, zamanKaynagi: () => simdi);

        saat.Oynat(simdi);
        simdi = simdi.AddSeconds(12.0);

        Assert.IsType<GorunumModu.Izgara>(saat.Gorunum);

        // Odak moduna geç
        saat.GorunumModuDegistir(new GorunumModu.Odak("kamera1"));
        Assert.IsType<GorunumModu.Odak>(saat.Gorunum);
        Assert.True(saat.Oynatiliyor);
        Assert.Equal(12.0, saat.SimdiSanalZaman(simdi), precision: 3);

        // Tek kanal moduna geç
        saat.GorunumModuDegistir(new GorunumModu.Tek("kamera2"));
        Assert.IsType<GorunumModu.Tek>(saat.Gorunum);
        Assert.True(saat.Oynatiliyor);
        Assert.Equal(12.0, saat.SimdiSanalZaman(simdi), precision: 3);

        // Izgara moduna geri dön
        saat.GorunumModuDegistir(new GorunumModu.Izgara());
        Assert.IsType<GorunumModu.Izgara>(saat.Gorunum);
        Assert.True(saat.Oynatiliyor);
    }

    [Fact]
    public void GuvenilmezKanal_DurumEtiketi_SenkronGuvenilmezDoner()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);

        Assert.Equal("senkron güvenilir", saat.DurumEtiketi("kamera1"));
        Assert.Equal("senkron güvenilmez", saat.DurumEtiketi("kamera2"));
        Assert.Equal("senkron güvenilir", ders.Kanallar[0].DurumEtiketi);
        Assert.Equal("senkron güvenilmez", ders.Kanallar[1].DurumEtiketi);
    }

    [Fact]
    public void ElleOfsetUygula_BellekteTutulur_EllesOfsetlerSozluguDoner()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);
        var kanal2 = ders.Kanallar[1];

        saat.ElleOfsetUygula(kanal2, 1.25);

        Assert.True(saat.EllesOfsetler.ContainsKey("kamera2"));
        Assert.Equal(1.25, saat.EllesOfsetler["kamera2"]);
        Assert.Equal(1.25, saat.ElleOfsetler["kamera2"]);
        Assert.Equal(1.25, saat.ElleOfsetAl("kamera2"));

        // Orijinal ders nesnesinin OfsetSn değeri değişmemeli
        Assert.Equal(0.0, kanal2.OfsetSn);
    }

    [Fact]
    public void ElleOfsetUygula_KararHesabinaYansir()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);
        var kanal2 = ders.Kanallar[1];
        saat.Ara(100.0);

        // Ofset yokken 100 sn sanal konumda hedef 100 sn'dir. Bildirilen 100 olunca Yok döner.
        var k1 = saat.Karar(kanal2, 100.0);
        Assert.IsType<Eylem.Yok>(k1);

        // +1.5 sn elle ofset uygula -> hedef artık 101.5 sn olur
        // Bildirilen 100.0 olunca sapma = 1.5 sn > 1.0 sn (SertAtlama) -> Atla(101.5) döner
        saat.Ara(100.0); // Cooldown sıfırlansın
        saat.ElleOfsetUygula(kanal2, 1.5);
        var k2 = saat.Karar(kanal2, 100.0);
        var atla = Assert.IsType<Eylem.Atla>(k2);
        Assert.Equal(101.5, atla.HedefYerelSn, precision: 3);
    }

    [Fact]
    public void Simulasyon_1Saatlik4Kanal_RastgeleGurultuVe300ppmDrift_Sapma04snGecmez()
    {
        // 4 kanallı ders oluştur
        // Kanal 0 (ref): 0 ppm drift, 0 ofset
        // Kanal 1: +300 ppm drift, 0 ofset
        // Kanal 2: -300 ppm drift, 0 ofset
        // Kanal 3: +150 ppm drift, 0 ofset
        double sureSn = 3600.0;
        var kanallar = new List<Kanal>
        {
            new("kamera1", 0.0, 0.0, true, new List<ParcaTanimi> { new("k1.mp4", 0.0, sureSn + 500.0, 0.0, "kayit") }),
            new("kamera2", 0.0, 300.0, true, new List<ParcaTanimi> { new("k2.mp4", 0.0, sureSn + 500.0, 0.0, "kayit") }),
            new("kamera3", 0.0, -300.0, true, new List<ParcaTanimi> { new("k3.mp4", 0.0, sureSn + 500.0, 0.0, "kayit") }),
            new("kamera4", 0.0, 150.0, true, new List<ParcaTanimi> { new("k4.mp4", 0.0, sureSn + 500.0, 0.0, "kayit") })
        };

        var ders = new DersTanimi(
            1, "fizik101", "kamera1", kanallar,
            new SenkronBilgisi("ses", 0.0, 1.0, true),
            Array.Empty<ElleIsaret>()
        );

        DateTime baslangic = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        DateTime simdi = baslangic;

        var saat = new CokAciliSaat(ders, izinVerilenSapmaEsigi: 0.15, sertAtlamaEsigi: 1.0, () => simdi);
        saat.Oynat(simdi);

        // Her kanal için oyuncu yerel zamanı ve hızı
        double[] oyuncuYerel = [0.0, 0.0, 0.0, 0.0];
        double[] oyuncuHiz = [1.0, 1.0, 1.0, 1.0];

        double dt = 0.25; // 250 ms LibVLC zamanlayıcısı
        var rng = new Random(42); // Sabit tohumlu rastgele üreteç

        double maxGozlemlenenSapma = 0.0;

        for (double t = 0; t <= sureSn; t += dt)
        {
            simdi = baslangic.AddSeconds(t);
            double sanalSimdi = saat.SimdiSanalZaman(simdi);

            for (int k = 0; k < kanallar.Count; k++)
            {
                var kanal = kanallar[k];
                var karar = saat.Karar(kanal, oyuncuYerel[k], simdi);

                if (karar is Eylem.HafifDuzelt hd)
                {
                    oyuncuHiz[k] = hd.HizKatsayisi;
                }
                else if (karar is Eylem.Atla atla)
                {
                    oyuncuYerel[k] = atla.HedefYerelSn;
                    oyuncuHiz[k] = 1.0;
                }
                else if (karar is Eylem.Yok)
                {
                    if (!saat.HisterezisAktifMi(kanal.Ad))
                    {
                        oyuncuHiz[k] = 1.0;
                    }
                }

                // Sapmayı ölç: oyuncu yerel zamanı ile kanalın sanal zamana göre olması gereken yerel zamanı
                var (_, beklenenYerel, kayitVar) = kanal.SanalDenYerele(sanalSimdi);
                Assert.True(kayitVar);

                double anlikSapma = Math.Abs(oyuncuYerel[k] - beklenenYerel);
                if (anlikSapma > maxGozlemlenenSapma)
                {
                    maxGozlemlenenSapma = anlikSapma;
                }

                Assert.True(anlikSapma <= 0.4,
                    $"t={t:F2} sn anında {kanal.Ad} için sapma ({anlikSapma:F4} sn) 0.4 sn sınırını aştı!");

                // Bir sonraki adıma simülasyon: oyuncu hızı + küçük rastgele titreşim
                double gurultu = (rng.NextDouble() - 0.5) * 0.004; // +-2 ms rastgele gürültü
                oyuncuYerel[k] += dt * oyuncuHiz[k] + gurultu;
            }
        }

        // Test sonunda maksimum sapmanın 0.4'ün altında kaldığı kanıtlanmalı
        Assert.True(maxGozlemlenenSapma <= 0.4);
    }

    [Fact]
    public void AnaSaat_SimdiZamanKaynagiEnjekte_VeHizOzelligi()
    {
        var ders = BasitDersUret();
        DateTime t0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var saat = new CokAciliSaat(ders, () => t0);

        saat.Oynat(t0);
        saat.Hiz = 2.0;
        Assert.Equal(2.0, saat.Hiz);

        DateTime t1 = t0.AddSeconds(5.0);
        double sanal = saat.Simdi(() => t1);
        Assert.Equal(10.0, sanal, precision: 3);
    }

    [Fact]
    public void GorunumModu_UnicodeAliasVeOzellikGecisi()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);

        saat.GorunumModu = new GörünümModu.Odak("kamera1");
        Assert.IsType<GorunumModu.Odak>(saat.GorunumModu);

        saat.GorunumModu = new GörünümModu.Tek("kamera2");
        Assert.IsType<GorunumModu.Tek>(saat.GorunumModu);

        saat.GorunumModu = new GörünümModu.Izgara();
        Assert.IsType<GorunumModu.Izgara>(saat.GorunumModu);
    }

    [Fact]
    public void SesKanali_OzellikAtamasi_AktifSesKanaliniGunceller()
    {
        var ders = BasitDersUret();
        var saat = new CokAciliSaat(ders);

        saat.SesKanali = "kamera2";
        Assert.Equal("kamera2", saat.SesKanali);
        Assert.True(saat.Sessiz("kamera1"));
        Assert.False(saat.Sessiz("kamera2"));
    }

    [Fact]
    public async Task EszamanliCagrilar_CokKanalliVeAraCagrilari_SozlukBozulmazVeHataVermez()
    {
        var kanallar = new List<Kanal>();
        for (int i = 1; i <= 8; i++)
        {
            kanallar.Add(new Kanal($"kamera{i}", 0.0, 0.0, true, new List<ParcaTanimi>
            {
                new($"kamera{i}.mp4", 0.0, 3600.0, 0.0, "kayit")
            }));
        }

        var ders = new DersTanimi(1, "cok_kanalli", "kamera1", kanallar, new SenkronBilgisi("ses", 0, 1, true), Array.Empty<ElleIsaret>());
        var saat = new CokAciliSaat(ders);
        saat.Oynat();

        var istisnalar = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Çok sayıda iş parçacığı ile eşzamanlı Karar, Ara ve ElleOfset çağrıları yap
        var gorevler = new List<Task>();

        for (int i = 0; i < 8; i++)
        {
            int kanalNo = i;
            var kanal = kanallar[kanalNo];
            gorevler.Add(Task.Run(() =>
            {
                try
                {
                    for (int iter = 0; iter < 200; iter++)
                    {
                        double yerelSn = iter * 0.1;
                        saat.Karar(kanal, yerelSn);
                        saat.ElleOfsetUygula(kanal, (iter % 5) * 0.1);
                        _ = saat.HisterezisAktifMi(kanal.Ad);
                        _ = saat.KanalHizKatsayisi(kanal.Ad);
                    }
                }
                catch (Exception ex)
                {
                    istisnalar.Add(ex);
                }
            }));
        }

        // Eşzamanlı Ara (Seek) çağıran ayrı görev
        gorevler.Add(Task.Run(() =>
        {
            try
            {
                for (int iter = 0; iter < 100; iter++)
                {
                    saat.Ara(iter * 5.0);
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                istisnalar.Add(ex);
            }
        }));

        await Task.WhenAll(gorevler);

        Assert.Empty(istisnalar);
    }
}
