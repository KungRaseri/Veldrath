using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Client.Services;

namespace Veldrath.Client.Tests;

public class SessionStoreTests : TestBase
{
    // Each test gets its own isolated temp file so tests don't interfere with
    // each other or the real AppData session file.
    private static SessionStore MakeStore(out string tempFile)
    {
        tempFile = Path.Combine(Path.GetTempPath(), $"realm-session-test-{Guid.NewGuid()}.json");
        return new SessionStore(NullLogger<SessionStore>.Instance, tempFile);
    }

    private static SessionStore MakeStore() => MakeStore(out _);

    // Initial state
    [Fact]
    public void HasSavedEmail_Should_Be_False_On_Fresh_Store()
    {
        var store = MakeStore();
        store.HasSavedEmail.Should().BeFalse();
    }

    [Fact]
    public void SavedEmail_Should_Be_Null_On_Fresh_Store()
    {
        var store = MakeStore();
        store.SavedEmail.Should().BeNull();
    }

    // SaveEmail
    [Fact]
    public void SaveEmail_Should_Set_SavedEmail()
    {
        var store = MakeStore();
        store.SaveEmail("test@example.com");
        store.SavedEmail.Should().Be("test@example.com");
    }

    [Fact]
    public void SaveEmail_Should_Set_HasSavedEmail_True()
    {
        var store = MakeStore();
        store.SaveEmail("test@example.com");
        store.HasSavedEmail.Should().BeTrue();
    }

    [Fact]
    public void SaveEmail_Should_Overwrite_Previous_Email()
    {
        var store = MakeStore();
        store.SaveEmail("first@example.com");
        store.SaveEmail("second@example.com");
        store.SavedEmail.Should().Be("second@example.com");
    }

    // ClearEmail
    [Fact]
    public void ClearEmail_Should_Remove_SavedEmail()
    {
        var store = MakeStore();
        store.SaveEmail("test@example.com");

        store.ClearEmail();

        store.SavedEmail.Should().BeNull();
    }

    [Fact]
    public void ClearEmail_Should_Set_HasSavedEmail_False()
    {
        var store = MakeStore();
        store.SaveEmail("test@example.com");

        store.ClearEmail();

        store.HasSavedEmail.Should().BeFalse();
    }

    [Fact]
    public void ClearEmail_On_Empty_Store_Should_Not_Throw()
    {
        var store = MakeStore();
        var act = () => store.ClearEmail();
        act.Should().NotThrow();
    }

    // Persist round-trip
    [Fact]
    public void SaveEmail_Should_Persist_To_Disk_And_Load_On_Next_Construction()
    {
        MakeStore(out var tempFile).SaveEmail("persist@example.com");

        // New instance reads from same file
        var store2 = new SessionStore(NullLogger<SessionStore>.Instance, tempFile);

        store2.SavedEmail.Should().Be("persist@example.com");
        store2.HasSavedEmail.Should().BeTrue();
    }

    [Fact]
    public void ClearEmail_Should_Persist_Null_To_Disk()
    {
        MakeStore(out var tempFile).SaveEmail("clear@example.com");

        var store2 = new SessionStore(NullLogger<SessionStore>.Instance, tempFile);
        store2.ClearEmail();

        var store3 = new SessionStore(NullLogger<SessionStore>.Instance, tempFile);
        store3.HasSavedEmail.Should().BeFalse();
    }

    [Fact]
    public void SaveEmail_Then_ClearEmail_Should_Leave_Store_Empty()
    {
        var store = MakeStore();
        store.SaveEmail("cycle@example.com");
        store.ClearEmail();

        store.HasSavedEmail.Should().BeFalse();
        store.SavedEmail.Should().BeNull();
    }

    // Error resilience
    [Fact]
    public void Load_Should_Not_Throw_When_File_Contains_Invalid_Json()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"realm-bad-json-{Guid.NewGuid()}.json");
        File.WriteAllText(tempFile, "{ not valid json !!!");

        // Should survive a corrupt file without throwing
        var act = () => new SessionStore(NullLogger<SessionStore>.Instance, tempFile);
        act.Should().NotThrow();
    }

    [Fact]
    public void SaveEmail_Should_Not_Throw_When_Directory_Cannot_Be_Created()
    {
        // Use a path with a null byte which is illegal on all platforms — Persist will catch the exception
        var badPath = Path.Combine(Path.GetTempPath(), "realm\0bad", "session.json");
        var store   = new SessionStore(NullLogger<SessionStore>.Instance, badPath);

        // SaveEmail calls Persist which will fail silently
        var act = () => store.SaveEmail("test@example.com");
        act.Should().NotThrow();

        // In-memory state is still updated even though persist failed
        store.SavedEmail.Should().Be("test@example.com");
    }

    // Default file path
    [Fact]
    public void Constructor_Should_Use_DefaultFilePath_When_No_Path_Provided()
    {
        // Exercises the DefaultFilePath static field initializer and the ?? branch
        var act = () => new SessionStore(NullLogger<SessionStore>.Instance);
        act.Should().NotThrow();
    }
}
