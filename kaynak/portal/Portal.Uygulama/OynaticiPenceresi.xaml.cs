namespace Portal.Uygulama;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Portal.Cekirdek;

/// <summary>
/// Tek video oynatıcı penceresi ve transkript paneli (T11B).
/// </summary>
public partial class OynaticiPenceresi : Window
{
    private readonly LibVlcOynatici _oynatici;
    private readonly DispatcherTimer _zamanlayici;
    private readonly List<EfektifSegment> _efektifSegmentler = [];
    private readonly ObservableCollection<TranskriptSatirGorunumu> _goruntulenenSegmentler = [];

    private string? _geciciSrtYolu;
    private EfektifSegment? _oncekiAktifSegment;
    private bool _videoVar;
    private bool _tamEkran;
    private bool _kaydiracSurukleniyor;
    private double _toplamSureSn;
    private bool _kapatildi;

    private WindowState _oncekiWindowState = WindowState.Normal;
    private WindowStyle _oncekiWindowStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode _oncekiResizeMode = ResizeMode.CanResize;

    public DersKaydi Kayit { get; }
    public double BaslangicSn { get; }
    public IOynatici Oynatici => _oynatici;

    public OynaticiPenceresi()
        : this(new DersKaydi(string.Empty, string.Empty, string.Empty, null), 0)
    {
    }

    public OynaticiPenceresi(DersKaydi kayit, double baslangicSn = 0)
    {
        Kayit = kayit ?? new DersKaydi(string.Empty, string.Empty, string.Empty, null);
        BaslangicSn = baslangicSn;

        InitializeComponent();

        Title = !string.IsNullOrWhiteSpace(Kayit.Ad) ? $"Oynatıcı - {Kayit.Ad}" : "Oynatıcı";

        _oynatici = new LibVlcOynatici();
        VideoGosterici.MediaPlayer = _oynatici.MediaPlayer;

        TranskriptListesi.ItemsSource = _goruntulenenSegmentler;

        // 250 ms aralıklı zaman senkronizasyon zamanlayıcısı
        _zamanlayici = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _zamanlayici.Tick += Zamanlayici_Tick;

        // Transkript ve videoyu yükle
        TranskriptiYukle();
        VideoyuYukle(baslangicSn);

        _zamanlayici.Start();
    }

    /// <summary>
    /// Transkript dosyasını okur ve düzeltmeler deposuyla birleştirerek listeyi doldurur.
    /// </summary>
    private void TranskriptiYukle()
    {
        _efektifSegmentler.Clear();
        _goruntulenenSegmentler.Clear();

        string? transkriptYolu = BulTranskriptYolu();
        if (string.IsNullOrWhiteSpace(transkriptYolu) || !File.Exists(transkriptYolu))
        {
            TranskriptDurumMetni.Text = "Transkript dosyası bulunamadı.";
            return;
        }

        try
        {
            var hamTranskript = TranskriptOkuyucu.Oku(transkriptYolu);
            string klasor = !string.IsNullOrWhiteSpace(Kayit.KlasorYolu)
                ? Kayit.KlasorYolu
                : Path.GetDirectoryName(transkriptYolu) ?? string.Empty;

            var depo = new DuzeltmeDeposu(klasor);
            var birlestirme = depo.Birlestir(hamTranskript);

            foreach (var seg in birlestirme.Segmentler)
            {
                _efektifSegmentler.Add(seg);
            }

            FiltreyiUygula();

            // Eğer video süresi henüz bilinmiyorsa son segmentten yaklaşık süre al
            if (_efektifSegmentler.Count > 0 && _toplamSureSn <= 0)
            {
                double sonSn = _efektifSegmentler.Max(s => s.Son);
                if (sonSn > 0)
                {
                    _toplamSureSn = sonSn;
                    ZamanKaydiraci.Maximum = sonSn;
                    ZamanMetni.Text = $"{ZamanBicim.Bicimle(0)} / {ZamanBicim.Bicimle(sonSn)}";
                }
            }
        }
        catch (Exception ex)
        {
            TranskriptDurumMetni.Text = $"Transkript okunamadı: {ex.Message}";
        }
    }

    private string? BulTranskriptYolu()
    {
        if (!string.IsNullOrWhiteSpace(Kayit.JsonYolu) && File.Exists(Kayit.JsonYolu))
        {
            return Kayit.JsonYolu;
        }

        if (!string.IsNullOrWhiteSpace(Kayit.KlasorYolu) && Directory.Exists(Kayit.KlasorYolu))
        {
            string aday = Path.Combine(Kayit.KlasorYolu, Kayit.Ad + ".json");
            if (File.Exists(aday))
            {
                return aday;
            }

            try
            {
                var adaylar = Directory.GetFiles(Kayit.KlasorYolu, "*.json");
                foreach (var a in adaylar)
                {
                    string ad = Path.GetFileName(a);
                    if (!string.Equals(ad, "ders.json", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ad, "parola.json", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ad, "ayarlar.json", StringComparison.OrdinalIgnoreCase))
                    {
                        return a;
                    }
                }
            }
            catch
            {
                // Yut
            }
        }

        return null;
    }

    /// <summary>
    /// Video dosyasını yükler ve başlangıç saniyesinden (baslangicSn - 2) başlatır.
    /// Video yoksa kullanıcıya uyarı gösterir ve arayüzü yalnızca transkript moduna alır.
    /// </summary>
    private void VideoyuYukle(double baslangicSn)
    {
        double baslatmaSn = OynaticiYardimcisi.BaslangicZamaniHesapla(baslangicSn);

        string? videoYolu = BulVideoYolu();
        if (string.IsNullOrWhiteSpace(videoYolu) || !File.Exists(videoYolu))
        {
            _videoVar = false;
            VideoUyariMetni.Text = "Video dosyası bulunamadı — yalnızca transkript gösteriliyor.";
            VideoUyariKutusu.Visibility = Visibility.Visible;
            VideoGosterici.Visibility = Visibility.Collapsed;

            OynatDuraklatDugmesi.IsEnabled = false;
            GeriOnSnDugmesi.IsEnabled = false;
            IleriOnSnDugmesi.IsEnabled = false;
            ZamanKaydiraci.IsEnabled = false;
            HizSecici.IsEnabled = false;
            SesKaydiraci.IsEnabled = false;
            TamEkranDugmesi.IsEnabled = false;
            return;
        }

        try
        {
            _videoVar = true;
            VideoUyariKutusu.Visibility = Visibility.Collapsed;
            VideoGosterici.Visibility = Visibility.Visible;

            _oynatici.Yukle(videoYolu);

            // Oynatıcı hata dinleyicisi: anlaşılır Türkçe ileti
            _oynatici.MediaPlayer.EncounteredError += (_, _) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    VideoUyariMetni.Text = "Video oynatılırken bir hata oluştu.";
                    VideoUyariKutusu.Visibility = Visibility.Visible;
                });
            };

            // Uzunluk değiştiğinde süre güncelleme
            _oynatici.MediaPlayer.LengthChanged += (_, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    double sure = e.Length / 1000.0;
                    if (sure > 0)
                    {
                        _toplamSureSn = sure;
                        ZamanKaydiraci.Maximum = sure;
                        ZamanMetni.Text = $"{ZamanBicim.Bicimle(_oynatici.Zaman)} / {ZamanBicim.Bicimle(sure)}";
                    }
                });
            };

            // Oynatmaya başla
            _oynatici.MediaPlayer.Play();
            OynatDuraklatDugmesi.Content = "⏸ Duraklat";

            // Belirtilen saniyeye atla
            if (baslatmaSn > 0)
            {
                void OnPlaying(object? s, EventArgs e)
                {
                    _oynatici.MediaPlayer.Playing -= OnPlaying;
                    Dispatcher.InvokeAsync(() =>
                    {
                        _oynatici.Ara(baslatmaSn);
                    });
                }

                _oynatici.MediaPlayer.Playing += OnPlaying;
                _oynatici.Ara(baslatmaSn);
            }
        }
        catch (Exception ex)
        {
            _videoVar = false;
            VideoUyariMetni.Text = $"Video dosyası açılamadı:\n{ex.Message}";
            VideoUyariKutusu.Visibility = Visibility.Visible;
            VideoGosterici.Visibility = Visibility.Collapsed;

            OynatDuraklatDugmesi.IsEnabled = false;
            GeriOnSnDugmesi.IsEnabled = false;
            IleriOnSnDugmesi.IsEnabled = false;
            ZamanKaydiraci.IsEnabled = false;
            HizSecici.IsEnabled = false;
            SesKaydiraci.IsEnabled = false;
            TamEkranDugmesi.IsEnabled = false;
        }
    }

    private string? BulVideoYolu()
    {
        if (!string.IsNullOrWhiteSpace(Kayit.VideoYolu) && File.Exists(Kayit.VideoYolu))
        {
            return Kayit.VideoYolu;
        }

        if (!string.IsNullOrWhiteSpace(Kayit.KlasorYolu) && Directory.Exists(Kayit.KlasorYolu))
        {
            string[] uzantilar = [".mp4", ".mkv", ".avi", ".mov", ".webm"];
            foreach (var uzt in uzantilar)
            {
                string aday = Path.Combine(Kayit.KlasorYolu, Kayit.Ad + uzt);
                if (File.Exists(aday))
                {
                    return aday;
                }
            }

            try
            {
                var adaylar = Directory.GetFiles(Kayit.KlasorYolu)
                    .Where(f => uzantilar.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (adaylar.Count > 0)
                {
                    return adaylar[0];
                }
            }
            catch
            {
                // Yut
            }
        }

        return null;
    }

    /// <summary>
    /// 250 ms DispatcherTimer senkronizasyonu: zaman çubuğu, zaman metni ve aktif segment takibi.
    /// </summary>
    private void Zamanlayici_Tick(object? sender, EventArgs e)
    {
        if (_kapatildi) return;

        double anlikSn = _videoVar ? _oynatici.Zaman : ZamanKaydiraci.Value;
        double toplamSn = _toplamSureSn > 0
            ? _toplamSureSn
            : (_videoVar ? _oynatici.ToplamSure : ZamanKaydiraci.Maximum);

        if (toplamSn > 0 && Math.Abs(ZamanKaydiraci.Maximum - toplamSn) > 0.5)
        {
            _toplamSureSn = toplamSn;
            ZamanKaydiraci.Maximum = toplamSn;
        }

        if (!_kaydiracSurukleniyor)
        {
            ZamanKaydiraci.Value = Math.Clamp(anlikSn, 0, ZamanKaydiraci.Maximum);
            ZamanMetni.Text = $"{ZamanBicim.Bicimle(anlikSn)} / {ZamanBicim.Bicimle(ZamanKaydiraci.Maximum)}";
        }

        // Oynat/duraklat düğme metnini güncelle
        if (_videoVar)
        {
            if (_oynatici.Oynatiliyor && (string)OynatDuraklatDugmesi.Content != "⏸ Duraklat")
            {
                OynatDuraklatDugmesi.Content = "⏸ Duraklat";
            }
            else if (!_oynatici.Oynatiliyor && (string)OynatDuraklatDugmesi.Content != "▶ Oynat")
            {
                OynatDuraklatDugmesi.Content = "▶ Oynat";
            }
        }

        // Aktif segmenti bul ve vurgula
        var aktif = OynaticiYardimcisi.AktifSegmentBul(_efektifSegmentler, anlikSn);
        if (aktif != _oncekiAktifSegment)
        {
            _oncekiAktifSegment = aktif;
            TranskriptSatirGorunumu? aktifGorunum = null;

            foreach (var oge in _goruntulenenSegmentler)
            {
                bool eslesiyor = aktif != null && oge.Segment.No == aktif.No;
                oge.Vurgulu = eslesiyor;
                if (eslesiyor)
                {
                    aktifGorunum = oge;
                }
            }

            if (aktifGorunum != null && OtoKaydirKutusu.IsChecked == true)
            {
                TranskriptListesi.ScrollIntoView(aktifGorunum);
            }
        }
    }

    /// <summary>
    /// Transkript panel içi arama kutusuna göre segmentleri filtreler.
    /// </summary>
    private void TranskriptAramaKutusu_TextChanged(object sender, TextChangedEventArgs e)
    {
        FiltreyiUygula();
    }

    private void FiltreyiUygula()
    {
        string arama = TranskriptAramaKutusu.Text?.Trim() ?? string.Empty;
        var filtrelenmis = OynaticiYardimcisi.SegmentFiltrele(_efektifSegmentler, arama);

        _goruntulenenSegmentler.Clear();
        foreach (var seg in filtrelenmis)
        {
            var vm = new TranskriptSatirGorunumu(seg)
            {
                Vurgulu = _oncekiAktifSegment != null && seg.No == _oncekiAktifSegment.No
            };
            _goruntulenenSegmentler.Add(vm);
        }

        TranskriptDurumMetni.Text = string.IsNullOrWhiteSpace(arama)
            ? $"{_goruntulenenSegmentler.Count} segment"
            : $"{_goruntulenenSegmentler.Count} / {_efektifSegmentler.Count} segment bulundu";
    }

    /// <summary>
    /// Oynat / duraklat düğmesi tıklama eylemi.
    /// </summary>
    private void OynatDuraklat_Click(object sender, RoutedEventArgs e)
    {
        OynatDuraklat();
    }

    private void OynatDuraklat()
    {
        if (!_videoVar) return;

        _oynatici.OynatDuraklat();
        OynatDuraklatDugmesi.Content = _oynatici.Oynatiliyor ? "⏸ Duraklat" : "▶ Oynat";
    }

    /// <summary>
    /// -10 saniye atlama.
    /// </summary>
    private void GeriOnSn_Click(object sender, RoutedEventArgs e)
    {
        ZamanAtla(-10.0);
    }

    /// <summary>
    /// +10 saniye atlama.
    /// </summary>
    private void IleriOnSn_Click(object sender, RoutedEventArgs e)
    {
        ZamanAtla(10.0);
    }

    private void ZamanAtla(double deltaSn)
    {
        if (!_videoVar) return;

        double simdi = _oynatici.Zaman;
        double azami = _toplamSureSn > 0 ? _toplamSureSn : _oynatici.ToplamSure;
        double hedef = OynaticiYardimcisi.ZamanAtla(simdi, deltaSn, azami);
        _oynatici.Ara(hedef);
        ZamanKaydiraci.Value = hedef;
    }

    /// <summary>
    /// Hız seçimi değiştiğinde LibVLC oynatıcı hızını günceller.
    /// </summary>
    private void HizSecici_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HizSecici.SelectedItem is ComboBoxItem item &&
            double.TryParse(item.Tag?.ToString(), CultureInfo.InvariantCulture, out double hiz))
        {
            _oynatici.Hiz = hiz;
        }
    }

    /// <summary>
    /// Ses kaydıracı değiştiğinde ses seviyesini günceller.
    /// </summary>
    private void SesKaydiraci_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_oynatici != null)
        {
            int seviye = (int)Math.Round(e.NewValue);
            _oynatici.SesSeviyesi = seviye;
            if (SesMetni != null)
            {
                SesMetni.Text = $"%{seviye}";
            }
        }
    }

    /// <summary>
    /// İlerleme çubuğu sürükleme ve tıklama eylemleri.
    /// </summary>
    private void ZamanKaydiraci_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_kaydiracSurukleniyor && _videoVar)
        {
            ZamanMetni.Text = $"{ZamanBicim.Bicimle(e.NewValue)} / {ZamanBicim.Bicimle(ZamanKaydiraci.Maximum)}";
        }
    }

    private void ZamanKaydiraci_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _kaydiracSurukleniyor = true;
    }

    private void ZamanKaydiraci_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _kaydiracSurukleniyor = false;
        if (_videoVar)
        {
            _oynatici.Ara(ZamanKaydiraci.Value);
        }
    }

    private void ZamanKaydiraci_KeyUp(object sender, KeyEventArgs e)
    {
        if (_videoVar)
        {
            _oynatici.Ara(ZamanKaydiraci.Value);
        }
    }

    /// <summary>
    /// Transkript satırına tıklandığında ilgili saniyeye atlar (tıklama = atla).
    /// </summary>
    private void TranskriptListesi_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SegmenteAtla();
    }

    private void TranskriptListesi_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SegmenteAtla();
    }

    private void TranskriptListesi_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SegmenteAtla();
            e.Handled = true;
        }
    }

    private void SegmenteAtla()
    {
        if (TranskriptListesi.SelectedItem is TranskriptSatirGorunumu satir)
        {
            if (_videoVar)
            {
                _oynatici.Ara(satir.Segment.Bas);
            }
            else
            {
                ZamanKaydiraci.Value = Math.Clamp(satir.Segment.Bas, 0, ZamanKaydiraci.Maximum);
                ZamanMetni.Text = $"{ZamanBicim.Bicimle(satir.Segment.Bas)} / {ZamanBicim.Bicimle(ZamanKaydiraci.Maximum)}";
            }
        }
    }

    /// <summary>
    /// Tam ekran moduna geçiş veya tam ekrandan çıkış.
    /// </summary>
    private void TamEkran_Click(object sender, RoutedEventArgs e)
    {
        TamEkranGecis();
    }

    private void TamEkranGecis()
    {
        if (_tamEkran)
        {
            TamEkrandanCik();
        }
        else
        {
            TamEkranModunaGec();
        }
    }

    private void TamEkranModunaGec()
    {
        if (_tamEkran) return;
        _tamEkran = true;

        _oncekiWindowState = WindowState;
        _oncekiWindowStyle = WindowStyle;
        _oncekiResizeMode = ResizeMode;

        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;
        ResizeMode = ResizeMode.NoResize;

        // Transkript paneli ve bölücüyü gizle
        GridSplitterBolucu.Visibility = Visibility.Collapsed;
        TranskriptPaneli.Visibility = Visibility.Collapsed;

        TamEkranDugmesi.Content = "🗗 Pencere";

        // Tam ekranda efektif metinden geçici .srt üretip Media.AddSlave ile ver
        AltyaziyiTamEkranaYukle();
    }

    private void TamEkrandanCik()
    {
        if (!_tamEkran) return;
        _tamEkran = false;

        WindowStyle = _oncekiWindowStyle;
        WindowState = _oncekiWindowState == WindowState.Maximized ? WindowState.Normal : _oncekiWindowState;
        ResizeMode = _oncekiResizeMode;

        GridSplitterBolucu.Visibility = Visibility.Visible;
        TranskriptPaneli.Visibility = Visibility.Visible;

        TamEkranDugmesi.Content = "⛶ Tam Ekran";

        // Pencere modunda altyazı video üstüne çizilmez
        try
        {
            _oynatici.MediaPlayer.SetSpu(-1);
        }
        catch
        {
            // Yut
        }
    }

    /// <summary>
    /// Tam ekranda efektif metinden geçici .srt üretip Media.AddSlave ile verir.
    /// </summary>
    private void AltyaziyiTamEkranaYukle()
    {
        try
        {
            if (_oynatici.Medya != null && _efektifSegmentler.Count > 0)
            {
                if (string.IsNullOrEmpty(_geciciSrtYolu) || !File.Exists(_geciciSrtYolu))
                {
                    _geciciSrtYolu = SrtUretici.GeciciSrtDosyasiOlustur(_efektifSegmentler);
                    var srtUri = new Uri(_geciciSrtYolu).AbsoluteUri;
                    _oynatici.Medya.AddSlave(MediaSlaveType.Subtitle, 1, srtUri);
                }

                _oynatici.MediaPlayer.SetSpu(1);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Altyazı ekleme hatası: {ex.Message}");
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            TamEkranGecis();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (_tamEkran)
            {
                TamEkrandanCik();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Space)
        {
            if (!TranskriptAramaKutusu.IsFocused)
            {
                OynatDuraklat();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Left)
        {
            if (!TranskriptAramaKutusu.IsFocused)
            {
                ZamanAtla(-10.0);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Right)
        {
            if (!TranskriptAramaKutusu.IsFocused)
            {
                ZamanAtla(10.0);
                e.Handled = true;
            }
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_kapatildi) return;
        _kapatildi = true;

        _zamanlayici.Stop();

        try
        {
            VideoGosterici.MediaPlayer = null;
        }
        catch
        {
            // Yut
        }

        _oynatici.Dispose();
        GeciciSrtTemizle();
    }

    private void GeciciSrtTemizle()
    {
        try
        {
            if (!string.IsNullOrEmpty(_geciciSrtYolu) && File.Exists(_geciciSrtYolu))
            {
                File.Delete(_geciciSrtYolu);
                _geciciSrtYolu = null;
            }
        }
        catch
        {
            // Temizlik hatasını yut
        }
    }
}

/// <summary>
/// Transkript listesinde gösterilen satır görünüm modeli.
/// </summary>
public sealed class TranskriptSatirGorunumu : INotifyPropertyChanged
{
    private bool _vurgulu;

    public EfektifSegment Segment { get; }
    public string ZamanMetni => ZamanBicim.Bicimle(Segment.Bas);
    public string Metin => Segment.Metin;
    public bool Cakisma => Segment.Cakisma;

    public bool Vurgulu
    {
        get => _vurgulu;
        set
        {
            if (_vurgulu != value)
            {
                _vurgulu = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Vurgulu)));
            }
        }
    }

    public TranskriptSatirGorunumu(EfektifSegment segment)
    {
        Segment = segment;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
