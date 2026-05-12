using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Models;
using ConfigurationThemeSwitcher.Options;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class SettingsService : ISettingsService
	{
		private readonly AsyncPackage _package;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly IActivityLogService _activityLog;

		public SettingsService(AsyncPackage package, JoinableTaskFactory joinableTaskFactory, IActivityLogService activityLog)
		{
			_package = package;
			_joinableTaskFactory = joinableTaskFactory;
			_activityLog = activityLog;
		}

		public async Task<ExtensionSettings> GetSettingsAsync(CancellationToken cancellationToken)
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			var page = getOptionsPage();
			var errors = new List<string>();
			var mappings = MappingLineParser.Parse(page.ConfigurationThemeMappings, errors);
			foreach (var error in errors)
			{
				_activityLog.Warning("Ignoring invalid mapping setting: " + error);
			}

			return new ExtensionSettings
			{
				IsEnabled = page.EnableExtension,
				RestoreFallbackThemeWhenUnmapped = page.RestoreFallbackThemeWhenUnmapped,
				DebounceMilliseconds = page.DebounceMilliseconds < 0 ? 0 : page.DebounceMilliseconds,
				FallbackThemeId = page.FallbackThemeId,
				Mappings = mappings
			};
		}

		public async Task SaveSettingsAsync(ExtensionSettings settings, CancellationToken cancellationToken)
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			if (settings == null)
			{
				return;
			}

			var page = getOptionsPage();
			page.EnableExtension = settings.IsEnabled;
			page.RestoreFallbackThemeWhenUnmapped = settings.RestoreFallbackThemeWhenUnmapped;
			page.DebounceMilliseconds = settings.DebounceMilliseconds < 0 ? 0 : settings.DebounceMilliseconds;
			page.FallbackThemeId = settings.FallbackThemeId ?? string.Empty;
			page.ConfigurationThemeMappings = MappingLineParser.Format(settings.Mappings);
			page.SaveSettingsToStorage();
		}

		private ConfigurationThemeSwitcherOptionsPage getOptionsPage()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return (ConfigurationThemeSwitcherOptionsPage)_package.GetDialogPage(typeof(ConfigurationThemeSwitcherOptionsPage));
		}
	}
}
