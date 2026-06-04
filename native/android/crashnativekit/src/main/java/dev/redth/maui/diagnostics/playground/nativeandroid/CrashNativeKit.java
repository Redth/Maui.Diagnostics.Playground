package dev.redth.maui.diagnostics.playground.nativeandroid;

public final class CrashNativeKit {
    static {
        System.loadLibrary("crashnativekit");
    }

    private CrashNativeKit() {
    }

    public static String getVersion() {
        return nativeGetVersion();
    }

    public static void abortNow() {
        nativeAbort();
    }

    public static void raiseSegmentationFault() {
        nativeSegmentationFault();
    }

    public static void raiseIllegalInstruction() {
        nativeIllegalInstruction();
    }

    public static void crashOnBackgroundThread() {
        nativeBackgroundThread();
    }

    public static void crashThroughNativeFrames() {
        nativeMixedStack();
    }

    private static native String nativeGetVersion();
    private static native void nativeAbort();
    private static native void nativeSegmentationFault();
    private static native void nativeIllegalInstruction();
    private static native void nativeBackgroundThread();
    private static native void nativeMixedStack();
}
