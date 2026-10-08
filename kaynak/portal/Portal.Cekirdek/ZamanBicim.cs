namespace Portal.Cekirdek;

/// <summary>
/// Zaman biçimlendirme ve çözümleme yardımcı sınıfı.
/// Saniye cinsinden zamanı "m:ss" veya "h:mm:ss" metnine dönüştürür.
/// </summary>
public static class ZamanBicim
{
    /// <summary>
    /// Saniyeyi "m:ss" veya saat varsa "h:mm:ss" biçimine dönüştürür.
    /// sifirDoldur true ise "mm:ss" veya "hh:mm:ss" biçimine dönüştürür.
    /// </summary>
    public static string Bicimle(double saniye, bool sifirDoldur = false)
    {
        if (double.IsNaN(saniye) || double.IsInfinity(saniye) || saniye < 0)
        {
            saniye = 0;
        }

        long toplamSaniye = (long)Math.Round(saniye);
        long saat = toplamSaniye / 3600;
        long dakika = (toplamSaniye % 3600) / 60;
        long san = toplamSaniye % 60;

        if (saat > 0)
        {
            return sifirDoldur
                ? $"{saat:D2}:{dakika:D2}:{san:D2}"
                : $"{saat}:{dakika:D2}:{san:D2}";
        }

        return sifirDoldur
            ? $"{dakika:D2}:{san:D2}"
            : $"{dakika}:{san:D2}";
    }

    /// <summary>
    /// Saniyeyi her zaman iki basamaklı dakika ("mm:ss" veya "hh:mm:ss") biçiminde döndürür.
    /// </summary>
    public static string BicimleMmSs(double saniye) => Bicimle(saniye, sifirDoldur: true);

    /// <summary>
    /// "m:ss", "mm:ss", "h:mm:ss", "hh:mm:ss" biçimindeki metinleri saniyeye dönüştürür.
    /// </summary>
    public static bool Cozumle(string? metin, out double saniye)
    {
        saniye = 0;
        if (string.IsNullOrWhiteSpace(metin)) return false;

        string[] parcalar = metin.Trim().Split(':');
        if (parcalar.Length == 2)
        {
            if (int.TryParse(parcalar[0], out int d) &&
                double.TryParse(parcalar[1], System.Globalization.CultureInfo.InvariantCulture, out double s))
            {
                if (d >= 0 && s >= 0 && s < 60)
                {
                    saniye = d * 60 + s;
                    return true;
                }
            }
        }
        else if (parcalar.Length == 3)
        {
            if (int.TryParse(parcalar[0], out int h) &&
                int.TryParse(parcalar[1], out int d) &&
                double.TryParse(parcalar[2], System.Globalization.CultureInfo.InvariantCulture, out double s))
            {
                if (h >= 0 && d >= 0 && d < 60 && s >= 0 && s < 60)
                {
                    saniye = h * 3600 + d * 60 + s;
                    return true;
                }
            }
        }

        return false;
    }
}
