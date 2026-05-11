namespace ConfigurationThemeSwitcher.Models
{
	public sealed class ThemeInfo
	{
		public ThemeInfo()
		{
		}

		public ThemeInfo(string id, string displayName, bool isSystemTheme)
		{
			Id = id;
			DisplayName = displayName;
			IsSystemTheme = isSystemTheme;
		}

		public string Id { get; set; }

		public string DisplayName { get; set; }

		public bool IsSystemTheme { get; set; }
	}
}
