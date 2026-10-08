namespace Portal.Uygulama;

using System;
using System.IO;
using LibVLCSharp.Shared;
using Portal.Cekirdek;

/// <summary>
/// LibVLC tabanlı IOynatici uygulaması (T11B).
/// </summary>
public sealed class LibVlcOynatici : IOynatici, IDisposable
{
    private readonly bool _kendiLibVlc;
    private readonly bool _kendiPlayer;
    private bool _disposed;

    public LibVLC LibVlc { get; }
    public MediaPlayer MediaPlayer { get; }
    public Media? Medya { get; private set; }

    public LibVlcOynatici(LibVLC? libVlc = null, MediaPlayer? mediaPlayer = null)
    {
        if (libVlc == null)
        {
            Core.Initialize();
            LibVlc = new LibVLC();
            _kendiLibVlc = true;
        }
        else
        {
            LibVlc = libVlc;
            _kendiLibVlc = false;
        }

        if (mediaPlayer == null)
        {
            MediaPlayer = new MediaPlayer(LibVlc);
            _kendiPlayer = true;
        }
        else
        {
            MediaPlayer = mediaPlayer;
            _kendiPlayer = false;
        }
    }

    /// <summary>
    /// Belirtilen dosya yolundan medyayı yükler.
    /// </summary>
    public void Yukle(string dosyaYolu)
    {
        if (string.IsNullOrWhiteSpace(dosyaYolu))
        {
            throw new ArgumentException("Dosya yolu boş olamaz.", nameof(dosyaYolu));
        }

        if (!File.Exists(dosyaYolu))
        {
            throw new FileNotFoundException("Video dosyası bulunamadı.", dosyaYolu);
        }

        var eskiMedya = Medya;
        MediaPlayer.Stop();

        var yeniMedya = new Media(LibVlc, VlcYolu.Hazirla(dosyaYolu), FromType.FromPath);
        MediaPlayer.Media = yeniMedya;
        Medya = yeniMedya;

        eskiMedya?.Dispose();
    }

    /// <summary>
    /// Oynatmayı başlatır veya duraklatır.
    /// </summary>
    public void OynatDuraklat()
    {
        if (MediaPlayer.IsPlaying)
        {
            MediaPlayer.Pause();
        }
        else
        {
            MediaPlayer.Play();
        }
    }

    /// <summary>
    /// Belirtilen saniyeye atlar.
    /// </summary>
    public void Ara(double saniye)
    {
        long ms = (long)Math.Max(0, Math.Round(saniye * 1000.0));
        MediaPlayer.Time = ms;
    }

    /// <summary>
    /// Anlık oynatma zamanı (saniye cinsinden).
    /// </summary>
    public double Zaman => MediaPlayer.Time >= 0 ? MediaPlayer.Time / 1000.0 : 0.0;

    /// <summary>
    /// Toplam süre (saniye cinsinden).
    /// </summary>
    public double ToplamSure => MediaPlayer.Length > 0 ? MediaPlayer.Length / 1000.0 : 0.0;

    /// <summary>
    /// Oynatma hızı (0.25 - 4.0 arasında).
    /// </summary>
    public double Hiz
    {
        get => MediaPlayer.Rate;
        set => MediaPlayer.SetRate((float)Math.Clamp(value, 0.25, 4.0));
    }

    /// <summary>
    /// Ses seviyesi (0 - 100 arasında).
    /// </summary>
    public int SesSeviyesi
    {
        get => MediaPlayer.Volume;
        set => MediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Şu anda oynatılıyor mu?
    /// </summary>
    public bool Oynatiliyor => MediaPlayer.IsPlaying;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            MediaPlayer.Stop();
            MediaPlayer.Media = null;
        }
        catch
        {
            // Oynatıcı durdurma hatasını yut
        }

        Medya?.Dispose();
        Medya = null;

        if (_kendiPlayer)
        {
            MediaPlayer.Dispose();
        }

        if (_kendiLibVlc)
        {
            LibVlc.Dispose();
        }
    }
}
