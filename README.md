# CleanAndShutdown

[![Build and Test](https://github.com/ant-prakapniou/clean-and-shutdown/actions/workflows/build.yml/badge.svg)](https://github.com/ant-prakapniou/clean-and-shutdown/actions/workflows/build.yml)

A small Windows utility that cleans temporary directories and shuts the
machine down — with per-folder size thresholds, structured logging, and
a cancellable shutdown countdown.

The project started as a PowerShell script and was rewritten in C# /
.NET as a learning exercise and to remove the dependency on PowerShell 7
and `.bat` wrappers.

---

## What it does

The utility processes a configurable list of cleanup targets. For each
target it:

1. Expands environment-variable placeholders in the path
   (for example, `%TEMP%`).
2. Measures the directory size recursively.
3. Compares the size against a per-target threshold (in MB).
4. If the size is **below** the threshold, the target is skipped and
   nothing is deleted.
5. If the size is **at or above** the threshold, the top-level entries
   are deleted one by one. Locked or inaccessible entries are skipped
   and counted — they do not abort the run.
6. After all targets have been processed, renders a per-second
   countdown in the console and then shuts the machine down (unless
   disabled or running in dry-run mode). The countdown is implemented
   in the orchestrator and reported through the same progress channel
   as cleanup progress, with the target name `(shutdown)`;
   `shutdown.exe` is invoked with `/t 0` only after the countdown
   completes.

A `Ctrl+C` during the countdown cancels the shutdown before
`shutdown.exe` is invoked.

### Default targets

| Name | Path | Threshold |
|---|---|---|
| System Temp | `%SystemRoot%\Temp` | 100 MB |
| User Temp | `%TEMP%` | 100 MB |
| Windows Update cache | `%SystemRoot%\SoftwareDistribution\Download` | 100 MB |

The Windows Update service (`wuauserv`) is intentionally **not**
stopped. Files locked by the service are skipped during the run and
removed on a subsequent execution once they are released.

---

## Requirements

- **Windows 10 or later** (Windows 11 recommended).
- **Administrator privileges** at runtime. The manifest requests
  elevation via UAC; the app will not start otherwise.
- **.NET 11 runtime** if running from source. Not required when
  launching a self-contained published executable.

---

## Project layout

```
CleanAndShutdown/
├── src/
│   ├── CleanAndShutdown.Core/     # Library: all cleanup and shutdown logic
│   │   ├── Abstractions/          # IFileSystem, ICleanupTarget, IShutdownService
│   │   ├── Models/                # Options, results, progress, summary
│   │   ├── Services/              # File system, size calculator, orchestrator
│   │   └── Targets/               # Base class + concrete targets
│   └── CleanAndShutdown.Cli/      # Console executable
│       ├── Program.cs             # Entry point, DI, Serilog wiring
│       ├── appsettings.json       # Configuration
│       ├── appsettings.README.md  # Configuration reference
│       └── app.manifest           # requireAdministrator, longPathAware
└── tests/
    └── CleanAndShutdown.Tests/    # xUnit v3 tests + fakes
        ├── Fakes/                 # FakeFileSystem, FakeShutdownService, ...
        └── xunit.runner.json      # Test runner configuration
```

The design is layered:

- **Core** — pure logic, fully testable. No console output, no direct
  `System.IO` outside `PhysicalFileSystem`.
- **Cli** — thin wrapper: reads configuration, builds the DI container,
  prints the banner and the final report, handles `Ctrl+C`.
- **Tests** — 41 unit tests covering the base target logic, the
  orchestrator, and the size calculator. The file system and shutdown
  service are abstracted so tests never touch a real disk or shut down
  the machine.

---

## Building

```powershell
dotnet restore
dotnet build -c Release
```

## Running from source

```powershell
dotnet run --project src\CleanAndShutdown.Cli
```

> The `requireAdministrator` manifest is **not** applied when launching
> via `dotnet run`, so the app will report "must be run as Administrator"
> and exit with code `2`. To exercise the full flow, build a Release
> executable and run it directly — Windows will show a UAC prompt.

## Publishing a standalone executable

```powershell
dotnet publish src\CleanAndShutdown.Cli -c Release `
    -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true
```

The output is a self-contained `.exe` (roughly 40 MB) that runs without
a separately installed .NET runtime. It still needs `appsettings.json`
next to it: the application reads configuration from
`AppContext.BaseDirectory`, so the executable and the JSON file must
stay side by side.

Every GitHub release ships a single `.zip` containing both files:

CleanAndShutdown-vX.Y.Z-win-x64.zip
├── CleanAndShutdown.exe
└── appsettings.json

Unpack it, run the `.exe`, and adjust `appsettings.json` as needed. See
[`docs/publish.md`](docs/publish.md) for details.

---

## Configuration

All settings live in `appsettings.json` next to the executable. See
[`src/CleanAndShutdown.Cli/appsettings.README.md`](src/CleanAndShutdown.Cli/appsettings.README.md)
for the full reference.

Minimal example:

```json
{
  "Cleanup": {
    "DryRun": false,
    "ShutdownAfterCleanup": true,
    "ShutdownDelaySeconds": 5,
    "Targets": [
      { "Name": "System Temp", "Path": "%SystemRoot%\\Temp", "MinSizeMB": 100 },
      { "Name": "User Temp",   "Path": "%TEMP%",            "MinSizeMB": 100 },
      { "Name": "Windows Update cache",
        "Path": "%SystemRoot%\\SoftwareDistribution\\Download",
        "MinSizeMB": 100 }
    ]
  }
}
```

Settings can be overridden without editing the file:

- **Environment variables** with the `CLEANANDSHUTDOWN_` prefix:
  ```powershell
  $env:CLEANANDSHUTDOWN_Cleanup__DryRun = "true"
  ```
- **Command-line arguments** using the standard .NET configuration
  syntax:
  ```powershell
  CleanAndShutdown.Cli.exe --Cleanup:DryRun=true
  ```

---

## Dry run

To see what would be deleted without touching anything, set
`Cleanup.DryRun` to `true`. The utility will still measure directories
and report what it *would* have removed, but no files are deleted and
no shutdown is initiated.

Combined with `ShutdownAfterCleanup: false`, this is a safe way to
preview the behavior on a new machine:

```powershell
CleanAndShutdown.Cli.exe --Cleanup:DryRun=true --Cleanup:ShutdownAfterCleanup=false
```

---

## Logging

Logs are written to `logs/cleanup-YYYYMMDD.log` relative to the current
working directory. Files roll daily and are kept for 7 days. Console
output is human-readable; the file output includes timestamps and
exception details.

Log level and sinks are configured in the `Serilog` section of
`appsettings.json`.

---

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. |
| `1` | Unhandled exception. |
| `2` | Not running with administrator privileges. |
| `130` | Cancelled by the user (`Ctrl+C`). |

These are stable and intended for scripts and CI.

---

## Testing

The project uses **xUnit v3** with the **Microsoft.Testing.Platform**
runner. Tests are built as an executable:

```powershell
dotnet build .\tests\CleanAndShutdown.Tests
& ".\tests\CleanAndShutdown.Tests\bin\Debug\net11.0-windows\CleanAndShutdown.Tests.exe"
```

### Test coverage

- `DirectorySizeCalculatorTests` — empty/missing directories, recursive
  summation, zero-size files, large trees (no `long` overflow),
  argument validation.
- `CleanupTargetBaseTests` — threshold boundary, missing and empty
  directories, locked entries, dry run, environment-variable expansion,
  cancellation, progress reporting, size reporting.
- `CleanupOrchestratorTests` — target ordering, unknown-target skipping,
  failure isolation, shutdown policy, cancellation, aggregation,
  progress annotation, shutdown countdown (event ordering, zero delay,
  cancellation before `ShutdownNow`, `onSummaryReady` ordering),
  constructor guards.

Total: **41 tests**, all green.

---

## Design notes

A few decisions worth knowing about:

- **`SyncProgress<T>` instead of `Progress<T>`.** `Progress<T>`
  dispatches callbacks asynchronously via `SynchronizationContext` (or
  the thread pool). That breaks the ordering of progress events and
  makes tests non-deterministic. `SyncProgress<T>` invokes the callback
  inline, on the calling thread.
- **`IFileSystem` instead of `System.IO` directly.** Everywhere except
  `PhysicalFileSystem` goes through the abstraction, so tests can run
  against an in-memory file system.
- **No `wuauserv` shutdown.** Stopping the Windows Update service
  requires `Stop-Service`, introduces elevated privileges beyond what
  we already have, and risks interfering with updates in progress.
  Files locked by the service are simply skipped.
- **`requireAdministrator` in the manifest.** Elevation is required to
  clean `%SystemRoot%\Temp` and the Update cache. Relying on a `.bat`
  wrapper with `Start-Process -Verb RunAs` was fragile; the manifest is
  the correct mechanism.
- **Countdown lives in the orchestrator, not in the shutdown service.**
  `IShutdownService` is a thin wrapper over `shutdown.exe`; it does not
  wait. The per-second countdown is reported through the same
  `IProgress<CleanupProgress>` channel as cleanup progress, using
  `TargetName = "(shutdown)"`. This keeps the shutdown service
  trivially testable and makes `Ctrl+C` cancel before `shutdown.exe`
  is ever invoked. The CLI prints the final report before the countdown
  starts (via the `onSummaryReady` callback), so the report is visible
  even if `shutdown.exe` terminates the process shortly afterwards.

---

## License

This project is licensed under the MIT License — see the LICENSE file for details.

### Third-party dependencies

- **FluentAssertions** (v8.x) — used in tests only. Licensed under the
  [Xceed Community License](https://xceed.com/fluent-assertions-faq/),
  free for non-commercial and open-source use. Commercial use requires
  a paid subscription from Xceed.