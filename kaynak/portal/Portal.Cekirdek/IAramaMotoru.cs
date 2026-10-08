namespace Portal.Cekirdek;

/// <summary>
/// Ders transkriptleri üzerinde metin ve anlam tabanlı arama motoru arayüzü.
/// </summary>
public interface IAramaMotoru
{
    /// <summary>
    /// Verilen ders kayıtlarını indeksler.
    /// </summary>
    Task IndeksleAsync(IEnumerable<DersKaydi> dersler, IProgress<int>? ilerleme = null, CancellationToken iptal = default);

    /// <summary>
    /// Sorgu ifadesine göre indekslenmiş segmentlerde arama yapar.
    /// </summary>
    IReadOnlyList<AramaSonucu> Ara(string sorgu, int en_fazla = 50);

    /// <summary>
    /// Düzeltme sonrası tek bir dersin indeksini günceller.
    /// </summary>
    void DersiYenile(DersKaydi ders);
}
