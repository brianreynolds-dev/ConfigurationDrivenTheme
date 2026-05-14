using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IInstanceActivationMonitor : IDisposable
	{
		event EventHandler Activated;

		event EventHandler Deactivated;

		bool IsActive { get; }

		Task StartAsync(CancellationToken cancellationToken);
	}
}
