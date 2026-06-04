namespace Maui.Diagnostics.Playground.Features.Scenarios;

public sealed class CrashScenarioCatalog : ICrashScenarioCatalog
{
    private readonly IReadOnlyList<CrashScenarioDescriptor> scenarios =
    [
        new(
            "managed-ui-unhandled",
            "Unhandled managed exception",
            "Throws from the UI thread to validate managed exception capture, runtime compact reporting, and vendor unhandled-exception handlers.",
            CrashScenarioCategory.Managed,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonManagedArtifacts,
            ["managed", "ui-thread", "unhandled"]),
        new(
            "managed-background-unhandled",
            "Background thread exception",
            "Throws from a background task to compare UI-thread and worker-thread crash reporting.",
            CrashScenarioCategory.Managed,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonManagedArtifacts,
            ["managed", "background", "task"]),
        new(
            "managed-async-void",
            "Async void exception",
            "Throws after an awaited continuation from an async void path, matching event-handler style failures.",
            CrashScenarioCategory.Managed,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonManagedArtifacts,
            ["managed", "async", "event-handler"]),
        new(
            "runtime-null-reference",
            "NullReferenceException",
            "Dereferences a null object through a no-inline frame so unhandled managed exception reporting has a simple known root cause.",
            CrashScenarioCategory.Runtime,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonManagedArtifacts,
            ["runtime", "nullref"]),
        new(
            "runtime-access-violation",
            "Access violation",
            "Writes to an invalid native address to validate fatal access violation and signal reporting.",
            CrashScenarioCategory.Runtime,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonRuntimeArtifacts,
            ["runtime", "access-violation", "sigsegv"]),
        new(
            "runtime-failfast",
            "Environment.FailFast",
            "Terminates through the runtime fatal path and should produce a compact report with managed frames on CoreCLR.",
            CrashScenarioCategory.Runtime,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonRuntimeArtifacts,
            ["runtime", "failfast", "fatal"]),
        new(
            "runtime-stack-overflow",
            "Stack overflow",
            "Recurses until stack exhaustion to validate runtime stack overflow reporting and frame limits.",
            CrashScenarioCategory.Runtime,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonRuntimeArtifacts,
            ["runtime", "stackoverflow"]),
        new(
            "native-apple-abort",
            "Apple native abort",
            "Calls abort from the Xcode-built CrashNativeKit framework to validate native-origin crash reporting.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Apple,
            typeof(ScenarioDetailPage),
            AppleNativeArtifacts,
            ["native", "apple", "abort"]),
        new(
            "native-apple-sigsegv",
            "Apple native SIGSEGV",
            "Faults inside the Xcode-built CrashNativeKit framework to validate Apple native symbolication.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Apple,
            typeof(ScenarioDetailPage),
            AppleNativeArtifacts,
            ["native", "apple", "sigsegv"]),
        new(
            "native-apple-objc-exception",
            "Apple Objective-C exception",
            "Raises an uncaught NSException from the Xcode-built CrashNativeKit framework.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Apple,
            typeof(ScenarioDetailPage),
            AppleNativeArtifacts,
            ["native", "apple", "objc", "exception"]),
        new(
            "native-android-abort",
            "Android native abort",
            "Calls abort from the Gradle-built CrashNativeKit Android library to compare abort and SIGSEGV tombstone behavior.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Android,
            typeof(ScenarioDetailPage),
            AndroidNativeArtifacts,
            ["native", "android", "abort"]),
        new(
            "native-android-sigsegv",
            "Android native SIGSEGV",
            "Faults inside the Gradle-built CrashNativeKit Android library to validate native-origin crash reporting and tombstone output.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Android,
            typeof(ScenarioDetailPage),
            AndroidNativeArtifacts,
            ["native", "android", "sigsegv"]),
        new(
            "native-illegal-instruction",
            "Native illegal instruction",
            "Raises SIGILL from the project-backed native crash kit to validate non-SEGV signal reporting.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonNativeArtifacts,
            ["native", "sigill"]),
        new(
            "native-background-thread",
            "Background native thread crash",
            "Starts a named native background thread and faults from that thread.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonNativeArtifacts,
            ["native", "background"]),
        new(
            "mixed-managed-native",
            "Mixed managed/native stack",
            "Bounces through managed frames into the project-backed native crash kit before faulting so symbolication can validate interleaved stacks.",
            CrashScenarioCategory.Native,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonRuntimeArtifacts,
            ["native", "managed", "interleaved"]),
        new(
            "resource-memory-pressure",
            "Out-of-memory pressure",
            "Allocates retained memory in chunks until the runtime or operating system terminates the app.",
            CrashScenarioCategory.Resource,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            ["Managed OutOfMemoryException or OS low-memory termination", "Runtime compact report when the runtime can report before termination", "Platform memory pressure or jetsam/low-memory evidence"],
            ["resource", "memory", "oom"]),
        new(
            "resource-ui-hang",
            "UI thread hang",
            "Blocks the UI thread to exercise ANR, watchdog, and hang diagnostics instead of exception reporting.",
            CrashScenarioCategory.Resource,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            ["Android ANR or Apple watchdog evidence when the platform terminates the app", "No managed exception expected", "Vendor hang/session diagnostics when supported"],
            ["resource", "hang", "watchdog", "anr"]),
        new(
            "vendor-handled-exception",
            "Vendor handled exception",
            "Captures a handled exception through the active vendor abstraction without terminating the process.",
            CrashScenarioCategory.Vendor,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            ["Vendor event", "App remains running", "Scenario metadata attached"],
            ["vendor", "handled"]),
        new(
            "lifecycle-startup-crash",
            "Startup lifecycle crash",
            "Crashes early in app startup to verify what handlers are installed before the first page renders.",
            CrashScenarioCategory.Lifecycle,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonManagedArtifacts,
            ["startup", "lifecycle"]),
        new(
            "lifecycle-resume-crash",
            "Resume lifecycle crash",
            "Arms a crash that fires the next time the app resumes from the background.",
            CrashScenarioCategory.Lifecycle,
            CrashScenarioPlatform.Mobile,
            typeof(ScenarioDetailPage),
            CommonManagedArtifacts,
            ["resume", "lifecycle", "background"])
    ];

    public IReadOnlyList<CrashScenarioDescriptor> All => scenarios;

    public CrashScenarioDescriptor GetRequired(string key)
        => scenarios.FirstOrDefault(scenario => scenario.Key == key)
            ?? throw new InvalidOperationException($"Unknown crash scenario '{key}'.");

    private static readonly string[] CommonManagedArtifacts =
    [
        "Runtime compact report when CoreCLR crash reporting is configured",
        "Platform crash log or process termination record",
        "Active vendor event when vendor handlers are enabled"
    ];

    private static readonly string[] CommonRuntimeArtifacts =
    [
        "CoreCLR compact report with managed/native frames",
        "Module table with runtime module identifiers",
        "Platform crash log or tombstone"
    ];

    private static readonly string[] CommonNativeArtifacts =
    [
        "Platform crash log or tombstone",
        "Runtime compact report when CoreCLR observes the native signal",
        "Native signal and thread details"
    ];

    private static readonly string[] AppleNativeArtifacts =
    [
        "Apple .ips crash log",
        "App and native framework dSYMs",
        "CoreCLR stderr/minipal compact report when available"
    ];

    private static readonly string[] AndroidNativeArtifacts =
    [
        "Android tombstone or logcat fatal signal block",
        "DOTNET_CRASH logcat block when CoreCLR report is available",
        "ELF BuildIds for runtime and native crash kit libraries"
    ];
}
