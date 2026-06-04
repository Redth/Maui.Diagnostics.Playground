using Foundation;
using ObjCRuntime;

namespace Maui.Diagnostics.Playground.NativeAppleBinding;

[BaseType(typeof(NSObject))]
interface CrashNativeKit
{
    [Static]
    [Export("frameworkVersion")]
    string FrameworkVersion { get; }

    [Static]
    [Export("abortNow")]
    void AbortNow();

    [Static]
    [Export("raiseSegmentationFault")]
    void RaiseSegmentationFault();

    [Static]
    [Export("raiseIllegalInstruction")]
    void RaiseIllegalInstruction();

    [Static]
    [Export("crashOnBackgroundThread")]
    void CrashOnBackgroundThread();

    [Static]
    [Export("crashThroughNativeFrames")]
    void CrashThroughNativeFrames();

    [Static]
    [Export("raiseObjectiveCException")]
    void RaiseObjectiveCException();
}
