#import <Foundation/Foundation.h>

NS_ASSUME_NONNULL_BEGIN

#if defined(__cplusplus)
extern "C" {
#endif

#define CRASH_NATIVE_EXPORT __attribute__((visibility("default")))

CRASH_NATIVE_EXPORT const char *crash_native_version(void);
CRASH_NATIVE_EXPORT void crash_native_abort(void);
CRASH_NATIVE_EXPORT void crash_native_sigsegv(void);
CRASH_NATIVE_EXPORT void crash_native_sigill(void);
CRASH_NATIVE_EXPORT void crash_native_background_thread(void);
CRASH_NATIVE_EXPORT void crash_native_mixed_stack(void);
CRASH_NATIVE_EXPORT void crash_native_objc_exception(void);

#if defined(__cplusplus)
}
#endif

CRASH_NATIVE_EXPORT
@interface CrashNativeKit : NSObject

+ (NSString *)frameworkVersion;
+ (void)abortNow;
+ (void)raiseSegmentationFault;
+ (void)raiseIllegalInstruction;
+ (void)crashOnBackgroundThread;
+ (void)crashThroughNativeFrames;
+ (void)raiseObjectiveCException;

@end

NS_ASSUME_NONNULL_END
