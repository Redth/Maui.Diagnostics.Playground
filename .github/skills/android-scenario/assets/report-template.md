<!--
  Crash-quality report template for the android-scenario harness.
  Fill placeholders in {{ }} from the aggregated verdict-*.json files.
  Grades: A (excellent) · B (good) · C (usable, gaps) · D (poor/missing required artifacts).
-->
# Android Crash Scenario Report

- **Date**: {{date}}
- **Device**: {{device model / api level}}
- **App**: codes.redth.mauidiagnosticsgallery
- **SDK**: {{dotnet --version}}
- **Matrix cells**: MauiDiagnosticsUseCoreClr ∈ {false=Mono, true=CoreCLR} × Configuration ∈ {Debug, Release}

## Matrix overview

Grade per scenario per cell (A best, D worst).

| Scenario | Mono/Debug | Mono/Release | CoreCLR/Debug | CoreCLR/Release |
|----------|:----------:|:------------:|:-------------:|:---------------:|
| managed-ui-unhandled | {{}} | {{}} | {{}} | {{}} |
| managed-background-unhandled | {{}} | {{}} | {{}} | {{}} |
| managed-async-void | {{}} | {{}} | {{}} | {{}} |
| runtime-null-reference | {{}} | {{}} | {{}} | {{}} |
| runtime-access-violation | {{}} | {{}} | {{}} | {{}} |
| runtime-failfast | {{}} | {{}} | {{}} | {{}} |
| runtime-stack-overflow | {{}} | {{}} | {{}} | {{}} |
| native-android-abort | {{}} | {{}} | {{}} | {{}} |
| native-android-sigsegv | {{}} | {{}} | {{}} | {{}} |
| native-illegal-instruction | {{}} | {{}} | {{}} | {{}} |
| native-background-thread | {{}} | {{}} | {{}} | {{}} |
| mixed-managed-native | {{}} | {{}} | {{}} | {{}} |
| resource-memory-pressure | {{}} | {{}} | {{}} | {{}} |
| resource-ui-hang | {{}} | {{}} | {{}} | {{}} |
| vendor-handled-exception | {{}} | {{}} | {{}} | {{}} |
| lifecycle-startup-crash | {{}} | {{}} | {{}} | {{}} |
| lifecycle-resume-crash | {{}} | {{}} | {{}} | {{}} |

## Per-scenario detail

<!-- Repeat this block for each scenario × cell that needs discussion. -->
### {{scenario-key}} — {{runtime}}/{{config}}  · Grade {{grade}} ({{score}}/{{max}})

- **Expected**: {{expected artifacts for the category}}
- **Process output showed**: {{managed exception type / runtime report / nothing}}
- **logcat / crashpad showed**: {{signal, tombstone?, backtrace?, BuildIds, DOTNET report?}}
- **Process vs logcat agreement**: {{yes | process-only | logcat-only | neither}}
- **Missing required artifacts**: {{list or none}}
- **Assessment**: {{1–3 sentences on how good this crash is and what is missing}}

## Systemic findings

- {{e.g. "CoreCLR/Release never emitted scenario-runner managed frames (trimming)."}}
- {{e.g. "SIGABRT scenarios produced tombstones in all cells; BuildIds present for native kit + runtime."}}
- {{e.g. "resource-ui-hang only produced ANR on API 34, not API 31."}}

## Recommendations

- {{actionable next steps to improve crash fidelity / close gaps}}
