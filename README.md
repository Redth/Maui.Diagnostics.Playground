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

## Android Play Store builds

The Android app id is `codes.redth.mauidiagnosticsgallery`, matching the Play Console package name. The mobile workflow publishes an Android App Bundle and signs it with the configured upload key secrets.

Android versioning uses the GitHub Actions workflow run number as the default `versionCode`, with `versionName` defaulting to `1.0.<versionCode>`. For manual Play uploads, the workflow dispatch inputs `android_version_code` and `android_version_name` can override those values; `versionCode` must always increase for each Play upload.

Successful Android builds on `main` publish the generated AAB/APK to the Play Console `internal` testing track. Pull request builds only build and upload GitHub Actions artifacts.

Configure these GitHub Actions secrets before publishing:

| Secret | Purpose |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded Android upload keystore. |
| `ANDROID_KEY_ALIAS` | Alias for the upload key in the keystore. |
| `ANDROID_KEY_PASSWORD` | Password for the upload key. |
| `ANDROID_KEYSTORE_PASSWORD` | Password for the upload keystore. |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Plain JSON key for a Google Play service account with permission to release `codes.redth.mauidiagnosticsgallery`. |
| `SENTRY_DSN` | DSN embedded into Play Store release builds so the Sentry MAUI SDK can send events. |
| `SENTRY_AUTH_TOKEN` | Sentry token required for `main` Play Store publishes; creates releases, sets commits, uploads Android debug symbols, source bundles, ProGuard/R8 mappings, and the published Android build to Sentry Size Analysis. |

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
| `SentryOrg` | `dotnet-maui` | Sentry organization used by the Sentry MSBuild/CLI integration. |
| `SentryProject` | `maui-diagnostics-playground` | Sentry project used by the Sentry MSBuild/CLI integration. |
| `CrashReportFrameLimitPerThread` | `32` | Captures the intended compact crash report frame cap for self-reporting and future runtime configuration. |
| `MauiDiagnosticsCrashReportName` | `/data/data/<app-id>/files/dotnet_crash_%p` | Android CoreCLR in-proc crash report file template used by current net11p5-era runtimes. The runtime appends `.crashreport.json` when `DOTNET_EnableCrashReport=1`. |
| `MauiDiagnosticsCrashReportRootPath` | `/data/data/<app-id>/files` | Android CoreCLR crash report lifecycle root used by net11p6+ runtimes. Reports are written under `<root>/.dotnet/crash-reports/`. Older runtimes ignore this knob. |
| `MauiDiagnosticsCrashReportMaxFileCount` | `32` | Retention bound for net11p6+ lifecycle-managed in-proc JSON crash reports. Older runtimes ignore this knob. |

The landing page includes a runtime self-report so each run shows the requested runtime, detected runtime family, target framework, build configuration, and active vendor. Use **Diagnostics files** to list app-private crash artifacts, view their text payloads, and share them from the device.

On Android, the .NET SDK enables `System.Runtime.CrashReportBeforeSignalChaining` for CoreCLR by default, so the runtime emits its compact crash report to `logcat` before Android's signal handler writes the tombstone. Android CoreCLR's in-proc JSON crash reporter is opt-in; this project packages `DOTNET_EnableCrashReport=1`, `DOTNET_DbgMiniDumpName=$(MauiDiagnosticsCrashReportName)`, `DOTNET_CrashReportRootPath=$(MauiDiagnosticsCrashReportRootPath)`, and `DOTNET_CrashReportMaxFileCount=$(MauiDiagnosticsCrashReportMaxFileCount)` into the app's Android environment. Current net11p5-era runtimes keep writing app-private `dotnet_crash_<pid>.crashreport.json` files from `DOTNET_DbgMiniDumpName`; net11p6+ runtimes write lifecycle-managed `report-<timestampNs>-<pid>.crashreport.json` files under `<files>/.dotnet/crash-reports/` and prune to the configured retention bound. Native crash files are Android system tombstones under `/data/tombstones` and may be copied into DropBox; do not expect an app-private `.dmp` file unless the Android runtime explicitly ships and enables a dump writer.

## Sentry configuration

The app includes the Sentry MAUI SDK and reads its settings from `src/Maui.Diagnostics.Playground/appsettings.json`, then overlays ignored `appsettings.local.json` and environment variables with the `MAUI_DIAGNOSTICS_` prefix. The committed appsettings file intentionally leaves `Sentry:Dsn` empty so no real DSN is stored in source control.

Android Play Store builds compile with `CrashVendor=Sentry`; on `main`, the workflow requires the `SENTRY_DSN` secret and writes it to an ignored `appsettings.local.json` before publishing. The workflow also requires `SENTRY_AUTH_TOKEN` so Sentry's MSBuild integration can create the release, set commits, and upload Android symbols, source bundles, and ProGuard/R8 mappings to `dotnet-maui/maui-diagnostics-playground`. The Android publish enables R8 and mapping generation, reuses one generated ProGuard UUID for the app manifest and mapping upload, then runs explicit post-build `sentry-cli` uploads so missing debug files, missing mappings, or failed uploads block the `main` release. After selecting the signed Android publish artifact, CI uploads the same AAB/APK to Sentry Size Analysis with `GITHUB_SHA` as the head SHA and no base SHA so Sentry records it as a `main` base build.

Set the DSN at build/run time with an environment variable that maps to `Sentry:Dsn`:

```bash
export MAUI_DIAGNOSTICS_Sentry__Dsn="https://examplePublicKey@o0.ingest.sentry.io/0"
```

Use the landing page's "Send Sentry test" button to queue a verification message, logs, and metrics after the app starts.
