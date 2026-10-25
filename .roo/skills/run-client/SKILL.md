---
name: run-client
description: Start the Veldrath Avalonia desktop game client (ReactiveUI MVVM). Use when you need to launch the game UI for local development or testing alongside the server.
---

# Skill: run-client

Start the Veldrath Avalonia desktop game client, built with ReactiveUI (MVVM pattern).

## Usage

Invoke this skill when you need to:
- Launch the game UI for local development
- Test client-side features, UI components, or ReactiveUI bindings
- Debug the client-server connection via SignalR

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- **The game server should be running first** (see [run-server](../run-server/SKILL.md))
- Node 22 + npm (only to build the Svelte UI bundle)
- PowerShell terminal
- Working directory: the repository root
- A display (desktop environment) is required — the client is a GUI application

## Steps

1. **Ensure the server is running** (in a separate terminal):

   ```powershell
   # Terminal 1: Start server
   dotnet run --project Veldrath.Server
   ```

2. **Build the Svelte desktop bundle** (once, and again after any `web/client` change):

   ```powershell
   cd web
   npm run build:desktop   # writes web/client/build-desktop
   cd ..
   ```

   The game screen is a WebView2 control that `DesktopUiServer` serves out of
   `web/client/build-desktop` (dev) or `SvelteApp/` next to the executable (published).
   Without the bundle the client still starts but the centre panel falls back to the native
   text map, so build it to see the real UI.

3. **Start the client** (in a new terminal):

   ```powershell
   dotnet run --project Veldrath.Client
   ```

## Expected Output

The client window opens on screen. In the terminal, you'll see output similar to:

```
info: Veldrath.Client.Program[0]
      Connecting to server at https://localhost:5001...
info: Veldrath.Client.Program[0]
      Connected to SignalR hub.
```

## Stopping the Client

Close the application window, or press `Ctrl+C` in the terminal where the client is running.

## Notes

- The client connects to the server via **SignalR** using the URL configured in the client's `appsettings.json` (default: `https://localhost:5001`).
- **Hot reload** is supported for Avalonia: use `dotnet watch run --project Veldrath.Client` for automatic recompilation on file changes.
- The client uses **ReactiveUI** with `RaiseAndSetIfChanged` for bindable properties, `ReactiveCommand.CreateFromTask` for async operations, and `WhenAnyValue` for derived state.
- Client tests use **Avalonia.Headless.XUnit** with the `[AvaloniaFact]` attribute. Tests tagged `[Trait("Category", "UI")]` require a real display and are excluded from CI runs.
- If connection fails, verify the server is running and the URLs match between client and server configurations.

## See Also

- [run-server](../run-server/SKILL.md) — Start the game server
- [start-dev-stack](../start-dev-stack/SKILL.md) — Start full environment
- [`.github/agent-memory/unbound-memory.md`](../../.github/agent-memory/unbound-memory.md) — Client hub architecture and session notes
- [`.github/agent-memory/engine-codebase.md`](../../.github/agent-memory/engine-codebase.md) — Client codebase conventions
