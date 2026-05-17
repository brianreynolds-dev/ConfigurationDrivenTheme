using System.Collections.Generic;
using System.Linq;

using ConfigurationThemeSwitcher.Models;
using ConfigurationThemeSwitcher.Options;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConfigurationThemeSwitcher.Tests
{
	[TestClass]
	public sealed class ThemeSelectionValuesTests
	{
		[TestMethod]
		public void BuildDisplayNames_IncludesBlankWhenRequested()
		{
			var values = ThemeSelectionValues.BuildDisplayNames(createThemes(), currentValue: null, includeBlank: true);

			Assert.AreEqual(string.Empty, values[0]);
		}

		[TestMethod]
		public void BuildDisplayNames_SortsAndDeduplicatesThemeNames()
		{
			var values = ThemeSelectionValues.BuildDisplayNames(
				[
					new ThemeInfo("2", "Light", true),
					new ThemeInfo("1", "dark", true),
					new ThemeInfo("3", "Dark", true)
				],
				currentValue: null,
				includeBlank: false);

			CollectionAssert.AreEqual(new List<string> { "dark", "Light" }, values.ToList());
		}

		[TestMethod]
		public void BuildDisplayNames_PreservesUnknownCurrentValue()
		{
			var values = ThemeSelectionValues.BuildDisplayNames(createThemes(), "Legacy Theme", includeBlank: true);

			Assert.IsTrue(values.Contains("Legacy Theme"));
		}

		[TestMethod]
		public void BuildDisplayNames_DoesNotDuplicateKnownCurrentValue()
		{
			var values = ThemeSelectionValues.BuildDisplayNames(createThemes(), "Dark", includeBlank: true);

			Assert.AreEqual(1, values.Count(value => value == "Dark"));
		}

		private static IReadOnlyList<ThemeInfo> createThemes()
		{
			return
			[
				new ThemeInfo("1", "Dark", true),
				new ThemeInfo("2", "Light", true)
			];
		}
	}
}
