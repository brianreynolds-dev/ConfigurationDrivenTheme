using System;
using System.Collections.Generic;
using System.Linq;

using ConfigurationThemeSwitcher.Models;

namespace ConfigurationThemeSwitcher.Options
{
	internal static class ThemeSelectionValues
	{
		public static IReadOnlyList<string> BuildDisplayNames(IEnumerable<ThemeInfo> themes, string currentValue, bool includeBlank)
		{
			var values = new List<string>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (includeBlank)
			{
				values.Add(string.Empty);
				seen.Add(string.Empty);
			}

			foreach (var displayName in (themes ?? Enumerable.Empty<ThemeInfo>())
				.Where(theme => theme != null && !string.IsNullOrWhiteSpace(theme.DisplayName))
				.Select(theme => theme.DisplayName.Trim())
				.OrderBy(displayName => displayName, StringComparer.CurrentCultureIgnoreCase))
			{
				if (seen.Add(displayName))
				{
					values.Add(displayName);
				}
			}

			var current = currentValue == null ? string.Empty : currentValue.Trim();
			if (current.Length > 0 && seen.Add(current))
			{
				values.Add(current);
			}

			return values;
		}
	}
}
