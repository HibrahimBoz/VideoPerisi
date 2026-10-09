namespace Portal.Cekirdek;

/// <summary>LibVLC başlatma seçeneklerini birleştiren saf mantık (test edilebilir).</summary>
public static class VlcSecenekleri
{
    public static readonly string[] Varsayilan =
    [
        "--avcodec-hw=any",
        "--no-video-title-show",
        "--no-snapshot-preview",
        "--no-osd",
        "--file-caching=1500",
        "--network-caching=1500",
        "--drop-late-frames",
        "--skip-frames",
        "--sub-autodetect-file"
    ];

    public static string[] Birlestir(IEnumerable<string>? ek)
    {
        return Varsayilan
            .Concat(ek ?? Array.Empty<string>())
            .Where(secenek => !string.IsNullOrWhiteSpace(secenek))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
