using System;
using System.Linq;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Models;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class ThemeMappingService : IThemeMappingService
	{
		public string ResolveThemeId(ExtensionSettings settings, string configurationName, bool isDebugging)
		{
			if (settings == null)
			{
				return null;
			}

			if (isDebugging)
			{
				var debuggingThemeId = emptyToNull(settings.DebuggingThemeId);
				return debuggingThemeId ?? resolveFromMappings(settings, "Debug");
			}

			return resolveFromMappings(settings, configurationName);
		}

		private static string resolveFromMappings(ExtensionSettings settings, string configurationName)
		{
			if (settings.Mappings == null || string.IsNullOrWhiteSpace(configurationName))
			{
				return null;
			}

			var exactMatch = settings.Mappings.FirstOrDefault(mapping =>
				mapping != null &&
				string.Equals(mapping.ConfigurationName, configurationName, StringComparison.OrdinalIgnoreCase));

			if (exactMatch != null)
			{
				return emptyToNull(exactMatch.ThemeId);
			}

			var normalizedConfiguration = NormalizeConfigurationName(configurationName);
			if (normalizedConfiguration.Length == 0)
			{
				return null;
			}

			var normalizedMatch = settings.Mappings.FirstOrDefault(mapping =>
				mapping != null &&
				string.Equals(NormalizeConfigurationName(mapping.ConfigurationName), normalizedConfiguration, StringComparison.OrdinalIgnoreCase));

			return normalizedMatch == null ? null : emptyToNull(normalizedMatch.ThemeId);
		}

		internal static string NormalizeConfigurationName(string configurationName)
		{
			if (string.IsNullOrWhiteSpace(configurationName))
			{
				return string.Empty;
			}

			var normalized = configurationName.Trim();
			var platformSeparator = normalized.IndexOf('|');
			if (platformSeparator >= 0)
			{
				normalized = normalized.Substring(0, platformSeparator).Trim();
			}

			return normalized.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
		}

		private static string emptyToNull(string value)
		{
			return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}
	}
}
