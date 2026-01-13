using Serilog;

namespace RealmForge;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		
		// Hook global exception handlers
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "RealmForge" };
	}
	
	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			Log.Fatal(ex, "Unhandled exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);
		}
		else
		{
			Log.Fatal("Unhandled non-exception error: {Error}", e.ExceptionObject);
		}
		
		Log.CloseAndFlush();
	}
	
	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Log.Error(e.Exception, "Unobserved task exception occurred");
		e.SetObserved(); // Prevent app crash
	}
}
