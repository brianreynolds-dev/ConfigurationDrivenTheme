using System;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Models;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IDebuggerStateMonitor : IDisposable
	{
		event EventHandler<DebuggingStateChangedEventArgs> DebuggingStateChanged;

		bool IsDebugging { get; }

		Task StartAsync(CancellationToken cancellationToken);

		Task RefreshAsync(CancellationToken cancellationToken);
	}
}
