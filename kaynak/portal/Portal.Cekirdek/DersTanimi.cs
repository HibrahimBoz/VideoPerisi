namespace Portal.Cekirdek;

/// <summary>
/// Çok açılı ders tanımını temsil eder (ders.json).
/// </summary>
public record DersTanimi(
    int SemaSurumu,
    string Ad,
    string ReferansKanal,
    IReadOnlyList<Kanal> Kanallar,
    SenkronBilgisi Senkron,
    IReadOnlyList<ElleIsaret> ElleIsaretler,
    string? DosyaYolu = null
)
{
    /// <summary>
    /// İsme göre kanal arar (büyük/küçük harf duyarsız).
    /// </summary>
    public Kanal? KanalBul(string ad) =>
        Kanallar.FirstOrDefault(k => string.Equals(k.Ad, ad, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Referans kanal nesnesini döner.
    /// </summary>
    public Kanal? ReferansKanali => KanalBul(ReferansKanal);

    /// <summary>
    /// Tüm kanalların kapsadığı en büyük sanal süreyi döner.
    /// </summary>
    public double ToplamSureSn => Kanallar.Count > 0
        ? Kanallar.Max(k => k.Parcalar.Count > 0 ? k.Parcalar.Max(p => p.SanalSon) : 0.0)
        : 0.0;
}

/// <summary>
/// Çok açılı dersteki tek bir kamerayı / kanalı temsil eder.
/// </summary>
public record Kanal(
    string Ad,
    double OfsetSn,
    double DriftPpm,
    bool Guvenilir,
    IReadOnlyList<ParcaTanimi> Parcalar
)
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// Güvenilirlik durum etiketini döner.
    /// </summary>
    public string DurumEtiketi => Guvenilir ? "senkron güvenilir" : "senkron güvenilmez";

    /// <summary>
    /// Sanal ders zamanını kanal zamanına dönüştürür:
    /// kanal_zamani = sanal * (1 + drift_ppm * 1e-6) + ofset_sn + ekOfsetSn
    /// (Python tarafıyla AYNI tanım).
    /// </summary>
    public double KanalZamaniHesapla(double sanalSn, double ekOfsetSn = 0.0)
    {
        return sanalSn * (1.0 + DriftPpm * 1e-6) + OfsetSn + ekOfsetSn;
    }

    /// <summary>
    /// Kanal zamanını sanal ders zamanına dönüştürür:
    /// sanal = (kanal_zamani - (ofset_sn + ekOfsetSn)) / (1 + drift_ppm * 1e-6)
    /// </summary>
    public double SanalZamanHesapla(double kanalZamani, double ekOfsetSn = 0.0)
    {
        double katsayi = 1.0 + DriftPpm * 1e-6;
        return (kanalZamani - (OfsetSn + ekOfsetSn)) / katsayi;
    }

    /// <summary>
    /// Sanal ders zamanını dosya adı ve dosya içi yerel saniyeye eşler (ikili arama).
    /// Sınır noktalarında ileri parçayı seçer.
    /// Boşluk veya kayıt dışı aralıklarda kayitVar=false döner.
    /// </summary>
    public (string? dosya, double yerelSn, bool kayitVar) SanalDenYerele(double sanalSn)
    {
        return SanalDenYerele(sanalSn, 0.0);
    }

    /// <summary>
    /// Sanal ders zamanını ek elle ofset ile dosya adı ve dosya içi yerel saniyeye eşler.
    /// </summary>
    public (string? dosya, double yerelSn, bool kayitVar) SanalDenYerele(double sanalSn, double ekOfsetSn)
    {
        if (Parcalar == null || Parcalar.Count == 0)
        {
            return (null, 0.0, false);
        }

        var parcalar = Parcalar;
        bool sirali = true;
        for (int i = 0; i < parcalar.Count - 1; i++)
        {
            if (parcalar[i].SanalBas > parcalar[i + 1].SanalBas)
            {
                sirali = false;
                break;
            }
        }
        if (!sirali)
        {
            parcalar = parcalar.OrderBy(p => p.SanalBas).ThenBy(p => p.YerelBas).ToList();
        }

        double kanalZamani = KanalZamaniHesapla(sanalSn, ekOfsetSn);

        // İlk parçadan önce veya son parçadan sonra kayıt yok
        if (kanalZamani < parcalar[0].SanalBas - Epsilon || kanalZamani > parcalar[^1].SanalSon + Epsilon)
        {
            return (null, 0.0, false);
        }

        // İkili arama ile kanal zamanını içeren parçayı bul
        int sol = 0;
        int sag = parcalar.Count - 1;
        int idx = -1;

        while (sol <= sag)
        {
            int orta = sol + (sag - sol) / 2;
            if (parcalar[orta].SanalBas <= kanalZamani + Epsilon)
            {
                idx = orta;
                sol = orta + 1;
            }
            else
            {
                sag = orta - 1;
            }
        }

        if (idx < 0 || idx >= parcalar.Count)
        {
            return (null, 0.0, false);
        }

        // Sınırlarda ileri parça kuralı: dilim sonu ile sonraki dilim başı kesişiminde sonraki dilim seçilir
        if (idx + 1 < parcalar.Count && kanalZamani >= parcalar[idx].SanalSon - Epsilon)
        {
            idx++;
        }

        var secilen = parcalar[idx];

        // Seçilen parça sınırları içinde mi? (Aralarda tanımlanmamış boşluk varsa)
        if (kanalZamani < secilen.SanalBas - Epsilon || kanalZamani > secilen.SanalSon + Epsilon)
        {
            return (null, 0.0, false);
        }

        // Boşluk diliminde kayıt yok
        if (string.Equals(secilen.Tur, "bosluk", StringComparison.OrdinalIgnoreCase) || secilen.Dosya == null)
        {
            return (null, 0.0, false);
        }

        double ofset = kanalZamani - secilen.SanalBas;
        double yerelSn = secilen.YerelBas + ofset;
        return (secilen.Dosya, yerelSn, true);
    }

    /// <summary>
    /// Dosya ve dosya içi yerel saniyeyi sanal ders zamanına dönüştürür.
    /// </summary>
    public double YereldenSanala(string? dosya, double yerelSn)
    {
        return YereldenSanala(dosya, yerelSn, 0.0);
    }

    /// <summary>
    /// Dosya ve dosya içi yerel saniyeyi ek elle ofset ile sanal ders zamanına dönüştürür.
    /// </summary>
    public double YereldenSanala(string? dosya, double yerelSn, double ekOfsetSn)
    {
        if (Parcalar == null || Parcalar.Count == 0)
        {
            throw new InvalidOperationException("Kanalda parça bulunamadı.");
        }

        // İlgili dosyaya ait parçaları filtrele
        var adayParcalar = new List<ParcaTanimi>();
        foreach (var p in Parcalar)
        {
            if (p.Dosya != null && DosyaEslesiyorMu(p.Dosya, dosya))
            {
                adayParcalar.Add(p);
            }
        }

        if (adayParcalar.Count == 0)
        {
            throw new ArgumentException($"Dosya kanalda bulunamadı: {dosya}", nameof(dosya));
        }

        // Yerel başlangıca göre sırala
        adayParcalar.Sort((a, b) => a.YerelBas.CompareTo(b.YerelBas));

        // İkili arama ile yerel saniyeye uyan parçayı bul
        int sol = 0;
        int sag = adayParcalar.Count - 1;
        int idx = -1;

        while (sol <= sag)
        {
            int orta = sol + (sag - sol) / 2;
            if (adayParcalar[orta].YerelBas <= yerelSn + Epsilon)
            {
                idx = orta;
                sol = orta + 1;
            }
            else
            {
                sag = orta - 1;
            }
        }

        if (idx < 0)
        {
            idx = 0;
        }

        // Sınırlarda ileri parça kuralı
        if (idx + 1 < adayParcalar.Count && yerelSn >= adayParcalar[idx].YerelSon - Epsilon)
        {
            idx++;
        }

        var secilen = adayParcalar[idx];
        if (yerelSn < secilen.YerelBas - Epsilon || yerelSn > secilen.YerelSon + Epsilon)
        {
            throw new ArgumentOutOfRangeException(nameof(yerelSn), $"Yerel saniye parça sınırları dışında: {yerelSn} (Aralık: {secilen.YerelBas} - {secilen.YerelSon})");
        }

        double ofsetYerel = yerelSn - secilen.YerelBas;
        double kanalZamani = secilen.SanalBas + ofsetYerel;
        return SanalZamanHesapla(kanalZamani, ekOfsetSn);
    }

    /// <summary>
    /// Önizleme videosu veya dosya adı verildiğinde, kanaldaki asıl parçanın dosyasını eşler.
    /// Doğrudan eşleşme yoksa (ör. önizleme mp4), yerel saniyeye uyan asıl parça döner.
    /// </summary>
    public string? OnizlemeDosyasiniEslestir(string? dosyaAdi, double yerelSn = 0.0)
    {
        if (string.IsNullOrWhiteSpace(dosyaAdi) || Parcalar == null || Parcalar.Count == 0)
        {
            return null;
        }

        foreach (var p in Parcalar)
        {
            if (p.Dosya != null && DosyaEslesiyorMu(p.Dosya, dosyaAdi))
            {
                return p.Dosya;
            }
        }

        var parca = Parcalar.FirstOrDefault(p => p.Dosya != null && yerelSn >= p.YerelBas - 0.05 && yerelSn <= p.YerelSon + 0.05)
                 ?? Parcalar.FirstOrDefault(p => p.Dosya != null);
        return parca?.Dosya;
    }

    private static bool DosyaEslesiyorMu(string dosyaA, string? dosyaB)
    {
        if (dosyaB == null) return false;
        if (string.Equals(dosyaA, dosyaB, StringComparison.OrdinalIgnoreCase)) return true;

        try
        {
            string tamA = Path.GetFullPath(dosyaA);
            string tamB = Path.GetFullPath(dosyaB);
            return string.Equals(tamA, tamB, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Kanal içerisindeki tek bir video parçasını veya boşluk dilimini temsil eder.
/// </summary>
public record ParcaTanimi(
    string? Dosya,
    double SanalBas,
    double SanalSon,
    double YerelBas,
    string Tur,
    double? OfsetSn = null,
    double? Guven = null
)
{
    public double Sure => SanalSon - SanalBas;
    public double YerelSon => YerelBas + Sure;
    public bool KayitVar => !string.Equals(Tur, "bosluk", StringComparison.OrdinalIgnoreCase) && Dosya != null;
}

/// <summary>
/// Çok açılı dersin genel senkron özet bilgilerini temsil eder.
/// </summary>
public record SenkronBilgisi(
    string Yontem,
    double DriftPpm,
    double Guven,
    bool Guvenilir,
    string? Neden = null
);

/// <summary>
/// Kullanıcı veya sistem tarafından kaydedilen elle eşleme işaretini temsil eder.
/// </summary>
public record ElleIsaret(
    string Kanal,
    double SanalSn,
    string OrtakAnId
);
