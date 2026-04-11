using System.Reactive.Concurrency;
using ReactiveUI;
using Veldrath.Client.Services;

namespace Veldrath.Client.Tests;

/// <summary>
/// Configures ReactiveUI to use a synchronous scheduler so that reactive
/// commands complete synchronously during unit tests without requiring a running
/// UI dispatcher.
/// </summary>
public abstract class TestBase
{
    protected TestBase()
    {
        RxApp.MainThreadScheduler = Scheduler.CurrentThread;
        RxApp.TaskpoolScheduler   = Scheduler.CurrentThread;
    }
}
