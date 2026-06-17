# Android Crash Scenarios

The 17 scenarios visible in the gallery on Android (Apple-only scenarios are filtered out).
Tap the **Gallery title** to open the scenario, then **Trigger scenario** → **Trigger**.

Catalog source: [CrashScenarioCatalog.cs](../../../../src/Maui.Diagnostics.Playground/Features/Scenarios/CrashScenarioCatalog.cs).
Runner: [ManagedCrashScenarioRunner.cs](../../../../src/Maui.Diagnostics.Playground/Features/Scenarios/ManagedCrashScenarioRunner.cs).

| Key | Gallery title | Category | Trigger mechanism | Expected logcat / crashpad | Expected process view |
|-----|---------------|----------|-------------------|----------------------------|-----------------------|
| `managed-ui-unhandled` | Unhandled managed exception | Managed | `throw` on UI thread | `FATAL EXCEPTION: main`, `MonoDroid: UNHANDLED EXCEPTION`, activity force-finished, process dies | `InvalidOperationException`, managed stack incl. `ManagedCrashScenarioRunner` |
| `managed-background-unhandled` | Background thread exception | Managed | `throw` on background `Thread` | FATAL EXCEPTION on worker thread; process dies | `InvalidOperationException` from `CrashGallery.UnhandledBackground` |
| `managed-async-void` | Async void exception | Managed | `throw` after `await` in `async void` | FATAL EXCEPTION via `ThrowAsync`; process dies | `InvalidOperationException` async continuation |
| `runtime-null-reference` | NullReferenceException | Runtime | deref null (no-inline frame) | FATAL EXCEPTION; process dies | `NullReferenceException` with known root cause |
| `runtime-access-violation` | Access violation | Runtime | invalid native write (SIGSEGV) | `Fatal signal 11 (SIGSEGV)`, tombstone, backtrace | CoreCLR may emit compact report plus app-private JSON; Mono native fault |
| `runtime-failfast` | Environment.FailFast | Runtime | `Environment.FailFast` | `Fatal signal 6 (SIGABRT)`, tombstone | CoreCLR compact report w/ managed frames and app-private JSON when configured; process dies |
| `runtime-stack-overflow` | Stack overflow | Runtime | infinite recursion | `Fatal signal` (SIGSEGV) + tombstone; possibly SO message | stack overflow termination |
| `native-android-abort` | Android native abort | Native | `crash_native_abort` in `libcrashnativekit.so` | `Fatal signal 6 (SIGABRT)`, tombstone w/ `libcrashnativekit.so` + `libmonosgen`/`libcoreclr` frames, BuildIds | native abort, no managed exception |
| `native-android-sigsegv` | Android native SIGSEGV | Native | `crash_native_sigsegv` | `Fatal signal 11 (SIGSEGV)`, tombstone, BuildIds | native fault |
| `native-illegal-instruction` | Native illegal instruction | Native | SIGILL via crash kit | `Fatal signal 4 (SIGILL)`, tombstone | native fault |
| `native-background-thread` | Background native thread crash | Native | fault on named native thread | `Fatal signal`, tombstone naming the bg thread | native fault off the main thread |
| `mixed-managed-native` | Mixed managed/native stack | Native | managed frames → native crash kit → fault | tombstone with interleaved managed + `libcrashnativekit.so` frames | managed stack references `NativeCrashInterop` |
| `resource-memory-pressure` | Out-of-memory pressure | Resource | retain 64 MB chunks until termination | low-memory kill / `lmkd` / process death (often no tombstone) | `OutOfMemoryException` OR OS termination |
| `resource-ui-hang` | UI thread hang | Resource | `Thread.Sleep(Infinite)` on UI thread | `ANR in codes.redth.mauidiagnosticsgallery`, `Input dispatching timed out` | no managed exception expected |
| `vendor-handled-exception` | Vendor handled exception | Vendor | catch + report, app keeps running | NO fatal block; app stays alive | handled `InvalidOperationException` logged; process survives |
| `lifecycle-startup-crash` | Startup lifecycle crash | Lifecycle | arms a crash; **force-stop + relaunch** | FATAL EXCEPTION during startup before first page | managed exception during init |
| `lifecycle-resume-crash` | Resume lifecycle crash | Lifecycle | arms a crash; **background + foreground** | FATAL EXCEPTION on resume callback | managed exception during resume |

## Special handling

- **`lifecycle-startup-crash`**: after triggering, the app shows an alert. Then
  `adb shell am force-stop codes.redth.mauidiagnosticsgallery` and relaunch. The crash
  fires before the first Shell page and auto-clears the flag.
- **`lifecycle-resume-crash`**: after triggering, send the app to background
  (`adb shell input keyevent KEYCODE_HOME`) then foreground it (relaunch the activity). The
  crash fires in the resume callback and auto-clears the flag.
- **`resource-memory-pressure`**: can make the emulator sluggish. Run last in a cell and give
  the device time to recover before the next cell.
- **`vendor-handled-exception`**: with `CrashVendor=None` this only writes to debug output and
  the process must **stay alive** — that is the pass condition, not a crash.

## Runtime fingerprints

| Requested | `MauiDiagnosticsUseCoreClr` | Loaded native runtime | logcat marker |
|-----------|------------------|------------------------|---------------|
| Mono | `false` | `libmonosgen-2.0.so`, `libmonodroid.so` | `MonoDroid:` tags |
| CoreCLR | `true` (default) | `libcoreclr.so` | `DOTNET` tag, compact crash report |

The landing-page self-report ("Runtime family" fact) is the ground truth for what actually
loaded; always compare it to the requested matrix value.

## App-private CoreCLR JSON reports

When the harness captures app-private artifacts, pass the artifact directory to
`analyze-crash.cs` with `--artifacts <dir>`. The analyzer recognizes both output
generations:

- net11p5-era `dotnet_crash_<pid>.crashreport.json` files in the app data root.
- net11p6+ lifecycle-managed `report-<timestampNs>-<pid>.crashreport.json` files
  under `.dotnet/crash-reports/`.

Current net11p5 runs should keep producing the first form. net11p6+ runs should
produce the lifecycle-managed form and respect `DOTNET_CrashReportMaxFileCount`.
