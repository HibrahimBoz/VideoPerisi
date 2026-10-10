using System.Windows;

namespace Portal.Uygulama.Temalar;

/// <summary>
/// PencereKabugu ResourceDictionary arka plan sınıfı.
/// Özel başlık çubuğu pencere kontrol düğmelerinin (küçült, büyüt/eski boyut, kapat) tıklama olaylarını yönetir.
/// </summary>
public partial class PencereKabugu : ResourceDictionary
{
    public PencereKabugu()
    {
        InitializeComponent();
    }

    private void SimgeDurumunaAl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject obj && Window.GetWindow(obj) is Window win)
        {
            SystemCommands.MinimizeWindow(win);
        }
    }

    private void BuyutGeriYukle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject obj && Window.GetWindow(obj) is Window win)
        {
            if (win.WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(win);
            }
            else
            {
                SystemCommands.MaximizeWindow(win);
            }
        }
    }

    private void Kapat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject obj && Window.GetWindow(obj) is Window win)
        {
            SystemCommands.CloseWindow(win);
        }
    }
}
