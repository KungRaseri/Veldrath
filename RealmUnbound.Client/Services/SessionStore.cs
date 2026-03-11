using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealmUnbound.Client.Services;

/// <summary>
/// Persists lightweight session preferences to disk so the login screen can
/// pre-fill the user's email address on next launch.
///
/// Security note: Only the email address is ever written to disk.
/// Passwords and tokens are never persisted here.
/// </summary>
public class SessionStore
{
    private static readonly string DefaultFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RealmUnbound", "session.json");

    private readonly string _filePath;
    private readonly ILogger<SessionStore> _logger;

    /// <summary>The email address saved from the last successful login, or null if none.</summary>
    public string? SavedEmail { get; private set; }

    public bool HasSavedEmail => SavedEmail is not null;

    public SessionStore(ILogger<SessionStore> logger, string? filePath = null)
    {
        _logger   = logger;
        _filePath = filePath ?? DefaultFilePath;
        Load();
    }

    /// <summary>Persists <paramref name="email"/> so it can be pre-filled next launch.</summary>
    public void SaveEmail(string email)
    {
        SavedEmail = email;
        Persist();
    }

    /// <summary>Removes any saved email from disk and memory.</summary>
    public void ClearEmail()
    {
        SavedEmail = null;
        Persist();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<SessionData>(json);
            SavedEmail = data?.Email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load session file — starting fresh");
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(new SessionData(SavedEmail));
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save session file");
        }
    }

    private record SessionData(string? Email);
}
