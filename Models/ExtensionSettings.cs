using System.Collections.Generic;

namespace ConfigurationThemeSwitcher.Models
{
	public sealed class ExtensionSettings
	{
		public ExtensionSettings()
		{
			IsEnabled = true;
			RestoreFallbackThemeWhenUnmapped = true;
			DebounceMilliseconds = 250;
			Mappings = new List<ThemeMapping>();
		}

		public bool IsEnabled { get; set; }

		public bool RestoreFallbackThemeWhenUnmapped { get; set; }

		public int DebounceMilliseconds { get; set; }

		public string FallbackThemeId { get; set; }

		public List<ThemeMapping> Mappings { get; set; }
	}
}
