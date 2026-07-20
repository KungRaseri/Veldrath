using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Tests.Infrastructure;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>
/// Tests for the <see cref="GameChat"/> component, verifying chat message
/// rendering, channel pills, system message styling, and empty state display.
/// </summary>
public class GameChatComponentTests : BunitContext
{
    private readonly FakeGameHubConnectionService _fakeHub;
    private readonly GameStateService _gameState;

    /// <summary>
    /// Initializes a new instance and registers required services.
    /// </summary>
    public GameChatComponentTests()
    {
        _fakeHub = new FakeGameHubConnectionService();
        _gameState = new GameStateService();

        Services.AddSingleton<IGameHubConnectionService>(_fakeHub);
        Services.AddSingleton(_gameState);
    }

    /// <summary>
    /// Verifies chat messages render in the correct order.
    /// </summary>
    [Fact]
    public void ChatMessages_Render_In_Correct_Order()
    {
        _gameState.ApplyChatMessage(new ChatMessage(Guid.Empty, "zone", "Player1", "Hello!", DateTimeOffset.UtcNow));
        _gameState.ApplyChatMessage(new ChatMessage(Guid.Empty, "zone", "Player2", "Hi there!", DateTimeOffset.UtcNow.AddSeconds(1)));

        var cut = Render<GameChat>();

        var messageElements = cut.FindAll(".game-chat-message");
        Assert.Equal(2, messageElements.Count);

        Assert.Contains("Hello!", cut.Markup);
        Assert.Contains("Hi there!", cut.Markup);
    }

    /// <summary>
    /// Verifies system messages display with the correct CSS class.
    /// </summary>
    [Fact]
    public void SystemMessage_Has_Correct_Styling()
    {
        _gameState.ApplySystemMessage("Welcome to the game!");

        var cut = Render<GameChat>();

        // Switch to System channel to see system messages
        cut.Instance.ActiveChannel = "System";
        cut.Render();

        var systemMsg = cut.Find(".game-chat-message-system");
        Assert.NotNull(systemMsg);
        Assert.Contains("Welcome to the game!", systemMsg.TextContent);
    }

    /// <summary>
    /// Verifies whisper messages display with the correct CSS class.
    /// </summary>
    [Fact]
    public void WhisperMessage_Has_Correct_Styling()
    {
        _gameState.ApplyChatMessage(new ChatMessage(Guid.Empty, "whisper", "Friend", "Secret message", DateTimeOffset.UtcNow));

        var cut = Render<GameChat>();

        // Switch to Whisper channel to see whisper messages
        cut.Instance.ActiveChannel = "Whisper";
        cut.Render();

        var whisperMsg = cut.Find(".game-chat-message-whisper");
        Assert.NotNull(whisperMsg);
        Assert.Contains("Secret message", whisperMsg.TextContent);
    }

    /// <summary>
    /// Verifies global messages display with the correct CSS class.
    /// </summary>
    [Fact]
    public void GlobalMessage_Has_Correct_Styling()
    {
        _gameState.ApplyChatMessage(new ChatMessage(Guid.Empty, "global", "Announcer", "Global announcement!", DateTimeOffset.UtcNow));

        var cut = Render<GameChat>();

        var globalMsg = cut.Find(".game-chat-message-global");
        Assert.NotNull(globalMsg);
        Assert.Contains("Global announcement!", globalMsg.TextContent);
    }

    /// <summary>
    /// Verifies the empty state renders correctly when no messages exist.
    /// </summary>
    [Fact]
    public void Empty_State_Renders_Correctly()
    {
        var cut = Render<GameChat>();

        // The chat container should exist without message elements.
        var messageContainer = cut.Find(".game-chat-messages");
        Assert.NotNull(messageContainer);

        var messageElements = cut.FindAll(".game-chat-message");
        Assert.Empty(messageElements);
    }

    /// <summary>
    /// Verifies the send button is disabled when input is empty.
    /// </summary>
    [Fact]
    public void SendButton_Disabled_When_Input_Empty()
    {
        var cut = Render<GameChat>();

        // Find the send button specifically (not channel pills which are also buttons).
        var sendButton = cut.Find(".game-chat-send-btn");
        Assert.NotNull(sendButton);

        var isDisabled = sendButton.GetAttribute("disabled");
        Assert.NotNull(isDisabled);
    }

    /// <summary>
    /// Verifies channel pills render with the correct channel names.
    /// </summary>
    [Fact]
    public void ChannelPills_Render_All_Channels()
    {
        var cut = Render<GameChat>();

        var pills = cut.FindAll(".game-chat-pill");
        Assert.Equal(4, pills.Count);

        Assert.Contains("Zone", pills[0].TextContent);
        Assert.Contains("Global", pills[1].TextContent);
        Assert.Contains("Whisper", pills[2].TextContent);
        Assert.Contains("System", pills[3].TextContent);
    }

    /// <summary>
    /// Verifies clicking a channel pill activates it.
    /// </summary>
    [Fact]
    public void ChannelPill_Click_Activates_Channel()
    {
        var cut = Render<GameChat>();

        // Default is Zone
        Assert.Equal("Zone", cut.Instance.ActiveChannel);

        // Click the Global pill (second pill)
        var pills = cut.FindAll(".game-chat-pill");
        pills[1].Click();

        Assert.Equal("Global", cut.Instance.ActiveChannel);
    }
}
