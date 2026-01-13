# Exception Logging Quick Reference

## How to Log Exceptions

### In Razor Components

All Razor components have access to `ILogger<T>` via dependency injection:

```razor
@inject ILogger<YourComponent> Logger

@code {
    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Your code here
            await SomeAsyncOperation();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize component");
            // Handle error appropriately
        }
    }
    
    private async Task OnButtonClick()
    {
        try
        {
            await DoSomething();
        }
        catch (SpecificException ex)
        {
            // Log with context
            Logger.LogError(ex, "Failed to process action for user {UserId}", userId);
        }
    }
}
```

### In Services

Services receive `ILogger<T>` via constructor injection:

```csharp
public class YourService
{
    private readonly ILogger<YourService> _logger;
    
    public YourService(ILogger<YourService> logger)
    {
        _logger = logger;
    }
    
    public async Task<Result> DoWorkAsync()
    {
        try
        {
            // Operation
            _logger.LogInformation("Starting work for {Item}", itemName);
            var result = await ProcessAsync();
            _logger.LogInformation("Work completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work failed for {Item}", itemName);
            throw; // Re-throw if caller needs to handle
        }
    }
}
```

## Logging Levels

### When to use each level:

**Fatal** - Application cannot continue, crash imminent
```csharp
Log.Fatal(ex, "Application startup failed");
```

**Error** - Operation failed but app can continue
```csharp
Logger.LogError(ex, "Failed to save file {FilePath}", filePath);
```

**Warning** - Unexpected but recoverable condition
```csharp
Logger.LogWarning("File {File} not found, using defaults", fileName);
```

**Information** - Normal operations
```csharp
Logger.LogInformation("User {User} logged in", username);
```

**Debug** - Detailed diagnostics (only in debug builds)
```csharp
Logger.LogDebug("Loading {Count} items from cache", count);
```

## Structured Logging

Always use structured logging with named parameters:

✅ **GOOD:**
```csharp
Logger.LogError(ex, "Failed to load {FileName} from {Path}", fileName, path);
```

❌ **BAD:**
```csharp
Logger.LogError(ex, $"Failed to load {fileName} from {path}");
```

**Why?** Structured logging allows log analysis tools to filter and query by specific properties.

## Common Patterns

### Async Method with Logging
```csharp
public async Task<T> LoadDataAsync(string id)
{
    try
    {
        Logger.LogDebug("Loading data for {Id}", id);
        var data = await _repository.GetAsync(id);
        Logger.LogInformation("Data loaded successfully for {Id}", id);
        return data;
    }
    catch (NotFoundException ex)
    {
        Logger.LogWarning(ex, "Data not found for {Id}", id);
        return null;
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load data for {Id}", id);
        throw;
    }
}
```

### Event Handler with Logging
```csharp
private async Task OnSaveClick()
{
    try
    {
        Logger.LogInformation("Saving changes");
        await SaveAsync();
        _message = "Saved successfully!";
        Logger.LogInformation("Save completed");
    }
    catch (ValidationException ex)
    {
        Logger.LogWarning(ex, "Validation failed during save");
        _message = $"Validation error: {ex.Message}";
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Save failed");
        _message = $"Error: {ex.Message}";
    }
}
```

### Initialization with Fallback
```csharp
protected override async Task OnInitializedAsync()
{
    try
    {
        _settings = await _settingsService.LoadSettingsAsync();
        Logger.LogDebug("Settings loaded");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load settings, using defaults");
        _settings = new Settings(); // Fallback to defaults
    }
}
```

## Where Exceptions Are Logged

### 1. Component Level
Every component lifecycle method and event handler should have try-catch.

### 2. Service Level
Service methods log exceptions with business context.

### 3. Global Level
`App.xaml.cs` catches anything that escapes component/service level:
- `AppDomain.CurrentDomain.UnhandledException`
- `TaskScheduler.UnobservedTaskException`

## Viewing Logs

### During Development
Logs appear in:
- **Console Output** (if running in terminal)
- **Debug Output** (Visual Studio/Rider)
- **Log Files** (always)

### Log File Location
```
Windows: C:\Users\{username}\AppData\Local\Packages\{package}\LocalState\logs\
File: realmforge-YYYY-MM-DD.txt
```

To find the exact path, add this to your code:
```csharp
var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs");
Logger.LogInformation("Log directory: {Path}", logPath);
```

### Reading Logs
```powershell
# View today's log
Get-Content "$env:LOCALAPPDATA\Packages\*RealmForge*\LocalState\logs\realmforge-$(Get-Date -Format yyyy-MM-dd).txt"

# Tail logs in real-time
Get-Content "$env:LOCALAPPDATA\Packages\*RealmForge*\LocalState\logs\realmforge-$(Get-Date -Format yyyy-MM-dd).txt" -Wait
```

## Best Practices

### ✅ DO:
- Log at appropriate levels (Error for failures, Debug for diagnostics)
- Include context in log messages (IDs, names, paths)
- Use structured logging with named parameters
- Log before and after important operations
- Catch specific exceptions when possible

### ❌ DON'T:
- Use string interpolation in log messages
- Log sensitive data (passwords, tokens, PII)
- Catch Exception without logging (silent failures)
- Log the same error multiple times
- Log inside tight loops (performance impact)

## Examples from RealmForge

### Good Exception Logging
```csharp
// From FileManagementService.cs
public async Task<JObject> LoadJsonFileAsync(string filePath)
{
    try
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FilePath}", filePath);
            return new JObject();
        }

        var json = await File.ReadAllTextAsync(filePath);
        var jObject = JObject.Parse(json);
        _logger.LogInformation("Loaded JSON file: {FilePath}", filePath);
        return jObject;
    }
    catch (JsonException ex)
    {
        _logger.LogError(ex, "Invalid JSON in file: {FilePath}", filePath);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load file: {FilePath}", filePath);
        throw;
    }
}
```

## Testing Exception Logging

To verify your exception logging works:

1. **Add a test exception:**
```csharp
protected override async Task OnInitializedAsync()
{
    throw new Exception("Test exception");
}
```

2. **Run the app**
3. **Check logs** - Should see error with stack trace
4. **Remove test exception**

## Troubleshooting

**Problem:** Logs not appearing
- Check `FileSystem.AppDataDirectory` path
- Verify Serilog is configured in `MauiProgram.cs`
- Check file permissions

**Problem:** Too many logs
- Increase minimum log level: `.MinimumLevel.Information()`
- Disable debug logging in production

**Problem:** Missing context
- Add more structured parameters to log messages
- Include relevant IDs, names, or state information

## Additional Resources

- [Serilog Documentation](https://serilog.net/)
- [Microsoft Logging Best Practices](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Structured Logging Guide](https://nblumhardt.com/2016/06/structured-logging-concepts-in-net-series-1/)
