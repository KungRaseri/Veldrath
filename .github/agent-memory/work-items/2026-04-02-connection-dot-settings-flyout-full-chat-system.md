---
kind: work-item
date: 2026-04-02
area: content
title: "Connection dot, settings flyout, full chat system"
---

### Session-Pass2-HUD (2026-04-02) — Connection dot, settings flyout, full chat system

**Feature**: HUD Pass 2 — connection health indicator, settings flyout with mute controls, zone/global/whisper/system chat.

**ConnectionState enum extended** (`ServerConnectionService.cs`):
- Added values: `Degraded` (connected, ping ≥ 200ms), `Reconnecting` (SignalR auto-reconnect in progress)
- Ping timer: `System.Timers.Timer` (5 s interval) started on connect/reconnect, stopped on disconnect/dispose
- `MeasurePingAsync()` added to `IServerConnectionService` + implementation (Stopwatch + silent catch → Serilog Debug)
- Thresholds: `< 200ms = Connected`, `≥ 200ms = Degraded`
- `IHubConnection` interface + `HubConnectionWrapper` + `FakeHubConnection` gained `Reconnecting` event
- `FakeHubConnection.SimulateReconnectingAsync()` test helper added

**IAudioPlayer extended** (`IAudioPlayer.cs`):
- New members: `bool IsMusicMuted`, `bool IsSfxMuted`, `ToggleMusicMute()`, `ToggleSfxMute()`
- Implemented in `LibVlcAudioPlayer`, `NullAudioPlayer`, `FakeAudioPlayer`

**GameHub chat methods** (`GameHub.cs`):
- `Task<long> Ping()` — returns UTC Unix milliseconds (one-liner)
- `Task SendZoneMessage(SendZoneChatMessageHubRequest)` — broadcasts to zone group → `"ReceiveChatMessage"` with `ChatMessageHubDto`
- `Task SendGlobalMessage(SendGlobalChatMessageHubRequest)` — broadcasts to all → `"ReceiveChatMessage"`
- `Task SendWhisper(SendWhisperHubRequest)` — routes to target via `_playerSessionRepo.GetByCharacterNameAsync`; echoes to sender with `Sender = "To {target}"`
- New DTOs at bottom: `SendZoneChatMessageHubRequest(string Message)`, `SendGlobalChatMessageHubRequest(string Message)`, `SendWhisperHubRequest(string TargetCharacterName, string Message)`, `ChatMessageHubDto(string Channel, string Sender, string Message, DateTimeOffset Timestamp)`
- `IZoneSessionRepository.GetByCharacterNameAsync(string characterName)` added (interface + EF implementation)

**New ViewModels** (`Veldrath.Client/ViewModels/`):
- `ChatMessageViewModel.cs` — record; channel color map (Zone=#94a3b8, Global=#60a5fa, Whisper=#f472b6, System=#4ade80); `ChannelLabel`, `ChannelColor`, `FormattedMessage` properties
- `OnlinePlayerViewModel.cs` — `ReactiveObject`; `Name` (string), `StartWhisperCommand` (calls `Action<string>` callback)

**GameViewModel additions**:
- `ConnectionStateValue : ConnectionState`, `ConnectionStatusColor : string` (hex), `ConnectionStatusTooltip : string`
- `IsSettingsOpen : bool`, `IsMusicMuted / IsSfxMuted` (delegated to `_audioPlayer`), `MusicMuteLabel / SfxMuteLabel` (computed strings)
- `ChatMessages : ObservableCollection<ChatMessageViewModel>`, `ChatInput : string` (with `/w name` prefix parser → sets Whisper channel + target)
- `ActiveChatChannel : string` (default "Zone"), `WhisperTarget : string`, `IsWhisperChannelActive : bool`, `IsChatInputVisible : bool`
- `UseHotbarAbilityCommand : ReactiveCommand<string, Unit>` → `DoUseHotbarAbilityAsync` → dispatches to `DoUseAbilityInCombatAsync` when `IsInCombat`, else `DoUseAbilityAsync`
- `ToggleSettingsCommand`, `ToggleMusicMuteCommand`, `ToggleSfxMuteCommand`, `SetChatChannelCommand`, `SendChatCommand`
- `OnlinePlayers` changed: `ObservableCollection<string>` → `ObservableCollection<OnlinePlayerViewModel>` (each constructed with `StartWhisperFromPlayer` callback)
- `OnChatMessageReceived(string channel, string sender, string message, DateTimeOffset timestamp)` added
- `StartWhisperFromPlayer(string name)` private helper: sets `ActiveChatChannel = "Whisper"` + `WhisperTarget = name`
- `HotbarSlotViewModel` internal field/param renamed `_useHotbarAbilityCommand` (was `_useAbilityInCombatCommand`)

**CharacterSelectViewModel additions**:
- `_chatMessageSub`: field + disposal + `"ReceiveChatMessage"` subscription wiring `OnChatMessageReceived`
- `ChatMessagePayload(string Channel, string Sender, string Message, DateTimeOffset Timestamp)` internal record

**GameView.axaml changes**:
- **Header Col 3**: Added connection dot (`Ellipse` bound to `ConnectionStatusColor`/`ConnectionStatusTooltip`) + settings flyout button (`⚙`) with `Flyout` popup containing music mute, SFX mute (bound to `MusicMuteLabel`/`SfxMuteLabel`), and Logout button. Old standalone Logout button removed.
- **Right panel**: Added chat section between Action Log and Online Players:
  - Channel pills: Zone / Global / Whisper / System (each bound to `SetChatChannelCommand`)
  - Whisper recipient `TextBox` (`IsVisible="{Binding IsWhisperChannelActive}"`)
  - Message list (`ObservableCollection<ChatMessageViewModel>`, 160px `ScrollViewer`)
  - Send row: `TextBox` + ↵ button (`IsVisible="{Binding IsChatInputVisible}"`)
- **Online Players DataTemplate**: updated to `x:DataType="vm:OnlinePlayerViewModel"` with `.Name` binding + `[W]` whisper button bound to `StartWhisperCommand`

**Test fixes**:
- `GameViewModelTests.cs`: 4 assertions changed from `Contain("string")` / `NotContain("string")` / `BeEquivalentTo(string[])` to use lambda predicate `p => p.Name == "..."` and `.Select(p => p.Name)` overloads
- `CharacterSelectViewModelTests.cs`: same 3 assertions
- `ViewDataBindingTests.cs`: `.Add("Gandalf"/"Aragorn"/"Legolas")` changed to `new OnlinePlayerViewModel("name", _ => { })`

**Final count: 584 client + 530 server (5 pre-existing CharacterCreationSession failures) = still 584 client passing**
