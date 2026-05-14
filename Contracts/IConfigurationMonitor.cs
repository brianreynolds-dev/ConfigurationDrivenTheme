using ConfigurationThemeSwitcher.Models;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IConfigurationMonitor : IDisposable
	{
		event EventHandler<ActiveConfigurationChangedEventArgs> ActiveConfigurationChanged;

		event EventHandler SolutionClosed;

		Task StartAsync(CancellationToken cancellationToken);

		Task RefreshAsync(CancellationToken cancellationToken);
	}
}
