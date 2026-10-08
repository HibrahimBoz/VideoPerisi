namespace Portal.Testleri;

using System.Text;
using Portal.Cekirdek;
using Xunit;

public class YardimciTestleri
{
    // ==========================================
    // ZamanBicim Testleri
    // ==========================================

    [Fact]
    public void ZamanBicim_Sifir_000Doner()
    {
        string sonuc = ZamanBicim.Bicimle(0);
        Assert.Equal("0:00", sonuc);

        string sonucSifirli = ZamanBicim.Bicimle(0, sifirDoldur: true);
        Assert.Equal("00:00", sonucSifirli);
    }

    [Fact]
    public void ZamanBicim_BirDakikadanKisa_TekBasamakDakikaVeIkiBasamakSaniyeDoner()
    {
        string sonuc5 = ZamanBicim.Bicimle(5);
        Assert.Equal("0:05", sonuc5);

        string sonuc45 = ZamanBicim.Bicimle(45);
        Assert.Equal("0:45", sonuc45);
    }

    [Fact]
    public void ZamanBicim_BirDakikadanUzun_DakikaVeSaniyeDoner()
    {
        string sonuc = ZamanBicim.Bicimle(65);
        Assert.Equal("1:05", sonuc);

        string sonucDakika = ZamanBicim.Bicimle(754); // 12 dk 34 sn
        Assert.Equal("12:34", sonucDakika);
    }

    [Fact]
    public void ZamanBicim_BirSaatUzeri_SaatDakikaSaniyeDoner()
    {
        string sonuc1Saat = ZamanBicim.Bicimle(3600);
        Assert.Equal("1:00:00", sonuc1Saat);

        string sonuc1Saat5Dk = ZamanBicim.Bicimle(3905); // 1 saat 5 dk 5 sn
        Assert.Equal("1:05:05", sonuc1Saat5Dk);
    }

    [Fact]
    public void ZamanBicim_SifirDoldur_IkiBasamakliDakikaDoner()
    {
        string sonuc = ZamanBicim.Bicimle(65, sifirDoldur: true);
        Assert.Equal("01:05", sonuc);

        string sonucSaat = ZamanBicim.Bicimle(3905, sifirDoldur: true);
        Assert.Equal("01:05:05", sonucSaat);

        string sonucMmSs = ZamanBicim.BicimleMmSs(7);
        Assert.Equal("00:07", sonucMmSs);
    }

    [Fact]
    public void ZamanBicim_NegatifVeGecersizDegerler_SifirOlarakIsler()
    {
        Assert.Equal("0:00", ZamanBicim.Bicimle(-15));
        Assert.Equal("0:00", ZamanBicim.Bicimle(double.NaN));
        Assert.Equal("0:00", ZamanBicim.Bicimle(double.PositiveInfinity));
    }

    [Fact]
    public void ZamanBicim_Cozumle_GecerliMetinlerDogruSaniyeyeDonusur()
    {
        bool basarili1 = ZamanBicim.Cozumle("1:05", out double s1);
        Assert.True(basarili1);
        Assert.Equal(65.0, s1);

        bool basarili2 = ZamanBicim.Cozumle("01:05:05", out double s2);
        Assert.True(basarili2);
        Assert.Equal(3905.0, s2);
    }

    [Fact]
    public void ZamanBicim_Cozumle_GecersizMetinlerFalseDoner()
    {
        Assert.False(ZamanBicim.Cozumle(null, out _));
        Assert.False(ZamanBicim.Cozumle("", out _));
        Assert.False(ZamanBicim.Cozumle("gecersiz", out _));
        Assert.False(ZamanBicim.Cozumle("1:2:3:4", out _));
        Assert.False(ZamanBicim.Cozumle("1:75", out _)); // 75 saniye geçersiz
    }

    // ==========================================
    // SrtUretici Testleri
    // ==========================================

    [Fact]
    public void SrtUretici_ZamanDamgasi_StandartFormattaUretir()
    {
        Assert.Equal("00:00:00,000", SrtUretici.ZamanDamgasi(0));
        Assert.Equal("00:00:01,500", SrtUretici.ZamanDamgasi(1.5));
        Assert.Equal("01:02:03,456", SrtUretici.ZamanDamgasi(3723.456));
        Assert.Equal("00:00:00,000", SrtUretici.ZamanDamgasi(-5));
    }

    [Fact]
    public void SrtUretici_Uret_SegmentListesiniSrtFormatinaDonusturur()
    {
        var segmentler = new List<Segment>
        {
            new(1, 1.0, 3.5, "Birinci cümle"),
            new(2, 4.0, 7.25, "İkinci cümle")
        };

        string srt = SrtUretici.Uret(segmentler);

        string beklenen = $"1{Environment.NewLine}00:00:01,000 --> 00:00:03,500{Environment.NewLine}Birinci cümle{Environment.NewLine}{Environment.NewLine}" +
                          $"2{Environment.NewLine}00:00:04,000 --> 00:00:07,250{Environment.NewLine}İkinci cümle{Environment.NewLine}{Environment.NewLine}";
        Assert.Equal(beklenen, srt);
    }

    [Fact]
    public void SrtUretici_Uret_BoslukVeGecersizSegmentleriAtlar()
    {
        var segmentler = new List<Segment>
        {
            new(1, 0.0, 1.0, "   "),
            new(2, 3.0, 5.0, "Geçerli metin")
        };

        string srt = SrtUretici.Uret(segmentler);

        // Yalnız 1 numaralı cue üretilmeli ve geçerli metin olmalı (tam SRT karşılaştırması)
        string beklenen = $"1{Environment.NewLine}00:00:03,000 --> 00:00:05,000{Environment.NewLine}Geçerli metin{Environment.NewLine}{Environment.NewLine}";
        Assert.Equal(beklenen, srt);
    }

    [Fact]
    public void SrtUretici_GeciciDosya_FizikselDosyaOlustururVeIcerikDogrudur()
    {
        var efektifler = new List<EfektifSegment>
        {
            new(1, 10.0, 15.0, "Düzeltilmiş metin", Array.Empty<Duzeltme>(), false)
        };

        string dosyaYolu = SrtUretici.GeciciSrtDosyasiOlustur(efektifler);
        try
        {
            Assert.True(File.Exists(dosyaYolu));
            string icerik = File.ReadAllText(dosyaYolu, Encoding.UTF8);
            Assert.Contains("00:00:10,000 --> 00:00:15,000", icerik);
            Assert.Contains("Düzeltilmiş metin", icerik);
        }
        finally
        {
            if (File.Exists(dosyaYolu))
            {
                File.Delete(dosyaYolu);
            }
        }
    }

    // ==========================================
    // ParolaGecikmeSayaci Testleri
    // ==========================================

    [Fact]
    public void ParolaGecikmeSayaci_BaslangictaKilitliDegil()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        Assert.False(sayac.KilitliMi());
        Assert.Equal(0, sayac.HataliDenemeSayisi);
        Assert.Equal(0, sayac.KalanBeklemeSaniyesi());
        Assert.True(sayac.DenemeYapilabilirMi());
    }

    [Fact]
    public void ParolaGecikmeSayaci_BesHataliDenemedeKilitlenir()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        var baslangic = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 4; i++)
        {
            sayac.HataliGiris(baslangic);
            Assert.False(sayac.KilitliMi(baslangic));
            Assert.True(sayac.DenemeYapilabilirMi(baslangic));
        }

        // 5. hatalı giriş
        sayac.HataliGiris(baslangic);
        Assert.True(sayac.KilitliMi(baslangic));
        Assert.False(sayac.DenemeYapilabilirMi(baslangic));
        Assert.Equal(5, sayac.HataliDenemeSayisi);
        Assert.Equal(30, sayac.KalanBeklemeSaniyesi(baslangic));
    }

    [Fact]
    public void ParolaGecikmeSayaci_KilitSuresiDoluncaKilitAcar()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        var baslangic = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            sayac.HataliGiris(baslangic);
        }

        // 20 saniye sonra hâlâ kilitli
        var sonra20Sn = baslangic.AddSeconds(20);
        Assert.True(sayac.KilitliMi(sonra20Sn));
        Assert.Equal(10, sayac.KalanBeklemeSaniyesi(sonra20Sn));

        // 31 saniye sonra kilit açılmış olmalı
        var sonra31Sn = baslangic.AddSeconds(31);
        Assert.False(sayac.KilitliMi(sonra31Sn));
        Assert.True(sayac.DenemeYapilabilirMi(sonra31Sn));
        Assert.Equal(0, sayac.KalanBeklemeSaniyesi(sonra31Sn));
    }

    [Fact]
    public void ParolaGecikmeSayaci_BasariliGirisSayaciSifirlar()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        sayac.HataliGiris();
        sayac.HataliGiris();
        Assert.Equal(2, sayac.HataliDenemeSayisi);

        sayac.BasariliGiris();
        Assert.Equal(0, sayac.HataliDenemeSayisi);
        Assert.False(sayac.KilitliMi());
    }

    [Fact]
    public void ParolaGecikmeSayaci_KalanBeklemeSaniyesiDogruHesaplanir()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 3, kilitSuresiSaniye: 60);
        var zaman = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 3; i++)
        {
            sayac.HataliGiris(zaman);
        }

        Assert.Equal(60, sayac.KalanBeklemeSaniyesi(zaman));
        Assert.Equal(45, sayac.KalanBeklemeSaniyesi(zaman.AddSeconds(15)));
        Assert.Equal(1, sayac.KalanBeklemeSaniyesi(zaman.AddSeconds(59.1)));
        Assert.Equal(0, sayac.KalanBeklemeSaniyesi(zaman.AddSeconds(60)));
    }

    [Fact]
    public void ParolaGecikmeSayaci_KilitSuresiDoluncaYeniDenemedeSayacSifirlanirVeTekHatadaKilitlenmez()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        var baslangic = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            sayac.HataliGiris(baslangic);
        }
        Assert.True(sayac.KilitliMi(baslangic));

        // 31 saniye sonra kilit süresi doldu, yeni bir hatalı giriş yapıldı
        var sonra31Sn = baslangic.AddSeconds(31);
        sayac.HataliGiris(sonra31Sn);

        // Yeni döngüde henüz 1 hatalı deneme oldu, hemen kilitlenmemeli
        Assert.False(sayac.KilitliMi(sonra31Sn));
        Assert.True(sayac.DenemeYapilabilirMi(sonra31Sn));
        Assert.Equal(1, sayac.HataliDenemeSayisi);
    }

    [Fact]
    public void ParolaGecikmeSayaci_DenemeYapilabilirMiKilitSuresiDoluncaTemizler()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        var baslangic = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            sayac.HataliGiris(baslangic);
        }
        Assert.False(sayac.DenemeYapilabilirMi(baslangic));

        var sonra35Sn = baslangic.AddSeconds(35);
        Assert.True(sayac.DenemeYapilabilirMi(sonra35Sn));
        Assert.Equal(0, sayac.HataliDenemeSayisi);
        Assert.False(sayac.KilitliMi(sonra35Sn));
    }

    [Fact]
    public async Task ParolaGecikmeSayaci_CokluIsParcacigiErisimiGuvenlidir()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 50, kilitSuresiSaniye: 30);
        var gorevler = new Task[10];

        for (int i = 0; i < 10; i++)
        {
            gorevler[i] = Task.Run(() =>
            {
                for (int j = 0; j < 5; j++)
                {
                    sayac.HataliGiris();
                }
            });
        }

        await Task.WhenAll(gorevler);
        Assert.Equal(50, sayac.HataliDenemeSayisi);
        Assert.True(sayac.KilitliMi());
    }
}
