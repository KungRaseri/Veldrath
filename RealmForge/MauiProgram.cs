using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Blazored.LocalStorage;
using Serilog;
using Serilog.Events;
using RealmEngine.Core.Settings;

namespace RealmForge;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		try
		{
			// Configure Serilog using RealmEngine.Core standards
			var loggingSettings = new LoggingSettings
			{
				LogLevel = "Debug",
				LogToFile = true,
				LogToConsole = true,
				LogPath = Path.Combine(FileSystem.AppDataDirectory, "logs"),
				RetainDays = 7,
				EnableStructuredLogging = true
			};

			var logPath = Path.Combine(loggingSettings.LogPath, "realmforge-.txt");
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
			.MinimumLevel.Override("System", LogEventLevel.Warning)
			.Enrich.FromLogContext()
			.Enrich.WithProperty("Application", "RealmForge")
			.WriteTo.File(
				logPath,
				rollingInterval: RollingInterval.Day,
				retainedFileCountLimit: loggingSettings.RetainDays,
				outputTemplate: loggingSettings.EnableStructuredLogging
					? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
					: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
			.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
			.WriteTo.Debug()
			.CreateLogger();

		Log.Information("RealmForge v3.1 starting...");
		Log.Debug("Log directory: {LogPath}", loggingSettings.LogPath);

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Phase 1: Core Services
		builder.Services.AddMudServices();
		builder.Services.AddBlazoredLocalStorage();
		
		// Add Serilog to Microsoft.Extensions.Logging
		builder.Logging.AddSerilog(dispose: true);

		// Register RealmEngine.Core logging settings
		builder.Services.AddSingleton(loggingSettings);

		// Register RealmForge application services
		builder.Services.AddSingleton<RealmForge.Services.EditorSettingsService>();
		builder.Services.AddSingleton<RealmForge.Services.FileManagementService>();
		builder.Services.AddScoped<RealmForge.Services.ModelValidationService>();
		builder.Services.AddSingleton<RealmForge.Services.ReferenceResolverService>();

		Log.Debug("Registered application services");

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		Log.Debug("Blazor WebView Developer Tools enabled");
#endif

		Log.Information("RealmForge initialized successfully");
		return builder.Build();
		}
		catch (Exception ex)
		{
			// If Serilog is configured, use it; otherwise fall back to Console
			try
			{
				Log.Fatal(ex, "Fatal error during application startup");
				Log.CloseAndFlush();
			}
			catch
			{
				Console.WriteLine($"FATAL ERROR DURING STARTUP: {ex}");
			}
			
			throw;
		}
	}
}
