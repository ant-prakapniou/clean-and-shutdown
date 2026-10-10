# appsettings.json — reference

Configuration for the CleanAndShutdown CLI. The file is copied to the
output directory next to the executable and read at startup.

## `Cleanup` section

Bound to `CleanAndShutdown.Core.Models.CleanupOptions`.

| Key | Type | Default | Description |
|---|---|---|---|
| `DryRun` | bool | `false` | When `true`, the app reports what would be deleted but does not remove any files and does not shut down the machine. |
| `ShutdownAfterCleanup` | bool | `true` | When `false`, the cleanup runs normally but the machine is not shut down. Useful for debugging and scheduled maintenance. |
| `ShutdownDelaySeconds` | int | `5` | Number of seconds to wait before initiating shutdown. This window gives the user a chance to cancel with Ctrl+C. |
| `Targets` | array | (empty) | Ordered list of cleanup targets. See below. |

### `Cleanup.Targets[]`

Bound to `CleanAndShutdown.Core.Models.CleanupTargetOptions`.

| Key | Type | Default | Description |
|---|---|---|---|
| `Name` | string | (required) | Human-readable target name. Must match `ICleanupTarget.Name` of a registered target. If no match is found, the entry is skipped and a warning is logged. |
| `Path` | string | (required) | Directory to clean. Environment variables such as `%TEMP%` and `%SystemRoot%` are expanded at runtime. |
| `MinSizeMB` | int | `100` | Minimum directory size, in megabytes, required before cleanup is performed. A value of `0` disables the threshold check. |

**Order matters.** Targets are processed in the order they appear in
the array. The first element is processed first.

## `Serilog` section

Read directly by Serilog via `ReadFrom.Configuration`. See the
[Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration)
documentation for the full schema.

| Key | Description |
|---|---|
| `MinimumLevel.Default` | Default minimum level for all loggers. |
| `MinimumLevel.Override` | Per-source overrides. `Microsoft` and `System` are set to `Warning` to avoid noise from the host and framework. |
| `WriteTo[]` | List of sinks. Two sinks are configured: `Console` (compact, human-readable) and `File` (verbose, rolling daily, retained for 7 days). |

### Log file location

The file sink uses a **relative path** (`logs/cleanup-.log`), resolved
against the **current working directory**:

- when the executable is launched by double-clicking → the directory
  containing the exe;
- when launched from a terminal → the directory the terminal is in;
- when launched with `dotnet run` → the project directory.

The `logs/` folder is created automatically. Files roll daily and
older files are kept for 7 days.

## Environment variable overrides

Any option can be overridden via environment variables using the
`CLEANANDSHUTDOWN_` prefix and a double underscore as a path
separator. For example:

```powershell
$env:CLEANANDSHUTDOWN_Cleanup__DryRun = "true"
$env:CLEANANDSHUTDOWN_Cleanup__ShutdownDelaySeconds = "10"
```

## Command-line overrides

The same overrides can be passed on the command line:

```powershell
CleanAndShutdown.Cli.exe --Cleanup:DryRun=true
CleanAndShutdown.Cli.exe --Cleanup:ShutdownAfterCleanup=false
```