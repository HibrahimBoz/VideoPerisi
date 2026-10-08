using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Portal.Cekirdek;

namespace Portal.Uygulama;

/// <summary>
/// Arama penceresi (MainWindow): ders listesi, 250 ms gecikmeli arama, sonuç listesi ve oynatıcı açma.
/// </summary>
public partial class AramaPenceresi : Window
{
    private readonly string _kokKlasor;
    private readonly IAramaMotoru _aramaMotoru;
    private readonly DispatcherTimer _aramaZamanlayici;
    private readonly ObservableCollection<DersKaydi> _dersler = [];
    private readonly ObservableCollection<SonucGorunumu> _sonuclar = [];

    public AramaPenceresi()
        : this(Directory.GetCurrentDirectory())
    {
    }

    public AramaPenceresi(string kokKlasor)
    {
        InitializeComponent();
        _kokKlasor = string.IsNullOrWhiteSpace(kokKlasor) ? Directory.GetCurrentDirectory() : kokKlasor;
        _aramaMotoru = new MetinAramaMotoru(new IndeksOnbellek(), new DuzeltmeDeposu(_kokKlasor));

        DersListesi.ItemsSource = _dersler;
        SonucListesi.ItemsSource = _sonuclar;

        DurumKullanici.Text = $"Kullanıcı: {Kimlik.Varsayilan.KullaniciAdi()}";
        DurumBilgisi.Text = "Hazır";

        _aramaZamanlayici = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _aramaZamanlayici.Tick += AramaZamanlayici_Tick;
    }

    private async void Pencere_Loaded(object sender, RoutedEventArgs e)
    {
        await IndeksleAsync();
    }

    private async Task IndeksleAsync()
    {
        IlerlemeCubugu.Visibility = Visibility.Visible;
        IlerlemeCubugu.Value = 0;
        DurumBilgisi.Text = "Dersler taranıyor...";

        try
        {
            var bulunanlar = await Task.Run(() => Tarayici.Tara(_kokKlasor));
            _dersler.Clear();
            foreach (var ders in bulunanlar)
            {
                _dersler.Add(ders);
            }

            DersListesiBaslik.Text = $"Dersler ({_dersler.Count})";
            DurumDersSayisi.Text = $"Ders: {_dersler.Count}";

            if (_dersler.Count == 0)
            {
                DurumBilgisi.Text = "Kök klasörde ders kaydı bulunamadı.";
                IlerlemeCubugu.Visibility = Visibility.Collapsed;
                return;
            }

            DurumBilgisi.Text = "İndeksleniyor...";
            var ilerleme = new Progress<int>(yuzde =>
            {
                IlerlemeCubugu.Value = yuzde;
                DurumBilgisi.Text = $"İndeksleniyor... %{yuzde}";
            });

            await _aramaMotoru.IndeksleAsync(_dersler, ilerleme);
            DurumBilgisi.Text = "Hazır";
        }
        catch (Exception ex)
        {
            DurumBilgisi.Text = "İndeksleme hatası: " + ex.Message;
        }
        finally
        {
            IlerlemeCubugu.Visibility = Visibility.Collapsed;
        }
    }

    private void AramaKutusu_TextChanged(object sender, TextChangedEventArgs e)
    {
        _aramaZamanlayici.Stop();
        _aramaZamanlayici.Start();
    }

    private void AramaZamanlayici_Tick(object? sender, EventArgs e)
    {
        _aramaZamanlayici.Stop();
        AramayiCalistir();
    }

    private void AramayiCalistir()
    {
        string sorgu = AramaKutusu.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sorgu))
        {
            _sonuclar.Clear();
            SonucSayisiMetni.Text = "Arama Sonuçları";
            return;
        }

        try
        {
            var sonuclar = _aramaMotoru.Ara(sorgu, en_fazla: 100);
            _sonuclar.Clear();
            foreach (var s in sonuclar)
            {
                _sonuclar.Add(new SonucGorunumu(s));
            }
            SonucSayisiMetni.Text = $"Arama Sonuçları ({_sonuclar.Count})";
        }
        catch (Exception ex)
        {
            DurumBilgisi.Text = "Arama hatası: " + ex.Message;
        }
    }

    private void AramaKutusu_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (SonucListesi.Items.Count > 0)
            {
                if (SonucListesi.SelectedIndex < 0)
                {
                    SonucListesi.SelectedIndex = 0;
                }
                SonucListesi.Focus();
                var item = (ListViewItem?)SonucListesi.ItemContainerGenerator.ContainerFromIndex(SonucListesi.SelectedIndex);
                item?.Focus();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            if (SonucListesi.SelectedItem != null)
            {
                SeciliSonucuAc();
                e.Handled = true;
            }
            else if (SonucListesi.Items.Count > 0)
            {
                SonucListesi.SelectedIndex = 0;
                SeciliSonucuAc();
                e.Handled = true;
            }
        }
    }

    private void SonucListesi_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SeciliSonucuAc();
            e.Handled = true;
        }
        else if (e.Key == Key.Up && SonucListesi.SelectedIndex <= 0)
        {
            AramaKutusu.Focus();
            e.Handled = true;
        }
    }

    private void SonucListesi_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SeciliSonucuAc();
    }

    private void DersListesi_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SeciliDersiAc();
            e.Handled = true;
        }
    }

    private void DersListesi_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SeciliDersiAc();
    }

    private void SeciliSonucuAc()
    {
        if (SonucListesi.SelectedItem is not SonucGorunumu oge) return;

        var ders = _dersler.FirstOrDefault(d => string.Equals(d.JsonYolu, oge.Sonuc.JsonYolu, StringComparison.OrdinalIgnoreCase))
                ?? _dersler.FirstOrDefault(d => string.Equals(d.Ad, oge.DersAd, StringComparison.OrdinalIgnoreCase));

        if (ders == null)
        {
            string klasor = Path.GetDirectoryName(oge.Sonuc.JsonYolu) ?? _kokKlasor;
            ders = new DersKaydi(oge.DersAd, klasor, oge.Sonuc.JsonYolu, null);
        }

        double baslangicSn = Math.Max(0, oge.BasSn - 2.0);
        DersPenceresiniAc(ders, baslangicSn);
    }

    private void SeciliDersiAc()
    {
        if (DersListesi.SelectedItem is not DersKaydi ders) return;
        DersPenceresiniAc(ders, 0.0);
    }

    private void DersPenceresiniAc(DersKaydi ders, double baslangicSn)
    {
        try
        {
            string dersJson = Path.Combine(ders.KlasorYolu, "ders.json");
            Window pencere;
            if (File.Exists(dersJson))
            {
                var dersTanimi = DersTanimiOkuyucu.Oku(dersJson);
                pencere = new CokAciliOynaticiPenceresi(dersTanimi, baslangicSn, ders.JsonYolu);
            }
            else
            {
                pencere = new OynaticiPenceresi(ders, baslangicSn);
            }

            pencere.Owner = this;
            pencere.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Ders açılırken hata oluştu:\n" + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_aramaMotoru is IDisposable disp)
        {
            disp.Dispose();
        }
    }
}

/// <summary>
/// Arama sonucu satırının görünüm modeli.
/// </summary>
public sealed class SonucGorunumu(AramaSonucu sonuc)
{
    public AramaSonucu Sonuc { get; } = sonuc;
    public string DersAd => Sonuc.DersAd;
    public double BasSn => Sonuc.BasSn;
    public string ZamanMetni => ZamanBicim.Bicimle(Sonuc.BasSn);
    public string Metin => Sonuc.Metin;
    public (int Bas, int Uzunluk)[] Vurgular => Sonuc.Vurgular;
}

/// <summary>
/// Arama sonuçlarında eşleşen kelimeleri sarı kalın vurguyla gösteren bağlı özellik yardımcısı.
/// </summary>
public static class VurguYardimcisi
{
    public static readonly DependencyProperty VurguBilgisiProperty =
        DependencyProperty.RegisterAttached(
            "VurguBilgisi",
            typeof(SonucGorunumu),
            typeof(VurguYardimcisi),
            new PropertyMetadata(null, OnVurguBilgisiChanged));

    public static SonucGorunumu? GetVurguBilgisi(DependencyObject obj) =>
        (SonucGorunumu?)obj.GetValue(VurguBilgisiProperty);

    public static void SetVurguBilgisi(DependencyObject obj, SonucGorunumu? value) =>
        obj.SetValue(VurguBilgisiProperty, value);

    private static void OnVurguBilgisiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;

        tb.Inlines.Clear();
        if (e.NewValue is not SonucGorunumu sg) return;

        string metin = sg.Metin;
        var vurgular = sg.Vurgular;

        if (string.IsNullOrEmpty(metin)) return;

        if (vurgular == null || vurgular.Length == 0)
        {
            tb.Inlines.Add(new Run(metin));
            return;
        }

        int mevcut = 0;
        foreach (var (bas, uz) in vurgular)
        {
            if (bas < 0 || uz <= 0 || bas >= metin.Length) continue;

            int duzgunBas = Math.Max(mevcut, bas);
            if (duzgunBas > mevcut)
            {
                tb.Inlines.Add(new Run(metin.Substring(mevcut, duzgunBas - mevcut)));
            }

            int son = Math.Min(bas + uz, metin.Length);
            if (son > duzgunBas)
            {
                var run = new Run(metin.Substring(duzgunBas, son - duzgunBas))
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
                    Background = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0))
                };
                tb.Inlines.Add(run);
                mevcut = son;
            }
            else
            {
                mevcut = Math.Max(mevcut, duzgunBas);
            }
        }

        if (mevcut < metin.Length)
        {
            tb.Inlines.Add(new Run(metin.Substring(mevcut)));
        }
    }
}
