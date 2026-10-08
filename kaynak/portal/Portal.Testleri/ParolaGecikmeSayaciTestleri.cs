namespace Portal.Testleri;

using Portal.Cekirdek;
using Xunit;

public class ParolaGecikmeSayaciTestleri
{
    [Fact]
    public void Sayac_OzelSinirlarlaBaslatilabilir()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 3, kilitSuresiSaniye: 15);
        Assert.Equal(3, sayac.AzamiHataliDeneme);
        Assert.Equal(15, sayac.KilitSuresiSaniye);
        Assert.Equal(0, sayac.HataliDenemeSayisi);
        Assert.False(sayac.KilitliMi());
    }

    [Fact]
    public void HataliGiris_UcuncuDenemedeKilitlenir()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 3, kilitSuresiSaniye: 10);
        var baslangic = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        sayac.HataliGiris(baslangic);
        Assert.False(sayac.KilitliMi(baslangic));
        Assert.True(sayac.DenemeYapilabilirMi(baslangic));

        sayac.HataliGiris(baslangic);
        Assert.False(sayac.KilitliMi(baslangic));

        sayac.HataliGiris(baslangic);
        Assert.True(sayac.KilitliMi(baslangic));
        Assert.False(sayac.DenemeYapilabilirMi(baslangic));
        Assert.Equal(10, sayac.KalanBeklemeSaniyesi(baslangic));
    }

    [Fact]
    public void KalanBeklemeSaniyesi_ZamanIlerledikceAzalir()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        var t0 = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            sayac.HataliGiris(t0);
        }

        Assert.Equal(30, sayac.KalanBeklemeSaniyesi(t0));
        Assert.Equal(20, sayac.KalanBeklemeSaniyesi(t0.AddSeconds(10)));
        Assert.Equal(5, sayac.KalanBeklemeSaniyesi(t0.AddSeconds(25)));
        Assert.Equal(0, sayac.KalanBeklemeSaniyesi(t0.AddSeconds(30)));
    }

    [Fact]
    public void KilitSuresiBittiğinde_KilitAcilir()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        var t0 = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            sayac.HataliGiris(t0);
        }

        Assert.True(sayac.KilitliMi(t0.AddSeconds(29)));
        Assert.False(sayac.KilitliMi(t0.AddSeconds(30.1)));
        Assert.True(sayac.DenemeYapilabilirMi(t0.AddSeconds(30.1)));
    }

    [Fact]
    public void BasariliGiris_HataliSayisiniVeKilitDurumunuSifirlar()
    {
        var sayac = new ParolaGecikmeSayaci(azamiHataliDeneme: 5, kilitSuresiSaniye: 30);
        var t0 = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            sayac.HataliGiris(t0);
        }
        Assert.True(sayac.KilitliMi(t0));

        sayac.BasariliGiris();
        Assert.False(sayac.KilitliMi(t0));
        Assert.Equal(0, sayac.HataliDenemeSayisi);
        Assert.Equal(0, sayac.KalanBeklemeSaniyesi(t0));
    }
}
