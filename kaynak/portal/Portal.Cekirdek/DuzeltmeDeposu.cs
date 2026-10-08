namespace Portal.Cekirdek;

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

/// <summary>
/// Düzeltmeler uygulandıktan sonraki segment durumu, geçmişi ve çakışma bayrağı.
/// </summary>
public record EfektifSegment(
    int No,
    double Bas,
    double Son,
    string Metin,
    IReadOnlyList<Duzeltme> Gecmis,
    bool Cakisma
) : Segment(No, Bas, Son, Metin)
{
    public Segment AsilSegment => new(No, Bas, Son, Metin);
}

/// <summary>
/// Transkript ile düzeltmelerin birleştirilme sonucu.
/// </summary>
public class BirlestirmeSonucu : IReadOnlyList<EfektifSegment>
{
    public IReadOnlyList<EfektifSegment> Segmentler { get; }
    public IReadOnlyList<Duzeltme> Yetimler { get; }
    public int AtlananDosyaSayisi { get; }
    public bool CakismaVar => Segmentler.Any(s => s.Cakisma);

    public BirlestirmeSonucu(
        IReadOnlyList<EfektifSegment> segmentler,
        IReadOnlyList<Duzeltme> yetimler,
        int atlananDosyaSayisi = 0)
    {
        Segmentler = segmentler ?? Array.Empty<EfektifSegment>();
        Yetimler = yetimler ?? Array.Empty<Duzeltme>();
        AtlananDosyaSayisi = atlananDosyaSayisi;
    }

    public int Count => Segmentler.Count;
    public EfektifSegment this[int index] => Segmentler[index];
    public IEnumerator<EfektifSegment> GetEnumerator() => Segmentler.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Segment düzeltmelerini dosya sisteminde yönetir ve transkript ile birleştirir.
/// </summary>
public class DuzeltmeDeposu
{
    private static readonly JsonSerializerOptions JsonAyar = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };

    private readonly string? _kokKlasor;
    private readonly IKimlik _kimlik;

    /// <summary>
    /// Okuma sırasında atlanan (.tmp veya ayrıştırılamayan) dosya sayısı.
    /// </summary>
    public int AtlananDosyaSayisi { get; private set; }

    public DuzeltmeDeposu(string? kokKlasor = null, IKimlik? kimlik = null)
    {
        _kokKlasor = kokKlasor;
        _kimlik = kimlik ?? Kimlik.Varsayilan;
    }

    /// <summary>
    /// Verilen ders için düzeltmeler klasörünü belirler.
    /// </summary>
    public string HedefKlasor(string? dersAdi = null)
    {
        if (!string.IsNullOrWhiteSpace(_kokKlasor))
        {
            if (_kokKlasor.EndsWith(".duzeltmeler", StringComparison.OrdinalIgnoreCase) ||
                _kokKlasor.EndsWith(".duzeltmeler/", StringComparison.OrdinalIgnoreCase) ||
                _kokKlasor.EndsWith(".duzeltmeler\\", StringComparison.OrdinalIgnoreCase))
            {
                return _kokKlasor;
            }

            if (!string.IsNullOrWhiteSpace(dersAdi))
            {
                return Path.Combine(_kokKlasor, $"{dersAdi}.duzeltmeler");
            }

            return _kokKlasor;
        }

        string ad = string.IsNullOrWhiteSpace(dersAdi) ? "ders" : dersAdi;
        return Path.Combine(Directory.GetCurrentDirectory(), $"{ad}.duzeltmeler");
    }

    /// <summary>
    /// Dosya adı için kullanıcı bilgisini güvenli karakterlere dönüştürür.
    /// </summary>
    public static string GuvenliKullaniciAdi(string? kullanici)
    {
        if (string.IsNullOrWhiteSpace(kullanici))
        {
            return "anonim";
        }

        var sb = new StringBuilder(kullanici.Length);
        var gecersizler = Path.GetInvalidFileNameChars();

        foreach (char c in kullanici)
        {
            if (c == '\\' || c == '/' || c == ':' || c == '*' || c == '?' ||
                c == '"' || c == '<' || c == '>' || c == '|' || c == ' ' ||
                Array.IndexOf(gecersizler, c) >= 0)
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
        }

        string sonuc = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(sonuc) ? "anonim" : sonuc;
    }

    /// <summary>
    /// Belirtilen düzeltme için standart dosya adını üretir:
    /// yyyyMMddTHHmmssfffZ_kullanici_guidİlk6.json
    /// </summary>
    public static string DosyaAdiUret(Duzeltme duzeltme)
    {
        if (duzeltme == null)
        {
            throw new ArgumentNullException(nameof(duzeltme));
        }

        string zamanStr = duzeltme.ZamanUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        string guvenliKullanici = GuvenliKullaniciAdi(duzeltme.Kullanici);
        string guidKisa = duzeltme.Id.Replace("-", "");
        if (guidKisa.Length > 6)
        {
            guidKisa = guidKisa[..6];
        }
        else
        {
            guidKisa = guidKisa.PadRight(6, '0');
        }

        return $"{zamanStr}_{guvenliKullanici}_{guidKisa}.json";
    }

    /// <summary>
    /// Düzeltmeyi deposuna kaydeder.
    /// </summary>
    public string Yaz(Duzeltme duzeltme)
    {
        if (duzeltme == null)
        {
            throw new ArgumentNullException(nameof(duzeltme));
        }

        string klasor = HedefKlasor(duzeltme.Ders);
        return Yaz(klasor, duzeltme);
    }

    /// <summary>
    /// Düzeltmeyi belirtilen klasöre atomik olarak (.tmp + Move) yazar.
    /// </summary>
    public string Yaz(string klasor, Duzeltme duzeltme)
    {
        if (string.IsNullOrWhiteSpace(klasor))
        {
            throw new ArgumentException("Klasör yolu boş olamaz.", nameof(klasor));
        }
        if (duzeltme == null)
        {
            throw new ArgumentNullException(nameof(duzeltme));
        }

        Directory.CreateDirectory(klasor);

        string dosyaAdi = DosyaAdiUret(duzeltme);
        string sonDosyaYolu = Path.Combine(klasor, dosyaAdi);

        // Nadir çakışma durumunda benzersizleştirme
        if (File.Exists(sonDosyaYolu))
        {
            string guidKalan = duzeltme.Id.Replace("-", "");
            string ek = guidKalan.Length > 6 ? guidKalan[6..Math.Min(12, guidKalan.Length)] : Guid.NewGuid().ToString("N")[..6];
            string zamanStr = duzeltme.ZamanUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            string guvenliKullanici = GuvenliKullaniciAdi(duzeltme.Kullanici);
            dosyaAdi = $"{zamanStr}_{guvenliKullanici}_{guidKalan[..6]}_{ek}.json";
            sonDosyaYolu = Path.Combine(klasor, dosyaAdi);
        }

        string tmpDosyaAdi = $"{Path.GetFileNameWithoutExtension(dosyaAdi)}_{Guid.NewGuid():N}.tmp";
        string tmpYolu = Path.Combine(klasor, tmpDosyaAdi);

        try
        {
            using (var fs = new FileStream(tmpYolu, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                using (var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false, Encoder = JsonAyar.Encoder }))
                {
                    JsonSerializer.Serialize(writer, duzeltme, JsonAyar);
                }
                fs.Flush(true);
            }

            int deneme = 0;
            while (true)
            {
                try
                {
                    File.Move(tmpYolu, sonDosyaYolu);
                    break;
                }
                catch (IOException) when (deneme < 10)
                {
                    deneme++;
                    Thread.Sleep(5 * deneme);
                }
            }

            return sonDosyaYolu;
        }
        catch
        {
            // Yalnızca tmp başarısızlık temizliği
            try
            {
                if (File.Exists(tmpYolu))
                {
                    File.Delete(tmpYolu);
                }
            }
            catch
            {
                // Temizlik istisnasını yut
            }
            throw;
        }
    }

    /// <summary>
    /// Klasördeki geçerli tüm düzeltmeleri okur, bozuk ve tmp dosyaları sayar.
    /// </summary>
    public IReadOnlyList<Duzeltme> DuzeltmeleriYukle(string klasor)
    {
        var liste = new List<Duzeltme>();
        if (string.IsNullOrWhiteSpace(klasor) || !Directory.Exists(klasor))
        {
            return liste;
        }

        foreach (string dosyaYolu in Directory.EnumerateFiles(klasor))
        {
            if (dosyaYolu.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                AtlananDosyaSayisi++;
                continue;
            }

            try
            {
                string json = File.ReadAllText(dosyaYolu, Encoding.UTF8);
                var d = JsonSerializer.Deserialize<Duzeltme>(json, JsonAyar);
                if (d == null || d.Sema != 1 || string.IsNullOrWhiteSpace(d.Id))
                {
                    AtlananDosyaSayisi++;
                    continue;
                }
                liste.Add(d);
            }
            catch
            {
                AtlananDosyaSayisi++;
            }
        }

        return liste;
    }

    /// <summary>
    /// Transkript ile varsayılan klasördeki düzeltmeleri birleştirir.
    /// </summary>
    public BirlestirmeSonucu Birlestir(Transkript transkript)
    {
        if (transkript == null)
        {
            throw new ArgumentNullException(nameof(transkript));
        }

        string klasor = HedefKlasor(transkript.Ad);
        return Birlestir(transkript, klasor);
    }

    /// <summary>
    /// Transkript ile belirtilen klasördeki düzeltmeleri birleştirir.
    /// </summary>
    public BirlestirmeSonucu Birlestir(Transkript transkript, string klasor)
    {
        if (transkript == null)
        {
            throw new ArgumentNullException(nameof(transkript));
        }

        AtlananDosyaSayisi = 0;
        var tumDuzeltmeler = DuzeltmeleriYukle(klasor);

        var segmentDuzeltmeleri = new Dictionary<int, List<Duzeltme>>();
        var yetimler = new List<Duzeltme>();

        foreach (var s in transkript.Segmentler)
        {
            segmentDuzeltmeleri[s.No] = new List<Duzeltme>();
        }

        foreach (var d in tumDuzeltmeler)
        {
            bool kaynakOzetUyusuyor = KaynakOzetUyusuyor(transkript.KaynakOzet, d.KaynakOzet);

            if (kaynakOzetUyusuyor)
            {
                if (segmentDuzeltmeleri.TryGetValue(d.SegmentNo, out var liste))
                {
                    liste.Add(d);
                }
                else
                {
                    yetimler.Add(d);
                }
            }
            else
            {
                // Kaynak özet uyuşmazlığında: (bas zamanı ±0.5 s ve eski metin eşleşmesi)
                Segment? eslesenSegment = SegmentEslestir(transkript.Segmentler, d);
                if (eslesenSegment != null && segmentDuzeltmeleri.TryGetValue(eslesenSegment.No, out var liste))
                {
                    liste.Add(d);
                }
                else
                {
                    yetimler.Add(d);
                }
            }
        }

        var efektifSegmentler = new List<EfektifSegment>(transkript.Segmentler.Count);

        foreach (var s in transkript.Segmentler)
        {
            var bagli = segmentDuzeltmeleri[s.No];
            var sirali = bagli
                .OrderBy(d => d.ZamanUtc)
                .ThenBy(d => d.Id, StringComparer.Ordinal)
                .ToList();

            if (sirali.Count == 0)
            {
                efektifSegmentler.Add(new EfektifSegment(s.No, s.Bas, s.Son, s.Metin, Array.Empty<Duzeltme>(), false));
                continue;
            }

            // Aynı onceki_id'yi paylaşan ≥2 düzeltme → Cakisma=true (ikisi de geçmişte kalır)
            var cakisanOncekiIdler = sirali
                .Where(d => !string.IsNullOrEmpty(d.OncekiId))
                .GroupBy(d => d.OncekiId)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key!)
                .ToHashSet(StringComparer.Ordinal);

            bool cakisma = cakisanOncekiIdler.Count > 0;
            string guncelMetin = s.Metin;

            if (!cakisma)
            {
                foreach (var d in sirali)
                {
                    if (string.Equals(d.Tur, "geri_al", StringComparison.OrdinalIgnoreCase))
                    {
                        var hedef = sirali.FirstOrDefault(x => x.Id == d.GeriAlHedef)
                                 ?? tumDuzeltmeler.FirstOrDefault(x => x.Id == d.GeriAlHedef);
                        if (hedef != null)
                        {
                            guncelMetin = hedef.Eski;
                        }
                        else if (!string.IsNullOrEmpty(d.Yeni))
                        {
                            guncelMetin = d.Yeni;
                        }
                    }
                    else
                    {
                        guncelMetin = d.Yeni;
                    }
                }
            }
            else
            {
                // Çakışan düzeltmeler ve onların ardılları elenir
                var elenenIdler = new HashSet<string>(StringComparer.Ordinal);
                foreach (var d in sirali)
                {
                    if (d.OncekiId != null && cakisanOncekiIdler.Contains(d.OncekiId))
                    {
                        elenenIdler.Add(d.Id);
                    }
                }

                bool yeniElendi;
                do
                {
                    yeniElendi = false;
                    foreach (var d in sirali)
                    {
                        if (d.OncekiId != null && elenenIdler.Contains(d.OncekiId) && elenenIdler.Add(d.Id))
                        {
                            yeniElendi = true;
                        }
                    }
                } while (yeniElendi);

                foreach (var d in sirali)
                {
                    if (elenenIdler.Contains(d.Id))
                    {
                        continue;
                    }

                    if (string.Equals(d.Tur, "geri_al", StringComparison.OrdinalIgnoreCase))
                    {
                        var hedef = sirali.FirstOrDefault(x => x.Id == d.GeriAlHedef)
                                 ?? tumDuzeltmeler.FirstOrDefault(x => x.Id == d.GeriAlHedef);
                        if (hedef != null)
                        {
                            guncelMetin = hedef.Eski;
                        }
                        else if (!string.IsNullOrEmpty(d.Yeni))
                        {
                            guncelMetin = d.Yeni;
                        }
                    }
                    else
                    {
                        guncelMetin = d.Yeni;
                    }
                }
            }

            efektifSegmentler.Add(new EfektifSegment(s.No, s.Bas, s.Son, guncelMetin, sirali, cakisma));
        }

        return new BirlestirmeSonucu(efektifSegmentler, yetimler, AtlananDosyaSayisi);
    }

    private static Segment? SegmentEslestir(IReadOnlyList<Segment> segmentler, Duzeltme d)
    {
        Segment? enIyi = null;
        double enKucukFark = double.MaxValue;

        foreach (var s in segmentler)
        {
            bool metinUyuyor = MetinlerEslesiyor(s.Metin, d.Eski);
            if (!metinUyuyor)
            {
                continue;
            }

            if (d.Bas.HasValue)
            {
                double fark = Math.Abs(s.Bas - d.Bas.Value);
                if (fark <= 0.5001 && fark < enKucukFark)
                {
                    enKucukFark = fark;
                    enIyi = s;
                }
            }
            else
            {
                if (s.No == d.SegmentNo)
                {
                    return s;
                }
                if (enIyi == null)
                {
                    enIyi = s;
                }
            }
        }

        return enIyi;
    }

    private static bool MetinlerEslesiyor(string? metin1, string? metin2)
    {
        if (metin1 == null && metin2 == null) return true;
        if (metin1 == null || metin2 == null) return false;
        if (string.Equals(metin1, metin2, StringComparison.Ordinal)) return true;
        return string.Equals(metin1.Trim(), metin2.Trim(), StringComparison.Ordinal);
    }

    private static bool KaynakOzetUyusuyor(string? transkriptOzet, string? duzeltmeOzet)
    {
        if (string.IsNullOrEmpty(transkriptOzet) && string.IsNullOrEmpty(duzeltmeOzet))
        {
            return true;
        }
        if (string.IsNullOrEmpty(transkriptOzet) || string.IsNullOrEmpty(duzeltmeOzet))
        {
            return false;
        }
        return string.Equals(transkriptOzet, duzeltmeOzet, StringComparison.Ordinal);
    }
}
