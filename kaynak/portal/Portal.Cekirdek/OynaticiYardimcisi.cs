namespace Portal.Cekirdek;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tek video oynatıcı ve transkript paneli için saf yardımcı fonksiyonlar (T11B).
/// </summary>
public static class OynaticiYardimcisi
{
    /// <summary>
    /// Desteklenen oynatma hızları: 0.5, 0.75, 1.0, 1.25, 1.5, 2.0.
    /// </summary>
    public static readonly double[] DesteklenenHizlar = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];

    /// <summary>
    /// Başlangıç saniyesinden 2 saniye öncesini hesaplar (en az 0 saniye).
    /// </summary>
    public static double BaslangicZamaniHesapla(double baslangicSn)
    {
        if (double.IsNaN(baslangicSn) || double.IsInfinity(baslangicSn) || baslangicSn <= 2.0)
        {
            return 0.0;
        }

        return Math.Max(0.0, baslangicSn - 2.0);
    }

    /// <summary>
    /// Anlık zamana delta (ör. ±10 sn) ekler ve 0 ile azami süre arasına kırpar.
    /// </summary>
    public static double ZamanAtla(double anlikSn, double deltaSn, double azamiSn = double.MaxValue)
    {
        if (double.IsNaN(anlikSn) || double.IsInfinity(anlikSn) || anlikSn < 0)
        {
            anlikSn = 0.0;
        }

        if (double.IsNaN(deltaSn) || double.IsInfinity(deltaSn))
        {
            deltaSn = 0.0;
        }

        if (double.IsNaN(azamiSn) || double.IsInfinity(azamiSn) || azamiSn < 0)
        {
            azamiSn = double.MaxValue;
        }

        double hedef = anlikSn + deltaSn;
        return Math.Clamp(hedef, 0.0, azamiSn);
    }

    /// <summary>
    /// Belirtilen anlık saniyede oynatılan aktif segmenti bulur.
    /// </summary>
    public static EfektifSegment? AktifSegmentBul(IEnumerable<EfektifSegment>? segmentler, double anlikSn)
    {
        if (segmentler == null || double.IsNaN(anlikSn) || double.IsInfinity(anlikSn) || anlikSn < 0)
        {
            return null;
        }

        return segmentler.FirstOrDefault(s => anlikSn >= s.Bas && anlikSn <= s.Son);
    }

    /// <summary>
    /// Belirtilen anlık saniyede oynatılan segmentin dizinini bulur.
    /// Eşleşme yoksa -1 döner.
    /// </summary>
    public static int AktifSegmentIndexiBul(IReadOnlyList<EfektifSegment>? segmentler, double anlikSn)
    {
        if (segmentler == null || double.IsNaN(anlikSn) || double.IsInfinity(anlikSn) || anlikSn < 0)
        {
            return -1;
        }

        for (int i = 0; i < segmentler.Count; i++)
        {
            if (anlikSn >= segmentler[i].Bas && anlikSn <= segmentler[i].Son)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Segment metni içinde arama sorgusunu filtreler (Türkçe harf duyarsız normalizasyon ile).
    /// Sorgu boş veya null ise tüm segmentleri döner.
    /// </summary>
    public static IEnumerable<EfektifSegment> SegmentFiltrele(IEnumerable<EfektifSegment>? segmentler, string? aramaMetni)
    {
        if (segmentler == null)
        {
            return Enumerable.Empty<EfektifSegment>();
        }

        if (string.IsNullOrWhiteSpace(aramaMetni))
        {
            return segmentler;
        }

        string arananNorm = TrNormalizasyon.Normalle(aramaMetni);
        if (string.IsNullOrEmpty(arananNorm))
        {
            return segmentler;
        }

        return segmentler.Where(s =>
        {
            if (string.IsNullOrWhiteSpace(s.Metin))
            {
                return false;
            }

            string metinNorm = TrNormalizasyon.Normalle(s.Metin);
            return metinNorm.Contains(arananNorm, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Verilen oynatma hızının desteklenen hız listesinde olup olmadığını denetler.
    /// </summary>
    public static bool GecerliHizMi(double hiz)
    {
        if (double.IsNaN(hiz) || double.IsInfinity(hiz))
        {
            return false;
        }

        return DesteklenenHizlar.Any(h => Math.Abs(h - hiz) < 0.001);
    }
}
