namespace Portal.Testleri;

using System;
using System.Collections.Generic;
using System.Linq;
using Portal.Cekirdek;
using Xunit;

public class OynaticiYardimcisiTestleri
{
    [Fact]
    public void BaslangicZamaniHesapla_PozitifDeger_IkiSaniyeOnceyiVerir()
    {
        // 10 sn'den başlatılırsa 8 sn olmalı
        Assert.Equal(8.0, OynaticiYardimcisi.BaslangicZamaniHesapla(10.0));
        // 2.5 sn'den başlatılırsa 0.5 sn olmalı
        Assert.Equal(0.5, OynaticiYardimcisi.BaslangicZamaniHesapla(2.5));
    }

    [Fact]
    public void BaslangicZamaniHesapla_IkiSaniyeVeyaDahaKucuk_SifirVerir()
    {
        Assert.Equal(0.0, OynaticiYardimcisi.BaslangicZamaniHesapla(2.0));
        Assert.Equal(0.0, OynaticiYardimcisi.BaslangicZamaniHesapla(1.0));
        Assert.Equal(0.0, OynaticiYardimcisi.BaslangicZamaniHesapla(0.0));
        Assert.Equal(0.0, OynaticiYardimcisi.BaslangicZamaniHesapla(-5.0));
    }

    [Fact]
    public void BaslangicZamaniHesapla_GecersizDegerler_SifirVerir()
    {
        Assert.Equal(0.0, OynaticiYardimcisi.BaslangicZamaniHesapla(double.NaN));
        Assert.Equal(0.0, OynaticiYardimcisi.BaslangicZamaniHesapla(double.PositiveInfinity));
        Assert.Equal(0.0, OynaticiYardimcisi.BaslangicZamaniHesapla(double.NegativeInfinity));
    }

    [Fact]
    public void ZamanAtla_IleriVeGeri_SinirlaraGoreKirpar()
    {
        // +10 sn atlama
        Assert.Equal(25.0, OynaticiYardimcisi.ZamanAtla(15.0, 10.0, 100.0));
        // -10 sn atlama
        Assert.Equal(5.0, OynaticiYardimcisi.ZamanAtla(15.0, -10.0, 100.0));
        // 0'ın altına inemez
        Assert.Equal(0.0, OynaticiYardimcisi.ZamanAtla(5.0, -10.0, 100.0));
        // Azami sürenin üstüne çıkamaz
        Assert.Equal(100.0, OynaticiYardimcisi.ZamanAtla(95.0, 10.0, 100.0));
    }

    [Fact]
    public void AktifSegmentBul_ZamanAraliginaGore_DogruSegmentiDoner()
    {
        var segmentler = new List<EfektifSegment>
        {
            new(1, 0.0, 5.0, "Birinci segment", Array.Empty<Duzeltme>(), false),
            new(2, 5.1, 10.0, "İkinci segment", Array.Empty<Duzeltme>(), false),
            new(3, 10.1, 15.0, "Üçüncü segment", Array.Empty<Duzeltme>(), false)
        };

        var aktif1 = OynaticiYardimcisi.AktifSegmentBul(segmentler, 2.5);
        Assert.NotNull(aktif1);
        Assert.Equal(1, aktif1.No);

        var aktif2 = OynaticiYardimcisi.AktifSegmentBul(segmentler, 7.0);
        Assert.NotNull(aktif2);
        Assert.Equal(2, aktif2.No);

        var aktif3 = OynaticiYardimcisi.AktifSegmentBul(segmentler, 12.0);
        Assert.NotNull(aktif3);
        Assert.Equal(3, aktif3.No);
    }

    [Fact]
    public void AktifSegmentBul_AralikDisindaVeyaBos_NullDoner()
    {
        var segmentler = new List<EfektifSegment>
        {
            new(1, 5.0, 10.0, "Tek segment", Array.Empty<Duzeltme>(), false)
        };

        Assert.Null(OynaticiYardimcisi.AktifSegmentBul(segmentler, 2.0));
        Assert.Null(OynaticiYardimcisi.AktifSegmentBul(segmentler, 15.0));
        Assert.Null(OynaticiYardimcisi.AktifSegmentBul(segmentler, -1.0));
        Assert.Null(OynaticiYardimcisi.AktifSegmentBul(null, 5.0));
        Assert.Null(OynaticiYardimcisi.AktifSegmentBul(new List<EfektifSegment>(), 5.0));
    }

    [Fact]
    public void AktifSegmentIndexiBul_DogruIndexiDoner()
    {
        var segmentler = new List<EfektifSegment>
        {
            new(1, 0.0, 4.0, "A", Array.Empty<Duzeltme>(), false),
            new(2, 4.0, 8.0, "B", Array.Empty<Duzeltme>(), false),
            new(3, 8.0, 12.0, "C", Array.Empty<Duzeltme>(), false)
        };

        Assert.Equal(0, OynaticiYardimcisi.AktifSegmentIndexiBul(segmentler, 2.0));
        Assert.Equal(1, OynaticiYardimcisi.AktifSegmentIndexiBul(segmentler, 6.0));
        Assert.Equal(2, OynaticiYardimcisi.AktifSegmentIndexiBul(segmentler, 10.0));
        Assert.Equal(-1, OynaticiYardimcisi.AktifSegmentIndexiBul(segmentler, 20.0));
        Assert.Equal(-1, OynaticiYardimcisi.AktifSegmentIndexiBul(null, 2.0));
    }

    [Fact]
    public void SegmentFiltrele_TurkceHarfDuyarsiz_DogruSonucDoner()
    {
        var segmentler = new List<EfektifSegment>
        {
            new(1, 0.0, 5.0, "İstanbul Boğazı manzarası", Array.Empty<Duzeltme>(), false),
            new(2, 5.0, 10.0, "Şiir ve edebiyat dersi", Array.Empty<Duzeltme>(), false),
            new(3, 10.0, 15.0, "Ağaç dikme bayramı", Array.Empty<Duzeltme>(), false)
        };

        // 'istanbul' araması 'İstanbul' ile eşleşmeli
        var sonuc1 = OynaticiYardimcisi.SegmentFiltrele(segmentler, "istanbul").ToList();
        Assert.Single(sonuc1);
        Assert.Equal(1, sonuc1[0].No);

        // 'siir' araması 'Şiir' ile eşleşmeli
        var sonuc2 = OynaticiYardimcisi.SegmentFiltrele(segmentler, "siir").ToList();
        Assert.Single(sonuc2);
        Assert.Equal(2, sonuc2[0].No);

        // 'agac' araması 'Ağaç' ile eşleşmeli
        var sonuc3 = OynaticiYardimcisi.SegmentFiltrele(segmentler, "agac").ToList();
        Assert.Single(sonuc3);
        Assert.Equal(3, sonuc3[0].No);
    }

    [Fact]
    public void SegmentFiltrele_BosVeyaNullSorgu_TumListeyiDoner()
    {
        var segmentler = new List<EfektifSegment>
        {
            new(1, 0.0, 5.0, "Cümle 1", Array.Empty<Duzeltme>(), false),
            new(2, 5.0, 10.0, "Cümle 2", Array.Empty<Duzeltme>(), false)
        };

        Assert.Equal(2, OynaticiYardimcisi.SegmentFiltrele(segmentler, null).Count());
        Assert.Equal(2, OynaticiYardimcisi.SegmentFiltrele(segmentler, "").Count());
        Assert.Equal(2, OynaticiYardimcisi.SegmentFiltrele(segmentler, "   ").Count());
        Assert.Empty(OynaticiYardimcisi.SegmentFiltrele(null, "arama"));
    }

    [Fact]
    public void GecerliHizMi_DesteklenenHizlar_DogruDogrulanir()
    {
        Assert.True(OynaticiYardimcisi.GecerliHizMi(0.5));
        Assert.True(OynaticiYardimcisi.GecerliHizMi(0.75));
        Assert.True(OynaticiYardimcisi.GecerliHizMi(1.0));
        Assert.True(OynaticiYardimcisi.GecerliHizMi(1.25));
        Assert.True(OynaticiYardimcisi.GecerliHizMi(1.5));
        Assert.True(OynaticiYardimcisi.GecerliHizMi(2.0));

        Assert.False(OynaticiYardimcisi.GecerliHizMi(0.0));
        Assert.False(OynaticiYardimcisi.GecerliHizMi(0.8));
        Assert.False(OynaticiYardimcisi.GecerliHizMi(1.1));
        Assert.False(OynaticiYardimcisi.GecerliHizMi(3.0));
        Assert.False(OynaticiYardimcisi.GecerliHizMi(-1.0));
        Assert.False(OynaticiYardimcisi.GecerliHizMi(double.NaN));
    }
}
