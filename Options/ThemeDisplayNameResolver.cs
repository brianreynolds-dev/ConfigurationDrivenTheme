using System;
using System.Collections.Generic;
using System.Linq;

using ConfigurationThemeSwitcher.Models;
using ConfigurationThemeSwitcher.Services;

using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace ConfigurationThemeSwitcher.Options
{
	internal static class ThemeDisplayNameResolver
	{
		private static readonly IReadOnlyList<ThemeInfo> FallbackThemes = new[]
		{
			new ThemeInfo("1ded0138-47ce-435e-84ef-9ec1f439b749", "Dark", true),
			new ThemeInfo("de3dbbcd-f642-433c-8353-8f1df4370aba", "Light", true),
			new ThemeInfo("a4d6a176-b948-4b29-8c66-53c97a1ed7d0", "Blue", true)
		};

		public static IReadOnlyList<ThemeInfo> GetThemes()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			try
			{
				var themeService = Package.GetGlobalService(typeof(SVsColorThemeService)) as IVsColorThemeService;
				if (themeService == null || themeService.Themes == null)
				{
					return FallbackThemes;
				}

				var themes = new List<ThemeInfo>();
				for (var i = 0; i < themeService.Themes.Count; i++)
				{
					var theme = themeService.Themes[i];
					if (theme == null || !theme.IsUserVisible)
					{
						continue;
					}

					themes.Add(new ThemeInfo(theme.ThemeId.ToString("D"), theme.Name, true));
				}

				return themes.Count == 0 ? FallbackThemes : themes;
			}
			catch
			{
				return FallbackThemes;
			}
		}

		public static string ToDisplayName(string themeIdOrName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (string.IsNullOrWhiteSpace(themeIdOrName))
			{
				return string.Empty;
			}

			var requested = themeIdOrName.Trim();
			var theme = GetThemes().FirstOrDefault(candidate =>
				string.Equals(candidate.Id, requested, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(candidate.DisplayName, requested, StringComparison.OrdinalIgnoreCase));

			return theme == null ? requested : theme.DisplayName;
		}

		public static string NormalizeMappingsForDisplay(string mappingText)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var errors = new List<string>();
			var mappings = MappingLineParser.Parse(mappingText, errors);
			foreach (var mapping in mappings)
			{
				mapping.ThemeId = ToDisplayName(mapping.ThemeId);
			}

			return MappingLineParser.Format(mappings);
		}
	}
}
