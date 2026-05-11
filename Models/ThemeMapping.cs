namespace ConfigurationThemeSwitcher.Models
{
	public sealed class ThemeMapping
	{
		public ThemeMapping()
		{
		}

		public ThemeMapping(string configurationName, string themeId)
		{
			ConfigurationName = configurationName;
			ThemeId = themeId;
		}

		public string ConfigurationName { get; set; }

		public string ThemeId { get; set; }
	}
}
