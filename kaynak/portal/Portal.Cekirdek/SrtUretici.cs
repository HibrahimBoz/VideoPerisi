namespace Portal.Cekirdek;

using System.Globalization;
using System.Text;

/// <summary>
/// Geçici SRT altyazı dosyası ve metni üreten saf yardımcı sınıf.
/// LibVLC altyazısı ve tam ekran modunda airspace kısıtını aşmak için kullanılır.
/// </summary>
public static class SrtUretici
{
    /// <summary>
    /// Saniye cinsinden zamanı standart SRT zaman damgası biçimine ("hh:mm:ss,fff") dönüştürür.
    /// </summary>
    public static string ZamanDamgasi(double saniye)
    {
        if (double.IsNaN(saniye) || double.IsInfinity(saniye) || saniye < 0)
        {
            saniye = 0;
        }

        long toplamMs = (long)Math.Round(saniye * 1000.0);
        long saat = toplamMs / 3_600_000;
        long dakika = (toplamMs % 3_600_000) / 60_000;
        long san = (toplamMs % 60_000) / 1000;
        long ms = toplamMs % 1000;

        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2},{3:D3}", saat, dakika, san, ms);
    }

    /// <summary>
    /// Segment listesinden geçerli bir SRT altyazı metni üretir.
    /// </summary>
    public static string Uret(IEnumerable<Segment>? segmentler)
    {
        if (segmentler == null) return string.Empty;
        var araliklar = segmentler.Select(s => (s.Bas, s.Son, s.Metin));
        return Uret(araliklar);
    }

    /// <summary>
    /// Efektif segment listesinden SRT altyazı metni üretir.
    /// </summary>
    public static string Uret(IEnumerable<EfektifSegment>? segmentler)
    {
        if (segmentler == null) return string.Empty;
        var araliklar = segmentler.Select(s => (s.Bas, s.Son, s.Metin));
        return Uret(araliklar);
    }

    /// <summary>
    /// Aralık demetlerinden (Bas, Son, Metin) SRT altyazı metni üretir.
    /// </summary>
    public static string Uret(IEnumerable<(double Bas, double Son, string Metin)>? araliklar)
    {
        if (araliklar == null) return string.Empty;

        var sb = new StringBuilder();
        int sira = 1;

        foreach (var (bas, son, metin) in araliklar)
        {
            if (string.IsNullOrWhiteSpace(metin)) continue;
            double duzgunSon = Math.Max(bas, son);

            sb.AppendLine(sira.ToString(CultureInfo.InvariantCulture));
            sb.Append(ZamanDamgasi(bas));
            sb.Append(" --> ");
            sb.AppendLine(ZamanDamgasi(duzgunSon));
            sb.AppendLine(metin.Trim());
            sb.AppendLine();
            sira++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Efektif segmentleri geçici bir .srt dosyasına yazar ve oluşturulan dosyanın yolunu döner.
    /// </summary>
    public static string GeciciSrtDosyasiOlustur(IEnumerable<EfektifSegment> segmentler)
    {
        string dosyaYolu = Path.Combine(Path.GetTempPath(), $"altyazi_{Guid.NewGuid():N}.srt");
        string icerik = Uret(segmentler);
        File.WriteAllText(dosyaYolu, icerik, Encoding.UTF8);
        return dosyaYolu;
    }

    /// <summary>
    /// Segmentleri belirtilen dosya yoluna SRT biçiminde yazar.
    /// </summary>
    public static void DosyayaYaz(string dosyaYolu, IEnumerable<Segment> segmentler, Encoding? encoding = null)
    {
        string icerik = Uret(segmentler);
        File.WriteAllText(dosyaYolu, icerik, encoding ?? Encoding.UTF8);
    }
}
