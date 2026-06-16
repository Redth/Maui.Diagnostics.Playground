using System.Windows.Input;
using Maui.Diagnostics.Playground.Diagnostics;
using Maui.Diagnostics.Playground.Features.DiagnosticsFiles;
using Maui.Diagnostics.Playground.Features.Scenarios;
#if CRASH_VENDOR_SENTRY
using Sentry;
#endif

namespace Maui.Diagnostics.Playground.Features.Gallery;

public sealed class GalleryViewModel
{
    public GalleryViewModel(
        ICrashScenarioCatalog scenarioCatalog,
        IDiagnosticsSelfReportService diagnosticsSelfReportService)
    {
        Scenarios = scenarioCatalog.All
            .Where(scenario => scenario.SupportsCurrentDevice())
            .OrderBy(scenario => scenario.Category)
            .ThenBy(scenario => scenario.Title)
            .ToArray();
        Summary = diagnosticsSelfReportService.GetSummary();
        OpenScenarioCommand = new Command<CrashScenarioDescriptor>(OpenScenario);
        OpenDiagnosticsFilesCommand = new Command(OpenDiagnosticsFiles);
#if CRASH_VENDOR_SENTRY
        SendSentryDiagnosticsCommand = new Command(SendSentryDiagnostics);
#else
        SendSentryDiagnosticsCommand = new Command(() => { });
#endif
    }

    public IReadOnlyList<CrashScenarioDescriptor> Scenarios { get; }

    public DiagnosticsSummary Summary { get; }

    public ICommand OpenScenarioCommand { get; }

    public ICommand OpenDiagnosticsFilesCommand { get; }

    public ICommand SendSentryDiagnosticsCommand { get; }

    public bool IsSentryEnabled =>
#if CRASH_VENDOR_SENTRY
        true;
#else
        false;
#endif

    public string ScenarioCountText => $"{Scenarios.Count} scenarios available on {DeviceInfo.Current.Platform}";

    private static async void OpenScenario(CrashScenarioDescriptor? scenario)
    {
        if (scenario is null)
        {
            return;
        }

        await Shell.Current.GoToAsync(ScenarioDetailPage.Route, new ShellNavigationQueryParameters
        {
            { "scenarioKey", scenario.Key }
        });
    }

    private static async void OpenDiagnosticsFiles()
    {
        await Shell.Current.GoToAsync(DiagnosticsFilesPage.Route);
    }

#if CRASH_VENDOR_SENTRY
    private static async void SendSentryDiagnostics()
    {
        SentrySdk.CaptureMessage("MAUI Diagnostics Playground Sentry verification");
        SentrySdk.Logger.LogInfo("MAUI Diagnostics Playground Sentry verification log");
        SentrySdk.Logger.LogError("MAUI Diagnostics Playground formatted {0} log message", "verification");

        var tags = new KeyValuePair<string, object>[]
        {
            new("platform", DeviceInfo.Current.Platform.ToString()),
            new("app_version", AppInfo.Current.VersionString)
        };

        SentrySdk.Metrics.EmitCounter("maui_diagnostics_playground_test_click", 1, tags);
        SentrySdk.Metrics.EmitDistribution("maui_diagnostics_playground_test_latency", 15.0, MeasurementUnit.Duration.Millisecond, tags);
        SentrySdk.Metrics.EmitGauge("maui_diagnostics_playground_test_gauge", 15.0, MeasurementUnit.Duration.Millisecond, tags);

        await Shell.Current.DisplayAlertAsync(
            "Sentry test sent",
            "Message, logs, and metrics were queued. Configure Sentry:Dsn before expecting them to arrive in Sentry.",
            "OK");
    }
#endif
}
