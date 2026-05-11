using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Models;

using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class ConfigurationThemeCoordinator : IDisposable
	{
		private readonly ISettingsService _settingsService;
		private readonly IThemeMappingService _mappingService;
		private readonly IThemeCatalogService _themeCatalogService;
		private readonly IThemeApplicationService _themeApplicationService;
		private readonly IConfigurationMonitor _configurationMonitor;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly IActivityLogService _activityLog;

		public ConfigurationThemeCoordinator(
			ISettingsService settingsService,
			IThemeMappingService mappingService,
			IThemeCatalogService themeCatalogService,
			IThemeApplicationService themeApplicationService,
			IConfigurationMonitor configurationMonitor,
			JoinableTaskFactory joinableTaskFactory,
			IActivityLogService activityLog)
		{
			_settingsService = settingsService;
			_mappingService = mappingService;
			_themeCatalogService = themeCatalogService;
			_themeApplicationService = themeApplicationService;
			_configurationMonitor = configurationMonitor;
			_joinableTaskFactory = joinableTaskFactory;
			_activityLog = activityLog;
		}

		public async Task InitializeAsync(CancellationToken cancellationToken)
		{
			await _themeApplicationService.CaptureFallbackThemeAsync(cancellationToken).ConfigureAwait(false);
			await SeedDefaultMappingsAsync(cancellationToken).ConfigureAwait(false);

			_configurationMonitor.ActiveConfigurationChanged += OnActiveConfigurationChanged;
			_configurationMonitor.SolutionClosed += OnSolutionClosed;
			await _configurationMonitor.StartAsync(cancellationToken).ConfigureAwait(false);
		}

		public void Dispose()
		{
			_configurationMonitor.ActiveConfigurationChanged -= OnActiveConfigurationChanged;
			_configurationMonitor.SolutionClosed -= OnSolutionClosed;
		}

		private void OnActiveConfigurationChanged(object sender, ActiveConfigurationChangedEventArgs e)
		{
			_joinableTaskFactory.RunAsync(async delegate
			{
				await ApplyForConfigurationAsync(e.ConfigurationName, CancellationToken.None).ConfigureAwait(false);
			}).Task.Forget();
		}

		private void OnSolutionClosed(object sender, EventArgs e)
		{
			_joinableTaskFactory.RunAsync(async delegate
			{
				try
				{
					var settings = await _settingsService.GetSettingsAsync(CancellationToken.None).ConfigureAwait(false);
					if (settings.IsEnabled && settings.RestoreFallbackThemeWhenUnmapped)
					{
						await _themeApplicationService.RestoreFallbackThemeAsync(CancellationToken.None).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					_activityLog.Error("Failed to handle solution close theme restore.", ex);
				}
			}).Task.Forget();
		}

		private async Task ApplyForConfigurationAsync(string configurationName, CancellationToken cancellationToken)
		{
			try
			{
				var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
				if (settings == null || !settings.IsEnabled)
				{
					return;
				}

				var themeId = _mappingService.ResolveThemeId(settings, configurationName);
				if (!string.IsNullOrWhiteSpace(themeId))
				{
					var theme = await _themeCatalogService.FindThemeAsync(themeId, cancellationToken).ConfigureAwait(false);
					if (theme == null)
					{
						_activityLog.Warning("Mapped theme '" + themeId + "' for configuration '" + (configurationName ?? "<none>") + "' was not found.");
						return;
					}

					await _themeApplicationService.ApplyThemeAsync(theme.Id, cancellationToken).ConfigureAwait(false);
					return;
				}

				if (settings.RestoreFallbackThemeWhenUnmapped)
				{
					await _themeApplicationService.RestoreFallbackThemeAsync(cancellationToken).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_activityLog.Error("Failed to apply theme for active configuration '" + (configurationName ?? "<none>") + "'.", ex);
			}
		}

		private async Task SeedDefaultMappingsAsync(CancellationToken cancellationToken)
		{
			var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
			if (settings.Mappings != null && settings.Mappings.Count > 0)
			{
				return;
			}

			var themes = await _themeCatalogService.GetAvailableThemesAsync(cancellationToken).ConfigureAwait(false);
			var darkTheme = themes.FirstOrDefault(theme => theme.DisplayName != null && theme.DisplayName.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0);
			var lightTheme = themes.FirstOrDefault(theme => theme.DisplayName != null && theme.DisplayName.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0);

			if (darkTheme == null && lightTheme == null)
			{
				return;
			}

			settings.Mappings.Add(new ThemeMapping("Debug", darkTheme == null ? string.Empty : darkTheme.DisplayName));
			settings.Mappings.Add(new ThemeMapping("Release", lightTheme == null ? string.Empty : lightTheme.DisplayName));
			settings.Mappings = settings.Mappings.Where(mapping => !string.IsNullOrWhiteSpace(mapping.ThemeId)).ToList();

			if (settings.Mappings.Count > 0)
			{
				await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
