namespace Portal.Testleri;

using Portal.Cekirdek;
using Xunit;

public class DenetimIziCsvTestleri
{
    [Fact]
    public void Uret_BosVeyaNullListe_YalnizcaBaslikSatiriDoner()
    {
        string sonucNull = DenetimIziCsv.Uret((IEnumerable<Duzeltme>?)null);
        Assert.Equal(DenetimIziCsv.BaslikSatiri + Environment.NewLine, sonucNull);

        string sonucBos = DenetimIziCsv.Uret(Array.Empty<Duzeltme>());
        Assert.Equal(DenetimIziCsv.BaslikSatiri + Environment.NewLine, sonucBos);
    }

    [Fact]
    public void SatirUret_ParametrelerDogrudanVerildiginde_DogruSatirUretir()
    {
        string satir = DenetimIziCsv.SatirUret("ahmet", "2026-05-01T12:00:00.000Z", "matematik_1", 3, "eski metin", "yeni metin");
        Assert.Equal("ahmet,2026-05-01T12:00:00.000Z,matematik_1,3,eski metin,yeni metin", satir);
    }

    [Fact]
    public void SatirUret_DuzeltmeNesnesinden_DogruAlanlariVeIsouFormatiniUretir()
    {
        var zamanUtc = new DateTime(2026, 10, 8, 14, 30, 0, DateTimeKind.Utc);
        var duzeltme = new Duzeltme
        {
            Kullanici = "DOM\\mehmet",
            ZamanUtc = zamanUtc,
            Ders = "fizik_dersi",
            SegmentNo = 12,
            Eski = "yanlis kelime",
            Yeni = "dogru kelime"
        };

        string satir = DenetimIziCsv.SatirUret(duzeltme);
        Assert.Equal("DOM\\mehmet,2026-10-08T14:30:00.000Z,fizik_dersi,12,yanlis kelime,dogru kelime", satir);
    }

    [Fact]
    public void AlaniKacisla_VirgulIcerenMetin_TirnakIcineAlinir()
    {
        string kacisli = DenetimIziCsv.AlaniKacisla("merhaba, dunya");
        Assert.Equal("\"merhaba, dunya\"", kacisli);

        string satir = DenetimIziCsv.SatirUret("ali", "2026-01-01T00:00:00.000Z", "ders", 1, "eski, metin", "yeni, metin");
        Assert.Equal("ali,2026-01-01T00:00:00.000Z,ders,1,\"eski, metin\",\"yeni, metin\"", satir);
    }

    [Fact]
    public void AlaniKacisla_CiftTirnakIcerenMetin_CiftlenirVeTirnakIcineAlinir()
    {
        string kacisli = DenetimIziCsv.AlaniKacisla("soz: \"ornek\"");
        Assert.Equal("\"soz: \"\"ornek\"\"\"", kacisli);

        string satir = DenetimIziCsv.SatirUret("ayse", "2026-01-01T00:00:00.000Z", "ders", 2, "eski \"alinti\"", "yeni");
        Assert.Equal("ayse,2026-01-01T00:00:00.000Z,ders,2,\"eski \"\"alinti\"\"\",yeni", satir);
    }

    [Fact]
    public void AlaniKacisla_YeniSatirIcerenMetin_TirnakIcineAlinir()
    {
        string cokSatirli = "Birinci satir\r\nIkinci satir";
        string kacisli = DenetimIziCsv.AlaniKacisla(cokSatirli);
        Assert.Equal($"\"{cokSatirli}\"", kacisli);
    }

    [Fact]
    public void Uret_TurkceKarakterler_BozulmadanKorunur()
    {
        var duzeltme = new Duzeltme
        {
            Kullanici = "çağrı_öztürk",
            ZamanUtc = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            Ders = "türkçe_edebiyat",
            SegmentNo = 5,
            Eski = "şemsipaşa pasajında",
            Yeni = "Şemsi Paşa Pasajı'nda"
        };

        string csv = DenetimIziCsv.Uret(new[] { duzeltme });
        Assert.Contains("çağrı_öztürk", csv);
        Assert.Contains("türkçe_edebiyat", csv);
        Assert.Contains("şemsipaşa pasajında", csv);
        Assert.Contains("Şemsi Paşa Pasajı'nda", csv);
    }

    [Fact]
    public void Uret_CokluKayitlar_TumSatirlariVeBasligiEksiksizDoker()
    {
        var duzeltmeler = new List<Duzeltme>
        {
            new() { Kullanici = "kullanici1", Ders = "dersA", SegmentNo = 1, Eski = "eski1", Yeni = "yeni1" },
            new() { Kullanici = "kullanici2", Ders = "dersA", SegmentNo = 2, Eski = "eski2", Yeni = "yeni2" }
        };

        string csv = DenetimIziCsv.Uret(duzeltmeler);
        string[] satirlar = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, satirlar.Length);
        Assert.Equal("kim,zaman,ders,segment,eski,yeni", satirlar[0]);
        Assert.StartsWith("kullanici1,", satirlar[1]);
        Assert.StartsWith("kullanici2,", satirlar[2]);
    }

    [Fact]
    public void Uret_DemetKayitListesi_BaslikVeSatirlariUretir()
    {
        var kayitlar = new List<(string?, string?, string?, int, string?, string?)>
        {
            ("kullanici1", "2026-05-01T10:00:00.000Z", "ders1", 1, "eski1", "yeni1"),
            ("kullanici2", "2026-05-01T11:00:00.000Z", "ders1", 2, "eski2", "yeni2")
        };

        string csv = DenetimIziCsv.Uret(kayitlar);
        Assert.Contains("kullanici1,2026-05-01T10:00:00.000Z,ders1,1,eski1,yeni1", csv);
        Assert.Contains("kullanici2,2026-05-01T11:00:00.000Z,ders1,2,eski2,yeni2", csv);
    }

    [Fact]
    public void Uret_DemetKayitNull_YalnizcaBaslikDoner()
    {
        string csv = DenetimIziCsv.Uret((IEnumerable<(string?, string?, string?, int, string?, string?)>?)null);
        Assert.Equal(DenetimIziCsv.BaslikSatiri + Environment.NewLine, csv);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("=SUM(A1:B2)", "'=SUM(A1:B2)")]
    [InlineData("=cmd|' /C calc'!A0", "'=cmd|' /C calc'!A0")]
    public void AlaniKacisla_EsittirIleBaslayanMetin_FormulEnjeksiyonunaKarsiTekTirnaklaKacislar(string girdi, string beklenen)
    {
        string sonuc = DenetimIziCsv.AlaniKacisla(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Theory]
    [InlineData("+123", "'+123")]
    [InlineData("+cmd", "'+cmd")]
    public void AlaniKacisla_ArtiIleBaslayanMetin_FormulEnjeksiyonunaKarsiTekTirnaklaKacislar(string girdi, string beklenen)
    {
        string sonuc = DenetimIziCsv.AlaniKacisla(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Theory]
    [InlineData("-500", "'-500")]
    [InlineData("-1+2", "'-1+2")]
    public void AlaniKacisla_EksiIleBaslayanMetin_FormulEnjeksiyonunaKarsiTekTirnaklaKacislar(string girdi, string beklenen)
    {
        string sonuc = DenetimIziCsv.AlaniKacisla(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Theory]
    [InlineData("@kullanici", "'@kullanici")]
    [InlineData("@SUM(A1:A5)", "'@SUM(A1:A5)")]
    public void AlaniKacisla_EtIsaretiIleBaslayanMetin_FormulEnjeksiyonunaKarsiTekTirnaklaKacislar(string girdi, string beklenen)
    {
        string sonuc = DenetimIziCsv.AlaniKacisla(girdi);
        Assert.Equal(beklenen, sonuc);
    }

    [Fact]
    public void AlaniKacisla_FormulKarakteriVeVirgulIcerenMetin_HemTekTirnakHemCiftTirnaklaKacislar()
    {
        string girdi = "=SUM(1, 2)";
        string sonuc = DenetimIziCsv.AlaniKacisla(girdi);
        Assert.Equal("\"'=SUM(1, 2)\"", sonuc);
    }

    [Fact]
    public void SatirUret_FormulEnjeksiyonuIcerenDuzeltme_GuvenliCsvSatiriUretir()
    {
        var duzeltme = new Duzeltme
        {
            Kullanici = "=admin",
            ZamanUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc),
            Ders = "@ders",
            SegmentNo = 1,
            Eski = "-eski formül",
            Yeni = "+yeni formül"
        };

        string satir = DenetimIziCsv.SatirUret(duzeltme);
        Assert.Equal("'=admin,2026-10-08T12:00:00.000Z,'@ders,1,'-eski formül,'+yeni formül", satir);
    }

    [Fact]
    public void Uret_FormulKarakterleriIcerenKayitlar_ExcelFormuluneKarsiKorumaliDoker()
    {
        var kayitlar = new List<(string?, string?, string?, int, string?, string?)>
        {
            ("kullanici", "2026-10-08T12:00:00.000Z", "ders", 1, "=cmd", "+calc")
        };

        string csv = DenetimIziCsv.Uret(kayitlar);
        Assert.Contains("kullanici,2026-10-08T12:00:00.000Z,ders,1,'=cmd,'+calc", csv);
    }
}
