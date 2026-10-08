namespace Portal.Cekirdek;

/// <summary>
/// Çok açılı oynatıcı karar mantığı sonucu üretilen eylemler.
/// </summary>
public abstract record Eylem
{
    public sealed record Yok : Eylem;
    public sealed record HafifDuzelt(double HizKatsayisi) : Eylem
    {
        public double hizKatsayisi => HizKatsayisi;
    }
    public sealed record Atla(double HedefYerelSn) : Eylem
    {
        public double hedefYerelSn => HedefYerelSn;
    }
    public sealed record Gizle(string? Neden = "kayit yok/bitti") : Eylem
    {
        public string? neden => Neden;
    }

    public static readonly Yok SabitYok = new();
    public static HafifDuzelt HizAyarla(double hizKatsayisi) => new(hizKatsayisi);
    public static Atla KonumaAtla(double hedefYerelSn) => new(hedefYerelSn);
    public static Gizle OynatmayiGizle(string? neden = "kayit yok/bitti") => new(neden);
}

/// <summary>
/// Çok açılı görünüm modları.
/// </summary>
public abstract record GorunumModu
{
    public record Izgara : GorunumModu;
    public record Odak(string BuyukKanal) : GorunumModu;
    public record Tek(string Kanal) : GorunumModu;

    public static readonly Izgara SabitIzgara = new();
    public static Odak Odaklan(string buyukKanal) => new(buyukKanal);
    public static Tek TekKanal(string kanal) => new(kanal);
}

/// <summary>
/// GörünümModu Unicode Türkçe karakterli takma ad sınıfı.
/// </summary>
public abstract record GörünümModu : GorunumModu
{
    public new record Izgara : GorunumModu.Izgara;
    public new record Odak(string BuyukKanal) : GorunumModu.Odak(BuyukKanal);
    public new record Tek(string Kanal) : GorunumModu.Tek(Kanal);
}

/// <summary>
/// Çok açılı video oynatımı için UI'dan bağımsız saat ve karar mantığı bileşeni.
/// </summary>
public class CokAciliSaat
{
    private readonly object _kilit = new();
    private readonly Dictionary<string, double> _elleOfsetler = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KanalHafizasi> _kanalDurumlari = new(StringComparer.OrdinalIgnoreCase);

    private Func<DateTime>? _zamanKaynagi;
    private DateTime? _sonGuncellemeZamani;
    private double _hiz = 1.0;
    private bool _oynatiliyor;
    private double _sanalZaman;
    private string? _aktifSesKanali;
    private GorunumModu _gorunum = new GorunumModu.Izgara();

    public DersTanimi Ders { get; }
    public double IzinVerilenSapmaEsigi { get; }
    public double SertAtlamaEsigi { get; }

    public bool Oynatiliyor
    {
        get { lock (_kilit) return _oynatiliyor; }
        private set { lock (_kilit) _oynatiliyor = value; }
    }

    public double SanalZaman
    {
        get { lock (_kilit) return _sanalZaman; }
        private set { lock (_kilit) _sanalZaman = value; }
    }

    public double Hiz
    {
        get { lock (_kilit) return _hiz; }
        set => HizAyarla(value);
    }

    public string? AktifSesKanali
    {
        get { lock (_kilit) return _aktifSesKanali; }
        private set { lock (_kilit) _aktifSesKanali = value; }
    }

    public string? SesKanali
    {
        get => AktifSesKanali;
        set => SesKanaliSec(value);
    }

    public GorunumModu Gorunum
    {
        get { lock (_kilit) return _gorunum; }
        private set { lock (_kilit) _gorunum = value; }
    }

    public GorunumModu GorunumModu
    {
        get => Gorunum;
        set => GorunumModuDegistir(value);
    }

    public IReadOnlyDictionary<string, double> EllesOfsetler
    {
        get
        {
            lock (_kilit)
            {
                return new Dictionary<string, double>(_elleOfsetler, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public IReadOnlyDictionary<string, double> ElleOfsetler => EllesOfsetler;

    public CokAciliSaat(
        DersTanimi dersTanimi,
        double izinVerilenSapmaEsigi = 0.15,
        double sertAtlamaEsigi = 1.0,
        Func<DateTime>? zamanKaynagi = null)
    {
        Ders = dersTanimi ?? throw new ArgumentNullException(nameof(dersTanimi));
        IzinVerilenSapmaEsigi = izinVerilenSapmaEsigi;
        SertAtlamaEsigi = sertAtlamaEsigi;
        _zamanKaynagi = zamanKaynagi;
        _aktifSesKanali = dersTanimi.ReferansKanal;
    }

    public CokAciliSaat(
        DersTanimi dersTanimi,
        Func<DateTime> zamanKaynagi)
        : this(dersTanimi, 0.15, 1.0, zamanKaynagi)
    {
    }

    /// <summary>
    /// Zaman kaynağını enjekte eder (testte sahte saat kullanımı).
    /// </summary>
    public void ZamanKaynagiAyarla(Func<DateTime>? zamanKaynagi)
    {
        lock (_kilit)
        {
            _zamanKaynagi = zamanKaynagi;
        }
    }

    /// <summary>
    /// Enjekte edilen veya sistem saatini döner.
    /// </summary>
    public DateTime SimdiZamani(DateTime? acikZaman = null)
    {
        if (acikZaman.HasValue) return acikZaman.Value;
        Func<DateTime>? zk;
        lock (_kilit)
        {
            zk = _zamanKaynagi;
        }
        return zk != null ? zk() : DateTime.UtcNow;
    }

    /// <summary>
    /// Ana saatin mevcut sanal saniyesini döner.
    /// </summary>
    public double Simdi() => SimdiSanalZaman(null);
    public double Simdi(DateTime? simdi = null) => SimdiSanalZaman(simdi);
    public double Simdi(Func<DateTime> zamanKaynagi)
    {
        ZamanKaynagiAyarla(zamanKaynagi);
        return SimdiSanalZaman(zamanKaynagi());
    }

    /// <summary>
    /// Ana saatin mevcut sanal saniyesini hesaplar.
    /// </summary>
    public double SimdiSanalZaman(DateTime? simdi = null)
    {
        DateTime suan = SimdiZamani(simdi);
        lock (_kilit)
        {
            return SimdiSanalZamanKilitli(suan);
        }
    }

    private double SimdiSanalZamanKilitli(DateTime suan)
    {
        if (!_oynatiliyor || _sonGuncellemeZamani == null)
        {
            return _sanalZaman;
        }

        double gecenSn = (suan - _sonGuncellemeZamani.Value).TotalSeconds;
        if (gecenSn < 0) gecenSn = 0;
        return _sanalZaman + gecenSn * _hiz;
    }

    /// <summary>
    /// Oynatmayı başlatır.
    /// </summary>
    public void Oynat(DateTime? simdi = null)
    {
        DateTime suan = SimdiZamani(simdi);
        lock (_kilit)
        {
            if (!_oynatiliyor)
            {
                _oynatiliyor = true;
                _sonGuncellemeZamani = suan;
            }
        }
    }

    /// <summary>
    /// Oynatmayı duraklatır.
    /// </summary>
    public void Duraklat(DateTime? simdi = null)
    {
        DateTime suan = SimdiZamani(simdi);
        lock (_kilit)
        {
            if (_oynatiliyor)
            {
                _sanalZaman = SimdiSanalZamanKilitli(suan);
                _oynatiliyor = false;
                _sonGuncellemeZamani = suan;
            }
        }
    }

    /// <summary>
    /// Belirtilen sanal saniyeye atlar (seek).
    /// </summary>
    public void Ara(double sanalSn, DateTime? simdi = null)
    {
        DateTime suan = SimdiZamani(simdi);
        lock (_kilit)
        {
            _sanalZaman = Math.Max(0.0, sanalSn);
            _sonGuncellemeZamani = suan;

            foreach (var hafiza in _kanalDurumlari.Values)
            {
                hafiza.Sifirla();
            }
        }
    }

    /// <summary>
    /// Oynatma hızını ayarlar.
    /// </summary>
    public void HizAyarla(double yeniHiz, DateTime? simdi = null)
    {
        DateTime suan = SimdiZamani(simdi);
        lock (_kilit)
        {
            if (_oynatiliyor)
            {
                _sanalZaman = SimdiSanalZamanKilitli(suan);
                _sonGuncellemeZamani = suan;
            }
            _hiz = yeniHiz;
        }
    }

    /// <summary>
    /// Tek aktif ses kanalını seçer; diğer kanallar sessiz kalır.
    /// </summary>
    public void SesKanaliSec(string? kanalAdi)
    {
        lock (_kilit)
        {
            _aktifSesKanali = kanalAdi;
        }
    }

    /// <summary>
    /// Belirtilen kanalın sessiz (mute) olup olmadığını döner.
    /// </summary>
    public bool KanalSessizMi(string kanalAdi)
    {
        lock (_kilit)
        {
            return !string.Equals(_aktifSesKanali, kanalAdi, StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool KanalSessizMi(Kanal kanal) => KanalSessizMi(kanal.Ad);
    public bool Sessiz(string kanalAdi) => KanalSessizMi(kanalAdi);
    public bool Sessiz(Kanal kanal) => KanalSessizMi(kanal.Ad);

    /// <summary>
    /// Görünüm modunu değiştirir; oynatma durumu ve konumu tamamen korunur.
    /// </summary>
    public void GorunumModuDegistir(GorunumModu yeniMod)
    {
        ArgumentNullException.ThrowIfNull(yeniMod);
        lock (_kilit)
        {
            _gorunum = yeniMod switch
            {
                GorunumModu.Odak odak => odak.GetType() == typeof(GorunumModu.Odak) ? odak : new GorunumModu.Odak(odak.BuyukKanal),
                GorunumModu.Tek tek => tek.GetType() == typeof(GorunumModu.Tek) ? tek : new GorunumModu.Tek(tek.Kanal),
                GorunumModu.Izgara izgara => izgara.GetType() == typeof(GorunumModu.Izgara) ? izgara : new GorunumModu.Izgara(),
                _ => yeniMod
            };
        }
    }

    /// <summary>
    /// Kanal için durum etiketini döner.
    /// </summary>
    public string DurumEtiketi(Kanal kanal) => kanal.Guvenilir ? "senkron güvenilir" : "senkron güvenilmez";

    public string DurumEtiketi(string kanalAdi)
    {
        var k = Ders.KanalBul(kanalAdi);
        return k != null ? DurumEtiketi(k) : "bilinmeyen kanal";
    }

    /// <summary>
    /// Güvenilmez veya kaymış kanal için elle ek ofset uygular (yalnız bellek, ders.json'a yazma yok).
    /// </summary>
    public void ElleOfsetUygula(string kanalAdi, double ekSn)
    {
        lock (_kilit)
        {
            _elleOfsetler[kanalAdi] = ekSn;
        }
    }

    public void ElleOfsetUygula(Kanal kanal, double ekSn) => ElleOfsetUygula(kanal.Ad, ekSn);

    public double ElleOfsetAl(string kanalAdi)
    {
        lock (_kilit)
        {
            return _elleOfsetler.TryGetValue(kanalAdi, out var ek) ? ek : 0.0;
        }
    }

    public bool HisterezisAktifMi(string kanalAdi)
    {
        lock (_kilit)
        {
            return _kanalDurumlari.TryGetValue(kanalAdi, out var hafiza) && hafiza.HisterezisAktif;
        }
    }

    public double KanalHizKatsayisi(string kanalAdi)
    {
        lock (_kilit)
        {
            if (_kanalDurumlari.TryGetValue(kanalAdi, out var hafiza) && hafiza.HisterezisAktif)
            {
                return hafiza.SonHizKatsayisi;
            }
            return 1.0;
        }
    }

    /// <summary>
    /// Verilen kanalın bildirilen yerel süresine ve ana saate göre yapılması gereken eylemi belirler.
    /// </summary>
    public Eylem Karar(string kanalAdi, double bildirilenYerelSn, DateTime? simdi = null)
    {
        var kanal = Ders.KanalBul(kanalAdi) ?? throw new ArgumentException($"Kanal bulunamadı: {kanalAdi}", nameof(kanalAdi));
        return Karar(kanal, bildirilenYerelSn, simdi);
    }

    /// <summary>
    /// Verilen kanalın bildirilen yerel süresine ve ana saate göre yapılması gereken eylemi belirler.
    /// </summary>
    public Eylem Karar(Kanal kanal, double bildirilenYerelSn, DateTime? simdi = null)
    {
        DateTime suan = SimdiZamani(simdi);
        lock (_kilit)
        {
            double sanalSimdi = SimdiSanalZamanKilitli(suan);
            double ekOfset = _elleOfsetler.TryGetValue(kanal.Ad, out var ek) ? ek : 0.0;

            var (hedefDosya, hedefYerelSn, kayitVar) = kanal.SanalDenYerele(sanalSimdi, ekOfset);

            var hafiza = KanalHafizasiniAl(kanal.Ad);

            // Kayıt olmayan aralıkta gizle
            if (!kayitVar || hedefDosya == null)
            {
                hafiza.Sifirla();
                return new Eylem.Gizle("kayit yok/bitti");
            }

            double fark = hedefYerelSn - bildirilenYerelSn;
            double mutlakSapma = Math.Abs(fark);

            // 1. Sert atlama: mutlak sapma sert eşiği aşıyorsa
            if (mutlakSapma > SertAtlamaEsigi)
            {
                if (hafiza.SonDuzeltmeZamani != null && (suan - hafiza.SonDuzeltmeZamani.Value).TotalMilliseconds < 500)
                {
                    return new Eylem.Yok();
                }

                hafiza.SonDuzeltmeZamani = suan;
                hafiza.HisterezisAktif = false;
                hafiza.SonHizKatsayisi = 1.0;
                return new Eylem.Atla(hedefYerelSn);
            }

            // 2. Histerezis kontrolü:
            // Düzeltme sonrası sapma eşiğin yarısına inene kadar hızı koru
            double histerezisAltSiniri = IzinVerilenSapmaEsigi / 2.0;

            if (hafiza.HisterezisAktif)
            {
                if (mutlakSapma <= histerezisAltSiniri)
                {
                    // Sapma eşiğin yarısına indi: histerezis sonlanır
                    hafiza.HisterezisAktif = false;
                    hafiza.SonHizKatsayisi = 1.0;
                    return new Eylem.Yok();
                }

                // Histerezis devam ediyor: hızı koru
                if (hafiza.SonDuzeltmeZamani != null && (suan - hafiza.SonDuzeltmeZamani.Value).TotalMilliseconds < 500)
                {
                    return new Eylem.Yok();
                }

                hafiza.SonDuzeltmeZamani = suan;
                double korunanHiz = fark > 0 ? 1.03 : 0.97;
                hafiza.SonHizKatsayisi = korunanHiz;
                return new Eylem.HafifDuzelt(korunanHiz);
            }

            // 3. Normal durum: sapma eşiğin altındaysa düzeltme yok
            if (mutlakSapma <= IzinVerilenSapmaEsigi)
            {
                return new Eylem.Yok();
            }

            // 4. Hafif düzeltme: eşik < sapma <= sert atlama
            // Aşırı sık düzeltme önleme (en az 500 ms aralık)
            if (hafiza.SonDuzeltmeZamani != null && (suan - hafiza.SonDuzeltmeZamani.Value).TotalMilliseconds < 500)
            {
                return new Eylem.Yok();
            }

            double hizKatsayisi = fark > 0 ? 1.03 : 0.97;
            hafiza.SonDuzeltmeZamani = suan;
            hafiza.HisterezisAktif = true;
            hafiza.SonHizKatsayisi = hizKatsayisi;
            return new Eylem.HafifDuzelt(hizKatsayisi);
        }
    }

    private KanalHafizasi KanalHafizasiniAl(string kanalAdi)
    {
        if (!_kanalDurumlari.TryGetValue(kanalAdi, out var hafiza))
        {
            hafiza = new KanalHafizasi();
            _kanalDurumlari[kanalAdi] = hafiza;
        }
        return hafiza;
    }

    private class KanalHafizasi
    {
        public DateTime? SonDuzeltmeZamani { get; set; }
        public bool HisterezisAktif { get; set; }
        public double SonHizKatsayisi { get; set; } = 1.0;

        public void Sifirla()
        {
            SonDuzeltmeZamani = null;
            HisterezisAktif = false;
            SonHizKatsayisi = 1.0;
        }
    }
}
