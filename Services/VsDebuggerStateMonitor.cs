using System;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Models;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class VsDebuggerStateMonitor : IDebuggerStateMonitor, IVsDebuggerEvents
	{
		private readonly AsyncPackage _package;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly IActivityLogService _activityLog;
		private IVsDebugger _debugger;
		private uint _debuggerEventsCookie;
		private bool _isDebugging;
		private bool _started;

		public VsDebuggerStateMonitor(AsyncPackage package, JoinableTaskFactory joinableTaskFactory, IActivityLogService activityLog)
		{
			_package = package;
			_joinableTaskFactory = joinableTaskFactory;
			_activityLog = activityLog;
		}

		public event EventHandler<DebuggingStateChangedEventArgs> DebuggingStateChanged;

		public bool IsDebugging => _isDebugging;

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			if (_started)
			{
				return;
			}

			_started = true;
			_debugger = await _package.GetServiceAsync(typeof(SVsShellDebugger)).ConfigureAwait(true) as IVsDebugger;
			if (_debugger == null)
			{
				_activityLog.Warning("SVsShellDebugger is unavailable; debugger mode changes may not be detected.");
				return;
			}

			var hr = _debugger.AdviseDebuggerEvents(this, out uint cookie);
			if (ErrorHandler.Succeeded(hr))
			{
				_debuggerEventsCookie = cookie;
			}
			else
			{
				_activityLog.Warning("Unable to subscribe to debugger mode events. HRESULT: 0x" + hr.ToString("X8"));
			}

			await RefreshAsync(cancellationToken).ConfigureAwait(true);
		}

		public async Task RefreshAsync(CancellationToken cancellationToken)
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			if (_debugger == null)
			{
				return;
			}

			var mode = new DBGMODE[1];
			var hr = _debugger.GetMode(mode);
			if (ErrorHandler.Succeeded(hr))
			{
				updateIsDebugging(isDebuggingMode(mode[0]));
			}
			else
			{
				_activityLog.Warning("Unable to resolve debugger mode. HRESULT: 0x" + hr.ToString("X8"));
			}
		}

		public void Dispose()
		{
			_joinableTaskFactory.RunAsync(async delegate
			{
				await _joinableTaskFactory.SwitchToMainThreadAsync();

				if (_debugger != null && _debuggerEventsCookie != 0)
				{
					try
					{
						_debugger.UnadviseDebuggerEvents(_debuggerEventsCookie);
					}
					catch
					{
					}

					_debuggerEventsCookie = 0;
				}

				_debugger = null;
			}).Task.Forget();
		}

		public int OnModeChange(DBGMODE dbgmodeNew)
		{
			updateIsDebugging(isDebuggingMode(dbgmodeNew));
			return VSConstants.S_OK;
		}

		private static bool isDebuggingMode(DBGMODE mode)
		{
			return mode == DBGMODE.DBGMODE_Run || mode == DBGMODE.DBGMODE_Break;
		}

		private void updateIsDebugging(bool isDebugging)
		{
			if (_isDebugging == isDebugging)
			{
				return;
			}

			_isDebugging = isDebugging;
			DebuggingStateChanged?.Invoke(this, new DebuggingStateChangedEventArgs(isDebugging));
		}
	}
}
