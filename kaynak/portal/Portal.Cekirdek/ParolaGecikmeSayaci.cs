namespace Portal.Cekirdek;

/// <summary>
/// Düzenleme modu parola denemelerinde hatalı giriş sayısını ve kilitlenme süresini takip eden saf sınıf.
/// 5 hatalı denemede 30 saniye kilit uygular.
/// </summary>
public class ParolaGecikmeSayaci
{
    public const int VarsayilanAzamiHataliDeneme = 5;
    public const int VarsayilanKilitSuresiSaniye = 30;

    public static ParolaGecikmeSayaci Paylasilan { get; } = new();

    private readonly object _kilit = new();
    private int _hataliDeneme;
    private DateTime _kilitZamaniUtc = DateTime.MinValue;

    public int AzamiHataliDeneme { get; }
    public int KilitSuresiSaniye { get; }

    public int HataliDenemeSayisi
    {
        get { lock (_kilit) return _hataliDeneme; }
    }

    public ParolaGecikmeSayaci(
        int azamiHataliDeneme = VarsayilanAzamiHataliDeneme,
        int kilitSuresiSaniye = VarsayilanKilitSuresiSaniye)
    {
        AzamiHataliDeneme = Math.Max(1, azamiHataliDeneme);
        KilitSuresiSaniye = Math.Max(1, kilitSuresiSaniye);
    }

    /// <summary>
    /// Sayacın şu anda kilitli olup olmadığını döner.
    /// </summary>
    public bool KilitliMi(DateTime? simdiUtc = null)
    {
        lock (_kilit)
        {
            if (_hataliDeneme < AzamiHataliDeneme) return false;
            var simdi = simdiUtc ?? DateTime.UtcNow;
            return (simdi - _kilitZamaniUtc).TotalSeconds < KilitSuresiSaniye;
        }
    }

    /// <summary>
    /// Kilit açılana kadar kalan saniye miktarını döner. Kilitli değilse 0 döner.
    /// </summary>
    public int KalanBeklemeSaniyesi(DateTime? simdiUtc = null)
    {
        lock (_kilit)
        {
            if (_hataliDeneme < AzamiHataliDeneme) return 0;
            var simdi = simdiUtc ?? DateTime.UtcNow;
            double gecen = (simdi - _kilitZamaniUtc).TotalSeconds;
            if (gecen >= KilitSuresiSaniye) return 0;
            return (int)Math.Ceiling(KilitSuresiSaniye - gecen);
        }
    }

    /// <summary>
    /// Yeni bir denemenin yapılıp yapılamayacağını döner. Kilit süresi dolmuşsa kilit durumunu temizler.
    /// </summary>
    public bool DenemeYapilabilirMi(DateTime? simdiUtc = null)
    {
        lock (_kilit)
        {
            var simdi = simdiUtc ?? DateTime.UtcNow;
            if (_hataliDeneme >= AzamiHataliDeneme)
            {
                if ((simdi - _kilitZamaniUtc).TotalSeconds >= KilitSuresiSaniye)
                {
                    _hataliDeneme = 0;
                    _kilitZamaniUtc = DateTime.MinValue;
                    return true;
                }
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Hatalı bir parola girişini kaydeder. Eşik aşılırsa kilit zamanını başlatır.
    /// Kilit süresi geçmişse önceki kilidi sıfırlayıp yeni denemeyi başlatır.
    /// </summary>
    public void HataliGiris(DateTime? zamanUtc = null)
    {
        lock (_kilit)
        {
            var simdi = zamanUtc ?? DateTime.UtcNow;
            if (_hataliDeneme >= AzamiHataliDeneme && (simdi - _kilitZamaniUtc).TotalSeconds >= KilitSuresiSaniye)
            {
                _hataliDeneme = 0;
                _kilitZamaniUtc = DateTime.MinValue;
            }

            _hataliDeneme++;
            if (_hataliDeneme >= AzamiHataliDeneme)
            {
                _kilitZamaniUtc = simdi;
            }
        }
    }

    /// <summary>
    /// Başarılı parola girişini kaydeder; sayaç ve kilidi sıfırlar.
    /// </summary>
    public void BasariliGiris()
    {
        Sifirla();
    }

    /// <summary>
    /// Sayaç ve kilidi sıfırlar.
    /// </summary>
    public void Sifirla()
    {
        lock (_kilit)
        {
            _hataliDeneme = 0;
            _kilitZamaniUtc = DateTime.MinValue;
        }
    }
}
