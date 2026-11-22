using System.Text;
using shitbotteam.ultra.desktop.Models;

namespace shitbotteam.ultra.desktop.Services;

public static class CoreCredentialReader
{
    public static async Task<string?> TryReadAdminTokenAsync(
        string configDirectory,
        CancellationToken cancellationToken = default)
    {
        var path = CorePaths.GetAdminCredentialPath(configDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length is <= 0 or > 8192)
        {
            throw new InvalidDataException("The local admin token file size is outside the supported range.");
        }

        var token = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        if (token.Length == 0)
        {
            return null;
        }

        var byteCount = Encoding.UTF8.GetByteCount(token);
        if (byteCount is < 32 or > 4096 || token.Contains('\r') || token.Contains('\n') || token.Contains('\0'))
        {
            throw new InvalidDataException("The local admin token file does not contain a valid credential.");
        }

        return token;
    }
}
