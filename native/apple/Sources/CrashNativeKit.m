#import "CrashNativeKit.h"

#import <pthread.h>
#import <signal.h>
#import <stdlib.h>
#import <unistd.h>

static const char *CrashNativeVersion = "CrashNativeKit apple 1.0";

__attribute__((noinline)) static void crash_native_fault_leaf(void)
{
    volatile int *address = (volatile int *)0;
    *address = 0xC0FFEE;
}

__attribute__((noinline)) static void crash_native_frame_three(void)
{
    crash_native_fault_leaf();
}

__attribute__((noinline)) static void crash_native_frame_two(void)
{
    crash_native_frame_three();
}

__attribute__((noinline)) static void crash_native_frame_one(void)
{
    crash_native_frame_two();
}

static void *crash_native_thread_entry(void *context)
{
    (void)context;
    pthread_setname_np("CrashNativeKit.BG");
    usleep(100000);
    crash_native_frame_one();
    return NULL;
}

const char *crash_native_version(void)
{
    return CrashNativeVersion;
}

void crash_native_abort(void)
{
    abort();
}

void crash_native_sigsegv(void)
{
    crash_native_frame_one();
}

void crash_native_sigill(void)
{
    raise(SIGILL);
}

void crash_native_background_thread(void)
{
    pthread_t thread;
    int result = pthread_create(&thread, NULL, crash_native_thread_entry, NULL);
    if (result != 0)
    {
        abort();
    }

    pthread_detach(thread);
}

void crash_native_mixed_stack(void)
{
    crash_native_frame_one();
}

void crash_native_objc_exception(void)
{
    [NSException raise:@"CrashNativeKitObjectiveCException"
                format:@"Simulated Objective-C exception from CrashNativeKit."];
}

@implementation CrashNativeKit

+ (NSString *)frameworkVersion
{
    return @(crash_native_version());
}

+ (void)abortNow
{
    crash_native_abort();
}

+ (void)raiseSegmentationFault
{
    crash_native_sigsegv();
}

+ (void)raiseIllegalInstruction
{
    crash_native_sigill();
}

+ (void)crashOnBackgroundThread
{
    crash_native_background_thread();
}

+ (void)crashThroughNativeFrames
{
    crash_native_mixed_stack();
}

+ (void)raiseObjectiveCException
{
    crash_native_objc_exception();
}

@end
