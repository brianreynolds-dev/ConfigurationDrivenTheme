# Getting Started

Configuration Theme Switcher changes the active Visual Studio theme when your solution configuration changes, so states like Debug, Release, Staging, or Benchmark are visually obvious.

## Configure Theme Mappings

1. Open Visual Studio.
2. Go to **Tools > Options > Configuration Theme Switcher > General**.
3. Make sure **Enable automatic theme switching** is turned on.
4. Add one mapping per line:

```text
Debug=Dark
Release=Light
Benchmark=Blue
```

The left side is the solution configuration name. The right side is the Visual Studio theme display name.

## Fallback Theme

When a configuration is not mapped, the extension can restore a fallback theme. Set **Restore fallback theme when configuration is unmapped** and choose the fallback theme you want Visual Studio to use.

## Tips

- Exact configuration names are checked first.
- Platform-qualified names such as `Debug|Any CPU` are normalized to `Debug`.
- If a theme does not switch, confirm the mapping value matches the theme name shown in Visual Studio.
