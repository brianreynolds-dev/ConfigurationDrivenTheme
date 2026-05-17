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
		private readonly IDebuggerStateMonitor _debuggerStateMonitor;
		private readonly IInstanceActivationMonitor _instanceActivationMonitor;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly IActivityLogService _activityLog;
		private readonly object _configurationLock = new object();
		private string _lastConfigurationName;

		public ConfigurationThemeCoordinator(
			ISettingsService settingsService,
			IThemeMappingService mappingService,
			IThemeCatalogService themeCatalogService,
			IThemeApplicationService themeApplicationService,
			IConfigurationMonitor configurationMonitor,
			IDebuggerStateMonitor debuggerStateMonitor,
			IInstanceActivationMonitor instanceActivationMonitor,
			JoinableTaskFactory joinableTaskFactory,
			IActivityLogService activityLog)
		{
			_settingsService = settingsService;
			_mappingService = mappingService;
			_themeCatalogService = themeCatalogService;
			_themeApplicationService = themeApplicationService;
			_configurationMonitor = configurationMonitor;
			_debuggerStateMonitor = debuggerStateMonitor;
			_instanceActivationMonitor = instanceActivationMonitor;
			_joinableTaskFactory = joinableTaskFactory;
			_activityLog = activityLog;
		}

		public async Task InitializeAsync(CancellationToken cancellationToken)
		{
			await _themeApplicationService.CaptureFallbackThemeAsync(cancellationToken).ConfigureAwait(false);
			await seedDefaultMappingsAsync(cancellationToken).ConfigureAwait(false);

			_configurationMonitor.ActiveConfigurationChanged += onActiveConfigurationChanged;
			_configurationMonitor.SolutionClosed += onSolutionClosed;
			_debuggerStateMonitor.DebuggingStateChanged += onDebuggingStateChanged;
			_instanceActivationMonitor.Activated += onInstanceActivated;
			await _instanceActivationMonitor.StartAsync(cancellationToken).ConfigureAwait(false);
			await _debuggerStateMonitor.StartAsync(cancellationToken).ConfigureAwait(false);
			await _configurationMonitor.StartAsync(cancellationToken).ConfigureAwait(false);
		}

		public void Dispose()
		{
			_configurationMonitor.ActiveConfigurationChanged -= onActiveConfigurationChanged;
			_configurationMonitor.SolutionClosed -= onSolutionClosed;
			_debuggerStateMonitor.DebuggingStateChanged -= onDebuggingStateChanged;
			_instanceActivationMonitor.Activated -= onInstanceActivated;
		}

		private void onActiveConfigurationChanged(object sender, ActiveConfigurationChangedEventArgs e)
		{
			lock (_configurationLock)
			{
				_lastConfigurationName = e.ConfigurationName;
			}

			if (!_instanceActivationMonitor.IsActive)
			{
				return;
			}

			_joinableTaskFactory.RunAsync(async delegate
			{
				await applyForConfigurationAsync(e.ConfigurationName, _debuggerStateMonitor.IsDebugging, CancellationToken.None).ConfigureAwait(false);
			}).Task.Forget();
		}

		private void onDebuggingStateChanged(object sender, DebuggingStateChangedEventArgs e)
		{
			if (!_instanceActivationMonitor.IsActive)
			{
				return;
			}

			_joinableTaskFactory.RunAsync(async delegate
			{
				await applyForConfigurationAsync(getLastConfigurationName(), e.IsDebugging, CancellationToken.None).ConfigureAwait(false);
			}).Task.Forget();
		}

		private void onInstanceActivated(object sender, EventArgs e)
		{
			_joinableTaskFactory.RunAsync(async delegate
			{
				var lastKnownConfigurationName = getLastConfigurationName();
				try
				{
					await _debuggerStateMonitor.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
					await _configurationMonitor.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_activityLog.Error("Failed to refresh active configuration after Visual Studio activation.", ex);
					if (!string.IsNullOrWhiteSpace(lastKnownConfigurationName))
					{
						await applyForConfigurationAsync(lastKnownConfigurationName, _debuggerStateMonitor.IsDebugging, CancellationToken.None).ConfigureAwait(false);
					}
				}
			}).Task.Forget();
		}

		private void onSolutionClosed(object sender, EventArgs e)
		{
			if (!_instanceActivationMonitor.IsActive)
			{
				return;
			}

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

		private async Task applyForConfigurationAsync(string configurationName, bool isDebugging, CancellationToken cancellationToken)
		{
			try
			{
				var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
				if (settings == null || !settings.IsEnabled)
				{
					return;
				}

				var themeId = _mappingService.ResolveThemeId(settings, configurationName, isDebugging);
				if (!string.IsNullOrWhiteSpace(themeId))
				{
					var theme = await _themeCatalogService.FindThemeAsync(themeId, cancellationToken).ConfigureAwait(false);
					if (theme == null)
					{
						_activityLog.Warning("Mapped theme '" + themeId + "' for " + (isDebugging ? "debugging" : "configuration '" + (configurationName ?? "<none>") + "'") + " was not found.");
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
				_activityLog.Error("Failed to apply theme for active configuration '" + (configurationName ?? "<none>") + "'" + (isDebugging ? " while debugging" : string.Empty) + ".", ex);
			}
		}

		private string getLastConfigurationName()
		{
			lock (_configurationLock)
			{
				return _lastConfigurationName;
			}
		}

		private async Task seedDefaultMappingsAsync(CancellationToken cancellationToken)
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
			settings.Mappings = [.. settings.Mappings.Where(mapping => !string.IsNullOrWhiteSpace(mapping.ThemeId))];

			if (settings.Mappings.Count > 0)
			{
				await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
