using System;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Models;

using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class VsThemeApplicationService : IThemeApplicationService
	{
		private readonly AsyncPackage _package;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly VsThemeCatalogService _themeCatalog;
		private readonly ISettingsService _settingsService;
		private readonly IActivityLogService _activityLog;
		private string _capturedFallbackThemeId;

		public VsThemeApplicationService(
			AsyncPackage package,
			JoinableTaskFactory joinableTaskFactory,
			IThemeCatalogService themeCatalog,
			ISettingsService settingsService,
			IActivityLogService activityLog)
		{
			_package = package;
			_joinableTaskFactory = joinableTaskFactory;
			_themeCatalog = themeCatalog as VsThemeCatalogService;
			_settingsService = settingsService;
			_activityLog = activityLog;
		}

		public async Task<ThemeInfo> CaptureCurrentThemeAsync(CancellationToken cancellationToken)
		{
			try
			{
				await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
				var themeService = await getColorThemeServiceAsync().ConfigureAwait(true);
				var currentTheme = themeService?.CurrentTheme;
				if (currentTheme == null)
				{
					return null;
				}

				return new ThemeInfo(currentTheme.ThemeId.ToString("D"), currentTheme.Name, true);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_activityLog.Warning("Unable to capture the current Visual Studio theme: " + ex.Message);
				return null;
			}
		}

		public async Task CaptureFallbackThemeAsync(CancellationToken cancellationToken)
		{
			var currentTheme = await CaptureCurrentThemeAsync(cancellationToken).ConfigureAwait(false);
			if (currentTheme == null || string.IsNullOrWhiteSpace(currentTheme.DisplayName))
			{
				_activityLog.Warning("No fallback theme was captured because the current Visual Studio theme could not be resolved.");
				return;
			}

			_capturedFallbackThemeId = currentTheme.DisplayName;
			var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
			if (string.IsNullOrWhiteSpace(settings.FallbackThemeId))
			{
				settings.FallbackThemeId = currentTheme.DisplayName;
				await _settingsService.SaveSettingsAsync(settings, cancellationToken).ConfigureAwait(false);
			}
		}

		public async Task ApplyThemeAsync(string themeIdOrName, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(themeIdOrName))
			{
				return;
			}

			try
			{
				await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

				var themeService = await getColorThemeServiceAsync().ConfigureAwait(true);
				if (themeService == null || themeService.Themes == null)
				{
					_activityLog.Warning("Cannot apply theme because Visual Studio color theme service is unavailable.");
					return;
				}

				var targetTheme = resolveTheme(themeService, themeIdOrName);
				if (targetTheme == null)
				{
					_activityLog.Warning("Theme '" + themeIdOrName + "' was not found. No theme was applied.");
					return;
				}

				var currentTheme = themeService.CurrentTheme;
				if (currentTheme != null && currentTheme.ThemeId == targetTheme.ThemeId)
				{
					return;
				}

				targetTheme.Apply();
				_activityLog.Info("Applied Visual Studio theme '" + targetTheme.Name + "' for active solution configuration.");
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_activityLog.Error("Failed to apply Visual Studio theme '" + themeIdOrName + "'.", ex);
			}
		}

		public async Task RestoreFallbackThemeAsync(CancellationToken cancellationToken)
		{
			var settings = await _settingsService.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
			var fallbackTheme = string.IsNullOrWhiteSpace(settings.FallbackThemeId) ? _capturedFallbackThemeId : settings.FallbackThemeId;
			if (string.IsNullOrWhiteSpace(fallbackTheme))
			{
				_activityLog.Warning("Fallback theme restore was requested, but no fallback theme is available.");
				return;
			}

			await ApplyThemeAsync(fallbackTheme, cancellationToken).ConfigureAwait(false);
		}

		private async Task<IVsColorThemeService> getColorThemeServiceAsync()
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync();
			if (_themeCatalog != null)
			{
				return await _themeCatalog.GetColorThemeServiceAsync().ConfigureAwait(true);
			}

			return await _package.GetServiceAsync(typeof(SVsColorThemeService)).ConfigureAwait(true) as IVsColorThemeService;
		}

		private static IVsColorTheme resolveTheme(IVsColorThemeService themeService, string themeIdOrName)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (Guid.TryParse(themeIdOrName, out Guid themeId))
			{
				try
				{
					var byId = themeService.Themes.GetThemeFromId(themeId);
					if (byId != null)
					{
						return byId;
					}
				}
				catch
				{
				}
			}

			for (var i = 0; i < themeService.Themes.Count; i++)
			{
				var theme = themeService.Themes[i];
				if (theme == null)
				{
					continue;
				}

				if (string.Equals(theme.ThemeId.ToString("D"), themeIdOrName, StringComparison.OrdinalIgnoreCase) ||
					string.Equals(theme.Name, themeIdOrName, StringComparison.OrdinalIgnoreCase))
				{
					return theme;
				}
			}

			return null;
		}
	}
}
