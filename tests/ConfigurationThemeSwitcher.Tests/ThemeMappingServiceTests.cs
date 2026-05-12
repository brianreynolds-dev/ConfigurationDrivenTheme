using System.Collections.Generic;

using ConfigurationThemeSwitcher.Models;
using ConfigurationThemeSwitcher.Services;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConfigurationThemeSwitcher.Tests
{
	[TestClass]
	public sealed class ThemeMappingServiceTests
	{
		[TestMethod]
		public void ResolveThemeId_UsesExactMatch()
		{
			var service = new ThemeMappingService();
			var settings = new ExtensionSettings
			{
				Mappings =
				[
					new ThemeMapping("Debug", "DarkTheme")
				]
			};

			Assert.AreEqual("DarkTheme", service.ResolveThemeId(settings, "Debug"));
		}

		[TestMethod]
		public void ResolveThemeId_UsesCaseInsensitiveMatch()
		{
			var service = new ThemeMappingService();
			var settings = new ExtensionSettings
			{
				Mappings =
				[
					new ThemeMapping("Release", "LightTheme")
				]
			};

			Assert.AreEqual("LightTheme", service.ResolveThemeId(settings, "release"));
		}

		[TestMethod]
		public void ResolveThemeId_UsesNormalizedSolutionConfigurationName()
		{
			var service = new ThemeMappingService();
			var settings = new ExtensionSettings
			{
				Mappings =
				[
					new ThemeMapping("Benchmark", "BlueTheme")
				]
			};

			Assert.AreEqual("BlueTheme", service.ResolveThemeId(settings, "Benchmark|Any CPU"));
		}

		[TestMethod]
		public void ResolveThemeId_ReturnsNullForUnmappedConfiguration()
		{
			var service = new ThemeMappingService();
			var settings = new ExtensionSettings
			{
				Mappings =
				[
					new ThemeMapping("Debug", "DarkTheme")
				]
			};

			Assert.IsNull(service.ResolveThemeId(settings, "Staging"));
		}

		[TestMethod]
		public void ResolveThemeId_ReturnsNullWhenExtensionDisabledScenarioHasNoMapping()
		{
			var service = new ThemeMappingService();
			var settings = new ExtensionSettings
			{
				IsEnabled = false,
				Mappings = []
			};

			Assert.IsNull(service.ResolveThemeId(settings, "Debug"));
		}

		[TestMethod]
		public void ResolveThemeId_ReturnsNullForInvalidThemeSelection()
		{
			var service = new ThemeMappingService();
			var settings = new ExtensionSettings
			{
				Mappings =
				[
					new ThemeMapping("Debug", " ")
				]
			};

			Assert.IsNull(service.ResolveThemeId(settings, "Debug"));
		}
	}
}
