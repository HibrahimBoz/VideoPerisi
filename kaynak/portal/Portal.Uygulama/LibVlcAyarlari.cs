namespace Portal.Uygulama;

using System.IO;
using Portal.Cekirdek;
using System.Text.Json;
using LibVLCSharp.Shared;

public static class LibVlcAyarlari
{


    private static readonly object BaslatmaKilidi = new();
    private static bool _coreBaslatildi;

    public static LibVLC Olustur()
    {
        CoreBaslat();
        string[] secenekler = SecenekleriBirlestir(AyarDosyasindanOku());
        try
        {
            return new LibVLC(secenekler);
        }
        catch
        {
            return new LibVLC(DonanimsizSecenekler(secenekler));
        }
    }

    public static string[] SecenekleriBirlestir(IEnumerable<string>? ek) => VlcSecenekleri.Birlestir(ek);

    private static IEnumerable<string>? AyarDosyasindanOku()
    {
        try
        {
            string yol = Path.Combine(AppContext.BaseDirectory, "ayar.json");
            if (!File.Exists(yol))
            {
                return null;
            }

            using JsonDocument belge = JsonDocument.Parse(File.ReadAllText(yol));
            if (!belge.RootElement.TryGetProperty("vlcSecenekleri", out JsonElement dizi) ||
                dizi.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return dizi.EnumerateArray()
                .Where(oge => oge.ValueKind == JsonValueKind.String)
                .Select(oge => oge.GetString()!)
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static string[] DonanimsizSecenekler(IEnumerable<string> secenekler)
    {
        return secenekler
            .Select(secenek => secenek.StartsWith("--avcodec-hw=", StringComparison.Ordinal)
                ? "--avcodec-hw=none"
                : secenek)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void CoreBaslat()
    {
        lock (BaslatmaKilidi)
        {
            if (_coreBaslatildi)
            {
                return;
            }

            Core.Initialize();
            _coreBaslatildi = true;
        }
    }
}
