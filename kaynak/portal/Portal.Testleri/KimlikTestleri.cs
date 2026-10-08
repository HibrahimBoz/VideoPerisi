namespace Portal.Testleri;

using Portal.Cekirdek;
using Xunit;

public class KimlikTestleri
{
    private class SahteKimlik : IKimlik
    {
        public string SahteKullanici { get; set; } = "TESTDOMAIN\\ogretmen";
        public string SahteMakine { get; set; } = "LAB-PC-01";

        public string KullaniciAdi() => SahteKullanici;
        public string MakineAdi() => SahteMakine;
    }

    [Fact]
    public void Kimlik_VarsayilanKullaniciAdi_BosVeyaNullDonmez()
    {
        var kimlik = new Kimlik();
        string kullanici = kimlik.KullaniciAdi();

        Assert.False(string.IsNullOrWhiteSpace(kullanici));
    }

    [Fact]
    public void Kimlik_VarsayilanMakineAdi_BosVeyaNullDonmez()
    {
        var kimlik = new Kimlik();
        string makine = kimlik.MakineAdi();

        Assert.False(string.IsNullOrWhiteSpace(makine));
    }

    [Fact]
    public void Kimlik_EnjekteEdilebilirIKimlik_OzellestirilmisBilgileriDoner()
    {
        IKimlik sahte = new SahteKimlik
        {
            SahteKullanici = "OZEL\\denetmen",
            SahteMakine = "SUNUCU-99"
        };

        var hedef = new Duzeltme
        {
            Ders = "test_ders",
            SegmentNo = 1,
            Eski = "eski metin",
            Yeni = "yeni metin",
            Kullanici = "ORIJINAL\\kisi",
            Makine = "ORIJINAL-PC"
        };

        var geriAl = Duzeltme.GeriAlOlustur(hedef, sahte);

        Assert.Equal("OZEL\\denetmen", sahte.KullaniciAdi());
        Assert.Equal("SUNUCU-99", sahte.MakineAdi());
        Assert.Equal("OZEL\\denetmen", geriAl.Kullanici);
        Assert.Equal("SUNUCU-99", geriAl.Makine);
    }

    [Theory]
    [InlineData("DOMAIN\\kisi", "DOMAIN_kisi")]
    [InlineData("PC/alt/kullanici", "PC_alt_kullanici")]
    [InlineData("kullanici:test*ad?<>|", "kullanici_test_ad")]
    [InlineData("ahmet öztürk", "ahmet_öztürk")]
    [InlineData("   ", "anonim")]
    [InlineData(null, "anonim")]
    public void GuvenliKullaniciAdi_DosyaIcinGecersizKarakterleriTemizler(string? girdi, string beklenen)
    {
        string sonuc = DuzeltmeDeposu.GuvenliKullaniciAdi(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Fact]
    public void Kimlik_StatikMetotlar_VarsayilanKimlikDegerleriniDoner()
    {
        string kullanici = Kimlik.Varsayilan.KullaniciAdi();
        string makine = Kimlik.Varsayilan.MakineAdi();

        Assert.False(string.IsNullOrWhiteSpace(kullanici));
        Assert.False(string.IsNullOrWhiteSpace(makine));
        Assert.Equal(Kimlik.Varsayilan.KullaniciAdi(), kullanici);
        Assert.Equal(Kimlik.Varsayilan.MakineAdi(), makine);
    }
}
