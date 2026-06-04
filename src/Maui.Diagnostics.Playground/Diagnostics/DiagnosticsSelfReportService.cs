using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Maui.Diagnostics.Playground.Features.Scenarios;

namespace Maui.Diagnostics.Playground.Diagnostics;

public sealed class DiagnosticsSelfReportService : IDiagnosticsSelfReportService
{
    public DiagnosticsSummary GetSummary()
    {
        var assembly = typeof(DiagnosticsSelfReportService).Assembly;
        var crashVendor = GetMetadata(assembly, "CrashVendor", "None");
        var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Unknown";
        var useCoreClr = GetMetadata(assembly, "MauiDiagnosticsUseCoreClr", "true");
        var useMonoRuntime = GetMetadata(assembly, "UseMonoRuntime", "false");
        var useNativeProjects = GetMetadata(assembly, "MauiDiagnosticsUseNativeProjects", "true");
        var runtimeIdentifier = GetMetadata(assembly, "RuntimeIdentifier", "Default");
        var frameLimit = GetMetadata(assembly, "CrashReportFrameLimitPerThread", "32");
        var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
            ?? AppContext.TargetFrameworkName
            ?? "Unknown";
        var runtimeFamily = Type.GetType("Mono.Runtime") is null ? "CoreCLR" : "Mono";
        var targetRuntime = IsEnabled(useCoreClr) ? "CoreCLR requested" : "Mono requested";
        var platform = DeviceInfo.Current.Platform.ToString();
        var chips = new[]
        {
            runtimeFamily,
            targetRuntime,
            platform,
            configuration,
            $"Vendor: {crashVendor}"
        };

        var facts = new[]
        {
            new DiagnosticFact("Runtime family", runtimeFamily),
            new DiagnosticFact("Framework", RuntimeInformation.FrameworkDescription),
            new DiagnosticFact("OS", RuntimeInformation.OSDescription),
            new DiagnosticFact("Architecture", RuntimeInformation.ProcessArchitecture.ToString()),
            new DiagnosticFact("Device platform", platform),
            new DiagnosticFact("Target framework", targetFramework),
            new DiagnosticFact("Runtime identifier", EmptyFallback(runtimeIdentifier, "Default")),
            new DiagnosticFact("Build configuration", configuration),
            new DiagnosticFact("Crash vendor", crashVendor),
            new DiagnosticFact("CoreCLR requested", useCoreClr),
            new DiagnosticFact("UseMonoRuntime", useMonoRuntime),
            new DiagnosticFact("Native projects enabled", useNativeProjects),
            new DiagnosticFact("Native crash kit", NativeCrashInterop.GetNativeKitDescription()),
            new DiagnosticFact("Compact frame cap", frameLimit)
        };

        return new DiagnosticsSummary(
            runtimeFamily,
            RuntimeInformation.FrameworkDescription,
            crashVendor,
            configuration,
            chips,
            facts);
    }

    private static string GetMetadata(Assembly assembly, string key, string fallback)
        => assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)
            ?.Value ?? fallback;

    private static bool IsEnabled(string value)
        => bool.TryParse(value, out var result) && result;

    private static string EmptyFallback(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
