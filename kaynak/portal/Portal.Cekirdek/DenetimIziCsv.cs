namespace Portal.Cekirdek;

using System.Globalization;
using System.Text;

/// <summary>
/// Düzeltme denetim izi (audit trail) kayıtlarını RFC 4180 uyumlu CSV formatına aktaran saf yardımcı sınıf.
/// </summary>
public static class DenetimIziCsv
{
    public const string BaslikSatiri = "kim,zaman,ders,segment,eski,yeni";

    /// <summary>
    /// Tek bir denetim kaydını RFC 4180 uyumlu CSV satırına dönüştürür (kim, zaman, ders, segment, eski, yeni).
    /// </summary>
    public static string SatirUret(string? kim, string? zaman, string? ders, int segment, string? eski, string? yeni)
    {
        return $"{AlaniKacisla(kim)},{AlaniKacisla(zaman)},{AlaniKacisla(ders)},{segment},{AlaniKacisla(eski)},{AlaniKacisla(yeni)}";
    }

    /// <summary>
    /// Bir Duzeltme nesnesinden CSV satırı üretir.
    /// </summary>
    public static string SatirUret(Duzeltme duzeltme)
    {
        ArgumentNullException.ThrowIfNull(duzeltme);
        string zamanStr = duzeltme.ZamanUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        return SatirUret(duzeltme.Kullanici, zamanStr, duzeltme.Ders, duzeltme.SegmentNo, duzeltme.Eski, duzeltme.Yeni);
    }

    /// <summary>
    /// Verilen düzeltme listesini başlık satırıyla birlikte CSV metnine dönüştürür.
    /// </summary>
    public static string Uret(IEnumerable<Duzeltme>? duzeltmeler)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BaslikSatiri);

        if (duzeltmeler == null)
        {
            return sb.ToString();
        }

        foreach (var d in duzeltmeler)
        {
            if (d != null)
            {
                sb.AppendLine(SatirUret(d));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Ham demet kayıt listesinden başlık satırıyla birlikte CSV metni üretir.
    /// </summary>
    public static string Uret(IEnumerable<(string? kim, string? zaman, string? ders, int segment, string? eski, string? yeni)>? kayitlar)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BaslikSatiri);

        if (kayitlar == null)
        {
            return sb.ToString();
        }

        foreach (var k in kayitlar)
        {
            sb.AppendLine(SatirUret(k.kim, k.zaman, k.ders, k.segment, k.eski, k.yeni));
        }

        return sb.ToString();
    }

    /// <summary>
    /// RFC 4180 kurallarına göre CSV alanını biçimlendirir ve gerekliyse çift tırnak içine alır.
    /// Excel formül enjeksiyonunu engellemek için '=', '+', '-', '@' ile başlayan alanların başına tek tırnak eklenir.
    /// </summary>
    public static string AlaniKacisla(string? deger)
    {
        if (string.IsNullOrEmpty(deger))
        {
            return string.Empty;
        }

        string islenecek = deger;
        if (FormulKarakteriIleBasliyorMu(islenecek))
        {
            islenecek = "'" + islenecek;
        }

        bool kacisGerekli = islenecek.Contains(',') ||
                            islenecek.Contains('"') ||
                            islenecek.Contains('\n') ||
                            islenecek.Contains('\r');

        if (!kacisGerekli)
        {
            return islenecek;
        }

        return "\"" + islenecek.Replace("\"", "\"\"") + "\"";
    }

    private static bool FormulKarakteriIleBasliyorMu(string metin)
    {
        if (string.IsNullOrEmpty(metin))
        {
            return false;
        }

        char ilk = metin[0];
        if (ilk == '=' || ilk == '+' || ilk == '-' || ilk == '@')
        {
            return true;
        }

        string kirpilmis = metin.TrimStart();
        if (kirpilmis.Length > 0)
        {
            char kIlk = kirpilmis[0];
            if (kIlk == '=' || kIlk == '+' || kIlk == '-' || kIlk == '@')
            {
                return true;
            }
        }

        return false;
    }
}
