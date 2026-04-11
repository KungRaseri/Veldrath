using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Veldrath.Client.Services;

/// <summary>
/// Saves and loads the current user's token pair to/from an encrypted file on disk
/// using DPAPI (Windows Data Protection API) so tokens survive app restarts.
/// The file is scoped to the current Windows user account.
/// On non-Windows platforms all methods are no-ops.
/// </summary>
public sealed class TokenPersistenceService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Veldrath", "tokens.dat");

    [SupportedOSPlatform("windows")]
    public void Save(string accessToken, string refreshToken, string username,
        Guid accountId, DateTimeOffset expiry, bool isCurator)
    {
        var data = new TokenData(accessToken, refreshToken, username, accountId, expiry, isCurator);
        var json  = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, encrypted);
    }

    [SupportedOSPlatform("windows")]
    public TokenData? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(FilePath);
            var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json  = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<TokenData>(json);
        }
        catch
        {
            // File corrupted or from a different user — silently discard.
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    /// <summary>
    /// Saves the token data to persistent storage when running on Windows.
    /// A no-op on non-Windows platforms so call sites do not need platform guards.
    /// </summary>
    public void SaveCurrent(string accessToken, string refreshToken, string username,
        Guid accountId, DateTimeOffset expiry, bool isCurator)
    {
        if (!OperatingSystem.IsWindows()) return;
        Save(accessToken, refreshToken, username, accountId, expiry, isCurator);
    }
}

public sealed record TokenData(
    string          AccessToken,
    string          RefreshToken,
    string          Username,
    Guid            AccountId,
    DateTimeOffset  AccessTokenExpiry,
    bool            IsCurator);
