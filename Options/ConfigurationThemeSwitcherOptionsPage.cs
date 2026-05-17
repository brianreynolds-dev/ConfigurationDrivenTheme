using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Runtime.InteropServices;

using ConfigurationThemeSwitcher.Services;

using Microsoft.VisualStudio.Shell;

namespace ConfigurationThemeSwitcher.Options
{
	[DesignerCategory("Code")]
	[Guid("dfb65961-b0b1-4fae-b2d0-456db2e1e0c1")]
	public sealed class ConfigurationThemeSwitcherOptionsPage : DialogPage
	{
		public ConfigurationThemeSwitcherOptionsPage()
		{
			EnableExtension = true;
			RestoreFallbackThemeWhenUnmapped = true;
			DebounceMilliseconds = 250;
			FallbackThemeId = string.Empty;
			DebuggingThemeId = string.Empty;
			ConfigurationThemeMappings = string.Empty;
		}

		[Category("General")]
		[DisplayName("Enable automatic theme switching")]
		[Description("When false, configuration changes are ignored.")]
		public bool EnableExtension { get; set; }

		[Category("General")]
		[DisplayName("Restore fallback theme when configuration is unmapped")]
		[Description("When true, unmapped configurations restore the captured or selected fallback theme.")]
		public bool RestoreFallbackThemeWhenUnmapped { get; set; }

		[Category("General")]
		[DisplayName("Debounce milliseconds")]
		[Description("Delay after configuration events before applying a theme. Values below 0 are treated as 0.")]
		public int DebounceMilliseconds { get; set; }

		[Category("Themes")]
		[DisplayName("Fallback/default theme")]
		[Description("Theme to restore for unmapped configurations. Leave blank to use the theme captured when the package loaded.")]
		[TypeConverter(typeof(ThemeSelectionConverter))]
		public string FallbackThemeId { get; set; }

		[Category("Themes")]
		[DisplayName("Debugging theme")]
		[Description("Theme to apply while the Visual Studio debugger is running or paused. Leave blank to use the Debug mapping, then the fallback/default theme.")]
		[TypeConverter(typeof(ThemeSelectionConverter))]
		public string DebuggingThemeId { get; set; }

		[Category("Themes")]
		[DisplayName("Configuration-to-theme mappings")]
		[Description("Use the editor button to map configurations to themes selected from the available theme list.")]
		[Editor(typeof(ThemeMappingsEditor), typeof(UITypeEditor))]
		public string ConfigurationThemeMappings { get; set; }

		public override void LoadSettingsFromStorage()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			base.LoadSettingsFromStorage();
			FallbackThemeId = ThemeDisplayNameResolver.ToDisplayName(FallbackThemeId);
			DebuggingThemeId = ThemeDisplayNameResolver.ToDisplayName(DebuggingThemeId);
			ConfigurationThemeMappings = ThemeDisplayNameResolver.NormalizeMappingsForDisplay(ConfigurationThemeMappings);
		}

		protected override void OnApply(PageApplyEventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			FallbackThemeId = ThemeDisplayNameResolver.ToDisplayName(FallbackThemeId);
			DebuggingThemeId = ThemeDisplayNameResolver.ToDisplayName(DebuggingThemeId);
			ConfigurationThemeMappings = ThemeDisplayNameResolver.NormalizeMappingsForDisplay(ConfigurationThemeMappings);

			var errors = new List<string>();
			MappingLineParser.Parse(ConfigurationThemeMappings, errors);

			if (DebounceMilliseconds < 0)
			{
				errors.Add("Debounce milliseconds cannot be negative.");
			}

			if (errors.Any())
			{
				throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
			}

			base.OnApply(e);
		}
	}
}
