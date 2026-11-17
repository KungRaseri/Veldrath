using FluentAssertions;
using Veldrath.Server.Infrastructure;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// Runs alone: <see cref="DotEnvConfiguration.LoadForDevelopment"/> writes process environment
/// variables, which are shared by every test in the assembly, so these cases must not overlap with a
/// test that boots a server.
/// </summary>
[CollectionDefinition("DotEnv", DisableParallelization = true)]
public sealed class DotEnvCollection
{
    // Nothing but the collection identity: the definition exists to serialise the class below.
}

/// <summary>
/// Tests for <see cref="DotEnvConfiguration"/>: the loader that reads the repository <c>.env</c> for a
/// local <c>dotnet run</c> and copies the service-friendly names onto ASP.NET configuration keys.
/// </summary>
/// <remarks>
/// It is startup glue, so it had no tests and sat at 5.9% branch coverage. The branches that matter
/// are the ones that decide whether a developer's local run works: comment and export lines, quoted
/// values, unknown keys, an already-set variable that must win, and the environment gate.
/// </remarks>
[Collection("DotEnv")]
public class DotEnvConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "veldrath-dotenv-" + Guid.NewGuid().ToString("N"));

    public DotEnvConfigurationTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Writes the file the loader walks up to find.</summary>
    /// <param name="directory">Directory the file is written into.</param>
    /// <param name="lines">Raw lines of the .env file.</param>
    /// <returns>The directory, so a content root can be nested below it.</returns>
    private string WriteEnv(string directory, params string[] lines)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, ".env"), lines);
        return directory;
    }

    private string Subdirectory(string parent, params string[] names)
    {
        var path = parent;
        foreach (var name in names) path = Path.Combine(path, name);

        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Copies_an_aliased_key_onto_its_configuration_name()
    {
        using var env = new EnvironmentScope("ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "Jwt__Key");
        env.Set("ASPNETCORE_ENVIRONMENT", "Development");

        var root = WriteEnv(_root, "JWT_KEY=local-secret");

        DotEnvConfiguration.LoadForDevelopment(Subdirectory(root, "src", "Veldrath.Server"));

        Environment.GetEnvironmentVariable("Jwt__Key").Should().Be("local-secret");
    }

    [Fact]
    public void Never_overrides_a_variable_that_is_already_set()
    {
        using var env = new EnvironmentScope("ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "Jwt__Key");
        env.Set("ASPNETCORE_ENVIRONMENT", "Development");
        env.Set("Jwt__Key", "from-the-shell");

        var root = WriteEnv(_root, "JWT_KEY=from-the-file");

        DotEnvConfiguration.LoadForDevelopment(root);

        Environment.GetEnvironmentVariable("Jwt__Key").Should().Be("from-the-shell", "the shell is the developer's override");
    }

    [Fact]
    public void Skips_comments_blank_lines_and_keys_it_does_not_know()
    {
        using var env = new EnvironmentScope(
            "ASPNETCORE_ENVIRONMENT",
            "DOTNET_ENVIRONMENT",
            "Jwt__Key",
            "Email__User");
        env.Set("ASPNETCORE_ENVIRONMENT", "Development");

        var root = WriteEnv(
            _root,
            "# a comment",
            string.Empty,
            "unknown_key=ignored",
            "MISSING_SEPARATOR",
            "=leading-separator",
            "JWT_KEY=used");

        DotEnvConfiguration.LoadForDevelopment(root);

        Environment.GetEnvironmentVariable("Jwt__Key").Should().Be("used");
        Environment.GetEnvironmentVariable("Email__User").Should().BeNull();
    }

    [Fact]
    public void Strips_export_and_matching_quotes_and_trims_whitespace()
    {
        using var env = new EnvironmentScope(
            "ASPNETCORE_ENVIRONMENT",
            "DOTNET_ENVIRONMENT",
            "Jwt__Key",
            "Email__User",
            "Email__Password",
            "Email__SmtpHost",
            "Email__SmtpPort",
            "Email__EnableTls");
        env.Set("ASPNETCORE_ENVIRONMENT", "Development");

        var root = WriteEnv(
            _root,
            "  JWT_KEY = \"quoted value\"  ",
            "export EMAIL_USER='single-quoted'",
            "EMAIL_PASSWORD=\"unbalanced",
            "EMAIL_SMTP_HOST=",
            "EMAIL_SMTP_PORT=2525",
            "EMAIL_ENABLE_TLS=true");

        DotEnvConfiguration.LoadForDevelopment(root);

        Environment.GetEnvironmentVariable("Jwt__Key").Should().Be("quoted value");
        Environment.GetEnvironmentVariable("Email__User").Should().Be("single-quoted");
        Environment.GetEnvironmentVariable("Email__Password").Should().Be("\"unbalanced", "a lone quote is not a pair");
        Environment.GetEnvironmentVariable("Email__SmtpHost").Should().BeEmpty();
        Environment.GetEnvironmentVariable("Email__SmtpPort").Should().Be("2525");
        Environment.GetEnvironmentVariable("Email__EnableTls").Should().Be("true");
    }

    [Fact]
    public void Does_nothing_outside_development()
    {
        using var env = new EnvironmentScope("ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "Jwt__Key");
        env.Set("ASPNETCORE_ENVIRONMENT", "Test");

        var root = WriteEnv(_root, "JWT_KEY=must-not-load");

        DotEnvConfiguration.LoadForDevelopment(root);

        Environment.GetEnvironmentVariable("Jwt__Key").Should().BeNull("Docker and CI pass their values directly");
    }

    [Fact]
    public void Accepts_DOTNET_ENVIRONMENT_when_ASPNETCORE_ENVIRONMENT_is_unset()
    {
        using var env = new EnvironmentScope("ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "Jwt__Key");
        env.Set("ASPNETCORE_ENVIRONMENT", null);
        env.Set("DOTNET_ENVIRONMENT", "development");

        var root = WriteEnv(_root, "JWT_KEY=from-dotnet-env");

        DotEnvConfiguration.LoadForDevelopment(root);

        Environment.GetEnvironmentVariable("Jwt__Key").Should().Be("from-dotnet-env", "the comparison ignores case");
    }

    [Fact]
    public void Does_nothing_when_no_env_file_is_found()
    {
        using var env = new EnvironmentScope("ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "Jwt__Key");
        env.Set("ASPNETCORE_ENVIRONMENT", "Development");

        // No .env anywhere up the tree: the walk ends at the filesystem root and the loader returns.
        DotEnvConfiguration.LoadForDevelopment(Subdirectory(_root, "a", "b", "c"));

        Environment.GetEnvironmentVariable("Jwt__Key").Should().BeNull();
    }

    /// <summary>Restores the named variables when disposed, so one test cannot leak into the next.</summary>
    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originals = new(StringComparer.Ordinal);

        public EnvironmentScope(params string[] names)
        {
            foreach (var name in names)
                _originals[name] = Environment.GetEnvironmentVariable(name);
        }

        public void Set(string name, string? value) => Environment.SetEnvironmentVariable(name, value);

        public void Dispose()
        {
            foreach (var (name, value) in _originals)
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
