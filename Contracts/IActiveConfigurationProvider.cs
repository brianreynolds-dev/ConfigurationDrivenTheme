using System.Threading;
using System.Threading.Tasks;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IActiveConfigurationProvider
	{
		Task<string> GetActiveConfigurationNameAsync(CancellationToken cancellationToken);
	}
}
