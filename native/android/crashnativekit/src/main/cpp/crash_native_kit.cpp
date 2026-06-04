#include <jni.h>
#include <pthread.h>
#include <signal.h>
#include <stdlib.h>
#include <unistd.h>

#define CRASH_NATIVE_EXPORT extern "C" __attribute__((visibility("default")))

namespace
{
constexpr const char *CrashNativeVersion = "CrashNativeKit android 1.0";

__attribute__((noinline)) void crash_native_fault_leaf()
{
    volatile int *address = nullptr;
    *address = 0xC0FFEE;
}

__attribute__((noinline)) void crash_native_frame_three()
{
    crash_native_fault_leaf();
}

__attribute__((noinline)) void crash_native_frame_two()
{
    crash_native_frame_three();
}

__attribute__((noinline)) void crash_native_frame_one()
{
    crash_native_frame_two();
}

void *crash_native_thread_entry(void *)
{
    pthread_setname_np(pthread_self(), "CrashNativeKit.BG");
    usleep(100000);
    crash_native_frame_one();
    return nullptr;
}
}

CRASH_NATIVE_EXPORT const char *crash_native_version()
{
    return CrashNativeVersion;
}

CRASH_NATIVE_EXPORT void crash_native_abort()
{
    abort();
}

CRASH_NATIVE_EXPORT void crash_native_sigsegv()
{
    crash_native_frame_one();
}

CRASH_NATIVE_EXPORT void crash_native_sigill()
{
    raise(SIGILL);
}

CRASH_NATIVE_EXPORT void crash_native_background_thread()
{
    pthread_t thread;
    int result = pthread_create(&thread, nullptr, crash_native_thread_entry, nullptr);
    if (result != 0)
    {
        abort();
    }

    pthread_detach(thread);
}

CRASH_NATIVE_EXPORT void crash_native_mixed_stack()
{
    crash_native_frame_one();
}

extern "C" JNIEXPORT jstring JNICALL
Java_dev_redth_maui_diagnostics_playground_nativeandroid_CrashNativeKit_nativeGetVersion(
    JNIEnv *env,
    jclass)
{
    return env->NewStringUTF(crash_native_version());
}

extern "C" JNIEXPORT void JNICALL
Java_dev_redth_maui_diagnostics_playground_nativeandroid_CrashNativeKit_nativeAbort(
    JNIEnv *,
    jclass)
{
    crash_native_abort();
}

extern "C" JNIEXPORT void JNICALL
Java_dev_redth_maui_diagnostics_playground_nativeandroid_CrashNativeKit_nativeSegmentationFault(
    JNIEnv *,
    jclass)
{
    crash_native_sigsegv();
}

extern "C" JNIEXPORT void JNICALL
Java_dev_redth_maui_diagnostics_playground_nativeandroid_CrashNativeKit_nativeIllegalInstruction(
    JNIEnv *,
    jclass)
{
    crash_native_sigill();
}

extern "C" JNIEXPORT void JNICALL
Java_dev_redth_maui_diagnostics_playground_nativeandroid_CrashNativeKit_nativeBackgroundThread(
    JNIEnv *,
    jclass)
{
    crash_native_background_thread();
}

extern "C" JNIEXPORT void JNICALL
Java_dev_redth_maui_diagnostics_playground_nativeandroid_CrashNativeKit_nativeMixedStack(
    JNIEnv *,
    jclass)
{
    crash_native_mixed_stack();
}
