namespace Portal.Testleri;

using System.Text;
using Portal.Cekirdek;
using Xunit;

public class TranskriptOkuyucuTestleri : IDisposable
{
    private readonly string _geciciDizin;

    public TranskriptOkuyucuTestleri()
    {
        _geciciDizin = Path.Combine(Path.GetTempPath(), "TranskriptTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_geciciDizin);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_geciciDizin))
            {
                Directory.Delete(_geciciDizin, true);
            }
        }
        catch
        {
            // Temizleme hatalarını yut
        }
    }

    [Fact]
    public void Oku_GecerliJsonV2_BasariylaOkur()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "ders1.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "kaynak_ozet": "Ders başlangıç özeti",
            "sure_sn": 120.5,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 5.25, "metin": "Giriş ve selamlama" },
                { "no": 2, "bas": 5.25, "son": 15.0, "metin": "İlk konu anlatımı" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        var transkript = TranskriptOkuyucu.Oku(jsonYolu);

        Assert.Equal("ders1", transkript.Ad);
        Assert.False(transkript.EskiBicim);
        Assert.Equal("Ders başlangıç özeti", transkript.KaynakOzet);
        Assert.Equal(120.5, transkript.SureSn);
        Assert.Equal(jsonYolu, transkript.JsonYolu);
        Assert.Equal(2, transkript.Segmentler.Count);

        Assert.Equal(1, transkript.Segmentler[0].No);
        Assert.Equal(0.0, transkript.Segmentler[0].Bas);
        Assert.Equal(5.25, transkript.Segmentler[0].Son);
        Assert.Equal("Giriş ve selamlama", transkript.Segmentler[0].Metin);

        Assert.Equal(2, transkript.Segmentler[1].No);
        Assert.Equal(5.25, transkript.Segmentler[1].Bas);
        Assert.Equal(15.0, transkript.Segmentler[1].Son);
        Assert.Equal("İlk konu anlatımı", transkript.Segmentler[1].Metin);
    }

    [Fact]
    public void Oku_JsonV2SureSnYoksa_SonSegmentinSonDegeriniAlir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "ders_suresiz.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 42.8, "metin": "Tek segment" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        var transkript = TranskriptOkuyucu.Oku(jsonYolu);

        Assert.Equal(42.8, transkript.SureSn);
    }

    [Fact]
    public void Oku_EskiBicimSegmentlerYoksa_SrtDosyasindanOkur()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "eski_ders.json");
        string srtYolu = Path.Combine(_geciciDizin, "eski_ders.srt");

        // Segmentler ve sema sürümü alanı olmayan eski format JSON
        string jsonIcerik = """
        {
            "kaynak_ozet": "Eski sistem özeti"
        }
        """;
        string srtIcerik = """
        1
        00:00:01,000 --> 00:00:04,500
        Merhaba arkadaşlar

        2
        00:00:05,000 --> 00:00:08,200
        Bugünkü konumuz integral
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        var transkript = TranskriptOkuyucu.Oku(jsonYolu);

        Assert.Equal("eski_ders", transkript.Ad);
        Assert.True(transkript.EskiBicim);
        Assert.Equal("Eski sistem özeti", transkript.KaynakOzet);
        Assert.Equal(8.2, transkript.SureSn);
        Assert.Equal(2, transkript.Segmentler.Count);

        Assert.Equal(1, transkript.Segmentler[0].No);
        Assert.Equal(1.0, transkript.Segmentler[0].Bas);
        Assert.Equal(4.5, transkript.Segmentler[0].Son);
        Assert.Equal("Merhaba arkadaşlar", transkript.Segmentler[0].Metin);

        Assert.Equal(2, transkript.Segmentler[1].No);
        Assert.Equal(5.0, transkript.Segmentler[1].Bas);
        Assert.Equal(8.2, transkript.Segmentler[1].Son);
        Assert.Equal("Bugünkü konumuz integral", transkript.Segmentler[1].Metin);
    }

    [Fact]
    public void Oku_EskiBicim_CokSatirliCue_DogruBirlestirilir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "coksatir.json");
        string srtYolu = Path.Combine(_geciciDizin, "coksatir.srt");

        string jsonIcerik = "{}";
        string srtIcerik = """
        1
        00:01:02,345 --> 00:01:05,678
        Birinci satır metin
        İkinci satır metin
        Üçüncü satır metin
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        var transkript = TranskriptOkuyucu.Oku(jsonYolu);

        Assert.True(transkript.EskiBicim);
        Assert.Single(transkript.Segmentler);
        Assert.Equal(62.345, transkript.Segmentler[0].Bas);
        Assert.Equal(65.678, transkript.Segmentler[0].Son);
        Assert.Equal("Birinci satır metin\nİkinci satır metin\nÜçüncü satır metin", transkript.Segmentler[0].Metin);
    }

    [Fact]
    public void Oku_EskiBicim_BomIcerenSrtDosyasi_BasariylaOkur()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "bomlu_ders.json");
        string srtYolu = Path.Combine(_geciciDizin, "bomlu_ders.srt");

        string jsonIcerik = "{}";
        string srtIcerik = """
        1
        00:00:00,500 --> 00:00:02,000
        BOM içeren ilk segment
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, new UTF8Encoding(true));

        var transkript = TranskriptOkuyucu.Oku(jsonYolu);

        Assert.Single(transkript.Segmentler);
        Assert.Equal(1, transkript.Segmentler[0].No);
        Assert.Equal("BOM içeren ilk segment", transkript.Segmentler[0].Metin);
    }

    [Fact]
    public void Oku_EskiBicim_NoktaliZamanAyirici_DogruCozulur()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "noktali_zaman.json");
        string srtYolu = Path.Combine(_geciciDizin, "noktali_zaman.srt");

        string jsonIcerik = "{}";
        string srtIcerik = """
        1
        00:00:03.125 --> 00:00:07.875
        Noktalı milisaniye formatı
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        var transkript = TranskriptOkuyucu.Oku(jsonYolu);

        Assert.Single(transkript.Segmentler);
        Assert.Equal(3.125, transkript.Segmentler[0].Bas);
        Assert.Equal(7.875, transkript.Segmentler[0].Son);
    }

    [Fact]
    public void Oku_BozukJson_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "bozuk.json");
        File.WriteAllText(jsonYolu, "{ bu bir gecerli json degildir ...", Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentlerYokVeSrtDosyasiYok_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "segment_ve_srtsiz.json");
        File.WriteAllText(jsonYolu, """{ "kaynak_ozet": "test" }""", Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SrtDosyasiBos_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "bos_srtli.json");
        string srtYolu = Path.Combine(_geciciDizin, "bos_srtli.srt");

        File.WriteAllText(jsonYolu, "{}", Encoding.UTF8);
        File.WriteAllText(srtYolu, "", Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_OlmayanJsonDosyasi_FileNotFoundExceptionFirlatir()
    {
        string olmayanYol = Path.Combine(_geciciDizin, "yok.json");
        Assert.Throws<FileNotFoundException>(() => TranskriptOkuyucu.Oku(olmayanYol));
    }

    [Fact]
    public void Oku_BosVeyaNullParametre_ArgumentExceptionFirlatir()
    {
        Assert.Throws<ArgumentException>(() => TranskriptOkuyucu.Oku(""));
        Assert.Throws<ArgumentException>(() => TranskriptOkuyucu.Oku("   "));
    }

    // === BULGU 1 TESTLERİ: Sema sürümü 2 değilse veya segmentler bozuksa SRT'ye düşülmemeli ===

    [Fact]
    public void Oku_SemaSurumu2DegilseVeSrtMevcutsa_SrtYeDusmezInvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "yanlis_sema.json");
        string srtYolu = Path.Combine(_geciciDizin, "yanlis_sema.srt");

        // Sema sürümü 1 olarak verilmiş ancak segmentler var
        string jsonIcerik = """
        {
            "sema_surumu": 1,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        string srtIcerik = """
        1
        00:00:00,000 --> 00:00:05,000
        Eski yedek SRT metni
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SemaSurumuYoksaVeSegmentlerVarsa_SrtYeDusmezInvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "semasiz_segmentli.json");
        string srtYolu = Path.Combine(_geciciDizin, "semasiz_segmentli.srt");

        // sema_surumu alanı eksik
        string jsonIcerik = """
        {
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        string srtIcerik = """
        1
        00:00:00,000 --> 00:00:05,000
        Eski yedek SRT metni
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentlerDiziDegilseVeSrtMevcutsa_SrtYeDusmezInvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "bozuk_segmentler.json");
        string srtYolu = Path.Combine(_geciciDizin, "bozuk_segmentler.srt");

        // segmentler dizi yerine string yapılmış
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": "bu bir dizi degil"
        }
        """;
        string srtIcerik = """
        1
        00:00:00,000 --> 00:00:05,000
        Eski yedek SRT metni
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SemaSurumu2AmaSegmentlerEksikse_SrtYeDusmezInvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "segmentsiz_v2.json");
        string srtYolu = Path.Combine(_geciciDizin, "segmentsiz_v2.srt");

        // sema_surumu 2 belirtilmiş ancak segmentler alanı hiç yok
        string jsonIcerik = """
        {
            "sema_surumu": 2
        }
        """;
        string srtIcerik = """
        1
        00:00:00,000 --> 00:00:05,000
        Eski yedek SRT metni
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    // === BULGU 2 TESTLERİ: Segment alanlarının eksik/yanlış tipli olması durumunda InvalidDataException ===

    [Fact]
    public void Oku_SegmentNoAlaniEksikse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "no_eksik.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "bas": 0.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentNoAlaniGecersizTipse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "no_yanlis_tip.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": "bir", "bas": 0.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentBasAlaniEksikse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "bas_eksik.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentBasAlaniGecersizTipse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "bas_yanlis_tip.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": "sifir", "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentSonAlaniEksikse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "son_eksik.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 0.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentSonAlaniGecersizTipse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "son_yanlis_tip.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": "bes", "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentMetinAlaniEksikse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "metin_eksik.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 5.0 }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentMetinAlaniGecersizTipse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "metin_yanlis_tip.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 5.0, "metin": 12345 }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentNesneDegilse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "segment_nesne_degil.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                "gecersiz_segment_metni"
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentZamanlariNegatifse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "negatif_zaman.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": -1.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentSonBastanKucukse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "son_kucuktur_bas.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 10.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentBasAlaniSonsuzIse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "bas_sonsuz.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 1e999, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SegmentSonAlaniSonsuzIse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "son_sonsuz.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 1e999, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SureSnSonsuzIse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "sure_sn_sonsuz.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "sure_sn": 1e999,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_SureSnNegatifIse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "sure_sn_negatif.json");
        string jsonIcerik = """
        {
            "sema_surumu": 2,
            "sure_sn": -10.0,
            "segmentler": [
                { "no": 1, "bas": 0.0, "son": 5.0, "metin": "Test metin" }
            ]
        }
        """;
        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_EskiBicim_SureSnSonsuzIse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "eski_sure_sonsuz.json");
        string srtYolu = Path.Combine(_geciciDizin, "eski_sure_sonsuz.srt");

        string jsonIcerik = """
        {
            "sure_sn": 1e999
        }
        """;
        string srtIcerik = """
        1
        00:00:00,000 --> 00:00:05,000
        Test metin
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }

    [Fact]
    public void Oku_EskiBicim_SureSnNegatifIse_InvalidDataExceptionFirlatir()
    {
        string jsonYolu = Path.Combine(_geciciDizin, "eski_sure_negatif.json");
        string srtYolu = Path.Combine(_geciciDizin, "eski_sure_negatif.srt");

        string jsonIcerik = """
        {
            "sure_sn": -5.0
        }
        """;
        string srtIcerik = """
        1
        00:00:00,000 --> 00:00:05,000
        Test metin
        """;

        File.WriteAllText(jsonYolu, jsonIcerik, Encoding.UTF8);
        File.WriteAllText(srtYolu, srtIcerik, Encoding.UTF8);

        Assert.Throws<InvalidDataException>(() => TranskriptOkuyucu.Oku(jsonYolu));
    }
}
