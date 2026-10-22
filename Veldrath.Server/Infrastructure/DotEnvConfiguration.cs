namespace Veldrath.Server.Infrastructure;

/// <summary>
/// Loads repository-root <c>.env</c> values into process environment variables for local
/// <c>dotnet run</c> sessions. Docker Compose and CI pass the values directly, so this
/// loader is restricted to the Development environment and never overrides variables
/// that are already set.
/// </summary>
internal static class DotEnvConfiguration
{
    // .env uses service-friendly names; ASP.NET configuration uses __ separators.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["JWT_KEY"] = "Jwt__Key",
        ["OAUTH_DISCORD_CLIENT_ID"] = "OAuth__Discord__ClientId",
        ["OAUTH_DISCORD_CLIENT_SECRET"] = "OAuth__Discord__ClientSecret",
        ["OAUTH_GOOGLE_CLIENT_ID"] = "OAuth__Google__ClientId",
        ["OAUTH_GOOGLE_CLIENT_SECRET"] = "OAuth__Google__ClientSecret",
        ["OAUTH_MICROSOFT_CLIENT_ID"] = "OAuth__Microsoft__ClientId",
        ["OAUTH_MICROSOFT_CLIENT_SECRET"] = "OAuth__Microsoft__ClientSecret",
        ["FOUNDRY_PUBLIC_URL"] = "Foundry__BaseUrl",
        ["WEB_PUBLIC_URL"] = "Web__BaseUrl",
        ["ALLOWED_RETURN_URL_HOSTS"] = "AllowedReturnUrlHosts",
        ["EMAIL_SMTP_HOST"] = "Email__SmtpHost",
        ["EMAIL_SMTP_PORT"] = "Email__SmtpPort",
        ["EMAIL_ENABLE_TLS"] = "Email__EnableTls",
        ["EMAIL_USER"] = "Email__User",
        ["EMAIL_PASSWORD"] = "Email__Password",
        ["EMAIL_SENDER_ADDRESS"] = "Email__SenderAddress",
        ["SEQ_SERVER_URL"] = "Seq__ServerUrl",
    };

    /// <summary>
    /// Walks up from the supplied content root to find <c>.env</c> and copies known
    /// keys into process environment variables when running in Development.
    /// </summary>
    /// <param name="contentRootPath">The server project directory.</param>
    public static void LoadForDevelopment(string contentRootPath)
    {
        if (!IsDevelopment())
            return;

        var directory = new DirectoryInfo(contentRootPath);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".env")))
            directory = directory.Parent;

        if (directory is null)
            return;

        foreach (var rawLine in File.ReadLines(Path.Combine(directory.FullName, ".env")))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            if (!Aliases.TryGetValue(key, out var configurationKey))
                continue;

            if (Environment.GetEnvironmentVariable(configurationKey) is null)
                Environment.SetEnvironmentVariable(configurationKey, value);
        }
    }

    private static bool IsDevelopment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
