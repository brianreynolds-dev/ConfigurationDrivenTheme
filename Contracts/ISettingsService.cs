using ConfigurationThemeSwitcher.Models;

using System.Threading;
using System.Threading.Tasks;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface ISettingsService
	{
		Task<ExtensionSettings> GetSettingsAsync(CancellationToken cancellationToken);

		Task SaveSettingsAsync(ExtensionSettings settings, CancellationToken cancellationToken);
	}
}
