using System.Runtime.CompilerServices;

namespace Maui.Diagnostics.Playground.Features.Scenarios;

public sealed class ManagedCrashScenarioRunner : ICrashScenarioRunner
{
    private static readonly HashSet<string> SupportedScenarios =
    [
        "managed-ui-unhandled",
        "managed-background-unhandled",
        "managed-async-void",
        "runtime-null-reference",
        "runtime-access-violation",
        "runtime-failfast",
        "runtime-stack-overflow",
        "native-apple-abort",
        "native-apple-sigsegv",
        "native-apple-objc-exception",
        "native-android-abort",
        "native-android-sigsegv",
        "native-illegal-instruction",
        "native-background-thread",
        "mixed-managed-native",
        "resource-memory-pressure",
        "resource-ui-hang",
        "vendor-handled-exception",
        "lifecycle-startup-crash",
        "lifecycle-resume-crash"
    ];

    public bool CanRun(CrashScenarioDescriptor scenario)
        => SupportedScenarios.Contains(scenario.Key);

    public Task RunAsync(CrashScenarioDescriptor scenario)
    {
        return scenario.Key switch
        {
            "managed-ui-unhandled" => ThrowUnhandledOnCurrentThread(),
            "managed-background-unhandled" => ThrowUnhandledOnBackgroundThread(),
            "managed-async-void" => ThrowFromAsyncVoid(),
            "runtime-null-reference" => ThrowNullReference(),
            "runtime-access-violation" => AccessViolation(),
            "runtime-failfast" => FailFast(),
            "runtime-stack-overflow" => OverflowStack(),
            "native-apple-abort" => NativeAbort(),
            "native-apple-sigsegv" => NativeSegmentationFault(),
            "native-apple-objc-exception" => NativeObjectiveCException(),
            "native-android-abort" => NativeAbort(),
            "native-android-sigsegv" => NativeSegmentationFault(),
            "native-illegal-instruction" => NativeIllegalInstruction(),
            "native-background-thread" => NativeBackgroundThreadCrash(),
            "mixed-managed-native" => MixedManagedNativeCrash(),
            "resource-memory-pressure" => MemoryPressure(),
            "resource-ui-hang" => HangUiThread(),
            "vendor-handled-exception" => CaptureHandledException(),
            "lifecycle-startup-crash" => ArmStartupCrash(),
            "lifecycle-resume-crash" => ArmResumeCrash(),
            _ => throw new NotSupportedException($"Scenario '{scenario.Key}' is not wired to a crash runner yet.")
        };
    }

    private static Task ThrowUnhandledOnCurrentThread()
        => throw new InvalidOperationException("Simulated unhandled managed exception on the UI thread.");

    private static Task ThrowUnhandledOnBackgroundThread()
    {
        var thread = new Thread(() =>
        {
            throw new InvalidOperationException("Simulated unhandled managed exception on a background thread.");
        })
        {
            IsBackground = true,
            Name = "CrashGallery.UnhandledBackground"
        };

        thread.Start();
        return Task.CompletedTask;
    }

    private static Task ThrowFromAsyncVoid()
    {
        ThrowLater();
        return Task.CompletedTask;

        static async void ThrowLater()
        {
            await Task.Yield();
            throw new InvalidOperationException("Simulated unhandled exception from an async void continuation.");
        }
    }

    private static Task ThrowNullReference()
    {
        TriggerNullReference();
        return Task.CompletedTask;
    }

    private static Task AccessViolation()
    {
        NativeCrashInterop.RaiseSegmentationFault();
        return Task.CompletedTask;
    }

    private static Task FailFast()
    {
        Environment.FailFast("Simulated Environment.FailFast from the MAUI diagnostics playground.");
        return Task.CompletedTask;
    }

    private static Task OverflowStack()
    {
        RecurseUntilStackOverflow(0);
        return Task.CompletedTask;
    }

    private static Task CaptureHandledException()
    {
        try
        {
            throw new InvalidOperationException("Simulated handled exception for vendor capture.");
        }
        catch (InvalidOperationException exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }

        return Task.CompletedTask;
    }

    private static Task NativeAbort()
    {
        NativeCrashInterop.Abort();
        return Task.CompletedTask;
    }

    private static Task NativeSegmentationFault()
    {
        NativeCrashInterop.RaiseSegmentationFault();
        return Task.CompletedTask;
    }

    private static Task NativeIllegalInstruction()
    {
        NativeCrashInterop.RaiseIllegalInstruction();
        return Task.CompletedTask;
    }

    private static Task NativeObjectiveCException()
    {
        NativeCrashInterop.RaiseObjectiveCException();
        return Task.CompletedTask;
    }

    private static Task NativeBackgroundThreadCrash()
    {
        NativeCrashInterop.CrashOnBackgroundThread();
        return Task.CompletedTask;
    }

    private static Task MixedManagedNativeCrash()
    {
        NativeCrashInterop.CrashThroughManagedNativeBoundary();
        return Task.CompletedTask;
    }

    private static Task MemoryPressure()
    {
        var retained = new List<byte[]>();

        while (true)
        {
            retained.Add(new byte[64 * 1024 * 1024]);
            GC.KeepAlive(retained);
        }
    }

    private static Task HangUiThread()
    {
        Thread.Sleep(Timeout.InfiniteTimeSpan);
        return Task.CompletedTask;
    }

    private static async Task ArmStartupCrash()
    {
        StartupCrashCoordinator.Arm();
        await Shell.Current.DisplayAlertAsync(
            "Startup crash armed",
            "Force quit and relaunch the app. The next launch will throw before the first Shell page is created, then automatically clear the startup crash flag.",
            "OK");
    }

    private static async Task ArmResumeCrash()
    {
        StartupCrashCoordinator.ArmResumeCrash();
        await Shell.Current.DisplayAlertAsync(
            "Resume crash armed",
            "Background the app and bring it back to the foreground. The app will throw during the resume lifecycle callback, then automatically clear the resume crash flag.",
            "OK");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RecurseUntilStackOverflow(int depth)
    {
        var buffer = new byte[256];
        buffer[0] = (byte)(depth & 0xFF);
        return buffer[0] + RecurseUntilStackOverflow(depth + 1);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string TriggerNullReference()
    {
        object value = null!;
        return value.ToString()!;
    }
}
