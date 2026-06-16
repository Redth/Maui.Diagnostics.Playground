# Maui.Diagnostics.Playground

A .NET MAUI diagnostics playground for exercising crash scenarios on the .NET 11 mobile runtimes.

The app is intentionally a polished sample gallery: scenarios are registered in one catalog, shown on the landing page, and opened through Shell navigation. Each scenario documents the platform support and the diagnostic artifacts it is expected to produce.

## Prerequisites

- .NET SDK `11.0.100-preview.4.26230.115`
- .NET MAUI workloads from the matching .NET 11 preview band

```bash
dotnet workload restore
```

## Build

```bash
dotnet build src/Maui.Diagnostics.Playground/Maui.Diagnostics.Playground.csproj -f net11.0-android
dotnet build src/Maui.Diagnostics.Playground/Maui.Diagnostics.Playground.csproj -f net11.0-ios -r iossimulator-arm64
dotnet build src/Maui.Diagnostics.Playground/Maui.Diagnostics.Playground.csproj -f net11.0-maccatalyst
```

## Runtime and vendor switches

The repo defaults to CoreCLR-oriented testing:

```bash
dotnet build src/Maui.Diagnostics.Playground/Maui.Diagnostics.Playground.csproj \
  -f net11.0-android \
  -p:MauiDiagnosticsUseCoreClr=true \
  -p:CrashVendor=None
```

Important MSBuild properties:

| Property | Default | Purpose |
| --- | --- | --- |
| `MauiDiagnosticsUseCoreClr` | `true` | Requests the CoreCLR mobile runtime path. Set to `false` for Mono comparison runs. |
| `CrashVendor` | `None` | Selects the active crash-reporting vendor integration. Initial values are `None`, `Sentry`, `Raygun`, `NewRelic`, `Bugsee`, `Firebase`, `AppCenterLegacy`, and `NativePrototype`. |
| `CrashReportFrameLimitPerThread` | `32` | Captures the intended compact crash report frame cap for self-reporting and future runtime configuration. |
| `MauiDiagnosticsCrashReportName` | `/data/data/<app-id>/files/dotnet_crash_%p` | Android CoreCLR in-proc crash report file template. The runtime appends `.crashreport.json` when `DOTNET_EnableCrashReport=1`. |

The landing page includes a runtime self-report so each run shows the requested runtime, detected runtime family, target framework, build configuration, and active vendor. Use **Diagnostics files** to list app-private crash artifacts, view their text payloads, and share them from the device.

On Android, the .NET SDK enables `System.Runtime.CrashReportBeforeSignalChaining` for CoreCLR by default, so the runtime emits its compact crash report to `logcat` before Android's signal handler writes the tombstone. Android CoreCLR's in-proc JSON crash reporter is opt-in; this project packages `DOTNET_EnableCrashReport=1` and `DOTNET_DbgMiniDumpName=$(MauiDiagnosticsCrashReportName)` into the app's Android environment. Native crash files are Android system tombstones under `/data/tombstones` and may be copied into DropBox; do not expect an app-private `.dmp` file unless the Android runtime explicitly ships and enables a dump writer.

## Sentry configuration

The app includes the Sentry MAUI SDK and reads its settings from `src/Maui.Diagnostics.Playground/appsettings.json`, then overlays environment variables with the `MAUI_DIAGNOSTICS_` prefix. The committed appsettings file intentionally leaves `Sentry:Dsn` empty so no real DSN is stored in source control.

Set the DSN at build/run time with an environment variable that maps to `Sentry:Dsn`:

```bash
export MAUI_DIAGNOSTICS_Sentry__Dsn="https://examplePublicKey@o0.ingest.sentry.io/0"
```

Use the landing page's "Send Sentry test" button to queue a verification message, logs, and metrics after the app starts.
