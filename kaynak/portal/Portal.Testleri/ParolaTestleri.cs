namespace Portal.Testleri;

using System.Text.Json;
using Portal.Cekirdek;
using Xunit;

public class ParolaTestleri : IDisposable
{
    private readonly string _geciciKlasor;
    private readonly Parola _parolaYonetici;

    public ParolaTestleri()
    {
        _geciciKlasor = Path.Combine(Path.GetTempPath(), "VH_ParolaTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_geciciKlasor);
        _parolaYonetici = new Parola(_geciciKlasor);
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
                // Test temizleme hatasını yut
            }
        }
    }

    [Fact]
    public void OlusturVeDogrula_DogruParola_TrueDoner()
    {
        bool olusturuldu = _parolaYonetici.Olustur("GucluParola123!");
        Assert.True(olusturuldu);

        bool dogrulandi = _parolaYonetici.Dogrula("GucluParola123!");
        Assert.True(dogrulandi);
    }

    [Fact]
    public void Dogrula_YanlisParola_FalseDonerVeIstisnaFirlatmaz()
    {
        _parolaYonetici.Olustur("DogruParola888");

        bool yanlisDogrulama = _parolaYonetici.Dogrula("BaskaParola999");
        Assert.False(yanlisDogrulama);

        bool bosDogrulama = _parolaYonetici.Dogrula("");
        Assert.False(bosDogrulama);
    }

    [Fact]
    public void Olustur_KisaParola_FalseDonerVeDosyaYazilmaz()
    {
        // 8 karakterden kısa
        bool sonuc = _parolaYonetici.Olustur("kisa123");
        Assert.False(sonuc);
        Assert.False(File.Exists(_parolaYonetici.ParolaDosyaYolu));
    }

    [Fact]
    public void Olustur_MevcutParolaVarsa_ReddederVeEskiParolaDegismez()
    {
        bool ilk = _parolaYonetici.Olustur("IlkParola123");
        Assert.True(ilk);

        // İkinci kez Olustur çağrıldığında dosya zaten var olduğu için reddedilmeli
        bool ikinci = _parolaYonetici.Olustur("IkinciParola456");
        Assert.False(ikinci);

        // Orijinal parola hâlâ geçerli olmalı, yeni parola geçersiz olmalı
        Assert.True(_parolaYonetici.Dogrula("IlkParola123"));
        Assert.False(_parolaYonetici.Dogrula("IkinciParola456"));
    }

    [Fact]
    public void Degistir_GecerliEskiVeYeni_BasariliVeEskiArsivlenir()
    {
        _parolaYonetici.Olustur("EskiParola123");

        bool degisti = _parolaYonetici.Degistir("EskiParola123", "YeniParola456");
        Assert.True(degisti);

        // Yeni parola doğrulanmalı, eski parola artık geçersiz olmalı
        Assert.True(_parolaYonetici.Dogrula("YeniParola456"));
        Assert.False(_parolaYonetici.Dogrula("EskiParola123"));

        // Eski dosya parola_arsiv klasörüne taşınmış olmalı
        Assert.True(Directory.Exists(_parolaYonetici.ArsivKlasorYolu));
        var arsivDosyalar = Directory.GetFiles(_parolaYonetici.ArsivKlasorYolu, "*.json");
        Assert.Single(arsivDosyalar);
    }

    [Fact]
    public void Degistir_YanlisEskiParola_FalseDonerVeArsivlemeYapilmaz()
    {
        _parolaYonetici.Olustur("DogruEskiParola1");

        bool degisti = _parolaYonetici.Degistir("YanlisEskiParola2", "YeniParola3");
        Assert.False(degisti);

        // Orijinal parola hâlâ geçerli olmalı
        Assert.True(_parolaYonetici.Dogrula("DogruEskiParola1"));

        // Arşiv klasörü oluşmamalı veya dosya taşınmamalı
        if (Directory.Exists(_parolaYonetici.ArsivKlasorYolu))
        {
            Assert.Empty(Directory.GetFiles(_parolaYonetici.ArsivKlasorYolu));
        }
    }

    [Fact]
    public void Degistir_KisaYeniParola_FalseDoner()
    {
        _parolaYonetici.Olustur("GecerliEskiParola");

        bool degisti = _parolaYonetici.Degistir("GecerliEskiParola", "kisa7");
        Assert.False(degisti);

        // Eski parola geçerli kalmalı
        Assert.True(_parolaYonetici.Dogrula("GecerliEskiParola"));
    }

    [Fact]
    public void Dogrula_IterasyonDusukOlanDosya_ReddedilirVeFalseDoner()
    {
        _parolaYonetici.Olustur("GecerliParola123");

        // Dosyayı oku ve iter değerini 600000 altına düşür
        string json = File.ReadAllText(_parolaYonetici.ParolaDosyaYolu);
        var kayit = JsonSerializer.Deserialize<ParolaKaydi>(json)!;
        kayit.Iter = 599999;
        File.WriteAllText(_parolaYonetici.ParolaDosyaYolu, JsonSerializer.Serialize(kayit));

        // iter < 600000 reddedilmeli
        bool sonuc = _parolaYonetici.Dogrula("GecerliParola123");
        Assert.False(sonuc);
    }

    [Fact]
    public void Dogrula_BozukJsonDosyasi_FalseDonerIstisnaFirlatmaz()
    {
        string portalKlasor = Path.Combine(_geciciKlasor, ".portal");
        Directory.CreateDirectory(portalKlasor);
        File.WriteAllText(_parolaYonetici.ParolaDosyaYolu, "{ bozuk json formatı !!!");

        bool sonuc = _parolaYonetici.Dogrula("HerhangiBirParola");
        Assert.False(sonuc);
    }

    [Fact]
    public void Dogrula_DosyaMevcutDegilken_FalseDoner()
    {
        bool sonuc = _parolaYonetici.Dogrula("DosyaYokParola");
        Assert.False(sonuc);
    }

    [Fact]
    public void Dogrula_FarkliAlgoritmaOlanDosya_FalseDoner()
    {
        _parolaYonetici.Olustur("ParolaTest123");

        string json = File.ReadAllText(_parolaYonetici.ParolaDosyaYolu);
        var kayit = JsonSerializer.Deserialize<ParolaKaydi>(json)!;
        kayit.Alg = "SHA1";
        File.WriteAllText(_parolaYonetici.ParolaDosyaYolu, JsonSerializer.Serialize(kayit));

        bool sonuc = _parolaYonetici.Dogrula("ParolaTest123");
        Assert.False(sonuc);
    }

    [Fact]
    public void OlusturVeDogrula_TurkceKarakterliParola_BasariylaDogrulanir()
    {
        string turkceParola = "ŞemsiPaşaPasajıĞüçöı123";
        bool olustu = _parolaYonetici.Olustur(turkceParola);
        Assert.True(olustu);

        bool dogrulandi = _parolaYonetici.Dogrula(turkceParola);
        Assert.True(dogrulandi);
    }

    [Fact]
    public void Degistir_YeniKayitOncedenHazirlanirVeBasariliDegisimdeEskiArsivlenir()
    {
        // Bulgu 3: Yeni kayıt önceden hazırlanır, eski dosya arşive taşınır, yeni parola doğrulanır
        _parolaYonetici.Olustur("OrijinalParola123");
        string ilkDosyaIcerik = File.ReadAllText(_parolaYonetici.ParolaDosyaYolu);

        bool sonuc = _parolaYonetici.Degistir("OrijinalParola123", "GuncelParola789");
        Assert.True(sonuc);

        // Yeni parola etkin olmalı
        Assert.True(_parolaYonetici.Dogrula("GuncelParola789"));
        Assert.False(_parolaYonetici.Dogrula("OrijinalParola123"));

        // Arşivlenen dosya orijinal içerikle eşleşmeli
        var arsivDosyalar = Directory.GetFiles(_parolaYonetici.ArsivKlasorYolu, "*.json");
        Assert.Single(arsivDosyalar);
        string arsivIcerik = File.ReadAllText(arsivDosyalar[0]);
        Assert.Equal(ilkDosyaIcerik, arsivIcerik);
    }

    [Fact]
    public void Degistir_GecersizYeniParoladaEskiParolaDosyasiBozulmazVeGecerliKalir()
    {
        // Bulgu 3: Yeni parola geçersiz olduğunda eski parola dosyası asla silinmez/kaybolmaz
        _parolaYonetici.Olustur("GuvenliParola123");

        // Geçersiz yeni parola denemesi (kısa)
        bool sonuc = _parolaYonetici.Degistir("GuvenliParola123", "kisa");
        Assert.False(sonuc);

        // Etkin dosya yerinde ve eski parola geçerli kalmalı
        Assert.True(File.Exists(_parolaYonetici.ParolaDosyaYolu));
        Assert.True(_parolaYonetici.Dogrula("GuvenliParola123"));
    }

    [Fact]
    public void Degistir_KullaniciBelirtilirseDegistirenAlaniDogruKaydedilir()
    {
        _parolaYonetici.Olustur("IlkParola123", degistiren: "SISTEM\\yonetici1");

        bool degisti = _parolaYonetici.Degistir("IlkParola123", "IkinciParola456", degistiren: "DOMAIN\\yonetici2");
        Assert.True(degisti);

        string json = File.ReadAllText(_parolaYonetici.ParolaDosyaYolu);
        var kayit = JsonSerializer.Deserialize<ParolaKaydi>(json);
        Assert.NotNull(kayit);
        Assert.Equal("DOMAIN\\yonetici2", kayit.Degistiren);
    }
}
