# Getting Started

Configuration Theme Switcher changes the active Visual Studio 2026 theme when your solution configuration or debugger state changes, so states like Debug, Release, Staging, Benchmark, or active debugging are visually obvious.

## Configure Theme Mappings

1. Open Visual Studio.
2. Go to **Tools > Options > Configuration Theme Switcher > General**.
3. Make sure **Enable automatic theme switching** is turned on.
4. Open the mapping editor.
5. Add configuration rows and choose themes from the Theme dropdown:

```text
Debug=Dark
Release=Light
Benchmark=Blue
```

The left side is the solution configuration name. The right side is selected from the available Visual Studio themes.

## Fallback Theme

When a configuration is not mapped, the extension can restore a fallback theme. Set **Restore fallback theme when configuration is unmapped** and choose the fallback theme you want Visual Studio to use.

## Tips

- Exact configuration names are checked first.
- Platform-qualified names such as `Debug|Any CPU` are normalized to `Debug`.
- If a theme does not switch, confirm the selected theme still exists in Visual Studio.
