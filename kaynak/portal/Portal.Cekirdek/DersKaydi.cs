namespace Portal.Cekirdek;

/// <summary>
/// Taranan ders kaydının konum ve video bilgilerini temsil eder.
/// </summary>
public record DersKaydi(
    string Ad,
    string KlasorYolu,
    string JsonYolu,
    string? VideoYolu
);
