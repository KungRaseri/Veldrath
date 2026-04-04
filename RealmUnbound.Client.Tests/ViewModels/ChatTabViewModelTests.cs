using ReactiveUI;
using System.Reactive.Concurrency;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests.ViewModels;

/// <summary>Unit tests for <see cref="ChatTabViewModel"/> and its concrete subtypes.</summary>
public class ChatTabViewModelTests
{
    public ChatTabViewModelTests()
    {
        RxApp.MainThreadScheduler = Scheduler.CurrentThread;
        RxApp.TaskpoolScheduler   = Scheduler.CurrentThread;
    }

    // ── GlobalChatTabViewModel ───────────────────────────────────────────────

    [Fact]
    public void GlobalTab_Has_Correct_Header_And_Cannot_Close()
    {
        var tab = new GlobalChatTabViewModel();
        tab.TabHeader.Should().Be("Global");
        tab.CanClose.Should().BeFalse();
        tab.CloseCommand.Should().BeNull();
    }

    // ── ZoneChatTabViewModel ─────────────────────────────────────────────────

    [Fact]
    public void ZoneTab_Has_Correct_Header_And_Cannot_Close()
    {
        var tab = new ZoneChatTabViewModel();
        tab.TabHeader.Should().Be("Zone");
        tab.CanClose.Should().BeFalse();
        tab.CloseCommand.Should().BeNull();
    }

    // ── WhisperChatTabViewModel ──────────────────────────────────────────────

    [Fact]
    public void WhisperTab_Header_Includes_Target_Name()
    {
        var tab = new WhisperChatTabViewModel("Alice", _ => { });
        tab.TabHeader.Should().Be("W: Alice");
        tab.TargetName.Should().Be("Alice");
        tab.CanClose.Should().BeTrue();
        tab.CloseCommand.Should().NotBeNull();
    }

    [Fact]
    public void WhisperTab_CloseCommand_Invokes_OnClose_Callback()
    {
        WhisperChatTabViewModel? received = null;
        var tab = new WhisperChatTabViewModel("Bob", t => { received = t; });

        tab.CloseCommand!.Execute().Subscribe();

        received.Should().BeSameAs(tab);
    }

    // ── AddMessage / message cap ─────────────────────────────────────────────

    [Fact]
    public void AddMessage_Appends_To_Messages()
    {
        var tab = new GlobalChatTabViewModel();
        var msg = new ChatMessageViewModel("Global", "Alice", "Hello", DateTimeOffset.UtcNow, false);

        tab.AddMessage(msg);

        tab.Messages.Should().ContainSingle().Which.Should().BeSameAs(msg);
    }

    [Fact]
    public void AddMessage_Evicts_Oldest_When_Cap_Is_Reached()
    {
        var tab = new GlobalChatTabViewModel();
        var ts = DateTimeOffset.UtcNow;

        // Fill to cap
        for (var i = 0; i < 200; i++)
            tab.AddMessage(new ChatMessageViewModel("Global", "Sender", $"msg{i}", ts, false));

        tab.Messages.Should().HaveCount(200);
        tab.Messages[0].Message.Should().Be("msg0");

        // Adding one more should evict the oldest
        var newest = new ChatMessageViewModel("Global", "Sender", "msg200", ts, false);
        tab.AddMessage(newest);

        tab.Messages.Should().HaveCount(200);
        tab.Messages[0].Message.Should().Be("msg1");
        tab.Messages[^1].Should().BeSameAs(newest);
    }
}
