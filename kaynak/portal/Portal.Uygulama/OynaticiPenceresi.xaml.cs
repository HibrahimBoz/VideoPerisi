using System.Windows;
using Portal.Cekirdek;

namespace Portal.Uygulama;

/// <summary>
/// Tek video oynatıcı penceresi (T11B'de tam gövdesi yazılacak).
/// </summary>
public partial class OynaticiPenceresi : Window
{
    public DersKaydi? Kayit { get; }
    public double BaslangicSn { get; }

    public OynaticiPenceresi()
        : this(new DersKaydi(string.Empty, string.Empty, string.Empty, null), 0)
    {
    }

    public OynaticiPenceresi(DersKaydi kayit, double baslangicSn = 0)
    {
        Kayit = kayit;
        BaslangicSn = baslangicSn;
        InitializeComponent();
        Title = kayit != null ? $"Oynatıcı - {kayit.Ad}" : "Oynatıcı";
    }
}
