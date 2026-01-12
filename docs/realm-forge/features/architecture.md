# Architecture

**Last Updated**: January 12, 2026

---

## Technology Stack

### Core Framework
- **.NET 9.0** - Runtime and base class libraries
- **.NET MAUI** - Cross-platform application framework
- **Blazor Hybrid** - Web UI components in desktop app
- **WebView2** - Embedded Chromium browser (Windows)

### UI Layer
- **Razor Components** - HTML/CSS-based UI
- **Blazor Server/WebAssembly** - Component rendering
- **MudBlazor** (v6.x+) - Material Design component library
- **Bootstrap 5** - CSS framework (built-in, may phase out for MudBlazor)
- **Material Design Icons** - Icon library (via MudBlazor)

### Data Access
- **RealmEngine.Shared** - Game models and abstractions
- **RealmEngine.Data** - JSON data loaders and services
- **System.Text.Json** - JSON serialization
- **System.Reflection** - Dynamic form generation

### Editor & Validation (v3.1)
- **BlazorMonaco** - Monaco Editor integration for JSON mode
- **FluentValidation** (v12.1.1+) - Model validation
- **Serilog** (v4.3.0+) - Structured logging
- **Blazored.LocalStorage** - Settings persistence and auto-save

### Testing (v3.1)
- **bUnit** (v1.30.0+) - Blazor component testing
- **xUnit** - Test framework
- **FluentAssertions** - Test assertions
- **Moq** or **NSubstitute** - Mocking (v3.2)

### Utilities (v3.2+)
- **Polly** (v8.6.5+) - Resilience patterns
- **Humanizer** (v3.0+) - Text formatting
- **Microsoft.Extensions.Caching.Memory** - Performance caching

---

## Project Structure

```
RealmForge/
├── Components/
│   ├── Layout/                     # App chrome
│   │   ├── MainLayout.razor       # Root layout
│   │   ├── NavMenu.razor          # Navigation sidebar
│   │   └── MainLayout.razor.css   # Layout styles
│   ├── Pages/                      # Routable pages
│   │   ├── Home.razor             # Welcome screen
│   │   ├── JsonEditor.razor       # Main editor
│   │   └── Settings.razor         # App settings (planned)
│   ├── Shared/                     # Reusable components
│   │   ├── DynamicFormEditor.razor   # Form generator
│   │   ├── ReferencePickerDialog.razor   # (v3.1)
│   │   ├── ValidationPanel.razor   # (v3.1)
│   │   ├── MonacoEditor.razor     # Monaco wrapper (v3.1)
│   │   └── FileTreeView.razor     # MudTreeView wrapper (v3.1)
│   └── Routes.razor               # Route configuration
├── Models/                         # UI-specific models
│   ├── FileTreeNode.cs            # (planned)
│   ├── EditorState.cs             # (planned)
│   └── ValidationResult.cs        # (planned)
├── Services/                       # Business logic
│   ├── ValidationService.cs       # (v3.1)
│   ├── ReferenceResolverService.cs # (v3.1)
│   ├── SettingsService.cs         # (v3.1 - uses Blazored.LocalStorage)
│   ├── AutoSaveService.cs         # (v3.1)
│   └── ModService.cs              # (v4.0)
├── Resources/                      # Static assets
│   ├── Images/                    # Icons, logos
│   ├── Fonts/                     # Custom fonts
│   └── Raw/                       # Other assets
├── wwwroot/                        # Web assets
│   ├── css/                       # Stylesheets
│   │   ├── app.css               # Global styles
│   │   └── bootstrap/            # Bootstrap CSS
│   ├── js/                        # JavaScript (minimal)
│   └── index.html                # Entry HTML
├── Platforms/                      # Platform-specific code
│   ├── Windows/                   # Windows configuration
│   ├── MacCatalyst/              # macOS configuration
│   └── Android/                   # Android (unused)
├── MainPage.xaml                  # MAUI entry point
├── MauiProgram.cs                # App configuration
├── App.xaml                       # App resources
└── RealmForge.csproj             # Project file
```

---

## Component Architecture

### Blazor Component Model

**Razor Components:**
- `.razor` files containing HTML + C# code
- `@code` blocks for component logic
- Parameters for data input
- EventCallbacks for output
- Cascading values for shared state

**Example Component:**
```razor
@* Component markup *@
<div class="editor">
    <h3>@Title</h3>
    <button @onclick="HandleSave">Save</button>
</div>

@code {
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public EventCallback OnSave { get; set; }
    
    private async Task HandleSave()
    {
        await OnSave.InvokeAsync();
    }
}
```

### Key Components

#### DynamicFormEditor.razor
**Purpose:** Generic form generator from any model

**Parameters:**
- `TModel` - Type parameter (generic)
- `Model` - Instance to edit
- `OnSave` - Callback when saved
- `OnCancel` - Callback when cancelled

**Features:**
- Reflection-based property inspection
- Type-specific input rendering
- Automatic label generation
- Value binding and updates

**Flow:**
1. Receive model via parameter
2. Inspect properties using reflection
3. Render inputs based on property types
4. Handle value changes and update model
5. Invoke OnSave callback with updated model

#### JsonEditor.razor
**Purpose:** Main editing page with Form/JSON toggle

**State:**
- `EditorMode` - "form" or "json"
- `CurrentModel` - Deserialized model instance
- `JsonContent` - Raw JSON string
- `SelectedFile` - Current file path

**Features:**
- File browser (left panel)
- Editor area (right panel)
- Mode toggle (radio buttons)
- Model detection from filename
- Bi-directional sync (Form ↔ JSON)

**Flow:**
1. User selects file in browser
2. Load JSON from file system
3. Detect model type (Item, Enemy, etc.)
4. Deserialize to model if known type
5. Display in form mode by default
6. User can toggle to JSON mode
7. User makes edits
8. Save serializes model back to JSON

#### Home.razor
**Purpose:** Welcome screen and app navigation

**Features:**
- Welcome message
- Feature highlights (cards)
- Quick actions (buttons)
- Recent files (planned)
- Getting started guide (planned)

---

## Data Flow

### Form Editing Flow

```
User Input
    ↓
InputText/InputNumber/etc.
    ↓
@bind-Value / ValueChanged
    ↓
SetValue(PropertyInfo, object)
    ↓
prop.SetValue(Model, value)
    ↓
StateHasChanged()
    ↓
UI Updates
```

### Form ↔ JSON Sync

```
Form Mode:
    Model (C# object)
        ↓
    User edits via form
        ↓
    Model updated in-place
        ↓
    Switch to JSON mode
        ↓
    JsonSerializer.Serialize(Model)
        ↓
    Display JSON string

JSON Mode:
    JSON string
        ↓
    User edits raw JSON
        ↓
    JSON string updated
        ↓
    Switch to Form mode
        ↓
    JsonSerializer.Deserialize<T>(JSON)
        ↓
    Display in form
```

### Save Flow

```
User clicks Save
    ↓
Validate model (planned)
    ↓
Serialize to JSON
    ↓
Format JSON (indented)
    ↓
Write to file system
    ↓
Show success notification
```

---

## State Management

### Current Approach
- **Component State** - Local `@code` variables
- **Parameters** - Data passed between components
- **Cascading Values** - Shared state (planned)

### Planned Improvements (v3.2+)

**Application State Service:**
```csharp
public class AppState
{
    public List<OpenFile> OpenFiles { get; set; }
    public string CurrentWorkspace { get; set; }
    public EditorSettings Settings { get; set; }
    
    public event Action OnChange;
    
    public void NotifyStateChanged() => OnChange?.Invoke();
}
```

**Usage:**
```razor
@inject AppState State
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        State.OnChange += StateHasChanged;
    }
    
    public void Dispose()
    {
        State.OnChange -= StateHasChanged;
    }
}
```

---

## Dependency Injection

### Current Services

**Built-In MAUI Services:**
- `IFileSystem` - File system access
- `IPreferences` - Settings storage
- `IConnectivity` - Network status (future)

### Planned Services (v3.1+)

**Service Registration:**
```csharp
// MauiProgram.cs
builder.Services.AddSingleton<AppState>();
builder.Services.AddScoped<ValidationService>();
builder.Services.AddScoped<ReferenceResolverService>();
builder.Services.AddScoped<ModService>();
```

**Service Injection:**
```razor
@inject ValidationService ValidationService
@inject AppState AppState

@code {
    private async Task ValidateModel()
    {
        var result = await ValidationService.ValidateAsync(Model);
        // Handle result
    }
}
```

---

## Routing

### Current Routes
- `/` - Home page
- `/editor` - JSON editor page

### Planned Routes (v3.2+)
- `/settings` - Application settings
- `/mod-manager` - Mod management (v4.0)
- `/mod-wizard` - Mod creation wizard (v4.0)
- `/about` - About RealmForge

**Route Configuration:**
```razor
<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <p>Sorry, there's nothing at this address.</p>
    </NotFound>
</Router>
```

---

## Error Handling

### Current Approach
- Basic try/catch blocks
- Console logging
- Generic error messages

### Planned Improvements (v3.2)

**Global Error Boundary:**
```razor
<ErrorBoundary>
    <ChildContent>
        @Body
    </ChildContent>
    <ErrorContent Context="ex">
        <ErrorDisplay Exception="@ex" />
    </ErrorContent>
</ErrorBoundary>
```

**Structured Logging:**
```csharp
Log.Error(ex, "Failed to load file {FilePath}", filePath);
Log.Warning("Validation failed for {ModelType}", modelType.Name);
Log.Information("Saved {FileCount} files", fileCount);
```

---

## Performance

### Current Performance
- Fast for files <100KB
- Instant form rendering for simple models
- Minimal overhead from Blazor

### Optimization Strategies (Planned)

**Virtual Scrolling:**
```razor
<Virtualize Items="@longList" Context="item">
    <div>@item.Name</div>
</Virtualize>
```

**Lazy Loading:**
```razor
<LazyLoad>
    <HeavyComponent />
</LazyLoad>
```

**Memoization:**
```csharp
private PropertyInfo[]? _cachedProperties;

private PropertyInfo[] GetProperties()
{
    return _cachedProperties ??= typeof(TModel)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance);
}
```

---

## Testing Strategy

### Planned Tests (v3.2+)

**Unit Tests:**
- Component logic tests
- Service tests
- Model validation tests
- Utility function tests

**Integration Tests:**
- End-to-end workflows
- File I/O operations
- Cross-component communication
- State management

**UI Tests (Future):**
- Playwright for browser automation
- Component rendering tests
- Interaction tests
- Accessibility tests

---

## Build & Deployment

### Development Build
```powershell
dotnet build -f net9.0-windows10.0.19041.0
dotnet run --project RealmForge.csproj -f net9.0-windows10.0.19041.0
```

### Release Build
```powershell
dotnet publish -c Release -f net9.0-windows10.0.19041.0 \
    -p:PublishSingleFile=true \
    -p:SelfContained=false \
    -p:PublishReadyToRun=true
```

### Platform-Specific Builds
```powershell
# Windows
dotnet publish -f net9.0-windows10.0.19041.0

# macOS
dotnet publish -f net9.0-maccatalyst

# Linux (planned)
dotnet publish -f net9.0-linux
```

---

## Security Considerations

### File System Access
- Validate file paths (no directory traversal)
- Restrict access to data folders only
- Require user permission for folder picker

### JSON Deserialization
- Validate JSON before deserializing
- Limit maximum file size
- Timeout for large files
- Sanitize user input

### Mod Loading (v4.0)
- Verify mod signatures
- Sandbox mod scripts
- Permission system for file access
- Validate mod manifest

---

## Accessibility

### Current State
- Basic HTML semantics
- Keyboard navigation (native)

### Planned Features (v3.3)
- ARIA labels on all interactive elements
- Screen reader announcements
- High contrast mode support
- Focus management
- Keyboard shortcuts
- Skip navigation links

---

## Cross-Platform Considerations

### Windows (Current)
- ✅ Full support
- WebView2 embedded browser
- Native file dialogs
- Windows-specific features

### macOS (Planned v3.2)
- Same Blazor components
- MacCatalyst framework
- Native macOS UI elements
- File system differences

### Linux (Planned v3.3)
- GTK+ for UI
- Different file dialogs
- Package as .deb or .rpm
- Flatpak distribution

---

## Future Architecture

### Plugin System (v5.0+)
- Load external assemblies
- Custom editors for mod types
- Community-contributed components
- API for plugin development

### Cloud Sync (v5.0+)
- Save settings to cloud
- Sync recent files across devices
- Share mods via cloud storage
- Collaborative editing

### AI Integration (v5.0+)
- Content generation assistance
- Balance suggestions
- Description generation
- Error detection and fixes
