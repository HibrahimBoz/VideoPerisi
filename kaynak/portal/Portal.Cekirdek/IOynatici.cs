namespace Portal.Cekirdek;

/// <summary>
/// Video oynatıcı denetim arayüzü (T8 madde 5 gereksinimi).
/// </summary>
public interface IOynatici
{
    void Yukle(string dosyaYolu);
    void OynatDuraklat();
    void Ara(double saniye);
    double Zaman { get; }
    double Hiz { get; set; }
    int SesSeviyesi { get; set; }
}
