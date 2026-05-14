using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using ConfigurationThemeSwitcher.Contracts;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class VsInstanceActivationMonitor : IInstanceActivationMonitor
	{
		private const int WM_ACTIVATEAPP = 0x001C;

		private readonly AsyncPackage _package;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly IActivityLogService _activityLog;
		private MainWindowMessageSink _messageSink;
		private bool _isActive;
		private bool _started;

		public VsInstanceActivationMonitor(
			AsyncPackage package,
			JoinableTaskFactory joinableTaskFactory,
			IActivityLogService activityLog)
		{
			_package = package;
			_joinableTaskFactory = joinableTaskFactory;
			_activityLog = activityLog;
		}

		public event EventHandler Activated;

		public event EventHandler Deactivated;

		public bool IsActive
		{
			get { return _isActive; }
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			if (_started)
			{
				return;
			}

			_started = true;

			if (await _package.GetServiceAsync(typeof(SVsUIShell)).ConfigureAwait(true) is not IVsUIShell uiShell)
			{
				_activityLog.Warning("SVsUIShell is unavailable; instance activation changes may not be detected.");
				return;
			}

			var hr = uiShell.GetDialogOwnerHwnd(out IntPtr mainWindowHandle);
			if (ErrorHandler.Failed(hr) || mainWindowHandle == IntPtr.Zero)
			{
				_activityLog.Warning("Unable to resolve the Visual Studio main window handle. HRESULT: 0x" + hr.ToString("X8"));
				return;
			}

			_messageSink = new MainWindowMessageSink(mainWindowHandle, setActive);
			_isActive = isWindowActive(mainWindowHandle);
		}

		public void Dispose()
		{
			_joinableTaskFactory.RunAsync(async delegate
			{
				await _joinableTaskFactory.SwitchToMainThreadAsync();
				_messageSink?.Dispose();
				_messageSink = null;
			}).Task.Forget();
		}

		private void setActive(bool isActive)
		{
			if (_isActive == isActive)
			{
				return;
			}

			_isActive = isActive;
			if (isActive)
			{
				Activated?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				Deactivated?.Invoke(this, EventArgs.Empty);
			}
		}

		private static bool isWindowActive(IntPtr mainWindowHandle)
		{
			var foregroundWindow = GetForegroundWindow();
			return foregroundWindow == mainWindowHandle || IsChild(mainWindowHandle, foregroundWindow);
		}

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

		private sealed class MainWindowMessageSink : NativeWindow, IDisposable
		{
			private readonly Action<bool> _onActivationChanged;

			public MainWindowMessageSink(IntPtr handle, Action<bool> onActivationChanged)
			{
				_onActivationChanged = onActivationChanged;
				AssignHandle(handle);
			}

			public void Dispose()
			{
				ReleaseHandle();
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == WM_ACTIVATEAPP)
				{
					_onActivationChanged(m.WParam != IntPtr.Zero);
				}

				base.WndProc(ref m);
			}
		}
	}
}
