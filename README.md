# Configuration Theme Switcher

Configuration Theme Switcher is a Visual Studio 2026 VSIX extension that changes the active Visual Studio theme when the active solution configuration or debugger state changes. It is intended to make states like Debug, Release, Staging, Benchmark, or active debugging visually obvious.

## Local install and debug

1. Open `ConfigurationThemeSwitcher.slnx` in Visual Studio 2026.
2. Build the `ConfigurationThemeSwitcher` project.
3. Press F5 to launch the experimental instance.
4. In the experimental instance, open a solution with multiple configurations.
5. Open **Tools > Options > Configuration Theme Switcher > General**.

The VSIX manifest targets Visual Studio 2026 (`[18.0,19.0)`) and amd64.

## Usage

Mappings are configured one per line:

```text
Debug=Dark
Release=Light
Benchmark=Blue
```

The right side is shown as a discovered theme display name, such as `Dark`, `Light`, or a custom theme name. Existing GUID-based mappings are converted back to display names when the options page loads. Matching is case-insensitive. Exact configuration names are checked first, then normalized names such as `Debug|Any CPU` -> `Debug`.

When Visual Studio is actively debugging, debugger run mode and break mode use this precedence:

1. Debugging theme
2. `Debug` configuration mapping
3. Fallback/default theme restore when unmapped

Leaving the Debugging theme blank preserves the existing behavior for most users because active debugging falls back to the `Debug` mapping.

Settings exposed in Tools > Options:

- Enable automatic theme switching
- Restore fallback theme when configuration is unmapped
- Debounce milliseconds
- Fallback/default theme
- Debugging theme
- Configuration-to-theme mappings

On package load, the current theme is captured as the runtime fallback theme. If no explicit fallback theme is stored, that captured theme is persisted for future restores.

## APIs and architecture

The extension uses an `AsyncPackage` with background autoload contexts. The package class only composes services.

Key adapters:

- `ConfigurationMonitor` subscribes to `IVsUpdateSolutionEvents2.OnActiveProjectCfgChange` and `IVsSolutionEvents`.
- `VsDebuggerStateMonitor` subscribes to `IVsDebuggerEvents.OnModeChange` and reads `IVsDebugger.GetMode`.
- `DteActiveConfigurationProvider` isolates the guarded DTE fallback used to read `SolutionBuild.ActiveConfiguration.Name`.
- `VsThemeCatalogService` uses `IVsColorThemeService` when available and falls back to a documented list of common built-in themes.
- `VsThemeApplicationService` captures, compares, applies, and restores themes.
- `SettingsService` persists options through `DialogPage`.

Threading follows Visual Studio async guidance: no `.Result`/`.Wait()`, UI-thread switching only at VS API boundaries, and debounced background work for rapid configuration events.

## Known limitations

Visual Studio Theme application is a global change that can impact all open solutions and tool windows. This extension does not attempt to isolate theme changes to specific windows or contexts, but it will update according to the currently selected instance.

Visual Studio theme enumeration and application rely on the Visual Studio color theme service. If the service changes in a future VS 2026 SDK, the adapter will log a warning and avoid applying an unknown theme. The fallback built-in theme GUID list is intentionally isolated in `VsThemeCatalogService` and marked with a TODO to re-verify against the current VS 2026 SDK.

The options page uses a `DialogPage` property grid. It is reliable and lightweight, but not as polished as a custom WPF mapping editor.

## Troubleshooting

If a theme does not apply:

- Confirm the mapping value matches a theme display name or ID.
- Check **Tools > Options > Configuration Theme Switcher > General** for duplicate or empty mapping lines.
- Confirm automatic theme switching is enabled.
- Open the Visual Studio ActivityLog and search for `Configuration Theme Switcher`.

If configuration changes are missed:

- Confirm the solution has an active solution configuration.
- Increase debounce milliseconds if solution/project events are noisy.
- Check ActivityLog warnings for unavailable `SVsSolutionBuildManager`, `SVsSolution`, or DTE services.

## Manual test

1. Launch the Visual Studio experimental instance.
2. Open a solution with Debug and Release configurations.
3. Go to **Tools > Options > Configuration Theme Switcher > General**.
4. Map `Debug` to `Dark`.
5. Map `Release` to `Light`.
6. Switch Debug -> Release and confirm the active theme changes.
7. Switch Release -> Debug and confirm the active theme changes.
8. Set Debugging theme to a distinct theme, start debugging, and confirm that theme is applied.
9. Break in the debugger and confirm the Debugging theme remains active.
10. Stop debugging and confirm the active configuration theme is restored.
