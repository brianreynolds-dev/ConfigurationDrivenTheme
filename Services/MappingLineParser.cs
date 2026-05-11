using System;
using System.Collections.Generic;
using System.Linq;

using ConfigurationThemeSwitcher.Models;

namespace ConfigurationThemeSwitcher.Services
{
	public static class MappingLineParser
	{
		public static List<ThemeMapping> Parse(string mappingText, IList<string> errors)
		{
			var mappings = new List<ThemeMapping>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (string.IsNullOrWhiteSpace(mappingText))
			{
				return mappings;
			}

			var lines = mappingText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i].Trim();
				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
				{
					continue;
				}

				var separatorIndex = line.IndexOf("=>", StringComparison.Ordinal);
				var separatorLength = 2;
				if (separatorIndex < 0)
				{
					separatorIndex = line.IndexOf('=');
					separatorLength = 1;
				}

				if (separatorIndex <= 0 || separatorIndex >= line.Length - separatorLength)
				{
					AddError(errors, "Line " + (i + 1) + " must use Configuration=ThemeId.");
					continue;
				}

				var configuration = line.Substring(0, separatorIndex).Trim();
				var theme = line.Substring(separatorIndex + separatorLength).Trim();

				if (configuration.Length == 0 || theme.Length == 0)
				{
					AddError(errors, "Line " + (i + 1) + " must include both a configuration and a theme.");
					continue;
				}

				if (!seen.Add(configuration))
				{
					AddError(errors, "Duplicate configuration mapping: " + configuration + ".");
					continue;
				}

				mappings.Add(new ThemeMapping(configuration, theme));
			}

			return mappings;
		}

		public static string Format(IEnumerable<ThemeMapping> mappings)
		{
			if (mappings == null)
			{
				return string.Empty;
			}

			return string.Join(Environment.NewLine, mappings
				.Where(mapping => mapping != null)
				.Select(mapping => (mapping.ConfigurationName ?? string.Empty).Trim() + "=" + (mapping.ThemeId ?? string.Empty).Trim()));
		}

		private static void AddError(IList<string> errors, string error)
		{
			if (errors != null)
			{
				errors.Add(error);
			}
		}
	}
}
