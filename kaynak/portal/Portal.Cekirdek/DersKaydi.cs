namespace Portal.Cekirdek;

/// <summary>
/// Taranan ders kaydının konum ve video bilgilerini temsil eder.
/// </summary>
public record DersKaydi(
    string Ad,
    string KlasorYolu,
    string JsonYolu,
    string? VideoYolu
)
{
    /// <summary>
    /// İndeks ve önbellek sözlük anahtarı: JSON dosyasının normalize tam yolu.
    /// </summary>
    public string IndeksAnahtari => YoluNormalizeEt(JsonYolu);

    /// <summary>
    /// Yolu tam ve ayırıcıları tutarlı hale getirir. Boşsa boş döner.
    /// </summary>
    public static string YoluNormalizeEt(string? yol)
    {
        if (string.IsNullOrWhiteSpace(yol))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(yol);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return yol.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }
    }
}
