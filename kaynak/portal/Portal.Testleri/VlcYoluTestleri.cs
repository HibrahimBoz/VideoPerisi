using Portal.Cekirdek;
using Xunit;

namespace Portal.Testleri;

public class VlcYoluTestleri
{
    [Fact]
    public void Unc_TersEgikCizgi_EgikCizgiyeCevrilir()
    {
        Assert.Equal("//sunucu/paylasim/ders/a.mp4", VlcYolu.Hazirla(@"\\sunucu\paylasim\ders\a.mp4"));
    }

    [Fact]
    public void Unc_ZatenEgikCizgili_DegismezAma_TersEgikKalirsaCevrilir()
    {
        Assert.Equal("//sunucu/p/a.mp4", VlcYolu.Hazirla("//sunucu/p/a.mp4"));
        Assert.Equal("//sunucu/p/a.mp4", VlcYolu.Hazirla(@"//sunucu/p\a.mp4"));
    }

    [Fact]
    public void Unc_IpVeDolarIceren_YolKorunur()
    {
        Assert.Equal("//127.0.0.1/c$/x/y.mp4", VlcYolu.Hazirla(@"\\127.0.0.1\c$\x\y.mp4"));
    }

    [Fact]
    public void YerelYol_Degismez()
    {
        Assert.Equal(@"C:\Dersler\a.mp4", VlcYolu.Hazirla(@"C:\Dersler\a.mp4"));
        Assert.Equal(@"D:\a b\ç ğ.mp4", VlcYolu.Hazirla(@"D:\a b\ç ğ.mp4"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void BosGirdi_OlduguGibiDoner(string? yol)
    {
        Assert.Equal(yol, VlcYolu.Hazirla(yol!));
    }
}
