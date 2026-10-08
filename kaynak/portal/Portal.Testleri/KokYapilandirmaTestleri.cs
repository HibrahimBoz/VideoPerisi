namespace Portal.Testleri;

using System.Text;
using Portal.Cekirdek;
using Xunit;

/// <summary>
/// Kök klasör yapılandırması (öncelik sırası, JSON okuma/yazma, UNC, Türkçe karakterler) testleri.
/// </summary>
public class KokYapilandirmaTestleri : IDisposable
{
    private readonly string _geciciKlasor;
    private readonly string _kullaniciAyarYolu;
    private readonly string _paketAyarYolu;

    public KokYapilandirmaTestleri()
    {
        _geciciKlasor = Path.Combine(Path.GetTempPath(), "VH_KokTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_geciciKlasor);
        _kullaniciAyarYolu = Path.Combine(_geciciKlasor, "kullanici.json");
        _paketAyarYolu = Path.Combine(_geciciKlasor, "ayar.json");
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
                // Test temizliği hatasını yut
            }
        }
    }

    [Fact]
    public void Coz_Oncelik1_KomutSatiriVarIken_KomutSatiriniDondurur()
    {
        string[] args = ["--kok", @"C:\ArgKok"];
        KokYapilandirma.Kaydet(@"C:\UserKok", _kullaniciAyarYolu);
        KokYapilandirma.Kaydet(@"C:\PkgKok", _paketAyarYolu);

        string? sonuc = KokYapilandirma.Coz(args, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\ArgKok", sonuc);
    }

    [Fact]
    public void Coz_Oncelik1_KomutSatiriEsittirFormati_DogruAyrristirilir()
    {
        string[] args = [@"--kok=""C:\ArgEsittirKok"""];
        KokYapilandirma.Kaydet(@"C:\UserKok", _kullaniciAyarYolu);

        string? sonuc = KokYapilandirma.Coz(args, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\ArgEsittirKok", sonuc);
    }

    [Fact]
    public void Coz_Oncelik2_KomutSatiriYokKullaniciAyariVarIken_KullaniciAyariniDondurur()
    {
        string[]? args = null;
        KokYapilandirma.Kaydet(@"C:\UserKok", _kullaniciAyarYolu);
        KokYapilandirma.Kaydet(@"C:\PkgKok", _paketAyarYolu);

        string? sonuc = KokYapilandirma.Coz(args, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\UserKok", sonuc);
    }

    [Fact]
    public void Coz_Oncelik3_KomutVeKullaniciYokPaketAyariVarIken_PaketAyariniDondurur()
    {
        string[]? args = [];
        KokYapilandirma.Kaydet(@"\\sunucu\Dersler", _paketAyarYolu);

        string? sonuc = KokYapilandirma.Coz(args, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"\\sunucu\Dersler", sonuc);
    }

    [Fact]
    public void Coz_Oncelik4_HicbirAyarYoksa_NullDondurur()
    {
        string[]? args = null;

        string? sonuc = KokYapilandirma.Coz(args, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Null(sonuc);
    }

    [Fact]
    public void Coz_KomutSatiriBosIse_KullaniciAyarinaGecer()
    {
        string[] args = ["--kok", "   "];
        KokYapilandirma.Kaydet(@"C:\UserKok", _kullaniciAyarYolu);

        string? sonuc = KokYapilandirma.Coz(args, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\UserKok", sonuc);
    }

    [Fact]
    public void Coz_KullaniciJsonBozukIse_PaketAyarinaGecer()
    {
        File.WriteAllText(_kullaniciAyarYolu, "{ bu geçerli bir JSON verisi değildir! %%%", Encoding.UTF8);
        KokYapilandirma.Kaydet(@"C:\PkgKok", _paketAyarYolu);

        string? sonuc = KokYapilandirma.Coz(null, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\PkgKok", sonuc);
    }

    [Fact]
    public void Coz_PaketJsonBozukIse_NullDondurur()
    {
        File.WriteAllText(_paketAyarYolu, "{\"kok\": ", Encoding.UTF8);

        string? sonuc = KokYapilandirma.Coz(null, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Null(sonuc);
    }

    [Fact]
    public void Coz_KullaniciJsonBosKokIceriyorsa_PaketAyarinaGecer()
    {
        File.WriteAllText(_kullaniciAyarYolu, "{\"kok\":\"\"}", Encoding.UTF8);
        KokYapilandirma.Kaydet(@"C:\PkgKok", _paketAyarYolu);

        string? sonuc = KokYapilandirma.Coz(null, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\PkgKok", sonuc);
    }

    [Fact]
    public void Coz_PaketJsonBosKokIceriyorsa_NullDondurur()
    {
        File.WriteAllText(_paketAyarYolu, "{\"kok\":\"   \"}", Encoding.UTF8);

        string? sonuc = KokYapilandirma.Coz(null, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Null(sonuc);
    }

    [Fact]
    public void Coz_JsonDosyasiSifirBaytIse_YokSayilir()
    {
        File.WriteAllBytes(_kullaniciAyarYolu, []);
        KokYapilandirma.Kaydet(@"C:\PkgKok", _paketAyarYolu);

        string? sonuc = KokYapilandirma.Coz(null, _kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\PkgKok", sonuc);
    }

    [Fact]
    public void KaydetVeOku_GidisDonus_AyniYoluVerir()
    {
        const string hedefYol = @"C:\Dersler\Portal\Guncel";

        KokYapilandirma.Kaydet(hedefYol, _kullaniciAyarYolu);
        string? okunan = KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu);

        Assert.Equal(hedefYol, okunan);
    }

    [Fact]
    public void KaydetVeOku_UncYolu_CiftBaskaslakKorunur()
    {
        const string uncYol = @"\\sunucu\Dersler\2024\Donem1";

        KokYapilandirma.Kaydet(uncYol, _kullaniciAyarYolu);
        string? okunan = KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu);

        Assert.Equal(uncYol, okunan);
        Assert.NotNull(okunan);
        Assert.StartsWith(@"\\", okunan);

        // JSON dosya metninde backslash'lerin kaçışlandığını doğrula
        string dosyaIcerik = File.ReadAllText(_kullaniciAyarYolu, Encoding.UTF8);
        Assert.Contains(@"\\\\sunucu\\Dersler", dosyaIcerik);
    }

    [Fact]
    public void KaydetVeOku_TurkceKarakterliYol_BozulmadanSaklanir()
    {
        const string turkceYol = @"D:\Eğitim\Türkçe_Dersi_İşlemleri\Şiirler_Örnek_Çağdaş";

        KokYapilandirma.Kaydet(turkceYol, _kullaniciAyarYolu);
        string? okunan = KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu);

        Assert.Equal(turkceYol, okunan);

        // UTF-8 olarak Türkçe karakterlerin metinde doğrudan yer aldığını doğrula
        string dosyaIcerik = File.ReadAllText(_kullaniciAyarYolu, Encoding.UTF8);
        Assert.Contains("Türkçe_Dersi_İşlemleri", dosyaIcerik);
        Assert.Contains("Şiirler_Örnek_Çağdaş", dosyaIcerik);
    }

    [Fact]
    public void Coz_OlmayanKlasorYolu_YoluDondurur_KokErisilebilirMiFalseDondurur()
    {
        string olmayanKlasor = Path.Combine(_geciciKlasor, "kesinlikle_olmayan_klasor_" + Guid.NewGuid().ToString("N"));
        KokYapilandirma.Kaydet(olmayanKlasor, _kullaniciAyarYolu);
        KokYapilandirma.Kaydet(@"C:\DigerYol", _paketAyarYolu);

        // Coz yolu döndürür, bir sonraki kaynağa DÜŞMEZ
        string? cozuldu = KokYapilandirma.Coz(null, _kullaniciAyarYolu, _paketAyarYolu);
        Assert.Equal(olmayanKlasor, cozuldu);

        // Erişim denetimi ayrı metotta yapılır ve false döner
        bool erisilebilir = KokYapilandirma.KokErisilebilirMi(cozuldu);
        Assert.False(erisilebilir);
    }

    [Fact]
    public void KokErisilebilirMi_VarOlanKlasorIcin_TrueDondurur()
    {
        bool erisilebilir = KokYapilandirma.KokErisilebilirMi(_geciciKlasor);
        Assert.True(erisilebilir);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"Z:\gecersiz<>*?|yol")]
    public void KokErisilebilirMi_GecersizVeNullYollar_FalseDondururVeHataFirlatmaz(string? gecersizYol)
    {
        bool erisilebilir = KokYapilandirma.KokErisilebilirMi(gecersizYol);
        Assert.False(erisilebilir);
    }

    [Fact]
    public void Kaydet_AtomikYazma_GeciciDosyaKalmaz()
    {
        string hedef = Path.Combine(_geciciKlasor, "atomik_ayar.json");
        KokYapilandirma.Kaydet(@"C:\TestKlasor", hedef);

        Assert.True(File.Exists(hedef));
        var tmpDosyalar = Directory.GetFiles(_geciciKlasor, "*.tmp");
        Assert.Empty(tmpDosyalar);
    }

    [Fact]
    public void KokYapilandirma_OrnekKullanimi_EnjekteEdilenYollariKullanir()
    {
        KokYapilandirma.Kaydet(@"C:\OrnekKullanici", _kullaniciAyarYolu);
        KokYapilandirma.Kaydet(@"C:\OrnekPaket", _paketAyarYolu);

        var yapilandirma = new KokYapilandirma(_kullaniciAyarYolu, _paketAyarYolu);

        Assert.Equal(@"C:\OrnekKullanici", yapilandirma.Coz());
        Assert.Equal(@"C:\OrnekKullanici", yapilandirma.KullaniciKokuOku());
        Assert.Equal(@"C:\OrnekPaket", yapilandirma.PaketlenenKokuOku());
    }

    [Fact]
    public void JsonDosyasindanKokOku_BuyukKucukHarfDuyarsizKokAnahtari()
    {
        File.WriteAllText(_kullaniciAyarYolu, "{\"KOK\": \"C:\\\\BuyukHarf\"}", Encoding.UTF8);

        string? okunan = KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu);

        Assert.Equal(@"C:\BuyukHarf", okunan);
    }

    [Fact]
    public void UygulamaAkisi_KokErisilebilirIse_UyariVeyaSecimOlmadanDogrudanDondurur()
    {
        KokYapilandirma.Kaydet(_geciciKlasor, _kullaniciAyarYolu);

        bool uyariCagirildi = false;
        bool secimCagirildi = false;

        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: _ => { uyariCagirildi = true; return false; },
            klasorSec: () => { secimCagirildi = true; return null; },
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Equal(_geciciKlasor, sonuc);
        Assert.False(uyariCagirildi);
        Assert.False(secimCagirildi);
    }

    [Fact]
    public void UygulamaAkisi_HicbirAyarYoksa_SecimYapilirVeKullaniciAyarinaKaydedilir()
    {
        string secilenKlasor = Path.Combine(_geciciKlasor, "secilen_ders_koku");
        Directory.CreateDirectory(secilenKlasor);

        bool uyariCagirildi = false;

        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: _ => { uyariCagirildi = true; return false; },
            klasorSec: () => secilenKlasor,
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Equal(secilenKlasor, sonuc);
        Assert.False(uyariCagirildi);
        Assert.Equal(secilenKlasor, KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu));
    }

    [Fact]
    public void UygulamaAkisi_HicbirAyarYoksa_KullaniciSecimiIptalEderse_NullDondururVeKaydetmez()
    {
        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: _ => false,
            klasorSec: () => null,
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Null(sonuc);
        Assert.False(File.Exists(_kullaniciAyarYolu));
    }

    [Fact]
    public void UygulamaAkisi_ErisilemeyenKok_SonrakiKaynagaDusmez_KullaniciyaUyarirVeVazgecinceNullDondurur()
    {
        string olmayanKlasor = Path.Combine(_geciciKlasor, "kesinlikle_olmayan_kok_1");
        KokYapilandirma.Kaydet(olmayanKlasor, _kullaniciAyarYolu);
        KokYapilandirma.Kaydet(_geciciKlasor, _paketAyarYolu); // Paket ayarı geçerli olsa bile düşmemeli

        string? uyarilanYol = null;
        bool secimCagirildi = false;

        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: yol =>
            {
                uyarilanYol = yol;
                return false; // Değiştirmek istemiyor / vazgeçti
            },
            klasorSec: () =>
            {
                secimCagirildi = true;
                return null;
            },
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Null(sonuc);
        Assert.Equal(olmayanKlasor, uyarilanYol);
        Assert.False(secimCagirildi);
        // Kullanıcı ayarı değişmemeli
        Assert.Equal(olmayanKlasor, KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu));
    }

    [Fact]
    public void UygulamaAkisi_ErisilemeyenKok_KullaniciDegistirSecerse_YeniSecilenKaydedilirVeDondurulur()
    {
        string olmayanKlasor = Path.Combine(_geciciKlasor, "kesinlikle_olmayan_kok_2");
        KokYapilandirma.Kaydet(olmayanKlasor, _kullaniciAyarYolu);

        string yeniGecerliKlasor = Path.Combine(_geciciKlasor, "yeni_gecerli_dersler");
        Directory.CreateDirectory(yeniGecerliKlasor);

        string? uyarilanYol = null;

        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: yol =>
            {
                uyarilanYol = yol;
                return true; // Değiştirmeyi kabul etti
            },
            klasorSec: () => yeniGecerliKlasor,
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Equal(olmayanKlasor, uyarilanYol);
        Assert.Equal(yeniGecerliKlasor, sonuc);
        // kullanici.json'a yeni yol kaydedilmiş olmalı
        Assert.Equal(yeniGecerliKlasor, KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu));
    }

    [Fact]
    public void UygulamaAkisi_ErisilemeyenKok_KullaniciDegistirSecipSecimiIptalEderse_NullDondurur()
    {
        string olmayanKlasor = Path.Combine(_geciciKlasor, "kesinlikle_olmayan_kok_3");
        KokYapilandirma.Kaydet(olmayanKlasor, _kullaniciAyarYolu);

        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: _ => true,
            klasorSec: () => null, // Seçim iptal edildi
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Null(sonuc);
        Assert.Equal(olmayanKlasor, KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu));
    }

    [Fact]
    public void UygulamaAkisi_ErisilemeyenKok_DongudeOnceGecersizSonraGecerliSecilirse_DonguCalisirVeKaydeder()
    {
        string olmayanKlasorIlk = Path.Combine(_geciciKlasor, "olmayan_kok_a");
        string olmayanKlasorIkinci = Path.Combine(_geciciKlasor, "olmayan_kok_b");
        string gecerliSon = Path.Combine(_geciciKlasor, "son_gecerli_kok");
        Directory.CreateDirectory(gecerliSon);

        KokYapilandirma.Kaydet(olmayanKlasorIlk, _kullaniciAyarYolu);

        var uyarilanYollar = new List<string>();
        int secimSayaci = 0;

        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: yol =>
            {
                uyarilanYollar.Add(yol);
                return true;
            },
            klasorSec: () =>
            {
                secimSayaci++;
                return secimSayaci == 1 ? olmayanKlasorIkinci : gecerliSon;
            },
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Equal(gecerliSon, sonuc);
        Assert.Equal(2, uyarilanYollar.Count);
        Assert.Equal(olmayanKlasorIlk, uyarilanYollar[0]);
        Assert.Equal(olmayanKlasorIkinci, uyarilanYollar[1]);
        Assert.Equal(gecerliSon, KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu));
    }

    [Fact]
    public void UygulamaAkisi_KomutSatiriArgumaniErisilemezse_UyarirVeSecilenYeniKlasoruKaydeder()
    {
        string cliOlmayan = @"Z:\CliOlmayanKok_" + Guid.NewGuid().ToString("N");
        string[] args = ["--kok", cliOlmayan];

        string gecerliYeni = Path.Combine(_geciciKlasor, "cli_yerine_secilen");
        Directory.CreateDirectory(gecerliYeni);

        string? uyarilanYol = null;

        string? sonuc = KokYapilandirma.UygulamaKokuBelirle(
            args,
            uyariVeDegistirSor: yol =>
            {
                uyarilanYol = yol;
                return true;
            },
            klasorSec: () => gecerliYeni,
            _kullaniciAyarYolu,
            _paketAyarYolu
        );

        Assert.Equal(cliOlmayan, uyarilanYol);
        Assert.Equal(gecerliYeni, sonuc);
        Assert.Equal(gecerliYeni, KokYapilandirma.KullaniciKokuOku(_kullaniciAyarYolu));
    }

    [Fact]
    public void UygulamaAkisi_OrnekMetodu_EnjekteEdilenYollariKullanir()
    {
        string olmayanKlasor = Path.Combine(_geciciKlasor, "ornek_icin_olmayan");
        KokYapilandirma.Kaydet(olmayanKlasor, _kullaniciAyarYolu);

        string yeniKlasor = Path.Combine(_geciciKlasor, "ornek_icin_gecerli");
        Directory.CreateDirectory(yeniKlasor);

        var yapilandirma = new KokYapilandirma(_kullaniciAyarYolu, _paketAyarYolu);

        string? sonuc = yapilandirma.UygulamaKokuBelirle(
            null,
            uyariVeDegistirSor: _ => true,
            klasorSec: () => yeniKlasor
        );

        Assert.Equal(yeniKlasor, sonuc);
        Assert.Equal(yeniKlasor, yapilandirma.KullaniciKokuOku());
    }
}
