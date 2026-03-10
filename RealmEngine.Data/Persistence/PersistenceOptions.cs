namespace RealmEngine.Data.Persistence;

/// <summary>
/// Configures how RealmEngine persists save games and Hall of Fame entries.
/// </summary>
public class PersistenceOptions
{
    internal PersistenceMode Mode { get; private set; } = PersistenceMode.InMemory;

    /// <summary>Gets the SQLite connection string when <see cref="UseSqlite"/> was called.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Returns true when in-memory storage is active (default).</summary>
    public bool IsInMemory => Mode == PersistenceMode.InMemory;

    /// <summary>Returns true when SQLite file storage is active.</summary>
    public bool IsSqlite => Mode == PersistenceMode.Sqlite;

    /// <summary>Returns true when the host supplies its own repository implementations.</summary>
    public bool IsExternal => Mode == PersistenceMode.External;

    /// <summary>
    /// Use pure in-memory storage. No file I/O. Ideal for unit tests.
    /// </summary>
    public void UseInMemory() => Mode = PersistenceMode.InMemory;

    /// <summary>
    /// Use SQLite for local file-based persistence (standalone / Godot client).
    /// </summary>
    /// <param name="connectionString">SQLite connection string. Defaults to a local file.</param>
    public void UseSqlite(string connectionString = "Data Source=savegames.db")
    {
        Mode = PersistenceMode.Sqlite;
        ConnectionString = connectionString;
    }

    /// <summary>
    /// The caller (e.g. RealmUnbound.Server) will register its own
    /// <see cref="RealmEngine.Shared.Abstractions.ISaveGameRepository"/> and
    /// <see cref="RealmEngine.Shared.Abstractions.IHallOfFameRepository"/> implementations.
    /// <c>AddRealmEngineCore</c> will not register any persistence services.
    /// </summary>
    public void UseExternal() => Mode = PersistenceMode.External;
}

internal enum PersistenceMode
{
    InMemory,
    Sqlite,
    External,
}
