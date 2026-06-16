using System.Globalization;
using System.Reflection;
using Maui.Diagnostics.Playground.Diagnostics;
using Maui.Diagnostics.Playground.Features.DiagnosticsFiles;
using Maui.Diagnostics.Playground.Features.Gallery;
using Maui.Diagnostics.Playground.Features.Scenarios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maui.Diagnostics.Playground;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		LoadConfiguration(builder.Configuration);

		builder.UseMauiApp<App>();

#if CRASH_VENDOR_SENTRY
		builder.UseSentry(options =>
		{
			var sentryConfiguration = builder.Configuration.GetSection("Sentry");
			options.Dsn = sentryConfiguration["Dsn"];
			options.Debug = GetBoolean(sentryConfiguration, "Debug", false);
			options.TracesSampleRate = GetDouble(sentryConfiguration, "TracesSampleRate", 0.0);
			options.EnableLogs = GetBoolean(sentryConfiguration, "EnableLogs", false);
			options.EnableMetrics = GetBoolean(sentryConfiguration, "EnableMetrics", true);
		});
#endif

		builder.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<ICrashScenarioCatalog, CrashScenarioCatalog>();
		builder.Services.AddSingleton<ICrashScenarioRunner, ManagedCrashScenarioRunner>();
		builder.Services.AddSingleton<IDiagnosticsArtifactService, DiagnosticsArtifactService>();
		builder.Services.AddSingleton<IDiagnosticsSelfReportService, DiagnosticsSelfReportService>();
		builder.Services.AddTransient<GalleryViewModel>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<DiagnosticsFilesPage>();
		builder.Services.AddTransient<DiagnosticsFileDetailPage>();
		builder.Services.AddTransient<ScenarioDetailPage>();

		return builder.Build();
	}

	private static void LoadConfiguration(ConfigurationManager configuration)
	{
		var assembly = typeof(MauiProgram).Assembly;
		AddEmbeddedJsonConfiguration(
			configuration,
			assembly,
			"Maui.Diagnostics.Playground.appsettings.json",
			required: true);
		AddEmbeddedJsonConfiguration(
			configuration,
			assembly,
			"Maui.Diagnostics.Playground.appsettings.local.json",
			required: false);
		configuration.AddEnvironmentVariables("MAUI_DIAGNOSTICS_");
	}

	private static void AddEmbeddedJsonConfiguration(
		ConfigurationManager configuration,
		Assembly assembly,
		string resourceName,
		bool required)
	{
		var stream = assembly.GetManifestResourceStream(resourceName);

		if (stream is null)
		{
			if (required)
			{
				throw new InvalidOperationException("Embedded appsettings.json was not found.");
			}

			return;
		}

		using (stream)
		{
			var configurationStream = new MemoryStream();
			stream.CopyTo(configurationStream);
			configurationStream.Position = 0;

			configuration.AddJsonStream(configurationStream);
		}
	}

	private static bool GetBoolean(IConfiguration configuration, string key, bool defaultValue)
	{
		return bool.TryParse(configuration[key], out var value) ? value : defaultValue;
	}

	private static double GetDouble(IConfiguration configuration, string key, double defaultValue)
	{
		return double.TryParse(configuration[key], CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
	}
}
