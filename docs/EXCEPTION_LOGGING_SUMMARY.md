# Exception Logging Summary

## Overview

Comprehensive exception logging has been implemented throughout RealmForge to ensure all errors are captured and logged using Serilog.

## Changes Made

### 1. Global Exception Handlers (App.xaml.cs)

Added application-wide exception handlers that catch unhandled exceptions and log them before the app crashes:

**Features:**
- `AppDomain.CurrentDomain.UnhandledException` - Catches fatal unhandled exceptions
- `TaskScheduler.UnobservedTaskException` - Catches async task exceptions that aren't awaited
- All exceptions logged with Serilog at appropriate levels (Fatal/Error)
- Graceful flush of logs before app termination

**Code Location:** `RealmForge\App.xaml.cs`

### 2. Startup Exception Logging (MauiProgram.cs)

Wrapped the entire application initialization in try-catch to capture startup failures:

**Features:**
- Logs fatal errors during application startup
- Falls back to Console.WriteLine if Serilog isn't configured yet
- Ensures log files are flushed before re-throwing exception

**Code Location:** `RealmForge\MauiProgram.cs`

### 3. Component Exception Handling

#### JsonEditor.razor
- `OnInitializedAsync()` - Catches initialization errors when loading settings
- `LoadFileAsync()` - Already had error handling (✓)
- `SaveFileAsync()` - Already had error handling (✓)
- `FormatJsonAsync()` - Already had error handling (✓)

#### FileTreeView.razor
- `OnInitialized()` - Catches errors during file tree building
- `BuildFileTree()` - Already had error handling (✓)
- `BuildTreeRecursive()` - Already had error handling (✓)

#### MonacoEditorWrapper.razor
- `OnParametersSetAsync()` - Catches errors when updating editor parameters
- `OnEditorInit()` - Catches errors during Monaco initialization
- `SetEditorValue()` - Catches errors when setting editor content
- `OnContentChanged()` - Catches errors during content change events
- `GetValueAsync()` - Already had error handling (✓)
- `FormatDocumentAsync()` - Already had error handling (✓)
- `UpdateThemeAsync()` - Already had error handling (✓)
- `ConfigureJsonFeatures()` - Already had error handling (✓)

### 4. Service Exception Handling

All services already had comprehensive exception handling:

#### EditorSettingsService.cs ✓
- `LoadSettingsAsync()` - Logs errors and returns defaults
- `SaveSettingsAsync()` - Logs errors and re-throws

#### FileManagementService.cs ✓
- `LoadJsonFileAsync()` - Handles JsonException and general exceptions
- `SaveJsonFileAsync()` - Logs errors and re-throws
- `GetDataFiles()` - Logs errors

#### ModelValidationService.cs ✓
- `ValidateJsonSyntax()` - Handles parsing exceptions

#### ReferenceResolverService.cs ✓
- All methods have appropriate error handling

### 5. Error Boundary Component

Created a reusable error boundary component for catching rendering errors:

**Features:**
- Catches exceptions during Blazor component rendering
- Displays user-friendly error message
- Logs exception details with Serilog
- Allows user to dismiss error and continue

**Code Location:** `RealmForge\Components\Shared\ErrorBoundary.razor`

**Usage:**
```razor
<ErrorBoundary>
    <YourComponent />
</ErrorBoundary>
```

## Logging Levels Used

- **Fatal** - Application crash or startup failure
- **Error** - Operation failed but app can continue
- **Warning** - Unexpected condition (not used for exceptions)
- **Information** - Normal operations (service initialization, file saves)
- **Debug** - Detailed diagnostic information

## Log File Location

Logs are written to:
```
{FileSystem.AppDataDirectory}\logs\realmforge-{date}.txt
```

On Windows, this is typically:
```
C:\Users\{username}\AppData\Local\Packages\{app-package-id}\LocalState\logs\
```

## Log Format

All exceptions are logged with structured logging format:
```
[HH:mm:ss LEVEL] Message {Properties}
{Exception Details}
```

Example:
```
[14:32:15 ERR] Failed to load file {File="catalog.json", Path="c:\data\catalog.json"}
System.IO.FileNotFoundException: Could not find file 'c:\data\catalog.json'
   at System.IO.FileStream.OpenHandle(...)
   ...
```

## Exception Flow

1. **Component Level** - Try-catch in lifecycle methods and event handlers
2. **Service Level** - Try-catch in service methods with contextual logging
3. **Global Level** - Unhandled exception handlers catch anything that escapes
4. **Startup Level** - MauiProgram catches fatal initialization errors

## Testing

All 3 existing RealmForge tests still pass after changes:
- ✅ EditorSettingsService tests
- ✅ FileManagementService tests
- ✅ Component initialization tests

## Next Steps

Consider adding:
1. Telemetry/crash reporting integration (AppCenter, Sentry, etc.)
2. User-facing error notifications with retry options
3. Error recovery strategies for common failures
4. Performance logging for slow operations

## Verification

To verify exception logging is working:

1. **View logs**: Check `{AppDataDirectory}\logs\realmforge-{date}.txt`
2. **Trigger test exception**: Add `throw new Exception("Test");` in a component
3. **Check console**: Run in debug mode and watch output window
4. **Review startup**: Check for initialization log entries

All exceptions should now appear in:
- Log files (always)
- Console output (debug builds)
- Debug output (Visual Studio/Rider)
