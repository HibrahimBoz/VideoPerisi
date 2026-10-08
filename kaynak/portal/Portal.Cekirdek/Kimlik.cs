namespace Portal.Cekirdek;

using System.Security.Principal;

/// <summary>
/// Kullanıcı ve makine kimlik bilgilerini sağlayan arayüz.
/// </summary>
public interface IKimlik
{
    string KullaniciAdi();
    string MakineAdi();
}

/// <summary>
/// Sistem kimlik bilgilerini sağlayan varsayılan sınıf.
/// </summary>
public class Kimlik : IKimlik
{
    private static IKimlik _varsayilan = new Kimlik();

    /// <summary>
    /// Testler veya özel durumlar için ayarlanabilir varsayılan kimlik örneği.
    /// </summary>
    public static IKimlik Varsayilan
    {
        get => _varsayilan;
        set => _varsayilan = value ?? new Kimlik();
    }



    /// <summary>
    /// Geçerli kullanıcının adını döndürür (WindowsIdentity veya ortam fallback).
    /// </summary>
    public string KullaniciAdi()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
#pragma warning disable CA1416
                var current = WindowsIdentity.GetCurrent();
                if (current != null && !string.IsNullOrWhiteSpace(current.Name))
                {
                    return current.Name;
                }
#pragma warning restore CA1416
            }
        }
        catch
        {
            // İzin kısıtlamasında sessiz devam et
        }

        string domain = Environment.UserDomainName;
        string user = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(user))
        {
            return $"{domain}\\{user}";
        }

        return string.IsNullOrWhiteSpace(user) ? "Kullanici" : user;
    }

    /// <summary>
    /// Geçerli makinenin adını döndürür.
    /// </summary>
    public string MakineAdi()
    {
        try
        {
            return Environment.MachineName;
        }
        catch
        {
            return "PC";
        }
    }
}
