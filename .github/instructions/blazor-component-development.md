# Blazor Component Development

Scope: All Blazor-based projects — [`Veldrath.Web`](../../Veldrath.Web/), [`Veldrath.GameClient.Components`](../../Veldrath.GameClient.Components/), [`RealmFoundry`](../../RealmFoundry/), and any future Blazor projects in this repository.

**Does NOT apply to** the Avalonia desktop client ([`Veldrath.Client`](../../Veldrath.Client/)) or the DB editor ([`RealmForge`](../../RealmForge/)).

---

## Component Selection Decision Tree

When you need UI, follow this priority order:

1. **MudBlazor component** — Always the first choice. MudBlazor v9.7.0 provides 87+ components covering the vast majority of UI needs. Check [`llms.txt`](../../llms.txt) for the complete component catalog and API reference before writing anything custom.
2. **Custom reusable Blazor component** — Create a new component only when a MudBlazor component does not exist for the task **and** the UI piece will be reused in at least two places. Use the code-behind pattern (see below).
3. **Embed logic directly in a page** — When the UI is truly one-off and will never be reused, it is acceptable to keep the markup and logic in the page `.razor` file. Do not create a separate component for single-use UI.
4. **Inline HTML with MudBlazor CSS utilities** — For trivial presentational wrappers that only add a `<div>` with MudBlazor utility classes, embedding in-place is preferred over a dedicated component.

**Anti-patterns to avoid:**
- Writing raw HTML for things MudBlazor already handles (buttons, inputs, cards, dialogs, chips, etc.)
- Creating a custom component that wraps a single MudBlazor component with no additional logic
- Creating one-off components that are used in exactly one place

---

## Code-Behind Pattern

When creating a custom Blazor component, use the **code-behind partial class pattern**:

```
Component.razor       ← Razor markup, `@inherits`, `@inject`, `@bind-*`, event handlers
Component.razor.cs    ← C# logic: parameters, injected services, methods, lifecycle
```

### Conventions

**File naming:** PascalCase matching the component name. Use descriptive, self-documenting names.

```
CharacterClassCard.razor          ← Good: clear, specific
CharacterClassCard.razor.cs       ← Good: partial class code-behind
Card.razor                        ← Bad: too generic
CharCard.razor                    ← Bad: abbreviated, ambiguous
```

**Code-behind (`Component.razor.cs`):**

```csharp
namespace ProjectName.Components.Pages;  // or .Shared, .Layout

/// <summary>Brief description of what this component does.</summary>
public partial class ComponentName : ComponentBase  // or IAsyncDisposable if needed
{
    // Use [Inject] for DI in code-behind (not @inject in .razor)
    [Inject]
    private ISomeService Service { get; set; } = null!;

    // Use [Parameter] for component parameters
    [Parameter]
    public string Title { get; set; } = string.Empty;

    // EventCallback<T> for child-to-parent communication
    [Parameter]
    public EventCallback<string> OnSelected { get; set; }

    // Private fields for internal state (no public fields)
    private bool _isLoading;

    // Lifecycle overrides
    protected override async Task OnInitializedAsync() { ... }
}
```

**Razor (`Component.razor`):**

- Prefer `@inject` only for services needed directly in markup (e.g., `NavigationManager` for `Href`). Move everything else to code-behind `[Inject]`.
- Use MudBlazor components exclusively — no raw HTML form elements, no raw buttons, no raw inputs.
- Use MudBlazor utility classes for spacing, layout, and typography (`Class="mt-4 mb-2 d-flex gap-2"`).
- Reference code-behind members directly (they are in the same partial class).

**Example from the codebase:** See [`CreateCharacter.razor`](../../Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor) + [`CreateCharacter.razor.cs`](../../Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor.cs).

---

## Where Components Live

### [`Veldrath.Web`](../../Veldrath.Web/)

| Directory | Purpose |
|---|---|
| `Components/Layout/` | Layout components (MainLayout, NavMenu, Footer) |
| `Components/Pages/` | Page-level components (one per route) |
| `Components/Pages/Account/` | Account-related pages |
| `Components/Pages/Lore/` | Lore browsing pages |
| `Components/Pages/PatchNotes/` | Patch note pages |

### [`Veldrath.GameClient.Components`](../../Veldrath.GameClient.Components/)

| Directory | Purpose |
|---|---|
| `Components/Layout/` | Game-specific layout (GameLayout) |
| `Components/Pages/` | Game page components (CreateCharacter, Game, GameChat, etc.) |
| `Components/Shared/` | Reusable game UI components (ActionBar, GamePanel, InventoryOverlay, StatusBar, CharacterClassCard, etc.) |
| `Hosted/` | Embedded app host and routing for Avalonia WebView2 |

### [`RealmFoundry`](../../RealmFoundry/)

| Directory | Purpose |
|---|---|
| `Components/Layout/` | Layout components (MainLayout, NavMenu) |
| `Components/Pages/` | Page components |
| `Components/Pages/Admin/` | Admin dashboard pages |
| `Components/Pages/Editorial/` | Editorial content pages |
| `Components/Pages/Mod/` | Moderator pages |

**Rule of thumb:** If a component is used by multiple pages, it belongs in `Components/Shared/`. If it defines a route (has `@page`), it belongs in `Components/Pages/`. If it defines the page chrome, it belongs in `Components/Layout/`.

---

## Component Parameters & Events

- Use `[Parameter]` for data flowing **into** a component.
- Use `EventCallback<T>` for events flowing **out** of a component.
- Use `@bind-Value` / `@bind-{Property}` for two-way binding (requires `EventCallback<T>` of the same name with `Changed` suffix — MudBlazor handles this for you).
- Never pass `EventCallback` from parent to child for the child to "call back" — that's what `EventCallback<T>` is for.
- Do not use `[CascadingParameter]` unless the parameter genuinely cascades through multiple levels. Prefer explicit `[Parameter]` passing.

---

## MudBlazor Usage

- The comprehensive MudBlazor reference is [`llms.txt`](../../llms.txt). Consult it before writing any UI code.
- MudBlazor v9.7.0 is the primary UI library. Do not introduce other component libraries.
- Use `MudThemeProvider`, `MudDialogProvider`, and `MudSnackbarProvider` at the app root (already configured in all Blazor projects).
- Prefer MudBlazor's built-in `MudGrid`/`MudItem` for grid layouts, `MudStack` for flexbox, `MudContainer` for page width constraints.
- Use `MudText` with `Typo` enum instead of raw `<h1>`–`<h6>` or `<p>` tags for consistent typography.
- Use `MudButton` instead of raw `<button>`. Use `MudIconButton` for icon-only buttons.
- Use `MudTextField`, `MudSelect`, `MudCheckBox`, `MudRadioGroup`, `MudSwitch` instead of raw HTML form controls.
- Reference the [`VeldrathTheme`](../../Veldrath.Web/Themes/VeldrathTheme.cs) `MudTheme` definition for palette, typography, and layout defaults applied to all MudBlazor components.

---

## Testing

- Blazor component tests use **bunit** v2.6.2.
- Test projects mirror the source structure: components in `Project/Components/Shared/` → tests in `Project.Tests/Components/Shared/`.
- Render components with `RenderComponent<T>()`, query with `Find()` / `FindAll()`, verify markup with `MarkupMatches()`.
- For components that depend on `MudBlazor` services, add the MudBlazor service collection in test setup (bunit's `TestContext.Services.AddMudServices()`).
- Test parameter passing, event callbacks, and rendering conditions. Do not test MudBlazor's internal behavior.
- See existing test projects for patterns: [`Veldrath.Web.Tests`](../../Veldrath.Web.Tests/), [`Veldrath.GameClient.Components.Tests`](../../Veldrath.GameClient.Components.Tests/), [`RealmFoundry.Tests`](../../RealmFoundry.Tests/).
