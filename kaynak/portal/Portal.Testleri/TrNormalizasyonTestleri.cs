namespace Portal.Testleri;

using Portal.Cekirdek;
using Xunit;

public class TrNormalizasyonTestleri
{
    [Theory]
    [InlineData("ı", "i")]
    [InlineData("İ", "i")]
    [InlineData("I", "i")]
    [InlineData("i", "i")]
    [InlineData("ş", "s")]
    [InlineData("Ş", "s")]
    [InlineData("ğ", "g")]
    [InlineData("Ğ", "g")]
    [InlineData("ü", "u")]
    [InlineData("Ü", "u")]
    [InlineData("ö", "o")]
    [InlineData("Ö", "o")]
    [InlineData("ç", "c")]
    [InlineData("Ç", "c")]
    public void Normalle_TurkceKarakterleri_DogruKatlar(string girdi, string beklenen)
    {
        string sonuc = TrNormalizasyon.Normalle(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707")]
    public void Normalle_TopluTurkceKarakterler_DogruKatlanir()
    {
        string girdi = "ŞĞÜÖÇIİşğüöçıi";
        string beklenen = "sguociisguocii";
        Assert.Equal(beklenen, TrNormalizasyon.Normalle(girdi));
    }

    [Theory]
    [InlineData("â", "a")]
    [InlineData("Â", "a")]
    [InlineData("î", "i")]
    [InlineData("Î", "i")]
    [InlineData("û", "u")]
    [InlineData("Û", "u")]
    [InlineData("kâğıt", "kagit")]
    [InlineData("millî", "milli")]
    [InlineData("sükût", "sukut")]
    public void Normalle_SapkaliHarfleri_DogruKatlar(string girdi, string beklenen)
    {
        string sonuc = TrNormalizasyon.Normalle(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Fact]
    public void Normalle_Apostrofu_BoslugaCevirir()
    {
        string girdi = "Ankara'da";
        string beklenen = "ankara da";
        Assert.Equal(beklenen, TrNormalizasyon.Normalle(girdi));
    }

    [Theory]
    [InlineData("Ahmet’in", "ahmet in")]
    [InlineData("Ali‘nin", "ali nin")]
    [InlineData("O`nun", "o nun")]
    [InlineData("Mehmet´in", "mehmet in")]
    public void Normalle_FarkliApostrofTurlerini_BoslugaCevirir(string girdi, string beklenen)
    {
        string sonuc = TrNormalizasyon.Normalle(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Fact]
    public void Normalle_NoktalamaIsaretlerini_BoslugaCevirir()
    {
        string girdi = "merhaba, dunya! nasilsin? (iyi). [harika]";
        string beklenen = "merhaba dunya nasilsin iyi harika";
        Assert.Equal(beklenen, TrNormalizasyon.Normalle(girdi));
    }

    [Fact]
    public void Normalle_CokluBosluklari_TekeIndirir()
    {
        string girdi = "bu    coklu      bosluklu   bir    metin";
        string beklenen = "bu coklu bosluklu bir metin";
        Assert.Equal(beklenen, TrNormalizasyon.Normalle(girdi));
    }

    [Fact]
    public void Normalle_BastaVeSondaKalanBoslukVeNoktalamalari_Temizler()
    {
        string girdi = "  ...selam millet!...  ";
        string beklenen = "selam millet";
        Assert.Equal(beklenen, TrNormalizasyon.Normalle(girdi));
    }

    [Fact]
    public void Normalle_NullGirdi_BosStringDondurur()
    {
        string sonuc = TrNormalizasyon.Normalle(null);
        Assert.Equal(string.Empty, sonuc);
    }

    [Fact]
    public void Normalle_BosGirdi_BosStringDondurur()
    {
        string sonuc = TrNormalizasyon.Normalle(string.Empty);
        Assert.Equal(string.Empty, sonuc);
    }

    [Fact]
    public void Normalle_SadeceBoslukGirdi_BosStringDondurur()
    {
        string sonuc = TrNormalizasyon.Normalle("   \t \r \n  ");
        Assert.Equal(string.Empty, sonuc);
    }

    [Fact]
    public void Normalle_SadeceNoktalamaGirdi_BosStringDondurur()
    {
        string sonuc = TrNormalizasyon.Normalle("...,;:!?'--__");
        Assert.Equal(string.Empty, sonuc);
    }

    [Fact]
    public void Kelimeler_StandartCumleyi_DiziyeAyirir()
    {
        string[] kelimeler = TrNormalizasyon.Kelimeler("Merhaba Dünya");
        Assert.Equal(new[] { "merhaba", "dunya" }, kelimeler);
    }

    [Fact]
    public void Kelimeler_ApostrofluMetni_AyrilmisKelimelerOlarakDondurur()
    {
        string[] kelimeler = TrNormalizasyon.Kelimeler("Ankara'da");
        Assert.Equal(new[] { "ankara", "da" }, kelimeler);
    }

    [Fact]
    public void Kelimeler_NullGirdi_BosDiziDondurur()
    {
        string[] kelimeler = TrNormalizasyon.Kelimeler(null);
        Assert.Empty(kelimeler);
    }

    [Fact]
    public void Kelimeler_BosGirdi_BosDiziDondurur()
    {
        string[] kelimeler = TrNormalizasyon.Kelimeler(string.Empty);
        Assert.Empty(kelimeler);
    }

    [Fact]
    public void Kelimeler_CokluNoktalamaVeBosluk_KelimeleriDondurur()
    {
        string[] kelimeler = TrNormalizasyon.Kelimeler("bir, iki... üç! dört? (beş)");
        Assert.Equal(new[] { "bir", "iki", "uc", "dort", "bes" }, kelimeler);
    }
}
