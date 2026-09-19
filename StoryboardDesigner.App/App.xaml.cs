using StoryboardDesigner.App.Composition;
using StoryboardDesigner.App.Services;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace StoryboardDesigner.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
	private const string SmokeProjectPathEnvironmentVariable = "STORYBOARD_DESIGNER_SMOKE_PROJECT_PATH";
	private static readonly HashSet<string> ShownExceptionSignatures = new(StringComparer.Ordinal);

	protected override void OnStartup(System.Windows.StartupEventArgs e)
	{
		RegisterGlobalUnhandledExceptionHandlers();
		UserEnvironmentVariableRefreshService.RefreshMissingProcessVariables();

		base.OnStartup(e);

		var composition = DesignerAppCompositionFactory.Create();
		var mainViewModel = composition.MainWindowViewModel;

		var smokeProjectPath = Environment.GetEnvironmentVariable(SmokeProjectPathEnvironmentVariable);
		if (!string.IsNullOrWhiteSpace(smokeProjectPath) && File.Exists(smokeProjectPath))
		{
			mainViewModel.OpenProject(smokeProjectPath);
		}

		var mainWindow = composition.MainWindow;
		MainWindow = mainWindow;
		mainWindow.Show();
	}

	private void RegisterGlobalUnhandledExceptionHandlers()
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		ShowUnhandledExceptionDetails("DispatcherUnhandledException", e.Exception, isTerminating: false);
		// Keep default fail-fast behavior for unknown crashes after reporting details.
		e.Handled = false;
	}

	private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			ShowUnhandledExceptionDetails("AppDomain.UnhandledException", ex, e.IsTerminating);
			return;
		}

		var report = BuildUnhandledExceptionReport(
			"AppDomain.UnhandledException",
			e.IsTerminating,
			$"Non-Exception unhandled object: {e.ExceptionObject}",
			null);
		ShowCrashReport(report);
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		ShowUnhandledExceptionDetails("TaskScheduler.UnobservedTaskException", e.Exception, isTerminating: false);
		e.SetObserved();
	}

	private void ShowUnhandledExceptionDetails(string source, Exception exception, bool isTerminating)
	{
		var report = BuildUnhandledExceptionReport(source, isTerminating, exception.ToString(), exception);

		if (ShouldSuppressInteractiveDialog(source, exception))
		{
			WriteCrashReportToTemp(report);
			return;
		}

		var signature = BuildExceptionSignature(source, exception);
		if (!TryRegisterShownException(signature))
		{
			WriteCrashReportToTemp(report);
			return;
		}

		ShowCrashReport(report);
	}

	private static bool ShouldSuppressInteractiveDialog(string source, Exception exception)
	{
		if (!string.Equals(source, "TaskScheduler.UnobservedTaskException", StringComparison.Ordinal))
		{
			return false;
		}

		var exceptionText = exception.ToString();
		return exceptionText.Contains("AADSTS53003", StringComparison.OrdinalIgnoreCase)
			|| exceptionText.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase)
			|| exceptionText.Contains("Conditional Access", StringComparison.OrdinalIgnoreCase);
	}

	private static string BuildExceptionSignature(string source, Exception exception)
	{
		return string.Concat(source, "|", exception.GetType().FullName, "|", exception.Message);
	}

	private static bool TryRegisterShownException(string signature)
	{
		lock (ShownExceptionSignatures)
		{
			return ShownExceptionSignatures.Add(signature);
		}
	}

	private static string BuildUnhandledExceptionReport(string source, bool isTerminating, string exceptionText, Exception? exception)
	{
		var sb = new StringBuilder();
		sb.AppendLine("=== STORYBOARD DESIGNER UNHANDLED EXCEPTION REPORT ===");
		sb.AppendLine($"UTC: {DateTime.UtcNow:O}");
		sb.AppendLine($"Source: {source}");
		sb.AppendLine($"IsTerminating: {isTerminating}");
		sb.AppendLine($"OS: {Environment.OSVersion}");
		sb.AppendLine($"Process: {Environment.ProcessPath}");
		sb.AppendLine($"ProcessId: {Environment.ProcessId}");
		sb.AppendLine($"ThreadId: {Environment.CurrentManagedThreadId}");
		sb.AppendLine($"CLR: {Environment.Version}");
		sb.AppendLine();
		sb.AppendLine("--- Exception.ToString() ---");
		sb.AppendLine(exceptionText);

		if (exception is not null)
		{
			sb.AppendLine();
			sb.AppendLine("--- StackTrace ---");
			sb.AppendLine(exception.StackTrace ?? "(null)");

			var current = exception.InnerException;
			var depth = 0;
			while (current is not null)
			{
				depth++;
				sb.AppendLine();
				sb.AppendLine($"--- InnerException[{depth}] ---");
				sb.AppendLine(current.ToString());
				current = current.InnerException;
			}
		}

		return sb.ToString();
	}

	private static void ShowCrashReport(string report)
	{
		var reportPaths = WriteCrashReportBestEffort(report);
		var primaryPath = reportPaths.FirstOrDefault() ?? "(write failed)";

		try
		{
			var pathSummary = string.Join("\n", reportPaths.Select(path => $"- {path}"));
			var message = $"Unhandled exception captured. Full technical report:\n\n{report}\n\nLog files:\n{pathSummary}";
			System.Windows.MessageBox.Show(
				message,
				"Unhandled Exception",
				System.Windows.MessageBoxButton.OK,
				System.Windows.MessageBoxImage.Error);
		}
		catch
		{
			_ = primaryPath;
			// Last-chance handler should never throw.
		}
	}

	private static string WriteCrashReportToTemp(string report)
	{
		return WriteCrashReportBestEffort(report).FirstOrDefault() ?? string.Empty;
	}

	private static IReadOnlyList<string> WriteCrashReportBestEffort(string report)
	{
		var utcNow = DateTime.UtcNow;
		var writtenPaths = new List<string>();

		var tempTimestampedPath = Path.Combine(
			Path.GetTempPath(),
			$"StoryboardDesigner.Unhandled.{utcNow:yyyyMMdd-HHmmss-fff}.log");

		TryWriteReport(tempTimestampedPath, report, writtenPaths);

		var tempLatestPath = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Unhandled.latest.log");
		TryWriteReport(tempLatestPath, report, writtenPaths);

		var localAppDataCrashFolder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"StoryboardDesigner",
			"CrashReports");
		var localAppDataLatestPath = Path.Combine(localAppDataCrashFolder, "Unhandled.latest.log");
		TryWriteReport(localAppDataLatestPath, report, writtenPaths);

		return writtenPaths;
	}

	private static void TryWriteReport(string path, string report, List<string> writtenPaths)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		try
		{
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
			using var writer = new StreamWriter(stream, Encoding.UTF8);
			writer.Write(report);
			writer.Flush();
			stream.Flush(true);
			writtenPaths.Add(path);
		}
		catch
		{
			// Best effort only during crash handling.
		}
	}
}


