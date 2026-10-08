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
    /// </summary>
    public static Duzeltme GeriAlOlustur(Duzeltme hedef, IKimlik? kimlik = null, string istemci = "1.0.0")
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
            OncekiId = hedef.Id,
            GeriAlHedef = hedef.Id,
            Kullanici = k.KullaniciAdi(),
            Makine = k.MakineAdi(),
            ZamanUtc = DateTime.UtcNow,
            Istemci = istemci
        };
    }
}
