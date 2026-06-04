using System.Globalization;
using System.Reflection;
using Maui.Diagnostics.Playground.Diagnostics;
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

		builder
			.UseMauiApp<App>()
			.UseSentry(options =>
			{
				var sentryConfiguration = builder.Configuration.GetSection("Sentry");
				options.Dsn = sentryConfiguration["Dsn"];
				options.Debug = GetBoolean(sentryConfiguration, "Debug", false);
				options.TracesSampleRate = GetDouble(sentryConfiguration, "TracesSampleRate", 0.0);
				options.EnableLogs = GetBoolean(sentryConfiguration, "EnableLogs", false);
				options.EnableMetrics = GetBoolean(sentryConfiguration, "EnableMetrics", true);
			})
			.ConfigureFonts(fonts =>
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
		builder.Services.AddSingleton<IDiagnosticsSelfReportService, DiagnosticsSelfReportService>();
		builder.Services.AddTransient<GalleryViewModel>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ScenarioDetailPage>();

		return builder.Build();
	}

	private static void LoadConfiguration(ConfigurationManager configuration)
	{
		var assembly = typeof(MauiProgram).Assembly;
		var stream = assembly.GetManifestResourceStream("Maui.Diagnostics.Playground.appsettings.json")
			?? throw new InvalidOperationException("Embedded appsettings.json was not found.");

		var configurationStream = new MemoryStream();
		stream.CopyTo(configurationStream);
		configurationStream.Position = 0;

		configuration.AddJsonStream(configurationStream);
		configuration.AddEnvironmentVariables("MAUI_DIAGNOSTICS_");
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
