namespace Portal.Cekirdek;

/// <summary>
/// libvlc'ye verilecek dosya yolunu hazırlar.
/// Ölçüldü (libvlc 3.0.24): "\\sunucu\paylasim\x.mp4" biçimi açılmaz (MRL bozulur);
/// "//sunucu/paylasim/x.mp4" biçimi açılır.
/// </summary>
public static class VlcYolu
{
    /// <summary>UNC yolunu (\\sunucu\...) eğik çizgili biçime çevirir; diğer yolları değiştirmez.</summary>
    public static string Hazirla(string yol)
    {
        if (string.IsNullOrEmpty(yol))
        {
            return yol;
        }

        bool unc = yol.StartsWith(@"\\", StringComparison.Ordinal) || yol.StartsWith("//", StringComparison.Ordinal);
        return unc ? yol.Replace('\\', '/') : yol;
    }
}
