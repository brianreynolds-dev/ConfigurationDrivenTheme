using ConfigurationThemeSwitcher.Models;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IThemeCatalogService
	{
		Task<IReadOnlyList<ThemeInfo>> GetAvailableThemesAsync(CancellationToken cancellationToken);

		Task<ThemeInfo> FindThemeAsync(string themeIdOrName, CancellationToken cancellationToken);
	}
}
