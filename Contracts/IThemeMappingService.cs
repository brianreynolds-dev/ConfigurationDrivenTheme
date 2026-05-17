using ConfigurationThemeSwitcher.Models;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IThemeMappingService
	{
		string ResolveThemeId(ExtensionSettings settings, string configurationName, bool isDebugging);
	}
}
