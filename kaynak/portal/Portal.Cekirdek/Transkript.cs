namespace Portal.Cekirdek;

/// <summary>
/// Transkript bilgilerini ve segment listesini temsil eder.
/// </summary>
public class Transkript
{
    public string Ad { get; init; } = string.Empty;
    public IReadOnlyList<Segment> Segmentler { get; init; } = Array.Empty<Segment>();
    public bool EskiBicim { get; init; }
    public string? KaynakOzet { get; init; }
    public double SureSn { get; init; }
    public string JsonYolu { get; init; } = string.Empty;
}
