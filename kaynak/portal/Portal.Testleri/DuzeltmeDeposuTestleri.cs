namespace Portal.Testleri;

using System.Text;
using System.Text.RegularExpressions;
using Portal.Cekirdek;
using Xunit;

public class DuzeltmeDeposuTestleri : IDisposable
{
    private readonly string _geciciKlasor;
    private readonly DuzeltmeDeposu _depo;

    public DuzeltmeDeposuTestleri()
    {
        _geciciKlasor = Path.Combine(Path.GetTempPath(), "VH_DuzeltmeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_geciciKlasor);
        _depo = new DuzeltmeDeposu(_geciciKlasor);
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

    private static Transkript OrnekTranskriptUret(string dersAdi = "fizik101", string kaynakOzet = "ozet123")
    {
        return new Transkript
        {
            Ad = dersAdi,
            KaynakOzet = kaynakOzet,
            SureSn = 60.0,
            Segmentler = new List<Segment>
            {
                new(0, 0.0, 5.0, "Merhaba değerli arkadaşlar"),
                new(1, 5.0, 10.0, "Bugün türev konusunu işleyeceğiz"),
                new(2, 10.0, 15.0, "Formülü tahtaya yazıyorum")
            }
        };
    }

    [Fact]
    public void Birlestir_SiraliMerge_TumDuzeltmeleriUygular()
    {
        var transkript = OrnekTranskriptUret();
        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        // Segment 0 için sıralı iki düzeltme
        var d1 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 0,
            Eski = "Merhaba değerli arkadaşlar",
            Yeni = "Merhaba sevgili arkadaşlar",
            ZamanUtc = DateTime.UtcNow.AddSeconds(-10)
        };
        _depo.Yaz(duzeltmelerKlasoru, d1);

        var d2 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 0,
            Eski = "Merhaba sevgili arkadaşlar",
            Yeni = "Merhaba kıymetli arkadaşlar",
            OncekiId = d1.Id,
            ZamanUtc = DateTime.UtcNow
        };
        _depo.Yaz(duzeltmelerKlasoru, d2);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Equal(3, sonuc.Segmentler.Count);
        Assert.False(sonuc.CakismaVar);
        Assert.Equal("Merhaba kıymetli arkadaşlar", sonuc[0].Metin);
        Assert.Equal(2, sonuc[0].Gecmis.Count);
        Assert.False(sonuc[0].Cakisma);
    }

    [Fact]
    public void Birlestir_SonKazanir_EnSonZamanliDuzeltmeEtkinOlur()
    {
        var transkript = OrnekTranskriptUret();
        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        DateTime zaman1 = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime zaman2 = new(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc);
        DateTime zaman3 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        var d1 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 1,
            Eski = "Bugün türev konusunu işleyeceğiz",
            Yeni = "İlk Düzeltme",
            ZamanUtc = zaman1
        };
        var d2 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 1,
            Eski = "İlk Düzeltme",
            Yeni = "İkinci Düzeltme",
            OncekiId = d1.Id,
            ZamanUtc = zaman2
        };
        var d3 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 1,
            Eski = "İkinci Düzeltme",
            Yeni = "Son Kazanan Düzeltme",
            OncekiId = d2.Id,
            ZamanUtc = zaman3
        };

        // Rastgele sırada yaz
        _depo.Yaz(duzeltmelerKlasoru, d2);
        _depo.Yaz(duzeltmelerKlasoru, d3);
        _depo.Yaz(duzeltmelerKlasoru, d1);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Equal("Son Kazanan Düzeltme", sonuc[1].Metin);
        Assert.Equal(3, sonuc[1].Gecmis.Count);
        Assert.False(sonuc[1].Cakisma);
    }

    [Fact]
    public void Birlestir_CakismaTespiti_AyniOncekiId_CakismaBayragiSetEdilirVeIkisiDeGecmisteKalir()
    {
        var transkript = OrnekTranskriptUret();
        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        // Ana düzeltme
        var ana = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 1,
            Eski = "Bugün türev konusunu işleyeceğiz",
            Yeni = "Türev ve limit konusunu işleyeceğiz",
            ZamanUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        _depo.Yaz(duzeltmelerKlasoru, ana);

        // Aynı ana düzeltmeyi temel alan iki eşzamanlı çakışan düzeltme
        var dalA = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 1,
            Eski = "Türev ve limit konusunu işleyeceğiz",
            Yeni = "Kullanıcı A'nın Önerisi",
            OncekiId = ana.Id,
            ZamanUtc = DateTime.UtcNow.AddMinutes(-2)
        };

        var dalB = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 1,
            Eski = "Türev ve limit konusunu işleyeceğiz",
            Yeni = "Kullanıcı B'nin Önerisi",
            OncekiId = ana.Id,
            ZamanUtc = DateTime.UtcNow.AddMinutes(-1)
        };

        _depo.Yaz(duzeltmelerKlasoru, dalA);
        _depo.Yaz(duzeltmelerKlasoru, dalB);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        var seg = sonuc[1];
        Assert.True(seg.Cakisma);
        Assert.True(sonuc.CakismaVar);

        // İkisi de çakıştığı için geçmişte kalır, çakışma öncesindeki ana düzeltme metni kazanır
        Assert.Equal("Türev ve limit konusunu işleyeceğiz", seg.Metin);
        Assert.Equal(3, seg.Gecmis.Count);
    }

    [Fact]
    public void Birlestir_GeriAl_HedefinEskisiniGeriYukler()
    {
        var transkript = OrnekTranskriptUret();
        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        var duzelt = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 2,
            Eski = "Formülü tahtaya yazıyorum",
            Yeni = "Hatalı Düzeltme Metni",
            ZamanUtc = DateTime.UtcNow.AddMinutes(-2)
        };
        _depo.Yaz(duzeltmelerKlasoru, duzelt);

        var geriAl = Duzeltme.GeriAlOlustur(duzelt);
        _depo.Yaz(duzeltmelerKlasoru, geriAl);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Equal("Formülü tahtaya yazıyorum", sonuc[2].Metin);
        Assert.Equal(2, sonuc[2].Gecmis.Count);
        Assert.False(sonuc[2].Cakisma);
    }

    [Fact]
    public void Birlestir_GeriAlZinciri_CokluGeriAlDogruUygulanir()
    {
        var transkript = OrnekTranskriptUret();
        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        // 1. Düzeltme: V1 -> V2
        var d1 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 0,
            Eski = "V1",
            Yeni = "V2",
            ZamanUtc = DateTime.UtcNow.AddMinutes(-10)
        };
        _depo.Yaz(duzeltmelerKlasoru, d1);

        // 2. Geri Al (d1'i geri al): V2 -> V1
        var d2 = new Duzeltme
        {
            Tur = "geri_al",
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 0,
            GeriAlHedef = d1.Id,
            OncekiId = d1.Id,
            Eski = "V2",
            Yeni = "V1",
            ZamanUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        _depo.Yaz(duzeltmelerKlasoru, d2);

        // 3. Yeni Düzeltme: V1 -> V3
        var d3 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 0,
            Eski = "V1",
            Yeni = "V3",
            OncekiId = d2.Id,
            ZamanUtc = DateTime.UtcNow
        };
        _depo.Yaz(duzeltmelerKlasoru, d3);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Equal("V3", sonuc[0].Metin);
        Assert.Equal(3, sonuc[0].Gecmis.Count);
        Assert.False(sonuc[0].Cakisma);
    }

    [Fact]
    public void Birlestir_TmpVeBozukDosyalar_YokSayilirVeSayacArtar()
    {
        var transkript = OrnekTranskriptUret();
        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);
        Directory.CreateDirectory(duzeltmelerKlasoru);

        // Geçerli bir düzeltme yaz
        var gecerli = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 0,
            Eski = "Merhaba değerli arkadaşlar",
            Yeni = "Geçerli Değişiklik"
        };
        _depo.Yaz(duzeltmelerKlasoru, gecerli);

        // .tmp dosyası bırak
        string tmpDosya = Path.Combine(duzeltmelerKlasoru, "gecici_yazilamamis.json.tmp");
        File.WriteAllText(tmpDosya, "yarım kalmış içerik");

        // Bozuk JSON dosyası bırak
        string bozukDosya = Path.Combine(duzeltmelerKlasoru, "bozuk_dosya.json");
        File.WriteAllText(bozukDosya, "{ sema: 1, gecersiz json");

        // Şeması uymayan dosya bırak
        string yanlisSemaDosya = Path.Combine(duzeltmelerKlasoru, "yanlis_sema.json");
        File.WriteAllText(yanlisSemaDosya, "{\"sema\":99,\"id\":\"123\"}");

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Equal("Geçerli Değişiklik", sonuc[0].Metin);
        Assert.Equal(3, _depo.AtlananDosyaSayisi);
        Assert.Equal(3, sonuc.AtlananDosyaSayisi);
    }

    [Fact]
    public void YazVeBirlestir_TurkceKarakterler_BozulmadanSaklanirVeBirlestirilir()
    {
        var transkript = new Transkript
        {
            Ad = "türkçe_dersi_şğüöçı",
            KaynakOzet = "özel_özet_şğü",
            Segmentler = new List<Segment>
            {
                new(0, 1.0, 4.0, "Şemsi Paşa Pasajında Sesi Boğuk Kuşlar Ötüşür")
            }
        };

        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        var d = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = transkript.KaynakOzet,
            SegmentNo = 0,
            Eski = "Şemsi Paşa Pasajında Sesi Boğuk Kuşlar Ötüşür",
            Yeni = "Şemsi Paşa Pasajında Sesi Boğuk Kuşlar Şakırdar",
            Kullanici = "ALAN\\çağatay_öztürk",
            Makine = "İSTANBUL-PC"
        };

        string dosyaYolu = _depo.Yaz(duzeltmelerKlasoru, d);

        // Dosya adı güvenli olmalı
        string dosyaAdi = Path.GetFileName(dosyaYolu);
        Assert.DoesNotContain("\\", dosyaAdi);

        // Dosya içeriği Türkçe karakterleri korumalı
        string icerik = File.ReadAllText(dosyaYolu, Encoding.UTF8);
        Assert.Contains("Şakırdar", icerik);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);
        Assert.Equal("Şemsi Paşa Pasajında Sesi Boğuk Kuşlar Şakırdar", sonuc[0].Metin);
    }

    [Fact]
    public void Birlestir_KaynakOzetUyusmazsa_ZamanVeEskiMetinleEslesir()
    {
        // Transkript güncellenmiş ve kaynak_özet değişmiş
        var transkript = new Transkript
        {
            Ad = "dersA",
            KaynakOzet = "yeni_sha256_ozet",
            Segmentler = new List<Segment>
            {
                new(0, 0.0, 3.0, "Giriş kısmı"),
                new(1, 10.2, 14.0, "Eski Metin Hedefi") // Eski segment 5 buraya kaymış (bas: 10.0 -> 10.2 +-0.5 sn)
            }
        };

        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        var d = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = "eski_farkli_ozet",
            SegmentNo = 5, // Eski segment no 5 idi
            Bas = 10.0,    // 10.2 ile ±0.5 sn içinde eşleşir
            Eski = "Eski Metin Hedefi",
            Yeni = "Başarıyla Eşleşen Yeni Metin"
        };
        _depo.Yaz(duzeltmelerKlasoru, d);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Empty(sonuc.Yetimler);
        Assert.Equal("Başarıyla Eşleşen Yeni Metin", sonuc[1].Metin);
    }

    [Fact]
    public void Birlestir_KaynakOzetUyusmazsa_EslesmeyenDuzeltmelerYetimListesineEklenir()
    {
        var transkript = new Transkript
        {
            Ad = "dersB",
            KaynakOzet = "yeni_ozet_456",
            Segmentler = new List<Segment>
            {
                new(0, 1.0, 5.0, "Mevcut Segment")
            }
        };

        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        // Ne zamanı ne metni eşleşmeyen düzeltme
        var yetimDuzeltme = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = "bambaska_eski_ozet",
            SegmentNo = 99,
            Bas = 88.0,
            Eski = "Silinmiş Eski Metin",
            Yeni = "Artık Yeri Olmayan Düzeltme"
        };
        _depo.Yaz(duzeltmelerKlasoru, yetimDuzeltme);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Single(sonuc.Yetimler);
        Assert.Equal(yetimDuzeltme.Id, sonuc.Yetimler[0].Id);
        Assert.Equal("Mevcut Segment", sonuc[0].Metin);
    }

    [Fact]
    public void Birlestir_KaynakOzetUyusmazsa_ZincirdekiIkinciDuzeltmeGuncelMetinleEslesirVeYetimOlmaz()
    {
        // Bulgu 1: Özet değişince zincirdeki ikinci düzeltme ilk düzeltmenin yeni metnini arar,
        // güncel metin üzerinden çözülmeli ve yetim olmamalı.
        var transkript = new Transkript
        {
            Ad = "tarih101",
            KaynakOzet = "yeni_ozet_hash_999",
            Segmentler = new List<Segment>
            {
                new(0, 5.2, 9.0, "Osmanlı Devleti Kuruluş Dönemi") // bas: 5.0 -> 5.2 (±0.5 sn içinde)
            }
        };

        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        // İlk düzeltme: "Kuruluş Dönemi" -> "Beylikten Devlete"
        var d1 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = "eski_farkli_ozet_111",
            SegmentNo = 12,
            Bas = 5.0,
            Eski = "Osmanlı Devleti Kuruluş Dönemi",
            Yeni = "Osmanlı Devleti Beylikten Devlete",
            ZamanUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        // İkinci düzeltme: ilk düzeltmenin Yeni metnini Eski olarak arar
        var d2 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = "eski_farkli_ozet_111",
            SegmentNo = 12,
            Bas = 5.0,
            Eski = "Osmanlı Devleti Beylikten Devlete",
            Yeni = "Osmanlı Devleti Klasik Çağ",
            OncekiId = d1.Id,
            ZamanUtc = new DateTime(2026, 10, 1, 10, 5, 0, DateTimeKind.Utc)
        };

        _depo.Yaz(duzeltmelerKlasoru, d1);
        _depo.Yaz(duzeltmelerKlasoru, d2);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Empty(sonuc.Yetimler);
        Assert.Equal("Osmanlı Devleti Klasik Çağ", sonuc[0].Metin);
        Assert.Equal(2, sonuc[0].Gecmis.Count);
        Assert.False(sonuc[0].Cakisma);
    }

    [Fact]
    public void Birlestir_KaynakOzetUyusmazsa_BasZamaniOlmayanDuzeltmeYetimSayilir()
    {
        // Bulgu 2: Bas yoksa zaman şartı atlanamaz, aynı metinli yanlış segmente uygulanmamalı, Yetim sayılmalı.
        var transkript = new Transkript
        {
            Ad = "fizik201",
            KaynakOzet = "yeni_ozet_hash_abc",
            Segmentler = new List<Segment>
            {
                new(0, 0.0, 5.0, "Deney Sonucu Aynı Metin"),
                new(1, 100.0, 105.0, "Deney Sonucu Aynı Metin")
            }
        };

        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        // Bas değeri null olan düzeltme (zaman bilgisi yok)
        var d = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = "eski_ozet_farkli",
            SegmentNo = 0,
            Bas = null, // Zaman yok
            Eski = "Deney Sonucu Aynı Metin",
            Yeni = "Yanlış Uygulanmaması Gereken Metin"
        };

        _depo.Yaz(duzeltmelerKlasoru, d);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        // Düzeltme Yetim sayılmalı, hiçbir segmente uygulanmamalı
        Assert.Single(sonuc.Yetimler);
        Assert.Equal(d.Id, sonuc.Yetimler[0].Id);
        Assert.Equal("Deney Sonucu Aynı Metin", sonuc[0].Metin);
        Assert.Equal("Deney Sonucu Aynı Metin", sonuc[1].Metin);
    }

    [Fact]
    public void Birlestir_KaynakOzetUyusmazsa_CokluZincirVeGeriAlDogruCozulur()
    {
        var transkript = new Transkript
        {
            Ad = "kimya101",
            KaynakOzet = "ozet_v2",
            Segmentler = new List<Segment>
            {
                new(0, 12.3, 15.0, "Atom Modelleri Giriş")
            }
        };

        string duzeltmelerKlasoru = _depo.HedefKlasor(transkript.Ad);

        var d1 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = "ozet_v1",
            Bas = 12.1,
            Eski = "Atom Modelleri Giriş",
            Yeni = "Bohr Atom Modeli",
            ZamanUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        var d2 = new Duzeltme
        {
            Tur = "geri_al",
            Ders = transkript.Ad,
            KaynakOzet = "ozet_v1",
            Bas = 12.1,
            GeriAlHedef = d1.Id,
            OncekiId = d1.Id,
            Eski = "Bohr Atom Modeli",
            Yeni = "Atom Modelleri Giriş",
            ZamanUtc = new DateTime(2026, 10, 1, 10, 2, 0, DateTimeKind.Utc)
        };

        var d3 = new Duzeltme
        {
            Ders = transkript.Ad,
            KaynakOzet = "ozet_v1",
            Bas = 12.1,
            OncekiId = d2.Id,
            Eski = "Atom Modelleri Giriş",
            Yeni = "Modern Atom Teorisi",
            ZamanUtc = new DateTime(2026, 10, 1, 10, 4, 0, DateTimeKind.Utc)
        };

        _depo.Yaz(duzeltmelerKlasoru, d1);
        _depo.Yaz(duzeltmelerKlasoru, d2);
        _depo.Yaz(duzeltmelerKlasoru, d3);

        var sonuc = _depo.Birlestir(transkript, duzeltmelerKlasoru);

        Assert.Empty(sonuc.Yetimler);
        Assert.Equal("Modern Atom Teorisi", sonuc[0].Metin);
        Assert.Equal(3, sonuc[0].Gecmis.Count);
    }

    [Fact]
    public void DosyaAdi_SpecFormatinaUygunUretilir()
    {
        var d = new Duzeltme
        {
            Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            Kullanici = "ALAN\\deneme.kullanici",
            ZamanUtc = new DateTime(2026, 10, 7, 10, 15, 12, 345, DateTimeKind.Utc)
        };

        string dosyaAdi = DuzeltmeDeposu.DosyaAdiUret(d);

        // Biçim: <zamanUTC yyyyMMddTHHmmssfffZ>_<kullanici güvenli>_<guid ilk 6>.json
        Assert.StartsWith("20261007T101512345Z_ALAN_deneme.kullanici_a1b2c3", dosyaAdi);
        Assert.EndsWith(".json", dosyaAdi);

        // Regex ile tam doğrulama
        var desen = new Regex(@"^\d{8}T\d{9}Z_[a-zA-Z0-9_\-.]*_[0-9a-fA-F]{6}\.json$");
        Assert.Matches(desen, dosyaAdi);
    }

    [Fact]
    public void Birlestir_BosKlasor_TranskriptSegmentleriniDegistirmedenDoner()
    {
        var transkript = OrnekTranskriptUret();
        string bosKlasor = Path.Combine(_geciciKlasor, "bos_klasor.duzeltmeler");

        var sonuc = _depo.Birlestir(transkript, bosKlasor);

        Assert.Equal(3, sonuc.Segmentler.Count);
        Assert.Empty(sonuc.Yetimler);
        Assert.False(sonuc.CakismaVar);
        Assert.Equal(transkript.Segmentler[0].Metin, sonuc[0].Metin);
    }

    [Fact]
    public async Task Eszamanlilik_8Gorev100YazimParalel_800DosyaSifirKayipSifirBozukBirlestirHatasiz()
    {
        string paralelKlasor = Path.Combine(_geciciKlasor, "paralel_test.duzeltmeler");
        Directory.CreateDirectory(paralelKlasor);

        var transkript = new Transkript
        {
            Ad = "paralel_ders",
            KaynakOzet = "ozet_paralel",
            Segmentler = Enumerable.Range(0, 10)
                .Select(i => new Segment(i, i * 2.0, (i + 1) * 2.0, $"segment_{i}"))
                .ToList()
        };

        const int gorevSayisi = 8;
        const int yazimSayisi = 100;
        var gorevler = new Task[gorevSayisi];

        for (int g = 0; g < gorevSayisi; g++)
        {
            int gorevId = g;
            gorevler[g] = Task.Run(() =>
            {
                // Ayrı DuzeltmeDeposu örnekleri aynı klasörde çalışır
                var yerelDepo = new DuzeltmeDeposu(paralelKlasor);

                for (int y = 0; y < yazimSayisi; y++)
                {
                    int segNo = (gorevId * yazimSayisi + y) % 10;
                    var d = new Duzeltme
                    {
                        Id = Guid.NewGuid().ToString("D"),
                        Ders = transkript.Ad,
                        KaynakOzet = transkript.KaynakOzet,
                        SegmentNo = segNo,
                        Eski = $"segment_{segNo}",
                        Yeni = $"guncel_{gorevId}_{y}",
                        Kullanici = $"TEST\\kullanici_{gorevId}",
                        Makine = $"PC-{gorevId}",
                        ZamanUtc = DateTime.UtcNow,
                        Istemci = "1.0.0"
                    };

                    yerelDepo.Yaz(paralelKlasor, d);
                }
            });
        }

        await Task.WhenAll(gorevler);

        // 800 dosya kontrolü (0 kayıp)
        var jsonDosyalar = Directory.GetFiles(paralelKlasor, "*.json");
        Assert.Equal(800, jsonDosyalar.Length);

        // 0 tmp dosyası kalmalı
        var tmpDosyalar = Directory.GetFiles(paralelKlasor, "*.tmp");
        Assert.Empty(tmpDosyalar);

        // Birlestir hatasız ve 0 bozuk
        var okumaDeposu = new DuzeltmeDeposu();
        var sonuc = okumaDeposu.Birlestir(transkript, paralelKlasor);

        Assert.Equal(0, okumaDeposu.AtlananDosyaSayisi);
        Assert.Equal(10, sonuc.Segmentler.Count);
        Assert.Empty(sonuc.Yetimler);
    }
}
