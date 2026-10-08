using System.IO;
using System.Windows;
using LibVLCSharp.Shared;
using Microsoft.Win32;

namespace Portal.Uygulama;

/// <summary>
/// Video Portalı WPF uygulaması ana giriş sınıfı.
/// </summary>
public partial class App : Application
{
    private static readonly string HataLogYolu = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VideoPortali",
        "hata.log"
    );

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Yakalanmamış istisnalar için genel dinleyiciler
        SetupHataYakali();

        // LibVLCSharp Core.Initialize() uygulama başında bir kez
        try
        {
            Core.Initialize();
        }
        catch (Exception ex)
        {
            HataKaydetVeGoster(ex, "LibVLC başlatılamadı");
        }

        // Komut satırı argümanı: --kok <klasör>
        string? kokKlasor = ParseKokArgumani(e.Args);

        if (string.IsNullOrWhiteSpace(kokKlasor) || !Directory.Exists(kokKlasor))
        {
            // FolderBrowserDialog yerine .NET 8 WPF yerleşik OpenFolderDialog
            var dialog = new OpenFolderDialog
            {
                Title = "Ders videoları kök klasörünü seçin",
                Multiselect = false
            };

            bool? sonuc = dialog.ShowDialog();
            if (sonuc == true && !string.IsNullOrWhiteSpace(dialog.FolderName) && Directory.Exists(dialog.FolderName))
            {
                kokKlasor = dialog.FolderName;
            }
            else
            {
                Shutdown();
                return;
            }
        }

        try
        {
            var anaPencere = new AramaPenceresi(kokKlasor!);
            MainWindow = anaPencere;
            anaPencere.Show();
        }
        catch (Exception ex)
        {
            HataKaydetVeGoster(ex, "Arama penceresi açılamadı");
            Shutdown();
        }
    }

    private static string? ParseKokArgumani(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--kok", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            else if (args[i].StartsWith("--kok=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i].Substring("--kok=".Length).Trim('"', '\'');
            }
        }

        return null;
    }

    private void SetupHataYakali()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                HataKaydetVeGoster(ex, "Kritik uygulama hatası");
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            HataKaydetVeGoster(args.Exception, "Arayüz hatası");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            HataKaydetVeGoster(args.Exception, "Arka plan işlem hatası");
            args.SetObserved();
        };
    }

    private static void HataKaydetVeGoster(Exception ex, string baslik)
    {
        try
        {
            string? dizin = Path.GetDirectoryName(HataLogYolu);
            if (!string.IsNullOrEmpty(dizin))
            {
                Directory.CreateDirectory(dizin);
            }

            string logIcerigi = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{baslik}] {ex}\n\n";
            File.AppendAllText(HataLogYolu, logIcerigi);
        }
        catch
        {
            // Günlük yazma hatasını yut
        }

        try
        {
            MessageBox.Show(
                $"Bir hata oluştu:\n{ex.Message}\n\nDetaylar hata günlüğüne kaydedildi:\n{HataLogYolu}",
                "Hata - Video Portalı",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        catch
        {
            // İleti kutusu gösterilemezse yut
        }
    }
}
