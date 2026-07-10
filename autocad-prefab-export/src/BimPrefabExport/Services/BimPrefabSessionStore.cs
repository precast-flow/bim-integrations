using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BimPrefabExport.Services;

/// <summary>
/// Oturum ayarları: API adresi, JWT, seçili proje. Token DPAPI ile şifrelenir.
/// Dosya: %LOCALAPPDATA%\BimPrefabExport\session.dat
/// </summary>
public sealed class BimPrefabSessionStore
{
    private static readonly byte[] Entropy = "BimPrefabExport.PrecastFlow.v1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static BimPrefabSessionStore Default { get; } = new();

    public string ConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BimPrefabExport");

    public string SessionFilePath => Path.Combine(ConfigDirectory, "session.dat");

    public string DefaultApiBaseUrl =>
        ResolveDefaultApiBaseUrl();

    private static string ResolveDefaultApiBaseUrl()
    {
        var envUrl = Environment.GetEnvironmentVariable("PRECASTFLOW_API_URL")?.Trim();
        if (!string.IsNullOrWhiteSpace(envUrl))
            return envUrl.TrimEnd('/');

        // Parallels: Windows VM → Mac host (override with PRECASTFLOW_API_URL if needed)
        var parallelsHost = Environment.GetEnvironmentVariable("PRECASTFLOW_PARALLELS_HOST")?.Trim();
        if (!string.IsNullOrWhiteSpace(parallelsHost))
            return $"http://{parallelsHost.TrimEnd('/')}:5255";

        return "http://10.211.55.2:5255";
    }

    public BimPrefabSessionData Load()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
                return new BimPrefabSessionData { ApiBaseUrl = DefaultApiBaseUrl };

            var raw = File.ReadAllBytes(SessionFilePath);
            var json = Unprotect(raw);
            var data = JsonSerializer.Deserialize<BimPrefabSessionData>(json, JsonOptions);
            if (data is null)
                return new BimPrefabSessionData { ApiBaseUrl = DefaultApiBaseUrl };

            if (string.IsNullOrWhiteSpace(data.ApiBaseUrl))
                data.ApiBaseUrl = DefaultApiBaseUrl;

            return data;
        }
        catch (Exception ex)
        {
            BimPrefabLog.Info($"Oturum dosyası okunamadı: {ex.Message}");
            return new BimPrefabSessionData { ApiBaseUrl = DefaultApiBaseUrl };
        }
    }

    public void Save(BimPrefabSessionData data)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllBytes(SessionFilePath, Protect(json));
    }

    public void ClearToken()
    {
        var data = Load();
        data.AccessToken = null;
        data.UserFullName = null;
        Save(data);
    }

    private static byte[] Protect(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
    }

    private static string Unprotect(byte[] protectedBytes)
    {
        var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}

public sealed class BimPrefabSessionData
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5255";
    public string? Email { get; set; }
    public string? UserFullName { get; set; }
    public string? AccessToken { get; set; }
    public Guid? SelectedProjectId { get; set; }
    public string? SelectedProjectLabel { get; set; }
}
