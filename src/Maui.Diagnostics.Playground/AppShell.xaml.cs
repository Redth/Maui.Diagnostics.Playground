using Microsoft.Extensions.DependencyInjection;
using Maui.Diagnostics.Playground.Features.DiagnosticsFiles;
using Maui.Diagnostics.Playground.Features.Gallery;
using Maui.Diagnostics.Playground.Features.Scenarios;

namespace Maui.Diagnostics.Playground;

public partial class AppShell : Shell
{
	public AppShell(IServiceProvider services)
	{
		InitializeComponent();

		Items.Add(new ShellContent
		{
			Title = "Gallery",
			Route = "gallery",
			ContentTemplate = new DataTemplate(() => services.GetRequiredService<MainPage>())
		});

		Routing.RegisterRoute(ScenarioDetailPage.Route, typeof(ScenarioDetailPage));
		Routing.RegisterRoute(DiagnosticsFilesPage.Route, typeof(DiagnosticsFilesPage));
		Routing.RegisterRoute(DiagnosticsFileDetailPage.Route, typeof(DiagnosticsFileDetailPage));
	}
}
