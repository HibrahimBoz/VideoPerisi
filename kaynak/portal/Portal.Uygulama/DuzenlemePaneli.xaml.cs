namespace Portal.Uygulama;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Portal.Cekirdek;

/// <summary>
/// Ortak segment düzenleme, geçmiş izleme ve geri alma kullanıcı kontrolü (T11C).
/// </summary>
public partial class DuzenlemePaneli : UserControl
{
    private EfektifSegment? _seciliSegment;
    private string? _dersAdi;
    private string? _kaynakOzet;
    private readonly ParolaGecikmeSayaci _parolaSayaci = new();
    private bool _oturumParolaDogrulandi;

    public DersKaydi? Ders { get; set; }
    public string? KokKlasor { get; set; }
    public string? DuzeltmelerKlasoru { get; set; }
    public MetinAramaMotoru? AramaMotoru { get; set; }
    public string? DersAdi { get => _dersAdi; set => _dersAdi = value; }
    public string? KaynakOzet { get => _kaynakOzet; set => _kaynakOzet = value; }

    public EfektifSegment? SeciliSegment => _seciliSegment;
    public bool OturumParolaDogrulandi { get => _oturumParolaDogrulandi; set => _oturumParolaDogrulandi = value; }
    public ParolaGecikmeSayaci ParolaSayaci => _parolaSayaci;

    /// <summary>
    /// Metin kutusunda kaydedilmemiş bir taslak değişiklik olup olmadığını belirtir.
    /// </summary>
    public bool TaslakVarMi => _seciliSegment != null &&
        !string.Equals(_seciliSegment.Metin, SegmentMetinKutusu.Text, StringComparison.Ordinal);

    /// <summary>
    /// Bir düzeltme veya geri alma kaydedildiğinde tetiklenen olay.
    /// </summary>
    public event Action<EfektifSegment>? DuzenlemeKaydedildi;

    public DuzenlemePaneli()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Düzenleme paneli için parola doğrulaması yapar (oturum boyunca bir kez sorulur).
    /// Parola.Dogrula mantığını ParolaPenceresi üzerinden çağırır; yanlışsa ileti gösterir;
    /// 5 yanlışta 30 sn bekleme uygular. parola.json yoksa uyarı verir ve düzenlemeyi kapatır.
    /// </summary>
    public bool Dogrula(Window? sahip = null)
    {
        var pencere = sahip ?? Window.GetWindow(this);
        return ParolaPenceresi.DogrulaVeIzinVer(pencere, KokKlasor, ref _oturumParolaDogrulandi, _parolaSayaci);
    }

    /// <summary>
    /// Harici sayaç ve oturum bayrağı ile parola doğrulaması yapar.
    /// </summary>
    public bool Dogrula(Window? sahip, ref bool oturumDogrulandi, ParolaGecikmeSayaci sayac)
    {
        var pencere = sahip ?? Window.GetWindow(this);
        bool sonuc = ParolaPenceresi.DogrulaVeIzinVer(pencere, KokKlasor, ref oturumDogrulandi, sayac);
        if (sonuc)
        {
            _oturumParolaDogrulandi = true;
        }
        return sonuc;
    }

    /// <summary>
    /// Kök klasör ve girilen parola ile Parola.Dogrula doğrulaması yapar.
    /// </summary>
    public static bool Dogrula(string kokKlasor, string girilenParola)
    {
        return ParolaPenceresi.ParolaDogrulaKok(kokKlasor, girilenParola);
    }

    /// <summary>
    /// Parola doğrudan Parola.Dogrula ile doğrulanır.
    /// </summary>
    public static bool ParolaDogrula(string kokKlasor, string girilenParola)
    {
        return Parola.Dogrula(kokKlasor, girilenParola);
    }

    /// <summary>
    /// Düzenlenecek segmenti ve ders bilgilerini panele yükler.
    /// taslagiKoru true ise ve aynı segment için kaydedilmemiş taslak varsa metin kutusundaki taslak korunur.
    /// </summary>
    public void SegmentYukle(EfektifSegment? segment, string? dersAdi = null, string? kaynakOzet = null, bool taslagiKoru = false)
    {
        bool taslakAyniSegment = _seciliSegment != null && segment != null && _seciliSegment.No == segment.No;
        string? mevcutTaslak = (taslagiKoru && taslakAyniSegment && TaslakVarMi) ? SegmentMetinKutusu.Text : null;
        bool taslakKorundu = mevcutTaslak != null;

        _seciliSegment = segment;
        if (!string.IsNullOrWhiteSpace(dersAdi))
        {
            _dersAdi = dersAdi;
        }
        else if (Ders != null)
        {
            _dersAdi = Ders.Ad;
        }

        if (!string.IsNullOrWhiteSpace(kaynakOzet))
        {
            _kaynakOzet = kaynakOzet;
        }

        DurumMetni.Text = taslakKorundu
            ? "Segment dışarıdan güncellendi; taslağınız korundu."
            : string.Empty;

        if (segment == null)
        {
            SegmentBaslikMetni.Text = "Segment Düzenle (seçim yok)";
            SegmentMetinKutusu.Text = string.Empty;
            SegmentMetinKutusu.IsEnabled = false;
            KaydetDugmesi.IsEnabled = false;
            GeriAlDugmesi.IsEnabled = false;
            CakismaRozeti.Visibility = Visibility.Collapsed;
            GecmisListesi.ItemsSource = null;
            GecmisSayisiMetni.Text = "(0)";
            return;
        }

        SegmentMetinKutusu.IsEnabled = true;
        KaydetDugmesi.IsEnabled = true;
        SegmentBaslikMetni.Text = $"Segment #{segment.No} ({ZamanBicim.Bicimle(segment.Bas)} - {ZamanBicim.Bicimle(segment.Son)})";

        if (taslakKorundu)
        {
            SegmentMetinKutusu.Text = mevcutTaslak!;
        }
        else
        {
            SegmentMetinKutusu.Text = segment.Metin;
        }

        CakismaRozeti.Visibility = segment.Cakisma ? Visibility.Visible : Visibility.Collapsed;
        GeriAlDugmesi.IsEnabled = segment.Gecmis.Count > 0;

        // Geçmiş listesini doldur
        var gecmisOgeleri = segment.Gecmis
            .OrderByDescending(d => d.ZamanUtc)
            .ThenByDescending(d => d.Id, StringComparer.Ordinal)
            .Select(d => new GecmisOgesi(
                d.Kullanici,
                d.ZamanUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                d.Tur == "geri_al" ? "[Geri Al]" : "[Düzeltme]",
                $"{d.Eski}  ➔  {d.Yeni}"
            ))
            .ToList();

        GecmisListesi.ItemsSource = gecmisOgeleri;
        GecmisSayisiMetni.Text = $"({gecmisOgeleri.Count})";
    }

    /// <summary>
    /// Segment metninde yapılan değişikliği DuzeltmeDeposu ile kaydeder.
    /// onceki_id zincirini korur ve arama motorunda dersi yeniler.
    /// </summary>
    private void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        if (_seciliSegment == null)
        {
            DurumMetni.Text = "Segment seçilmedi.";
            return;
        }

        string yeniMetin = SegmentMetinKutusu.Text?.Trim() ?? string.Empty;
        if (string.Equals(_seciliSegment.Metin, yeniMetin, StringComparison.Ordinal))
        {
            DurumMetni.Text = "Metin değişmedi.";
            return;
        }

        string klasor = DuzeltmelerKlasoruBul();
        string dersAdi = _dersAdi ?? Ders?.Ad ?? "ders";

        // onceki_id zincirini koru: segmentin son düzeltmesi varsa onun Id'si
        string? oncekiId = _seciliSegment.Gecmis.Count > 0 ? _seciliSegment.Gecmis[_seciliSegment.Gecmis.Count - 1].Id : null;

        var duzeltme = new Duzeltme
        {
            Id = Guid.NewGuid().ToString("D"),
            Tur = "duzelt",
            Ders = dersAdi,
            KaynakOzet = _kaynakOzet,
            SegmentNo = _seciliSegment.No,
            Bas = _seciliSegment.Bas,
            Eski = _seciliSegment.Metin,
            Yeni = yeniMetin,
            OncekiId = oncekiId,
            Kullanici = Kimlik.Varsayilan.KullaniciAdi(),
            Makine = Kimlik.Varsayilan.MakineAdi(),
            ZamanUtc = DateTime.UtcNow,
            Istemci = "1.0.0"
        };

        try
        {
            var depo = new DuzeltmeDeposu(klasor);
            depo.Yaz(klasor, duzeltme);
        }
        catch (Exception ex)
        {
            DurumMetni.Text = "Kayıt hatası: " + ex.Message;
            return;
        }

        string? yenilemeHatasi = DersiYenileMotoru();
        DurumMetni.Text = yenilemeHatasi == null
            ? "Kaydedildi."
            : "Kaydedildi; arama indeksi yenilenemedi: " + yenilemeHatasi;
        DuzenlemeKaydedildi?.Invoke(_seciliSegment);
    }

    /// <summary>
    /// Son düzeltmeyi geri alan bir geri_al kaydı üretip depolar.
    /// </summary>
    private void GeriAl_Click(object sender, RoutedEventArgs e)
    {
        if (_seciliSegment == null || _seciliSegment.Gecmis.Count == 0)
        {
            DurumMetni.Text = "Geri alınacak kayıt yok.";
            return;
        }

        var sonDk = _seciliSegment.Gecmis[_seciliSegment.Gecmis.Count - 1];
        var geriAl = Duzeltme.GeriAlOlustur(sonDk, Kimlik.Varsayilan);

        try
        {
            string klasor = DuzeltmelerKlasoruBul();
            var depo = new DuzeltmeDeposu(klasor);
            depo.Yaz(klasor, geriAl);
        }
        catch (Exception ex)
        {
            DurumMetni.Text = "Geri alma hatası: " + ex.Message;
            return;
        }

        string? yenilemeHatasi = DersiYenileMotoru();
        DurumMetni.Text = yenilemeHatasi == null
            ? "Geri alındı."
            : "Geri alındı; arama indeksi yenilenemedi: " + yenilemeHatasi;
        DuzenlemeKaydedildi?.Invoke(_seciliSegment);
    }

    /// <summary>
    /// Düzeltmelerin denetim izini RFC 4180 CSV dosyası olarak dışa aktarır.
    /// </summary>
    private void CsvAktar_Click(object sender, RoutedEventArgs e)
    {
        string dersAdi = _dersAdi ?? Ders?.Ad ?? "ders";
        var dlg = new SaveFileDialog
        {
            Title = "Düzeltme Denetim İzini CSV Olarak Kaydet",
            Filter = "CSV Dosyası (*.csv)|*.csv",
            FileName = $"{dersAdi}_duzeltmeler.csv"
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            string klasor = DuzeltmelerKlasoruBul();
            var depo = new DuzeltmeDeposu(klasor);
            var duzeltmeler = depo.DuzeltmeleriYukle(klasor);

            var siraliDuzeltmeler = duzeltmeler
                .OrderBy(d => d.ZamanUtc)
                .ThenBy(d => d.Id, StringComparer.Ordinal)
                .ToList();

            string csv = DenetimIziCsv.Uret(siraliDuzeltmeler);
            File.WriteAllText(dlg.FileName, csv, Encoding.UTF8);

            DurumMetni.Text = $"{duzeltmeler.Count} kayıt aktarıldı.";
        }
        catch (Exception ex)
        {
            DurumMetni.Text = "CSV hatası: " + ex.Message;
        }
    }

    private string? DersiYenileMotoru()
    {
        try
        {
            if (AramaMotoru != null && Ders != null)
            {
                AramaMotoru.DersiYenile(Ders);
            }
            else if (Ders != null)
            {
                string kok = !string.IsNullOrEmpty(KokKlasor) ? KokKlasor : Ders.KlasorYolu;
                using var motor = new MetinAramaMotoru(new IndeksOnbellek(), new DuzeltmeDeposu(kok));
                motor.DersiYenile(Ders);
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public string DuzeltmelerKlasoruBul()
    {
        if (!string.IsNullOrEmpty(DuzeltmelerKlasoru))
        {
            return DuzeltmelerKlasoru;
        }

        if (Ders != null)
        {
            return DersOnbellekAnahtari.DuzeltmelerKlasoruBul(Ders);
        }

        if (!string.IsNullOrEmpty(KokKlasor))
        {
            string ad = _dersAdi ?? "ders";
            return Path.Combine(KokKlasor, $"{ad}.duzeltmeler");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "ders.duzeltmeler");
    }

    private sealed record GecmisOgesi(
        string Kullanici,
        string ZamanMetni,
        string TurMetni,
        string DegisimMetni
    );
}
