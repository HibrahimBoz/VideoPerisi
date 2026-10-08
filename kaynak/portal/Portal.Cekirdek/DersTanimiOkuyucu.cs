namespace Portal.Cekirdek;

using System.Text;
using System.Text.Json;

/// <summary>
/// ders.json dosyasını ayrıştırıp doğrulayan okuyucu sınıf.
/// </summary>
public static class DersTanimiOkuyucu
{
    /// <summary>
    /// Belirtilen ders.json dosyasını okur, doğrular ve DersTanimi nesnesi olarak döner.
    /// Göreli dosya yollarını ders klasörüne göre mutlak yapar.
    /// </summary>
    public static DersTanimi Oku(string yol)
    {
        if (string.IsNullOrWhiteSpace(yol))
        {
            throw new ArgumentException("Ders tanımı dosya yolu boş olamaz.", nameof(yol));
        }

        if (!File.Exists(yol))
        {
            throw new FileNotFoundException("Ders tanımı JSON dosyası bulunamadı.", yol);
        }

        string jsonMetni;
        try
        {
            jsonMetni = File.ReadAllText(yol, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidDataException("Ders tanımı JSON dosyası okunamadı.", ex);
        }

        string dersKlasoru = Path.GetDirectoryName(Path.GetFullPath(yol)) ?? string.Empty;
        return MetindenOku(jsonMetni, dersKlasoru, yol);
    }

    /// <summary>
    /// JSON metnini doğrudan ayrıştırır ve belirtilen temel klasöre göre yolları çözer.
    /// </summary>
    public static DersTanimi MetindenOku(string jsonMetni, string temelKlasor = "", string? dosyaYolu = null)
    {
        if (string.IsNullOrWhiteSpace(jsonMetni))
        {
            throw new InvalidDataException("JSON metni boş olamaz.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonMetni);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("JSON biçimi geçersiz veya bozuk.", ex);
        }

        using (doc)
        {
            var kok = doc.RootElement;
            if (kok.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("JSON kök elemanı bir nesne olmalıdır.");
            }

            // 1. sema_surumu
            if (!TryGetProp(kok, "sema_surumu", out var semaElem) ||
                semaElem.ValueKind != JsonValueKind.Number ||
                !semaElem.TryGetInt32(out int semaSurumu))
            {
                throw new InvalidDataException("'sema_surumu' alanı eksik veya geçerli bir tamsayı değil.");
            }

            if (semaSurumu != 1)
            {
                throw new InvalidDataException($"Bilinmeyen şema sürümü: {semaSurumu}. Beklenen sürüm: 1.");
            }

            // 2. ad
            if (!TryGetProp(kok, "ad", out var adElem) ||
                adElem.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(adElem.GetString()))
            {
                throw new InvalidDataException("'ad' alanı eksik veya boş.");
            }
            string ad = adElem.GetString()!;

            // 3. referans_kanal
            if (!TryGetProp(kok, "referans_kanal", out var refElem) ||
                refElem.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(refElem.GetString()))
            {
                throw new InvalidDataException("'referans_kanal' alanı eksik veya boş.");
            }
            string referansKanal = refElem.GetString()!;

            // 4. kanallar
            if (!TryGetProp(kok, "kanallar", out var kanallarElem) ||
                kanallarElem.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("'kanallar' alanı eksik veya dizi değil.");
            }

            var kanallar = new List<Kanal>();
            foreach (var kElem in kanallarElem.EnumerateArray())
            {
                if (kElem.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("Kanal bir nesne olmalıdır.");
                }

                if (!TryGetProp(kElem, "ad", out var kAdElem) ||
                    kAdElem.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(kAdElem.GetString()))
                {
                    throw new InvalidDataException("Kanal 'ad' alanı eksik veya boş.");
                }
                string kAd = kAdElem.GetString()!;

                if (!TryGetProp(kElem, "ofset_sn", out var ofsetElem) ||
                    ofsetElem.ValueKind != JsonValueKind.Number ||
                    !ofsetElem.TryGetDouble(out double ofsetSn) ||
                    !double.IsFinite(ofsetSn))
                {
                    throw new InvalidDataException($"Kanal '{kAd}' için 'ofset_sn' alanı eksik veya geçerli bir sayı değil.");
                }

                if (!TryGetProp(kElem, "drift_ppm", out var driftElem) ||
                    driftElem.ValueKind != JsonValueKind.Number ||
                    !driftElem.TryGetDouble(out double driftPpm) ||
                    !double.IsFinite(driftPpm))
                {
                    throw new InvalidDataException($"Kanal '{kAd}' için 'drift_ppm' alanı eksik veya geçerli bir sayı değil.");
                }

                if (!TryGetProp(kElem, "guvenilir", out var guvenilirElem) ||
                    (guvenilirElem.ValueKind != JsonValueKind.True && guvenilirElem.ValueKind != JsonValueKind.False))
                {
                    throw new InvalidDataException($"Kanal '{kAd}' için 'guvenilir' alanı eksik veya mantıksal değer değil.");
                }
                bool guvenilir = guvenilirElem.GetBoolean();

                if (!TryGetProp(kElem, "parcalar", out var parcalarElem) ||
                    parcalarElem.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException($"Kanal '{kAd}' için 'parcalar' alanı eksik veya dizi değil.");
                }

                var parcalar = new List<ParcaTanimi>();
                foreach (var pElem in parcalarElem.EnumerateArray())
                {
                    if (pElem.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' içerisindeki parça bir nesne olmalıdır.");
                    }

                    if (!TryGetProp(pElem, "tur", out var turElem) ||
                        turElem.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(turElem.GetString()))
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' parçasında 'tur' alanı eksik veya boş.");
                    }
                    string tur = turElem.GetString()!;

                    if (!string.Equals(tur, "kayit", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(tur, "bosluk", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(tur, "cakisma_kirpildi", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' parçasında geçersiz parça türü: '{tur}'.");
                    }

                    if (!TryGetProp(pElem, "sanal_bas", out var sBasElem) ||
                        sBasElem.ValueKind != JsonValueKind.Number ||
                        !sBasElem.TryGetDouble(out double sanalBas) ||
                        !double.IsFinite(sanalBas))
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' parçasında 'sanal_bas' alanı eksik veya sayı değil.");
                    }

                    if (!TryGetProp(pElem, "sanal_son", out var sSonElem) ||
                        sSonElem.ValueKind != JsonValueKind.Number ||
                        !sSonElem.TryGetDouble(out double sanalSon) ||
                        !double.IsFinite(sanalSon))
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' parçasında 'sanal_son' alanı eksik veya sayı değil.");
                    }

                    if (sanalSon < sanalBas)
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' parçasında 'sanal_son' ({sanalSon}) 'sanal_bas'tan ({sanalBas}) küçük olamaz.");
                    }

                    if (!TryGetProp(pElem, "yerel_bas", out var yBasElem) ||
                        yBasElem.ValueKind != JsonValueKind.Number ||
                        !yBasElem.TryGetDouble(out double yerelBas) ||
                        !double.IsFinite(yerelBas))
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' parçasında 'yerel_bas' alanı eksik veya sayı değil.");
                    }

                    // Dosya yolu: 'bosluk' diliminde dosya null olabilir, diğerlerinde zorunlu
                    string? mutlakDosya = null;
                    if (TryGetProp(pElem, "dosya", out var dosyaElem) &&
                        dosyaElem.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(dosyaElem.GetString()))
                    {
                        string hamDosya = dosyaElem.GetString()!;
                        mutlakDosya = Path.IsPathRooted(hamDosya) || string.IsNullOrEmpty(temelKlasor)
                            ? Path.GetFullPath(hamDosya)
                            : Path.GetFullPath(Path.Combine(temelKlasor, hamDosya));
                    }
                    else if (!string.Equals(tur, "bosluk", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Kanal '{kAd}' parçasında ('{tur}') 'dosya' alanı eksik veya boş.");
                    }

                    double? parcaOfsetSn = null;
                    if (TryGetProp(pElem, "ofset_sn", out var pOfsetElem))
                    {
                        if (pOfsetElem.ValueKind == JsonValueKind.Number &&
                            pOfsetElem.TryGetDouble(out double pOfsVal) &&
                            double.IsFinite(pOfsVal))
                        {
                            parcaOfsetSn = pOfsVal;
                        }
                        else if (pOfsetElem.ValueKind != JsonValueKind.Null)
                        {
                            throw new InvalidDataException($"Kanal '{kAd}' parçasında 'ofset_sn' geçerli bir sayı olmalıdır.");
                        }
                    }

                    double? parcaGuven = null;
                    if (TryGetProp(pElem, "guven", out var pGuvenElem))
                    {
                        if (pGuvenElem.ValueKind == JsonValueKind.Number &&
                            pGuvenElem.TryGetDouble(out double pGuvVal) &&
                            double.IsFinite(pGuvVal))
                        {
                            parcaGuven = pGuvVal;
                        }
                        else if (pGuvenElem.ValueKind != JsonValueKind.Null)
                        {
                            throw new InvalidDataException($"Kanal '{kAd}' parçasında 'guven' geçerli bir sayı olmalıdır.");
                        }
                    }

                    parcalar.Add(new ParcaTanimi(
                        mutlakDosya,
                        sanalBas,
                        sanalSon,
                        yerelBas,
                        tur,
                        parcaOfsetSn,
                        parcaGuven
                    ));
                }

                // Parçaları sanal ve yerel başlangıç zamanına göre sırala
                parcalar.Sort((a, b) =>
                {
                    int karsilastir = a.SanalBas.CompareTo(b.SanalBas);
                    return karsilastir != 0 ? karsilastir : a.YerelBas.CompareTo(b.YerelBas);
                });

                kanallar.Add(new Kanal(kAd, ofsetSn, driftPpm, guvenilir, parcalar));
            }

            // 5. senkron
            if (!TryGetProp(kok, "senkron", out var senkronElem) ||
                senkronElem.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("'senkron' alanı eksik veya nesne değil.");
            }

            if (!TryGetProp(senkronElem, "yontem", out var yontemElem) ||
                yontemElem.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(yontemElem.GetString()))
            {
                throw new InvalidDataException("Senkron 'yontem' alanı eksik veya boş.");
            }
            string yontem = yontemElem.GetString()!;

            if (!TryGetProp(senkronElem, "drift_ppm", out var sDriftElem) ||
                sDriftElem.ValueKind != JsonValueKind.Number ||
                !sDriftElem.TryGetDouble(out double sDriftPpm) ||
                !double.IsFinite(sDriftPpm))
            {
                throw new InvalidDataException("Senkron 'drift_ppm' alanı eksik veya geçerli bir sayı değil.");
            }

            if (!TryGetProp(senkronElem, "guven", out var sGuvenElem) ||
                sGuvenElem.ValueKind != JsonValueKind.Number ||
                !sGuvenElem.TryGetDouble(out double sGuven) ||
                !double.IsFinite(sGuven))
            {
                throw new InvalidDataException("Senkron 'guven' alanı eksik veya geçerli bir sayı değil.");
            }

            if (!TryGetProp(senkronElem, "guvenilir", out var sGuvenilirElem) ||
                (sGuvenilirElem.ValueKind != JsonValueKind.True && sGuvenilirElem.ValueKind != JsonValueKind.False))
            {
                throw new InvalidDataException("Senkron 'guvenilir' alanı eksik veya mantıksal değer değil.");
            }
            bool sGuvenilir = sGuvenilirElem.GetBoolean();

            string? neden = null;
            if (TryGetProp(senkronElem, "neden", out var nedenElem))
            {
                if (nedenElem.ValueKind == JsonValueKind.String)
                {
                    neden = nedenElem.GetString();
                }
                else if (nedenElem.ValueKind != JsonValueKind.Null)
                {
                    throw new InvalidDataException("Senkron 'neden' alanı geçerli bir metin olmalıdır.");
                }
            }

            var senkronBilgisi = new SenkronBilgisi(yontem, sDriftPpm, sGuven, sGuvenilir, neden);

            // 6. elle_isaretler
            if (!TryGetProp(kok, "elle_isaretler", out var isaretlerElem) ||
                isaretlerElem.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("'elle_isaretler' alanı eksik veya dizi değil.");
            }

            var elleIsaretler = new List<ElleIsaret>();
            foreach (var isElem in isaretlerElem.EnumerateArray())
            {
                if (isElem.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("Elle işaret kaydı bir nesne olmalıdır.");
                }

                if (!TryGetProp(isElem, "kanal", out var isKanalElem) ||
                    isKanalElem.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(isKanalElem.GetString()))
                {
                    throw new InvalidDataException("Elle işaret 'kanal' alanı eksik veya boş.");
                }
                string isKanal = isKanalElem.GetString()!;

                if (!TryGetProp(isElem, "sanal_sn", out var isSnElem) ||
                    isSnElem.ValueKind != JsonValueKind.Number ||
                    !isSnElem.TryGetDouble(out double isSn) ||
                    !double.IsFinite(isSn))
                {
                    throw new InvalidDataException("Elle işaret 'sanal_sn' alanı eksik veya sayı değil.");
                }

                if (!TryGetProp(isElem, "ortak_an_id", out var isIdElem) ||
                    isIdElem.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(isIdElem.GetString()))
                {
                    throw new InvalidDataException("Elle işaret 'ortak_an_id' alanı eksik veya boş.");
                }
                string ortakAnId = isIdElem.GetString()!;

                elleIsaretler.Add(new ElleIsaret(isKanal, isSn, ortakAnId));
            }

            return new DersTanimi(
                semaSurumu,
                ad,
                referansKanal,
                kanallar,
                senkronBilgisi,
                elleIsaretler,
                dosyaYolu
            );
        }
    }

    private static bool TryGetProp(JsonElement elem, string name, out JsonElement value)
    {
        if (elem.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var prop in elem.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name.Replace("_", ""), name.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
