namespace Portal.Cekirdek;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Parola JSON dosyasındaki alanları temsil eder.
/// </summary>
public class ParolaKaydi
{
    [JsonPropertyName("alg")]
    public string Alg { get; set; } = "PBKDF2-SHA256";

    [JsonPropertyName("iter")]
    public int Iter { get; set; } = 600000;

    [JsonPropertyName("tuz")]
    public string Tuz { get; set; } = string.Empty;

    [JsonPropertyName("ozet")]
    public string Ozet { get; set; } = string.Empty;

    [JsonPropertyName("degistiren")]
    public string Degistiren { get; set; } = string.Empty;

    [JsonPropertyName("zaman")]
    public string Zaman { get; set; } = string.Empty;
}

/// <summary>
/// Güvenli parola saklama, doğrulama ve değiştirme işlemlerini yönetir.
/// </summary>
public class Parola
{
    public const string StandartAlgoritma = "PBKDF2-SHA256";
    public const int StandartIterasyon = 600000;
    public const int EnAzParolaUzunlugu = 8;
    public const int TuzUzunluguBayt = 16;
    public const int OzetUzunluguBayt = 32;

    private readonly string _kokKlasor;
    private readonly IKimlik _kimlik;

    public Parola(string? kokKlasor = null, IKimlik? kimlik = null)
    {
        _kokKlasor = string.IsNullOrWhiteSpace(kokKlasor) ? Directory.GetCurrentDirectory() : kokKlasor;
        _kimlik = kimlik ?? Kimlik.Varsayilan;
    }

    public string ParolaDosyaYolu => Path.Combine(_kokKlasor, ".portal", "parola.json");
    public string ArsivKlasorYolu => Path.Combine(_kokKlasor, ".portal", "parola_arsiv");

    /// <summary>
    /// Yeni parola oluşturup .portal/parola.json dosyasına kaydeder.
    /// Mevcut parola dosyası varsa reddeder (değiştirme yalnızca Degistir ile yapılmalıdır).
    /// </summary>
    public bool Olustur(string parola, string? degistiren = null)
    {
        if (string.IsNullOrEmpty(parola) || parola.Length < EnAzParolaUzunlugu)
        {
            return false;
        }

        if (File.Exists(ParolaDosyaYolu))
        {
            return false;
        }

        string portalKlasor = Path.Combine(_kokKlasor, ".portal");
        string tmpDosya = Path.Combine(portalKlasor, $"parola_{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(portalKlasor);

            if (File.Exists(ParolaDosyaYolu))
            {
                return false;
            }

            byte[] tuz = RandomNumberGenerator.GetBytes(TuzUzunluguBayt);
            byte[] ozet = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(parola),
                tuz,
                StandartIterasyon,
                HashAlgorithmName.SHA256,
                OzetUzunluguBayt);

            var kayit = new ParolaKaydi
            {
                Alg = StandartAlgoritma,
                Iter = StandartIterasyon,
                Tuz = Convert.ToBase64String(tuz),
                Ozet = Convert.ToBase64String(ozet),
                Degistiren = degistiren ?? _kimlik.KullaniciAdi(),
                Zaman = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
            };

            string json = JsonSerializer.Serialize(kayit);

            using (var fs = new FileStream(tmpDosya, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            File.Move(tmpDosya, ParolaDosyaYolu, overwrite: false);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(tmpDosya))
                {
                    File.Delete(tmpDosya);
                }
            }
            catch
            {
                // Temizlik istisnasını yut
            }
            return false;
        }
    }

    /// <summary>
    /// Verilen parolayı .portal/parola.json ile karşılaştırır. İstisna fırlatmaz.
    /// iter &lt; 600000 olan dosyaları reddeder.
    /// </summary>
    public bool Dogrula(string parola)
    {
        if (string.IsNullOrEmpty(parola))
        {
            return false;
        }

        try
        {
            if (!File.Exists(ParolaDosyaYolu))
            {
                return false;
            }

            string json = File.ReadAllText(ParolaDosyaYolu, Encoding.UTF8);
            var kayit = JsonSerializer.Deserialize<ParolaKaydi>(json);
            if (kayit == null)
            {
                return false;
            }

            // iter < 600000 olan dosyayı reddet
            if (kayit.Iter < StandartIterasyon)
            {
                return false;
            }

            if (!string.Equals(kayit.Alg, StandartAlgoritma, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            byte[] tuz = Convert.FromBase64String(kayit.Tuz);
            byte[] beklenenOzet = Convert.FromBase64String(kayit.Ozet);

            if (tuz.Length != TuzUzunluguBayt || beklenenOzet.Length != OzetUzunluguBayt)
            {
                return false;
            }

            byte[] hesaplananOzet = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(parola),
                tuz,
                kayit.Iter,
                HashAlgorithmName.SHA256,
                beklenenOzet.Length);

            return CryptographicOperations.FixedTimeEquals(hesaplananOzet, beklenenOzet);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parolayı doğrular, yeni kaydı hazırlar, eski dosyayı .portal/parola_arsiv/ altına taşır ve yeni parolayı kaydeder.
    /// Yeni kayıt veya taşıma başarısız olursa eski dosyayı geri yükler.
    /// </summary>
    public bool Degistir(string eskiParola, string yeniParola, string? degistiren = null)
    {
        if (string.IsNullOrEmpty(yeniParola) || yeniParola.Length < EnAzParolaUzunlugu)
        {
            return false;
        }

        if (!Dogrula(eskiParola))
        {
            return false;
        }

        string portalKlasor = Path.Combine(_kokKlasor, ".portal");
        string tmpDosya = Path.Combine(portalKlasor, $"parola_{Guid.NewGuid():N}.tmp");
        string? arsivYolu = null;
        bool tasindi = false;

        try
        {
            Directory.CreateDirectory(portalKlasor);
            Directory.CreateDirectory(ArsivKlasorYolu);

            // 1. Yeni kaydı önce hazırla (.tmp olarak diske yaz ve flush et)
            byte[] tuz = RandomNumberGenerator.GetBytes(TuzUzunluguBayt);
            byte[] ozet = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(yeniParola),
                tuz,
                StandartIterasyon,
                HashAlgorithmName.SHA256,
                OzetUzunluguBayt);

            var kayit = new ParolaKaydi
            {
                Alg = StandartAlgoritma,
                Iter = StandartIterasyon,
                Tuz = Convert.ToBase64String(tuz),
                Ozet = Convert.ToBase64String(ozet),
                Degistiren = degistiren ?? _kimlik.KullaniciAdi(),
                Zaman = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
            };

            string json = JsonSerializer.Serialize(kayit);
            using (var fs = new FileStream(tmpDosya, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            // 2. Eski dosyayı arşive taşı
            string zamanStr = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            string arsivDosyaAdi = $"parola_{zamanStr}_{Guid.NewGuid():N}.json";
            arsivYolu = Path.Combine(ArsivKlasorYolu, arsivDosyaAdi);

            File.Move(ParolaDosyaYolu, arsivYolu);
            tasindi = true;

            // 3. Hazırlanmış yeni parolayı etkin parola dosyası yap
            File.Move(tmpDosya, ParolaDosyaYolu, overwrite: true);
            return true;
        }
        catch
        {
            // Yeni kayıt veya taşıma başarısızsa eski dosyayı geri yükle
            if (tasindi && arsivYolu != null && File.Exists(arsivYolu) && !File.Exists(ParolaDosyaYolu))
            {
                try
                {
                    File.Move(arsivYolu, ParolaDosyaYolu);
                }
                catch
                {
                    // Geri yükleme hatasını yut
                }
            }

            // Tmp dosya temizliği
            try
            {
                if (File.Exists(tmpDosya))
                {
                    File.Delete(tmpDosya);
                }
            }
            catch
            {
                // Temizlik hatasını yut
            }

            return false;
        }
    }

    public static bool Olustur(string kokKlasor, string parola, string? degistiren = null) =>
        new Parola(kokKlasor).Olustur(parola, degistiren);

    public static bool Dogrula(string kokKlasor, string parola) =>
        new Parola(kokKlasor).Dogrula(parola);

    public static bool Degistir(string kokKlasor, string eskiParola, string yeniParola, string? degistiren = null) =>
        new Parola(kokKlasor).Degistir(eskiParola, yeniParola, degistiren);
}
