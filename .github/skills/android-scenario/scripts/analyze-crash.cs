#:property PublishAot=false
// analyze-crash.cs — deterministic crash reconciliation for the Android scenario harness.
//
// Run as a file-based app:
//   dotnet run analyze-crash.cs -- --scenario <key> --runtime <mono|coreclr> \
//       --config <Debug|Release> --process process.log --logcat logcat.log --out verdict.json \
//       [--artifacts <dir>]
//
// It parses the process console output and the adb logcat capture, classifies the crash,
// checks the expected-artifact matrix for the scenario, scores the crash quality, and
// writes a JSON verdict plus a one-line markdown summary to stdout. No external packages.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var opts = ParseArgs(args);
if (opts is null)
{
    PrintUsage();
    return 2;
}

string processText = ReadIfExists(opts.ProcessPath);
string logcatText = ReadIfExists(opts.LogcatPath);
string artifactIndexText = ReadArtifactIndex(opts.ArtifactsPath);

if (processText.Length == 0 && logcatText.Length == 0 && artifactIndexText.Length == 0)
{
    Console.Error.WriteLine("error: --process, --logcat, and --artifacts produced no content.");
    return 2;
}

var signals = DetectSignals(processText, logcatText, artifactIndexText, opts.DetectedRuntime);
var expected = ExpectedFor(opts.Scenario);
var checks = Evaluate(expected, signals, opts);
var (grade, score, maxScore) = Grade(checks);

var verdict = new Verdict
{
    Scenario = opts.Scenario,
    Runtime = opts.Runtime,
    Configuration = opts.Config,
    Category = expected.Category,
    Grade = grade,
    Score = score,
    MaxScore = maxScore,
    Signals = signals,
    Checks = checks,
    Missing = checks.Where(c => !c.Passed && c.Required).Select(c => c.Name).ToArray(),
    ProcessVsLogcatAgree = ProcessAndLogcatAgree(signals),
    Notes = BuildNotes(expected, signals, opts),
};

var json = JsonSerializer.Serialize(verdict, VerdictContext.Default.Verdict);
if (!string.IsNullOrEmpty(opts.OutPath))
{
    File.WriteAllText(opts.OutPath, json);
    Console.Error.WriteLine($"wrote {opts.OutPath}");
}

// One-line markdown summary on stdout for quick aggregation.
string missing = verdict.Missing.Length == 0 ? "none" : string.Join(", ", verdict.Missing);
Console.WriteLine($"| {opts.Scenario} | {opts.Runtime}/{opts.Config} | {grade} ({score}/{maxScore}) | agree={verdict.ProcessVsLogcatAgree} | missing: {missing} |");
return 0;

// ----------------------------------------------------------------------------- helpers

static Options? ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
        string key = args[i][2..];
        string value = (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) ? args[++i] : "true";
        map[key] = value;
    }

    if (!map.TryGetValue("scenario", out var scenario)) return null;

    return new Options
    {
        Scenario = scenario.Trim(),
        Runtime = NormalizeRuntime(map.GetValueOrDefault("runtime", "unknown")),
        Config = map.GetValueOrDefault("config", "Unknown"),
        ProcessPath = map.GetValueOrDefault("process", ""),
        LogcatPath = map.GetValueOrDefault("logcat", ""),
        ArtifactsPath = map.GetValueOrDefault("artifacts", ""),
        OutPath = map.GetValueOrDefault("out", ""),
        // Optional ground-truth runtime (e.g. from nativeloader logcat / APK lib inspection),
        // since both runtimes ship libmonodroid.so and the on-device chip is unreliable.
        DetectedRuntime = NormalizeRuntimeOrEmpty(map.GetValueOrDefault("detected-runtime", "")),
    };
}

static string NormalizeRuntimeOrEmpty(string value)
    => string.IsNullOrWhiteSpace(value) ? "" : NormalizeRuntime(value);

static string NormalizeRuntime(string value)
    => value.Trim().ToLowerInvariant() switch
    {
        "mono" or "true" => "mono",
        "coreclr" or "false" => "coreclr",
        _ => "unknown",
    };

static void PrintUsage()
    => Console.Error.WriteLine(
        "usage: dotnet run analyze-crash.cs -- --scenario <key> --runtime <mono|coreclr> " +
        "--config <Debug|Release> --process <file> --logcat <file> [--out <file>] " +
        "[--artifacts <dir-or-file>] [--detected-runtime <mono|coreclr>]");

static string ReadIfExists(string path)
    => (!string.IsNullOrEmpty(path) && File.Exists(path)) ? File.ReadAllText(path) : "";

static string ReadArtifactIndex(string path)
{
    if (string.IsNullOrEmpty(path))
    {
        return "";
    }

    if (File.Exists(path))
    {
        return File.ReadAllText(path);
    }

    if (!Directory.Exists(path))
    {
        return "";
    }

    var builder = new StringBuilder();
    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
    {
        builder.AppendLine(file);
    }

    return builder.ToString();
}

static Signals DetectSignals(string proc, string logcat, string artifacts, string groundTruthRuntime)
{
    string all = proc + "\n" + logcat + "\n" + artifacts;

    // Native fatal signal block from libc, e.g.:
    // F libc : Fatal signal 6 (SIGABRT), code -1 ...
    var sigMatch = Regex.Match(logcat, @"Fatal signal (\d+) \((SIG[A-Z]+)\)", RegexOptions.IgnoreCase);
    string nativeSignal = sigMatch.Success ? $"{sigMatch.Groups[2].Value} ({sigMatch.Groups[1].Value})" : "";

    // Tombstone evidence.
    bool tombstone = Regex.IsMatch(logcat, @"tombstoned: .*Tombstone written to", RegexOptions.IgnoreCase)
                     || logcat.Contains("F DEBUG   :");
    bool backtrace = logcat.Contains("backtrace:");
    var buildIds = Regex.Matches(logcat, @"BuildId:\s*([0-9a-f]{8,})", RegexOptions.IgnoreCase)
        .Select(m => m.Groups[1].Value).Distinct().ToArray();

    // Managed crash surfaced by Android runtime / MonoDroid.
    bool androidFatalException = all.Contains("FATAL EXCEPTION");
    bool monoDroidUnhandled = all.Contains("MonoDroid: UNHANDLED EXCEPTION") || all.Contains("UNHANDLED EXCEPTION");
    bool dotnetCrashReport = Regex.IsMatch(all, @"createdump|Writing (mini)?dump|DOTNET.*crash|Triage buffer|compact (crash )?report", RegexOptions.IgnoreCase);
    bool legacyCrashReportFile = Regex.IsMatch(all, @"dotnet_crash_[^\s/\\]*\.crashreport\.json", RegexOptions.IgnoreCase);
    bool lifecycleCrashReportFile = Regex.IsMatch(all, @"report-\d+-\d+\.crashreport\.json", RegexOptions.IgnoreCase);

    // Managed stack referencing the scenario runner (good fidelity signal).
    bool managedStackHasRunner = Regex.IsMatch(all, @"ManagedCrashScenarioRunner|NativeCrashInterop|ScenarioDetailPage", RegexOptions.IgnoreCase);

    // Exception type / message captured by the process.
    var excMatch = Regex.Match(all, @"(System\.[A-Za-z\.]+Exception|NullReferenceException|InvalidOperationException|OutOfMemoryException)");
    string managedExceptionType = excMatch.Success ? excMatch.Groups[1].Value : "";

    // Native crash kit involvement.
    bool crashNativeKit = all.Contains("libcrashnativekit.so") || all.Contains("crash_native_");

    // Runtime fingerprint from loaded libraries. NOTE: libmonodroid.so ships in BOTH Mono and
    // CoreCLR Android apps (it is the Java<->managed bridge), so it is NOT runtime-discriminating.
    // Only libmonosgen-2.0.so is unique to Mono; libcoreclr.so is unique to CoreCLR. If the caller
    // passes --detected-runtime (ground truth from nativeloader/APK inspection), trust that.
    bool sawMono = all.Contains("libmonosgen-2.0.so") || Regex.IsMatch(all, @"Runtime family\W+Mono");
    bool sawCoreClr = all.Contains("libcoreclr.so") || Regex.IsMatch(all, @"Runtime family\W+CoreCLR");
    string detectedRuntime = !string.IsNullOrEmpty(groundTruthRuntime)
        ? groundTruthRuntime
        : sawCoreClr && !sawMono ? "coreclr" : sawMono && !sawCoreClr ? "mono" : sawCoreClr && sawMono ? "both" : "unknown";

    // Process death / kill.
    bool processDied = Regex.IsMatch(logcat, @"playground.*has died|Process .* exited due to signal|libprocessgroup: Successfully killed", RegexOptions.IgnoreCase);
    bool forceFinished = logcat.Contains("Force finishing activity");

    // ANR / watchdog (for hang scenario).
    bool anr = Regex.IsMatch(all, @"ANR in |Input dispatching timed out|not responding", RegexOptions.IgnoreCase);

    return new Signals
    {
        NativeSignal = nativeSignal,
        Tombstone = tombstone,
        Backtrace = backtrace,
        BuildIds = buildIds,
        AndroidFatalException = androidFatalException,
        MonoDroidUnhandled = monoDroidUnhandled,
        DotnetCrashReport = dotnetCrashReport,
        LegacyCrashReportFile = legacyCrashReportFile,
        LifecycleCrashReportFile = lifecycleCrashReportFile,
        ManagedStackHasRunner = managedStackHasRunner,
        ManagedExceptionType = managedExceptionType,
        CrashNativeKit = crashNativeKit,
        DetectedRuntime = detectedRuntime,
        ProcessDied = processDied,
        ForceFinished = forceFinished,
        Anr = anr,
    };
}

// Expected-artifact profile per scenario, keyed by the catalog key.
static Expected ExpectedFor(string key)
{
    // category buckets
    string[] managed = ["managed-ui-unhandled", "managed-background-unhandled", "managed-async-void", "runtime-null-reference"];
    string[] runtimeFatal = ["runtime-access-violation", "runtime-failfast", "runtime-stack-overflow"];
    string[] nativeFatal = ["native-android-abort", "native-android-sigsegv", "native-illegal-instruction", "native-background-thread", "mixed-managed-native"];

    if (managed.Contains(key))
        return new Expected("Managed/Runtime", wantManagedException: true, wantManagedStack: true, processDeath: true, wantNativeSignal: false, wantTombstone: false, wantNativeKit: false);

    if (runtimeFatal.Contains(key))
        return new Expected("Runtime (fatal)", wantManagedException: key == "runtime-stack-overflow", wantManagedStack: true, processDeath: true, wantNativeSignal: true, wantTombstone: true, wantNativeKit: false);

    if (nativeFatal.Contains(key))
        return new Expected("Native", wantManagedException: false, wantManagedStack: key == "mixed-managed-native", processDeath: true, wantNativeSignal: true, wantTombstone: true, wantNativeKit: true);

    return key switch
    {
        "resource-memory-pressure" => new Expected("Resource (OOM)", false, false, true, false, false, false) { Hang = false, Oom = true },
        "resource-ui-hang" => new Expected("Resource (hang)", false, false, false, false, false, false) { Hang = true },
        "vendor-handled-exception" => new Expected("Vendor (handled)", true, true, processDeath: false, false, false, false) { Handled = true },
        "lifecycle-startup-crash" => new Expected("Lifecycle", true, true, true, false, false, false),
        "lifecycle-resume-crash" => new Expected("Lifecycle", true, true, true, false, false, false),
        _ => new Expected("Unknown", true, true, true, false, false, false),
    };
}

static List<Check> Evaluate(Expected exp, Signals s, Options o)
{
    var checks = new List<Check>();

    void Add(string name, bool passed, bool required, string detail)
        => checks.Add(new Check { Name = name, Passed = passed, Required = required, Detail = detail });

    // Runtime detection matches requested cell.
    bool runtimeOk = o.Runtime == "unknown" || s.DetectedRuntime == "unknown" || s.DetectedRuntime == o.Runtime;
    Add("runtime-matches-request", runtimeOk, required: false,
        $"requested={o.Runtime} detected={s.DetectedRuntime}");

    if (exp.Handled)
    {
        Add("process-stays-alive", !s.ProcessDied, required: true, "handled exception must not kill the process");
        Add("managed-exception-captured", !string.IsNullOrEmpty(s.ManagedExceptionType) || s.MonoDroidUnhandled, required: false, s.ManagedExceptionType);
        return checks;
    }

    if (exp.Hang)
    {
        Add("anr-or-watchdog", s.Anr, required: true, "expected ANR/watchdog evidence for UI hang");
        Add("no-managed-exception", string.IsNullOrEmpty(s.ManagedExceptionType), required: false, "hang should not throw");
        return checks;
    }

    if (exp.Oom)
    {
        bool oomEvidence = s.ManagedExceptionType.Contains("OutOfMemory", StringComparison.OrdinalIgnoreCase) || s.ProcessDied;
        Add("oom-or-termination", oomEvidence, required: true, s.ManagedExceptionType);
        return checks;
    }

    Add("process-terminated", s.ProcessDied || s.ForceFinished, required: exp.WantProcessDeath, "process died / activity force-finished");

    if (exp.WantManagedException)
        Add("managed-exception-surfaced", s.AndroidFatalException || s.MonoDroidUnhandled || !string.IsNullOrEmpty(s.ManagedExceptionType),
            required: true, s.ManagedExceptionType);

    if (exp.WantManagedStack)
        Add("managed-stack-has-scenario-frames", s.ManagedStackHasRunner, required: false, "stack references runner/interop");

    if (exp.WantNativeSignal)
        Add("native-fatal-signal", !string.IsNullOrEmpty(s.NativeSignal), required: true, s.NativeSignal);

    if (exp.WantTombstone)
    {
        Add("tombstone-written", s.Tombstone, required: true, "tombstoned wrote a tombstone / F DEBUG block present");
        Add("native-backtrace", s.Backtrace, required: false, "backtrace present");
        Add("buildids-present", s.BuildIds.Length > 0, required: false, $"{s.BuildIds.Length} BuildId(s)");
    }

    if (exp.WantNativeKit)
        Add("crashnativekit-in-stack", s.CrashNativeKit, required: false, "libcrashnativekit.so / crash_native_* present");

    // CoreCLR is expected to add a managed-frame compact report on fatal native/runtime crashes.
    if (o.Runtime == "coreclr" && (exp.WantNativeSignal || exp.Category.StartsWith("Runtime")))
    {
        Add("coreclr-compact-report", s.DotnetCrashReport, required: false, "DOTNET crash/compact report observed");
        if (!string.IsNullOrEmpty(o.ArtifactsPath))
        {
            string detail = s.LifecycleCrashReportFile
                ? "lifecycle-managed report-*.crashreport.json observed"
                : s.LegacyCrashReportFile
                    ? "legacy dotnet_crash_*.crashreport.json observed"
                    : "no app-private CoreCLR JSON report observed";
            Add("coreclr-json-report-file", s.LegacyCrashReportFile || s.LifecycleCrashReportFile, required: false, detail);
        }
    }

    return checks;
}

static (string grade, int score, int max) Grade(List<Check> checks)
{
    int max = checks.Count;
    int score = checks.Count(c => c.Passed);

    // Any failed REQUIRED check caps the grade.
    bool requiredFailed = checks.Any(c => c.Required && !c.Passed);

    double ratio = max == 0 ? 0 : (double)score / max;
    string grade = requiredFailed
        ? (ratio >= 0.5 ? "C" : "D")
        : ratio >= 0.95 ? "A"
        : ratio >= 0.8 ? "B"
        : ratio >= 0.6 ? "C"
        : "D";

    return (grade, score, max);
}

static string ProcessAndLogcatAgree(Signals s)
{
    // Agreement = both the managed/process view and the native/logcat view describe a crash,
    // or both describe a survivable event.
    bool processView = s.AndroidFatalException || s.MonoDroidUnhandled || !string.IsNullOrEmpty(s.ManagedExceptionType) || s.DotnetCrashReport;
    bool nativeView = !string.IsNullOrEmpty(s.NativeSignal) || s.Tombstone || s.ProcessDied;
    if (processView && nativeView) return "yes";
    if (!processView && !nativeView) return "neither";
    return processView ? "process-only" : "logcat-only";
}

static string[] BuildNotes(Expected exp, Signals s, Options o)
{
    var notes = new List<string>();
    if (s.DetectedRuntime != "unknown" && o.Runtime != "unknown" && s.DetectedRuntime != o.Runtime)
        notes.Add($"Runtime mismatch: requested {o.Runtime} but detected {s.DetectedRuntime}.");
    if (exp.WantTombstone && !s.Tombstone)
        notes.Add("No tombstone captured — increase logcat window or check tombstoned permissions.");
    if (exp.WantManagedStack && !s.ManagedStackHasRunner)
        notes.Add("Managed stack did not reference the scenario runner — likely truncated or release-trimmed.");
    if (o.Runtime == "coreclr" && exp.Category.StartsWith("Runtime") && !s.DotnetCrashReport)
        notes.Add("CoreCLR did not emit a compact crash report for a runtime-fatal scenario.");
    if (!string.IsNullOrEmpty(o.ArtifactsPath) && o.Runtime == "coreclr" && exp.Category.StartsWith("Runtime") && !s.LegacyCrashReportFile && !s.LifecycleCrashReportFile)
        notes.Add("No app-private CoreCLR JSON crash report was captured from the artifact directory.");
    if (notes.Count == 0)
        notes.Add("No anomalies detected.");
    return notes.ToArray();
}

// ----------------------------------------------------------------------------- types

sealed class Options
{
    public string Scenario { get; init; } = "";
    public string Runtime { get; init; } = "unknown";
    public string Config { get; init; } = "Unknown";
    public string ProcessPath { get; init; } = "";
    public string LogcatPath { get; init; } = "";
    public string ArtifactsPath { get; init; } = "";
    public string OutPath { get; init; } = "";
    public string DetectedRuntime { get; init; } = "";
}

sealed class Expected
{
    public Expected(string category, bool wantManagedException, bool wantManagedStack, bool processDeath,
        bool wantNativeSignal, bool wantTombstone, bool wantNativeKit)
    {
        Category = category;
        WantManagedException = wantManagedException;
        WantManagedStack = wantManagedStack;
        WantProcessDeath = processDeath;
        WantNativeSignal = wantNativeSignal;
        WantTombstone = wantTombstone;
        WantNativeKit = wantNativeKit;
    }

    public string Category { get; }
    public bool WantManagedException { get; }
    public bool WantManagedStack { get; }
    public bool WantProcessDeath { get; }
    public bool WantNativeSignal { get; }
    public bool WantTombstone { get; }
    public bool WantNativeKit { get; }
    public bool Hang { get; init; }
    public bool Oom { get; init; }
    public bool Handled { get; init; }
}

sealed class Signals
{
    public string NativeSignal { get; init; } = "";
    public bool Tombstone { get; init; }
    public bool Backtrace { get; init; }
    public string[] BuildIds { get; init; } = [];
    public bool AndroidFatalException { get; init; }
    public bool MonoDroidUnhandled { get; init; }
    public bool DotnetCrashReport { get; init; }
    public bool LegacyCrashReportFile { get; init; }
    public bool LifecycleCrashReportFile { get; init; }
    public bool ManagedStackHasRunner { get; init; }
    public string ManagedExceptionType { get; init; } = "";
    public bool CrashNativeKit { get; init; }
    public string DetectedRuntime { get; init; } = "unknown";
    public bool ProcessDied { get; init; }
    public bool ForceFinished { get; init; }
    public bool Anr { get; init; }
}

sealed class Check
{
    public string Name { get; init; } = "";
    public bool Passed { get; init; }
    public bool Required { get; init; }
    public string Detail { get; init; } = "";
}

sealed class Verdict
{
    public string Scenario { get; init; } = "";
    public string Runtime { get; init; } = "";
    public string Configuration { get; init; } = "";
    public string Category { get; init; } = "";
    public string Grade { get; init; } = "";
    public int Score { get; init; }
    public int MaxScore { get; init; }
    public string ProcessVsLogcatAgree { get; init; } = "";
    public Signals Signals { get; init; } = new();
    public List<Check> Checks { get; init; } = [];
    public string[] Missing { get; init; } = [];
    public string[] Notes { get; init; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(Verdict))]
partial class VerdictContext : JsonSerializerContext;
