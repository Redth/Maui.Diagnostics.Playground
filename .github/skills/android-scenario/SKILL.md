---
name: android-scenario
description: 'End-to-end Android crash-scenario test harness for the MAUI Diagnostics Playground. USE FOR: exercising every crash scenario on an Android device/emulator, tapping through the gallery with the MAUI MCP, capturing process output and adb logcat (crashpad/tombstone), comparing managed vs native crash evidence, running the UseMonoRuntime x Configuration matrix, and producing a markdown crash-quality report. Trigger phrases: "test crash scenarios", "android crash matrix", "run every scenario", "crash quality report", "mono vs coreclr crash", "validate tombstones". DO NOT USE FOR: iOS/Mac crash testing, generic MAUI UI work, or non-crash diagnostics.'
argument-hint: 'Optional: scenario key(s) to limit, or matrix cell (e.g. MauiDiagnosticsUseCoreClr=true;Configuration=Release)'
---

# Android Crash Scenario Harness

Drives the MAUI Diagnostics Playground through every Android crash scenario, captures
both the **process output** (what `dotnet run` / the runtime surfaced) and **adb logcat**
(what crashpad / tombstoned recorded natively), reconciles them with a deterministic
C# script, and produces a markdown report rating how good each crash is.

## When to Use
- Validate that each crash scenario produces the expected diagnostics on Android.
- Compare crash fidelity across the **matrix**: runtime ∈ {Mono, CoreCLR} (via `MauiDiagnosticsUseCoreClr`) × `Configuration` ∈ {Debug, Release}.
- Detect missing artifacts (no tombstone, no DOTNET compact report, truncated managed stack, missing BuildIds).

## Prerequisites
- One Android device/emulator connected (`adb devices` shows exactly one online).
- **.NET 11 preview SDK** from [global.json](../../../global.json) (Program Files often only has 10.x, which fails `global.json`). Point at the preview SDK at the start of **every** terminal command:
  ```pwsh
  $env:DOTNET_ROOT="<path-to>\dotnet-sdk-11.0.100-preview.x-win-x64"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
  ```
- MAUI workloads restored.
- App project: [src/Maui.Diagnostics.Playground](../../../src/Maui.Diagnostics.Playground/Maui.Diagnostics.Playground.csproj), TFM `net11.0-android`.
- **UI driving:** the playground does **not** embed the MAUI DevFlow agent, so the `mcp_maui_*` tools have no connected agent for this app. Drive the UI with adb instead — the proven, parameterized [scripts/drive-scenario.ps1](./scripts/drive-scenario.ps1) (uiautomator text-based taps) is the reliable path. The `mcp_maui_*` tools remain a fallback only if an agent is present.

## Key Facts
- **Matrix axis is `MauiDiagnosticsUseCoreClr`** (NOT `UseMonoRuntime`). [Directory.Build.props](../../../Directory.Build.props) unconditionally derives `UseMonoRuntime` from `MauiDiagnosticsUseCoreClr`, so passing `-p:UseMonoRuntime=...` on the command line is silently overridden. Use:
  - **Mono** ⇒ `-p:MauiDiagnosticsUseCoreClr=false` (→ `UseMonoRuntime=true`, `libmonosgen-2.0.so`)
  - **CoreCLR** ⇒ `-p:MauiDiagnosticsUseCoreClr=true` (default, → `libcoreclr.so`)
- **The on-device runtime chip is UNRELIABLE — do not trust it for ground truth.** The landing "Runtime family" chip (and the analyzer's library fingerprint) can report the wrong runtime because **`libmonodroid.so` ships in BOTH Mono and CoreCLR** Android apps (it is the Java↔managed bridge) and is not runtime-discriminating. Establish ground truth from the build flag you passed, and confirm it on-device only via a discriminating signal:
  - `adb logcat | Select-String 'nativeloader'` around launch — look for `libcoreclr.so` (CoreCLR) vs `libmonosgen-2.0.so` (Mono).
  - or inspect the installed APK's native libs (presence of `libcoreclr.so` ⇒ CoreCLR, `libmonosgen-2.0.so` ⇒ Mono).
  Pass that confirmed value to the analyzer via `--detected-runtime <mono|coreclr>` (see step 5).
- **Android scenario list** (what the gallery shows on Android) and each scenario's expected evidence live in [references/scenarios.md](./references/scenarios.md). There are 17 Android-visible scenarios.
- Scenarios are tapped: gallery card (by Title) → "Trigger scenario" → confirm "Trigger" in the dialog.
- App id: `dev.redth.maui.diagnostics.playground`.

## Procedure

Run the matrix as an outer loop (4 cells) and the scenario list as an inner loop. Use the
todo list to track progress; one matrix cell at a time. A **full 4-cell × 17-scenario matrix
takes ~75–80 minutes** end-to-end — budget for it and **do not set short tool timeouts** on the
build/run steps.

### Fast path (recommended)
After building/deploying each cell (step 2), drive + analyze the whole cell with one call:
```pwsh
./.github/skills/android-scenario/scripts/run-cell.ps1 `
  -Runtime <mono|coreclr> -Config <Debug|Release> -RunRoot artifacts/crash-runs/<timestamp>
```
`run-cell.ps1` iterates all 17 scenarios, calls `drive-scenario.ps1` (adb UI driver) and
`analyze-crash.cs` per scenario, and writes `cell-summary.md`. Steps 3–5 below describe what
it does and the manual fallback.

### 1. Prepare output workspace
Create a run folder, e.g. `artifacts/crash-runs/<timestamp>/<runtime>-<config>/`.
For each scenario you will save: `process-<key>.log`, `logcat-<key>.log`, `verdict-<key>.json`.

### 2. For each matrix cell — build, deploy, launch
```pwsh
adb logcat -c
# Mono => MauiDiagnosticsUseCoreClr=false ; CoreCLR => MauiDiagnosticsUseCoreClr=true
dotnet build ./src/Maui.Diagnostics.Playground/ -f net11.0-android -t:Run `
  -c <Configuration> -p:MauiDiagnosticsUseCoreClr=<false|true> *> process-session.log
```
> **Release host-pack workaround (REQUIRED for `-c Release`).** A multi-TFM restore (triggered by
> `-f net11.0-android`) pulls the Mono AOT/marshal-method host pack
> `Microsoft.NETCore.App.Runtime.Mono.win-x64`, which is **not** in the offline preview SDK and
> fails with NU1102. Decouple restore from build and disable AOT + marshal methods (trimming stays
> on, so it is still representative Release):
> ```pwsh
> dotnet restore ./src/Maui.Diagnostics.Playground/ `
>   -p:TargetFramework=net11.0-android -p:Configuration=Release `
>   -p:MauiDiagnosticsUseCoreClr=<false|true> -p:RunAOTCompilation=false -p:AndroidEnableMarshalMethods=false
> dotnet build ./src/Maui.Diagnostics.Playground/ --no-restore -t:Run `
>   -p:TargetFramework=net11.0-android -c Release `
>   -p:MauiDiagnosticsUseCoreClr=<false|true> -p:RunAOTCompilation=false -p:AndroidEnableMarshalMethods=false *> process-session.log
> ```
> Note: use `-p:TargetFramework=` (single-TFM) here, **not** `-f` — `-f` re-triggers the failing
> multi-TFM restore. A transient `XABAA7000 Permission denied renaming temp file` at APK packaging
> usually clears on a plain retry (no clean needed).
> A flavor switch (Mono↔CoreCLR) or config switch is not always picked up incrementally.
> If the runtime ends up wrong, rebuild that cell after deleting the project
> `bin`/`obj` (or pass `-t:Clean` first).
- Use mode=async for the run so logcat capture can proceed in parallel; or launch the
  built APK and stream output. Capture everything `dotnet run` prints to `process-session.log`.
- Wait for the gallery to render: `mcp_maui_maui_wait` / `mcp_maui_maui_tree` until the
  "Scenario gallery" label is present.

### 3. Record the runtime self-report (ground truth for the cell)
Ground truth = the build flag you passed, confirmed by a discriminating device signal (see
Key Facts). **Do not rely on the on-screen "Runtime family" chip** — it can be wrong because
`libmonodroid.so` is in both runtimes. Confirm via `nativeloader` logcat (`libcoreclr.so` vs
`libmonosgen-2.0.so`) or APK lib inspection, and pass the confirmed value to the analyzer with
`--detected-runtime`. Record both the requested and confirmed runtime in the report.

### 4. For each scenario (inner loop)
1. `adb logcat -c` to start from a clean buffer.
2. Tap the scenario card by its Title (gallery title text from
   [references/scenarios.md](./references/scenarios.md)). Cards are in a scrollable list — scroll
   down to find off-screen cards.
3. Tap **"Trigger scenario"**, then confirm **"Trigger"** in the alert. The **"Trigger scenario"
   button is the last element on a scrollable detail page** and is frequently below the fold for
   scenarios with long "Expected artifacts" lists — **scroll the detail page down** until it is
   visible before tapping (`drive-scenario.ps1`'s `Tap-Trigger` handles this).
4. For armed scenarios (`lifecycle-startup-crash`, `lifecycle-resume-crash`) follow the
   on-screen instruction: force-stop + relaunch, or background + foreground. See the
   reference for the exact gesture.
5. Capture: `adb logcat -d > logcat-<key>.log` (and snapshot the tail of the process log
   that corresponds to this scenario into `process-<key>.log`). `drive-scenario.ps1`
   also attempts to pull Android CoreCLR in-proc JSON crash reports as
   `crashreport-<key>-*.crashreport.json`. When logcat names a system tombstone
   (`tombstone_<n>` / `/data/tombstones/tombstone_<n>`), it pulls that into the run folder as
   `tombstone-<key>-<name>`.
6. Relaunch the app for the next scenario (the previous one terminated the process):
   `adb shell am start -n dev.redth.maui.diagnostics.playground/crc64...MainActivity` or
   re-run, then wait for the gallery and navigate "Back to gallery" if needed.

> Run terminating scenarios last within a cell where practical, and always relaunch
> between scenarios. `resource-memory-pressure` can disturb the emulator — run it near the end.
> **`vendor-handled-exception` legitimately produces a weaker result in Release** (expected ~C,
> `agree=neither`): the app's `CaptureHandledException()` logs via `System.Diagnostics.Debug.WriteLine`,
> which is `[Conditional("DEBUG")]` and compiled out of Release. This is a genuine app finding, not a
> harness miss — **do not retry it as a failure.**

### 5. Reconcile process output vs logcat (deterministic)
For each scenario, run the analysis script. It is a single-file `dotnet run` C# app that
parses both logs, classifies the crash, checks the expected-artifact matrix, and scores quality:
```pwsh
dotnet run ./.github/skills/android-scenario/scripts/analyze-crash.cs -- `
  --scenario <key> --runtime <mono|coreclr> --config <Debug|Release> `
  --process process-<key>.log --logcat logcat-<key>.log --out verdict-<key>.json `
  --detected-runtime <mono|coreclr>
```
The script emits a JSON verdict and a one-line markdown summary. `--detected-runtime` is
optional ground truth (from step 3); supply it so the `runtime-matches-request` check is
meaningful — without it the analyzer reports `unknown` rather than guessing from the
non-discriminating `libmonodroid.so`. See
[scripts/analyze-crash.cs](./scripts/analyze-crash.cs) for flags and scoring rules.

### 6. Produce the report
Aggregate all `verdict-*.json` into one markdown report using
[assets/report-template.md](./assets/report-template.md). The report MUST include:
- A matrix overview table (rows = scenarios, columns = the 4 cells, cell = quality grade).
- Per-scenario sections: what was expected, what process output showed, what logcat/crashpad
  showed, whether they agreed, and a short assessment of how good the crash is + what's missing.
- A summary of systemic gaps (e.g. "CoreCLR/Release never emitted a managed stack",
  "no tombstone for SIGABRT under Mono/Debug").

Write the report to the repository root as `android-scenario.md` (overwrite on each run).
Keep the raw per-scenario artifacts (`process-*.log`, `logcat-*.log`, `verdict-*.json`,
and any `crashreport-*` / `tombstone-*` files) in the run folder for reference.

## Notes
- Drive the UI with [scripts/drive-scenario.ps1](./scripts/drive-scenario.ps1) (text-based
  uiautomator taps). The `mcp_maui_*` tools require a connected DevFlow agent, which this app
  does not embed, so they are a fallback only.
- Never use `--no-verify` or destructive device wipes. `adb logcat -c` and app relaunch are fine.
- If only some scenarios are requested (argument-hint), limit the inner loop accordingly.
