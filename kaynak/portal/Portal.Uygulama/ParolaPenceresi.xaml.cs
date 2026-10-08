namespace Portal.Uygulama;

using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Portal.Cekirdek;

/// <summary>
/// Düzenleme moduna ilk girişte sorulan parola doğrulama penceresi.
/// </summary>
public partial class ParolaPenceresi : Window
{
    private readonly string? _kokKlasor;
    private readonly ParolaGecikmeSayaci? _sayac;

    public string ParolaMetni => ParolaKutusu.Password;

    public ParolaPenceresi()
        : this(null, null)
    {
    }

    public ParolaPenceresi(string? kokKlasor, ParolaGecikmeSayaci? sayac)
    {
        _kokKlasor = string.IsNullOrWhiteSpace(kokKlasor) ? Directory.GetCurrentDirectory() : kokKlasor;
        _sayac = sayac;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Owner == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            ParolaKutusu.Focus();
        };
    }

    private void Tamam_Click(object sender, RoutedEventArgs e)
    {
        if (_sayac != null && _sayac.KilitliMi())
        {
            HataMetni.Text = $"Çok fazla hatalı deneme yapıldı. Lütfen {_sayac.KalanBeklemeSaniyesi()} saniye bekleyin.";
            return;
        }

        string girilen = ParolaKutusu.Password;
        bool gecerli = ParolaDogrulaKok(_kokKlasor ?? Directory.GetCurrentDirectory(), girilen);

        if (!gecerli)
        {
            _sayac?.HataliGiris();
            if (_sayac != null && _sayac.KilitliMi())
            {
                HataMetni.Text = $"Parola hatalı! 5 kez yanlış girildi, lütfen {_sayac.KilitSuresiSaniye} saniye bekleyin.";
            }
            else
            {
                int kalan = _sayac != null ? _sayac.AzamiHataliDeneme - _sayac.HataliDenemeSayisi : 0;
                HataMetni.Text = kalan > 0
                    ? $"Parola hatalı! Kalan deneme hakkı: {kalan}"
                    : "Parola hatalı!";
            }
            ParolaKutusu.SelectAll();
            return;
        }

        _sayac?.BasariliGiris();
        DialogResult = true;
    }

    private void Iptal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ParolaKutusu_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Tamam_Click(sender, e);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Kök klasör ve üst klasörlerinde .portal/parola.json dosyasını doğrular.
    /// </summary>
    public static bool ParolaDogrulaKok(string kokKlasor, string girilenParola)
    {
        if (string.IsNullOrEmpty(girilenParola))
        {
            return false;
        }

        string? dosyaYolu = ParolaDosyasiBul(kokKlasor);
        if (dosyaYolu == null)
        {
            return false;
        }

        string? klasor = Path.GetDirectoryName(dosyaYolu);
        if (klasor != null && Path.GetFileName(klasor).Equals(".portal", StringComparison.OrdinalIgnoreCase))
        {
            string? ust = Path.GetDirectoryName(klasor);
            if (ust != null && Parola.Dogrula(ust, girilenParola))
            {
                return true;
            }
        }

        return Parola.Dogrula(kokKlasor, girilenParola);
    }

    /// <summary>
    /// Kök klasörde veya üst dizinlerinde .portal/parola.json dosyasını arar.
    /// </summary>
    public static string? ParolaDosyasiBul(string? kokKlasor)
    {
        if (string.IsNullOrWhiteSpace(kokKlasor))
        {
            kokKlasor = Directory.GetCurrentDirectory();
        }

        string? mevcut = Path.GetFullPath(kokKlasor);
        while (!string.IsNullOrEmpty(mevcut))
        {
            string aday = Path.Combine(mevcut, ".portal", "parola.json");
            if (File.Exists(aday))
            {
                return aday;
            }

            var ust = Directory.GetParent(mevcut);
            if (ust == null || ust.FullName == mevcut)
            {
                break;
            }
            mevcut = ust.FullName;
        }

        string simdikiPortal = Path.Combine(Directory.GetCurrentDirectory(), ".portal", "parola.json");
        if (File.Exists(simdikiPortal))
        {
            return simdikiPortal;
        }

        return null;
    }

    /// <summary>
    /// Oturum için parola doğrulaması yapar. Parola tanımlı değilse veya yanlışsa kullanıcıyı bilgilendirir.
    /// 5 yanlışta 30 saniye kilit uygular.
    /// </summary>
    public static bool DogrulaVeIzinVer(Window? sahip, string? kokKlasor, ref bool oturumDogrulandi, ParolaGecikmeSayaci sayac)
    {
        if (oturumDogrulandi)
        {
            return true;
        }

        string? parolaDosyasi = ParolaDosyasiBul(kokKlasor);
        if (parolaDosyasi == null)
        {
            if (sahip != null)
            {
                MessageBox.Show(
                    sahip,
                    "Parola tanımlı değil — yöneticiye başvurun",
                    "Düzenleme Kapalı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            else
            {
                MessageBox.Show(
                    "Parola tanımlı değil — yöneticiye başvurun",
                    "Düzenleme Kapalı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            return false;
        }

        if (sayac.KilitliMi())
        {
            string kilitMesaji = $"Çok fazla hatalı deneme yapıldı. Lütfen {sayac.KalanBeklemeSaniyesi()} saniye bekleyin.";
            if (sahip != null)
            {
                MessageBox.Show(
                    sahip,
                    kilitMesaji,
                    "Bekleyin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            else
            {
                MessageBox.Show(
                    kilitMesaji,
                    "Bekleyin",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            return false;
        }

        var dlg = new ParolaPenceresi(kokKlasor, sayac);
        if (sahip != null)
        {
            dlg.Owner = sahip;
        }

        bool? sonuc = dlg.ShowDialog();
        if (sonuc == true)
        {
            oturumDogrulandi = true;
            return true;
        }

        return false;
    }
}
