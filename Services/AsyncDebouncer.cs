using System;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class AsyncDebouncer : IDisposable
	{
		private readonly object _gate = new();
		private readonly IActivityLogService _activityLog;
		private CancellationTokenSource _pendingCancellation;
		private Task _pendingTask = Task.CompletedTask;
		private bool _disposed;

		public AsyncDebouncer(IActivityLogService activityLog)
		{
			_activityLog = activityLog;
		}

		public void Schedule(int delayMilliseconds, Func<CancellationToken, Task> action)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}

			CancellationTokenSource previous = null;
			CancellationTokenSource current;

			lock (_gate)
			{
				if (_disposed)
				{
					return;
				}

				previous = _pendingCancellation;
				current = new CancellationTokenSource();
				_pendingCancellation = current;
				_pendingTask = runAsync(Math.Max(0, delayMilliseconds), action, current.Token);
			}

			if (previous != null)
			{
				previous.Cancel();
				previous.Dispose();
			}
		}

		public Task WhenIdleAsync()
		{
			lock (_gate)
			{
				return _pendingTask ?? Task.CompletedTask;
			}
		}

		public void Dispose()
		{
			CancellationTokenSource pending = null;

			lock (_gate)
			{
				if (_disposed)
				{
					return;
				}

				_disposed = true;
				pending = _pendingCancellation;
				_pendingCancellation = null;
			}

			if (pending != null)
			{
				pending.Cancel();
				pending.Dispose();
			}
		}

		private async Task runAsync(int delayMilliseconds, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
		{
			try
			{
				await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
				cancellationToken.ThrowIfCancellationRequested();
				await action(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				_activityLog?.Error("Debounced configuration operation failed.", ex);
			}
		}
	}
}
