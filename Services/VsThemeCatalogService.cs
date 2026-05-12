using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Models;

using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class VsThemeCatalogService : IThemeCatalogService
	{
		private static readonly IReadOnlyList<ThemeInfo> _documentedFallbackThemes =
		[
	            // Fallback GUIDs are used only when IVsColorThemeService cannot enumerate themes.
	            // TODO: Re-verify these identifiers against the current VS 2026 theme service:
	            // https://learn.microsoft.com/visualstudio/extensibility/migration/modernize-theme-colors
	            new ThemeInfo("1ded0138-47ce-435e-84ef-9ec1f439b749", "Dark", true),
			new ThemeInfo("de3dbbcd-f642-433c-8353-8f1df4370aba", "Light", true),
			new ThemeInfo("a4d6a176-b948-4b29-8c66-53c97a1ed7d0", "Blue", true)
		];

		private readonly AsyncPackage _package;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly IActivityLogService _activityLog;

		public VsThemeCatalogService(AsyncPackage package, JoinableTaskFactory joinableTaskFactory, IActivityLogService activityLog)
		{
			_package = package;
			_joinableTaskFactory = joinableTaskFactory;
			_activityLog = activityLog;
		}

		public async Task<IReadOnlyList<ThemeInfo>> GetAvailableThemesAsync(CancellationToken cancellationToken)
		{
			try
			{
				await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

				var themeService = await GetColorThemeServiceAsync().ConfigureAwait(true);
				if (themeService == null || themeService.Themes == null)
				{
					_activityLog.Warning("Visual Studio color theme service is unavailable; using documented fallback theme list.");
					return _documentedFallbackThemes;
				}

				var themes = new List<ThemeInfo>();
				var themeCollection = themeService.Themes;
				for (var i = 0; i < themeCollection.Count; i++)
				{
					var theme = themeCollection[i];
					if (theme == null || !theme.IsUserVisible)
					{
						continue;
					}

					themes.Add(new ThemeInfo(theme.ThemeId.ToString("D"), theme.Name, true));
				}

				if (themes.Count == 0)
				{
					_activityLog.Warning("Visual Studio returned no user-visible themes; using documented fallback theme list.");
					return _documentedFallbackThemes;
				}

				return themes;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_activityLog.Warning("Unable to enumerate Visual Studio themes; using documented fallback theme list. " + ex.Message);
				return _documentedFallbackThemes;
			}
		}

		public async Task<ThemeInfo> FindThemeAsync(string themeIdOrName, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(themeIdOrName))
			{
				return null;
			}

			var themes = await GetAvailableThemesAsync(cancellationToken).ConfigureAwait(false);
			var requested = themeIdOrName.Trim();

			return themes.FirstOrDefault(theme =>
				string.Equals(theme.Id, requested, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(theme.DisplayName, requested, StringComparison.OrdinalIgnoreCase));
		}

		internal async Task<IVsColorThemeService> GetColorThemeServiceAsync()
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync();
			return await _package.GetServiceAsync(typeof(SVsColorThemeService)).ConfigureAwait(true) as IVsColorThemeService;
		}
	}
}
