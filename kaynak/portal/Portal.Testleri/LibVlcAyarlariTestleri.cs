namespace Portal.Testleri;

using Portal.Cekirdek;
using Xunit;

public class LibVlcAyarlariTestleri
{
    [Fact]
    public void SecenekleriBirlestir_VarsayilanlariVeEkleriYinelemesizBirArayaGetirir()
    {
        string[] sonuc = VlcSecenekleri.Birlestir(
        [
            "--avcodec-hw=any",
            "--quiet",
            "--quiet",
            "",
            "  "
        ]);

        string[] beklenen =
        [
            "--avcodec-hw=any",
            "--no-video-title-show",
            "--no-snapshot-preview",
            "--no-osd",
            "--file-caching=1500",
            "--network-caching=1500",
            "--drop-late-frames",
            "--skip-frames",
            "--sub-autodetect-file",
            "--quiet"
        ];

        Assert.Equal(beklenen, sonuc);
    }

    [Fact]
    public void SecenekleriBirlestir_EkListeOlmadanVarsayilanlariDondurur()
    {
        string[] sonuc = VlcSecenekleri.Birlestir(null);

        Assert.Equal(9, sonuc.Length);
        Assert.Equal(sonuc.Length, sonuc.Distinct(StringComparer.Ordinal).Count());
    }
}
