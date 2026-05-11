using ConfigurationThemeSwitcher.Models;

using System.Threading;
using System.Threading.Tasks;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IThemeApplicationService
	{
		Task<ThemeInfo> CaptureCurrentThemeAsync(CancellationToken cancellationToken);

		Task CaptureFallbackThemeAsync(CancellationToken cancellationToken);

		Task ApplyThemeAsync(string themeIdOrName, CancellationToken cancellationToken);

		Task RestoreFallbackThemeAsync(CancellationToken cancellationToken);
	}
}
