namespace Portal.Cekirdek;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// ISO-8601 UTC tarih-zaman JSON dönüştürücüsü.
/// </summary>
public sealed class IsoUtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? str = reader.GetString();
            if (!string.IsNullOrEmpty(str) && DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            {
                return dt;
            }
        }
        return reader.GetDateTime().ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Tek bir segment düzeltme veya geri alma kaydını temsil eder.
/// </summary>
public class Duzeltme
{
    [JsonPropertyName("sema")]
    public int Sema { get; init; } = 1;

    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString("D");

    [JsonPropertyName("tur")]
    public string Tur { get; init; } = "duzelt";

    [JsonPropertyName("ders")]
    public string Ders { get; init; } = string.Empty;

    [JsonPropertyName("kaynak_ozet")]
    public string? KaynakOzet { get; init; }

    [JsonPropertyName("segment_no")]
    public int SegmentNo { get; init; }

    [JsonPropertyName("bas")]
    public double? Bas { get; init; }

    [JsonPropertyName("eski")]
    public string Eski { get; init; } = string.Empty;

    [JsonPropertyName("yeni")]
    public string Yeni { get; init; } = string.Empty;

    [JsonPropertyName("onceki_id")]
    public string? OncekiId { get; init; }

    [JsonPropertyName("geri_al_hedef")]
    public string? GeriAlHedef { get; init; }

    [JsonPropertyName("kullanici")]
    public string Kullanici { get; init; } = string.Empty;

    [JsonPropertyName("makine")]
    public string Makine { get; init; } = string.Empty;

    [JsonPropertyName("zaman_utc")]
    [JsonConverter(typeof(IsoUtcDateTimeConverter))]
    public DateTime ZamanUtc { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("istemci")]
    public string Istemci { get; init; } = "1.0.0";

    /// <summary>
    /// Belirtilen hedef düzeltmeyi geri almak için yeni bir geri_al kaydı üretir.
    /// oncekiId verilirse zincir ucuna eklenir; verilmezse hedefin kimliği kullanılır.
    /// </summary>
    public static Duzeltme GeriAlOlustur(Duzeltme hedef, IKimlik? kimlik = null, string istemci = "1.0.0", string? oncekiId = null)
    {
        if (hedef == null)
        {
            throw new ArgumentNullException(nameof(hedef));
        }

        var k = kimlik ?? Kimlik.Varsayilan;
        return new Duzeltme
        {
            Id = Guid.NewGuid().ToString("D"),
            Tur = "geri_al",
            Ders = hedef.Ders,
            KaynakOzet = hedef.KaynakOzet,
            SegmentNo = hedef.SegmentNo,
            Bas = hedef.Bas,
            Eski = hedef.Yeni,
            Yeni = hedef.Eski,
            OncekiId = string.IsNullOrEmpty(oncekiId) ? hedef.Id : oncekiId,
            GeriAlHedef = hedef.Id,
            Kullanici = k.KullaniciAdi(),
            Makine = k.MakineAdi(),
            ZamanUtc = DateTime.UtcNow,
            Istemci = istemci
        };
    }

    /// <summary>
    /// Geri alınmamış son içerik kaydı (duzelt / cozum). geri_al kayıtları ve onların hedefleri atlanır.
    /// </summary>
    public static Duzeltme? GeriAlinacakHedef(IReadOnlyList<Duzeltme>? gecmis)
    {
        if (gecmis == null || gecmis.Count == 0)
        {
            return null;
        }

        var sirali = gecmis
            .OrderBy(d => d.ZamanUtc)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        var geriAlinanlar = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in sirali)
        {
            if (string.Equals(d.Tur, "geri_al", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(d.GeriAlHedef))
            {
                geriAlinanlar.Add(d.GeriAlHedef);
            }
        }

        Duzeltme? hedef = null;
        foreach (var d in sirali)
        {
            if (string.Equals(d.Tur, "geri_al", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(d.Id) || geriAlinanlar.Contains(d.Id))
            {
                continue;
            }

            hedef = d;
        }

        return hedef;
    }

    /// <summary>
    /// Segment üzerinde geri alma işlemine izin verilip verilmediğini döner.
    /// Çakışma durumunda (Cakisma == true) veya geri alınacak hedef yoksa false döner.
    /// </summary>
    public static bool GeriAlIzinVerilir(EfektifSegment? segment)
    {
        if (segment == null || segment.Cakisma || segment.Gecmis == null || segment.Gecmis.Count == 0)
        {
            return false;
        }

        return GeriAlinacakHedef(segment.Gecmis) != null;
    }

    /// <summary>
    /// Geçmiş listesi ve çakışma durumuna göre geri alma işlemine izin verilip verilmediğini döner.
    /// </summary>
    public static bool GeriAlIzinVerilir(IReadOnlyList<Duzeltme>? gecmis, bool cakisma = false)
    {
        if (cakisma || gecmis == null || gecmis.Count == 0)
        {
            return false;
        }

        return GeriAlinacakHedef(gecmis) != null;
    }

    /// <summary>
    /// Efektif segmente göre geri alma kaydı üretir. Çakışma varsa null döner.
    /// </summary>
    public static Duzeltme? GeriAlKaydiUret(EfektifSegment? segment, IKimlik? kimlik = null, string istemci = "1.0.0")
    {
        if (segment == null || segment.Cakisma)
        {
            return null;
        }

        return GeriAlKaydiUret(segment.Gecmis, kimlik, istemci);
    }

    /// <summary>
    /// Çakışma bayrağı dikkate alınarak geri alma kaydı üretir. Çakışma varsa null döner.
    /// </summary>
    public static Duzeltme? GeriAlKaydiUret(IReadOnlyList<Duzeltme>? gecmis, bool cakisma, IKimlik? kimlik = null, string istemci = "1.0.0")
    {
        if (cakisma)
        {
            return null;
        }

        return GeriAlKaydiUret(gecmis, kimlik, istemci);
    }

    /// <summary>
    /// Geçmişteki geri alınmamış son düzeltmeyi bulup, zincir ucuna bağlanan yeni bir geri_al kaydı üretir.
    /// Geri alınacak kayıt yoksa null döner.
    /// </summary>
    public static Duzeltme? GeriAlKaydiUret(IReadOnlyList<Duzeltme>? gecmis, IKimlik? kimlik = null, string istemci = "1.0.0")
    {
        var hedef = GeriAlinacakHedef(gecmis);
        if (hedef == null)
        {
            return null;
        }

        string? zincirUcu = gecmis != null && gecmis.Count > 0 ? gecmis[gecmis.Count - 1].Id : hedef.Id;
        return GeriAlOlustur(hedef, kimlik, istemci, oncekiId: zincirUcu);
    }

    /// <summary>
    /// Segment için yeni bir düzeltme kaydı veya çakışma varsa dalları birleştiren çözüm kaydı üretir.
    /// </summary>
    public static Duzeltme DuzenlemeOlustur(
        string dersAdi,
        int segmentNo,
        string eskiMetin,
        string yeniMetin,
        double? bas = null,
        string? kaynakOzet = null,
        IReadOnlyList<Duzeltme>? gecmis = null,
        bool cakisma = false,
        IKimlik? kimlik = null,
        string istemci = "1.0.0")
    {
        var k = kimlik ?? Kimlik.Varsayilan;
        string? oncekiId = gecmis != null && gecmis.Count > 0 ? gecmis[gecmis.Count - 1].Id : null;
        string tur = cakisma ? "cozum" : "duzelt";

        return new Duzeltme
        {
            Id = Guid.NewGuid().ToString("D"),
            Tur = tur,
            Ders = dersAdi,
            KaynakOzet = kaynakOzet,
            SegmentNo = segmentNo,
            Bas = bas,
            Eski = eskiMetin,
            Yeni = yeniMetin,
            OncekiId = oncekiId,
            Kullanici = k.KullaniciAdi(),
            Makine = k.MakineAdi(),
            ZamanUtc = DateTime.UtcNow,
            Istemci = istemci
        };
    }

    /// <summary>
    /// Efektif segmente göre yeni bir düzeltme veya çakışma durumunda çözüm kaydı üretir.
    /// </summary>
    public static Duzeltme DuzenlemeOlustur(
        EfektifSegment segment,
        string dersAdi,
        string yeniMetin,
        string? kaynakOzet = null,
        IKimlik? kimlik = null,
        string istemci = "1.0.0")
    {
        if (segment == null)
        {
            throw new ArgumentNullException(nameof(segment));
        }

        return DuzenlemeOlustur(
            dersAdi: dersAdi,
            segmentNo: segment.No,
            eskiMetin: segment.Metin,
            yeniMetin: yeniMetin,
            bas: segment.Bas,
            kaynakOzet: kaynakOzet,
            gecmis: segment.Gecmis,
            cakisma: segment.Cakisma,
            kimlik: kimlik,
            istemci: istemci);
    }
}
