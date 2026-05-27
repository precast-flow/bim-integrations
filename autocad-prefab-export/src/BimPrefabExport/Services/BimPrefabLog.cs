using System.Globalization;
using System.IO;

namespace BimPrefabExport.Services;

/// <summary>
/// Eklenti tanılama logu: komut satırından bağımsız kalıcı kayıt.
/// </summary>
public static class BimPrefabLog
{
    private static readonly object Gate = new();

    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BimPrefabExport",
            "bim-prefab.log");

    public static void Info(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}  {message}{Environment.NewLine}";
            lock (Gate)
                File.AppendAllText(LogFilePath, line);
        }
        catch
        {
            // log yazılamazsa sessiz
        }
    }
}
