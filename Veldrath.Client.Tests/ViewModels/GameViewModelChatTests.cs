using System.Reactive.Threading.Tasks;
using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;
using Veldrath.Contracts.Chat;

namespace Veldrath.Client.Tests.ViewModels;

/// <summary>Chat-system unit tests for <see cref="GameViewModel"/>.</summary>
public class GameViewModelChatTests : TestBase
{
    private static GameViewModel MakeVm() =>
        new(new FakeServerConnectionService(), new FakeZoneService(), new TokenStore(), new FakeNavigationService());

    // ── Initial state ────────────────────────────────────────────────────────

    [Fact]
    public void ChatTabs_Should_Start_With_Global_And_Zone()
    {
        var vm = MakeVm();

        vm.ChatTabs.Should().HaveCount(2);
        vm.ChatTabs[0].Should().BeOfType<GlobalChatTabViewModel>();
        vm.ChatTabs[1].Should().BeOfType<ZoneChatTabViewModel>();
    }

    [Fact]
    public void ActiveChatTab_Should_Default_To_Global()
    {
        var vm = MakeVm();
        vm.ActiveChatTab.Should().BeOfType<GlobalChatTabViewModel>();
    }

    // ── OnChatMessageReceived routing ────────────────────────────────────────

    [Fact]
    public void OnChatMessageReceived_Global_Routes_Only_To_GlobalTab()
    {
        var vm = MakeVm();
        var zoneTab = (ZoneChatTabViewModel)vm.ChatTabs[1];

        vm.OnChatMessageReceived("Global", "Alice", "Hello", DateTimeOffset.UtcNow);

        var globalTab = (GlobalChatTabViewModel)vm.ChatTabs[0];
        globalTab.Messages.Should().ContainSingle();
        zoneTab.Messages.Should().BeEmpty();
    }

    [Fact]
    public void OnChatMessageReceived_Zone_Routes_Only_To_ZoneTab()
    {
        var vm = MakeVm();
        var globalTab = (GlobalChatTabViewModel)vm.ChatTabs[0];

        vm.OnChatMessageReceived("Zone", "Bob", "Hey", DateTimeOffset.UtcNow);

        var zoneTab = (ZoneChatTabViewModel)vm.ChatTabs[1];
        zoneTab.Messages.Should().ContainSingle();
        globalTab.Messages.Should().BeEmpty();
    }

    [Fact]
    public void OnChatMessageReceived_System_Broadcasts_To_All_Open_Tabs()
    {
        var vm = MakeVm();
        // Open a whisper tab so we have 3 tabs to test broadcast
        vm.OnChatMessageReceived("Whisper", "Alice", "hi", DateTimeOffset.UtcNow);

        vm.OnChatMessageReceived("System", "Server", "Maintenance soon.", DateTimeOffset.UtcNow);

        foreach (var tab in vm.ChatTabs)
            tab.Messages.Should().Contain(m => m.Channel == "System");
    }

    [Fact]
    public void OnChatMessageReceived_Whisper_From_Other_Creates_WhisperTab_For_Sender()
    {
        var vm = MakeVm();

        vm.OnChatMessageReceived("Whisper", "Carol", "psst", DateTimeOffset.UtcNow);

        var whisperTab = vm.ChatTabs.OfType<WhisperChatTabViewModel>().Single();
        whisperTab.TargetName.Should().Be("Carol");
        whisperTab.Messages.Should().ContainSingle();
    }

    [Fact]
    public void OnChatMessageReceived_Whisper_Echo_Uses_Name_After_To()
    {
        // When the server echoes an outgoing whisper it prefixes sender with "To Alice"
        var vm = MakeVm();

        vm.OnChatMessageReceived("Whisper", "To Alice", "message body", DateTimeOffset.UtcNow);

        var whisperTab = vm.ChatTabs.OfType<WhisperChatTabViewModel>().Single();
        whisperTab.TargetName.Should().Be("Alice");
    }

    // ── GetOrCreateWhisperTab ────────────────────────────────────────────────

    [Fact]
    public void StartWhisperFromPlayer_Creates_Tab_And_Activates_It()
    {
        var vm = MakeVm();

        vm.OnPlayerEntered("Dave");
        // Simulate clicking the Whisper button by raising the message directly
        vm.OnChatMessageReceived("Whisper", "Dave", "yo", DateTimeOffset.UtcNow);

        var whisperTab = vm.ChatTabs.OfType<WhisperChatTabViewModel>()
                           .FirstOrDefault(t => t.TargetName == "Dave");
        whisperTab.Should().NotBeNull();
    }

    [Fact]
    public void GetOrCreateWhisperTab_Returns_Same_Tab_On_Second_Call_For_Same_Name()
    {
        var vm = MakeVm();

        vm.OnChatMessageReceived("Whisper", "Eve", "first", DateTimeOffset.UtcNow);
        vm.OnChatMessageReceived("Whisper", "Eve", "second", DateTimeOffset.UtcNow);

        vm.ChatTabs.OfType<WhisperChatTabViewModel>().Should().ContainSingle(t => t.TargetName == "Eve");
    }

    [Fact]
    public void Opening_SixthWhisperTab_Displaces_Oldest()
    {
        var vm = MakeVm();
        var names = new[] { "A", "B", "C", "D", "E" };
        foreach (var name in names)
            vm.OnChatMessageReceived("Whisper", name, "msg", DateTimeOffset.UtcNow);

        vm.ChatTabs.Should().HaveCount(7); // 2 fixed + 5 whisper

        // Add a 6th whisper — should displace "A"
        vm.OnChatMessageReceived("Whisper", "F", "msg", DateTimeOffset.UtcNow);

        vm.ChatTabs.Should().HaveCount(7);
        vm.ChatTabs.OfType<WhisperChatTabViewModel>().Select(t => t.TargetName)
            .Should().NotContain("A").And.Contain("F");
    }

    // ── Tab close ────────────────────────────────────────────────────────────

    [Fact]
    public void Closing_ActiveWhisperTab_Falls_Back_To_ZoneTab()
    {
        var vm = MakeVm();
        vm.OnChatMessageReceived("Whisper", "Ghost", "boo", DateTimeOffset.UtcNow);
        var whisperTab = vm.ChatTabs.OfType<WhisperChatTabViewModel>().Single();
        vm.ActiveChatTab = whisperTab;

        whisperTab.CloseCommand!.Execute().Subscribe();

        vm.ActiveChatTab.Should().BeOfType<ZoneChatTabViewModel>();
        vm.ChatTabs.Should().NotContain(whisperTab);
    }

    [Fact]
    public void Closing_InactiveWhisperTab_Does_Not_Change_ActiveTab()
    {
        var vm = MakeVm();
        vm.OnChatMessageReceived("Whisper", "Hank", "hey", DateTimeOffset.UtcNow);
        var whisperTab = vm.ChatTabs.OfType<WhisperChatTabViewModel>().Single();
        // Leave global as the active tab
        vm.ActiveChatTab.Should().BeOfType<GlobalChatTabViewModel>();

        whisperTab.CloseCommand!.Execute().Subscribe();

        vm.ActiveChatTab.Should().BeOfType<GlobalChatTabViewModel>();
    }

    // ── SendChatCommand canExecute ────────────────────────────────────────────

    [Fact]
    public void SendChatCommand_Cannot_Execute_When_Input_Empty()
    {
        var vm = MakeVm();
        vm.ChatInput = string.Empty;

        var canExecute = false;
        vm.SendChatCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SendChatCommand_Can_Execute_When_Input_Has_Text_And_Tab_Is_Set()
    {
        var vm = MakeVm();
        vm.ChatInput = "Hello";

        var canExecute = false;
        vm.SendChatCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeTrue();
    }

    // ── /w prefix interception ───────────────────────────────────────────────

    [Fact]
    public void ChatInput_W_Prefix_Opens_WhisperTab_And_Strips_Prefix()
    {
        var vm = MakeVm();

        vm.ChatInput = "/w Ivan hello there";

        var whisperTab = vm.ChatTabs.OfType<WhisperChatTabViewModel>()
                           .FirstOrDefault(t => t.TargetName == "Ivan");
        whisperTab.Should().NotBeNull();
        vm.ActiveChatTab.Should().BeSameAs(whisperTab);
        vm.ChatInput.Should().Be("hello there");
    }

    // ── Slash command routing ────────────────────────────────────────────────

    [Fact]
    public async Task SendChat_SlashCommand_On_ZoneTab_Routes_To_SendChatMessage()
    {
        var fake = new FakeServerConnectionService();
        var vm = new GameViewModel(fake, new FakeZoneService(), new TokenStore(), new FakeNavigationService());
        vm.ChatInput = "/roll 20";

        await vm.SendChatCommand.Execute().ToTask();

        fake.SentCommands.Should().ContainSingle(c => c.Method == "SendChatMessage");
        fake.SentCommands.Should().NotContain(c => c.Method == "SendZoneMessage");
    }

    [Fact]
    public async Task SendChat_SlashCommand_On_GlobalTab_Routes_To_SendChatMessage()
    {
        var fake = new FakeServerConnectionService();
        var vm = new GameViewModel(fake, new FakeZoneService(), new TokenStore(), new FakeNavigationService());
        vm.ActiveChatTab = vm.ChatTabs.OfType<GlobalChatTabViewModel>().First();
        vm.ChatInput = "/help";

        await vm.SendChatCommand.Execute().ToTask();

        fake.SentCommands.Should().ContainSingle(c => c.Method == "SendChatMessage");
        fake.SentCommands.Should().NotContain(c => c.Method == "SendGlobalMessage");
    }

    [Fact]
    public async Task SendChat_NormalText_On_ZoneTab_Routes_To_SendZoneMessage()
    {
        var fake = new FakeServerConnectionService();
        var vm = new GameViewModel(fake, new FakeZoneService(), new TokenStore(), new FakeNavigationService());
        vm.ActiveChatTab = vm.ChatTabs.OfType<ZoneChatTabViewModel>().First();
        vm.ChatInput = "Hello everyone!";

        await vm.SendChatCommand.Execute().ToTask();

        fake.SentCommands.Should().ContainSingle(c => c.Method == "SendZoneMessage");
        fake.SentCommands.Should().NotContain(c => c.Method == "SendChatMessage");
    }

    [Fact]
    public async Task SendChat_NormalText_On_GlobalTab_Routes_To_SendGlobalMessage()
    {
        var fake = new FakeServerConnectionService();
        var vm = new GameViewModel(fake, new FakeZoneService(), new TokenStore(), new FakeNavigationService());
        vm.ActiveChatTab = vm.ChatTabs.OfType<GlobalChatTabViewModel>().First();
        vm.ChatInput = "Hello global!";

        await vm.SendChatCommand.Execute().ToTask();

        fake.SentCommands.Should().ContainSingle(c => c.Method == "SendGlobalMessage");
        fake.SentCommands.Should().NotContain(c => c.Method == "SendChatMessage");
    }

    [Fact]
    public async Task SendChat_NormalText_On_WhisperTab_Routes_To_SendWhisper()
    {
        var fake = new FakeServerConnectionService();
        var vm = new GameViewModel(fake, new FakeZoneService(), new TokenStore(), new FakeNavigationService());
        vm.OnChatMessageReceived("Whisper", "Alice", "hi", DateTimeOffset.UtcNow);
        var whisperTab = vm.ChatTabs.OfType<WhisperChatTabViewModel>().First();
        vm.ActiveChatTab = whisperTab;
        vm.ChatInput = "Hey Alice!";

        await vm.SendChatCommand.Execute().ToTask();

        fake.SentCommands.Should().ContainSingle(c => c.Method == "SendWhisper");
        fake.SentCommands.Should().NotContain(c => c.Method == "SendChatMessage");
    }

    [Fact]
    public async Task SendChat_SlashCommand_Clears_ChatInput_After_Send()
    {
        var fake = new FakeServerConnectionService();
        var vm = new GameViewModel(fake, new FakeZoneService(), new TokenStore(), new FakeNavigationService());
        vm.ChatInput = "/afk brb";

        await vm.SendChatCommand.Execute().ToTask();

        vm.ChatInput.Should().BeEmpty();
    }

    // ── Command suggestion popup ─────────────────────────────────────────────

    [Fact]
    public void OnChatCommandsReceived_Populates_AvailableCommands()
    {
        var vm = MakeVm();
        var commands = new List<ChatCommandInfoDto>
        {
            new("roll", "/roll [max]", "Roll dice", null),
            new("help", "/help", "Show help", null),
        };

        vm.OnChatCommandsReceived(commands);

        vm.AvailableCommands.Should().HaveCount(2);
    }

    [Fact]
    public void ChatInput_Slash_Alone_Opens_Popup_With_All_Commands()
    {
        var vm = MakeVm();
        vm.OnChatCommandsReceived([new("roll", "/roll [max]", "Roll dice", null)]);

        vm.ChatInput = "/";

        vm.IsCommandPopupOpen.Should().BeTrue();
        vm.CommandSuggestions.Should().HaveCount(1);
    }

    [Fact]
    public void ChatInput_Slash_With_Prefix_Filters_Suggestions()
    {
        var vm = MakeVm();
        vm.OnChatCommandsReceived(
        [
            new("roll", "/roll [max]", "Roll dice", null),
            new("respawn", "/respawn", "Respawn", null),
            new("help", "/help", "Show help", null),
        ]);

        vm.ChatInput = "/ro";

        vm.CommandSuggestions.Should().ContainSingle(c => c.Command == "roll");
        vm.CommandSuggestions.Should().NotContain(c => c.Command == "help");
    }

    [Fact]
    public void ChatInput_Without_Slash_Closes_Popup()
    {
        var vm = MakeVm();
        vm.OnChatCommandsReceived([new("roll", "/roll [max]", "Roll dice", null)]);
        vm.ChatInput = "/";

        vm.ChatInput = "Hello";

        vm.IsCommandPopupOpen.Should().BeFalse();
        vm.CommandSuggestions.Should().BeEmpty();
    }

    [Fact]
    public void ChatInput_After_Space_Closes_Popup()
    {
        var vm = MakeVm();
        vm.OnChatCommandsReceived([new("roll", "/roll [max]", "Roll dice", null)]);
        vm.ChatInput = "/";

        vm.ChatInput = "/roll 20";

        vm.IsCommandPopupOpen.Should().BeFalse();
    }

    [Fact]
    public void SelectCommand_On_Suggestion_Sets_ChatInput_To_Usage()
    {
        var vm = MakeVm();
        vm.OnChatCommandsReceived([new("roll", "/roll [max]", "Roll dice", null)]);
        vm.ChatInput = "/ro";
        var suggestion = vm.CommandSuggestions.First();

        suggestion.SelectCommand.Execute().Subscribe();

        vm.ChatInput.Should().Be("/roll [max]");
        vm.IsCommandPopupOpen.Should().BeFalse();
    }

    [Fact]
    public void ToggleCommandHelp_Opens_Full_List_When_Closed()
    {
        var vm = MakeVm();
        vm.OnChatCommandsReceived(
        [
            new("roll", "/roll [max]", "Roll dice", null),
            new("help", "/help", "Show help", null),
        ]);

        vm.ToggleCommandHelpCommand.Execute().Subscribe();

        vm.IsCommandPopupOpen.Should().BeTrue();
        vm.CommandSuggestions.Should().HaveCount(2);
    }

    [Fact]
    public void ToggleCommandHelp_Closes_Full_List_When_Open()
    {
        var vm = MakeVm();
        vm.OnChatCommandsReceived([new("roll", "/roll [max]", "Roll dice", null)]);
        vm.ToggleCommandHelpCommand.Execute().Subscribe();

        vm.ToggleCommandHelpCommand.Execute().Subscribe();

        vm.IsCommandPopupOpen.Should().BeFalse();
    }
}
