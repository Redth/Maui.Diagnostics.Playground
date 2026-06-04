using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Maui.Diagnostics.Playground.Features.Scenarios;

internal static partial class NativeCrashInterop
{
    private const int SigIll = 4;
    private const int SigSegv = 11;

    public static string GetNativeKitDescription()
    {
#if MAUI_DIAGNOSTICS_NATIVE_PROJECTS && (IOS || MACCATALYST || ANDROID)
        try
        {
            var version = Marshal.PtrToStringUTF8(NativeKitVersion());
            return string.IsNullOrWhiteSpace(version) ? "Native project enabled; version unavailable" : version;
        }
        catch (DllNotFoundException exception)
        {
            return $"Native project enabled; library load failed: {exception.Message}";
        }
        catch (EntryPointNotFoundException exception)
        {
            return $"Native project enabled; export lookup failed: {exception.Message}";
        }
#else
        return "Native project disabled; using platform C runtime fallback";
#endif
    }

    public static void Abort()
    {
        AbortNative();
    }

    public static void RaiseSegmentationFault()
    {
        RaiseNative(SigSegv);
    }

    public static void RaiseIllegalInstruction()
    {
        RaiseNative(SigIll);
    }

    public static void CrashOnBackgroundThread()
    {
#if MAUI_DIAGNOSTICS_NATIVE_PROJECTS && (IOS || MACCATALYST || ANDROID)
        BackgroundThreadNative();
#else
        var thread = new Thread(RaiseSegmentationFault)
        {
            IsBackground = true,
            Name = "CrashGallery.NativeBackground"
        };

        thread.Start();
#endif
    }

    public static void RaiseObjectiveCException()
    {
#if MAUI_DIAGNOSTICS_NATIVE_PROJECTS && (IOS || MACCATALYST)
        ObjectiveCExceptionNative();
#else
        throw new PlatformNotSupportedException("Objective-C exception scenarios require the Apple native CrashNativeKit project.");
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CrashThroughManagedNativeBoundary()
    {
        ManagedFrameOne();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ManagedFrameOne()
    {
        ManagedFrameTwo();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ManagedFrameTwo()
    {
        MixedStackNative();
    }

#if MAUI_DIAGNOSTICS_NATIVE_PROJECTS && (IOS || MACCATALYST)
    [DllImport("CrashNativeKit", EntryPoint = "crash_native_version")]
    private static extern IntPtr NativeKitVersion();

    [DllImport("CrashNativeKit", EntryPoint = "crash_native_abort")]
    private static extern void AbortNative();

    [DllImport("CrashNativeKit", EntryPoint = "crash_native_sigill")]
    private static extern void RaiseIllegalInstructionNative();

    [DllImport("CrashNativeKit", EntryPoint = "crash_native_background_thread")]
    private static extern void BackgroundThreadNative();

    [DllImport("CrashNativeKit", EntryPoint = "crash_native_mixed_stack")]
    private static extern void MixedStackNative();

    [DllImport("CrashNativeKit", EntryPoint = "crash_native_objc_exception")]
    private static extern void ObjectiveCExceptionNative();

    private static int RaiseNative(int signal)
    {
        if (signal == SigIll)
        {
            RaiseIllegalInstructionNative();
            return 0;
        }

        RaiseSegmentationFaultNative();
        return 0;
    }

    [DllImport("CrashNativeKit", EntryPoint = "crash_native_sigsegv")]
    private static extern void RaiseSegmentationFaultNative();
#elif MAUI_DIAGNOSTICS_NATIVE_PROJECTS && ANDROID
    [DllImport("crashnativekit", EntryPoint = "crash_native_version")]
    private static extern IntPtr NativeKitVersion();

    [DllImport("crashnativekit", EntryPoint = "crash_native_abort")]
    private static extern void AbortNative();

    [DllImport("crashnativekit", EntryPoint = "crash_native_sigsegv")]
    private static extern void RaiseSegmentationFaultNative();

    [DllImport("crashnativekit", EntryPoint = "crash_native_sigill")]
    private static extern void RaiseIllegalInstructionNative();

    [DllImport("crashnativekit", EntryPoint = "crash_native_background_thread")]
    private static extern void BackgroundThreadNative();

    [DllImport("crashnativekit", EntryPoint = "crash_native_mixed_stack")]
    private static extern void MixedStackNative();

    private static int RaiseNative(int signal)
    {
        if (signal == SigIll)
        {
            RaiseIllegalInstructionNative();
            return 0;
        }

        RaiseSegmentationFaultNative();
        return 0;
    }
#elif IOS || MACCATALYST
    [DllImport("__Internal", EntryPoint = "abort")]
    private static extern void AbortNative();

    [DllImport("__Internal", EntryPoint = "raise")]
    private static extern int RaiseNative(int signal);

    private static void MixedStackNative()
    {
        Abort();
    }
#elif ANDROID
    [DllImport("libc", EntryPoint = "abort")]
    private static extern void AbortNative();

    [DllImport("libc", EntryPoint = "raise")]
    private static extern int RaiseNative(int signal);

    private static void MixedStackNative()
    {
        RaiseSegmentationFault();
    }
#else
    private static void AbortNative()
    {
        Environment.FailFast("Native abort is only implemented for Android, iOS, and Mac Catalyst.");
    }

    private static int RaiseNative(int signal)
    {
        Environment.FailFast($"Native signal {signal} is only implemented for Android, iOS, and Mac Catalyst.");
        return 0;
    }

    private static void MixedStackNative()
    {
        Environment.FailFast("Mixed native stack crashes are only implemented for Android, iOS, and Mac Catalyst.");
    }
#endif
}
