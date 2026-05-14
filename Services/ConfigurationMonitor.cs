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
	public sealed class ConfigurationMonitor : IConfigurationMonitor, IVsUpdateSolutionEvents2, IVsSolutionEvents
	{
		private readonly AsyncPackage _package;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly ISettingsService _settingsService;
		private readonly IActiveConfigurationProvider _activeConfigurationProvider;
		private readonly AsyncDebouncer _debouncer;
		private readonly IActivityLogService _activityLog;
		private IVsSolutionBuildManager2 _solutionBuildManager;
		private IVsSolution _solution;
		private uint _buildEventsCookie;
		private uint _solutionEventsCookie;
		private bool _started;

		public ConfigurationMonitor(
			AsyncPackage package,
			JoinableTaskFactory joinableTaskFactory,
			ISettingsService settingsService,
			IActiveConfigurationProvider activeConfigurationProvider,
			AsyncDebouncer debouncer,
			IActivityLogService activityLog)
		{
			_package = package;
			_joinableTaskFactory = joinableTaskFactory;
			_settingsService = settingsService;
			_activeConfigurationProvider = activeConfigurationProvider;
			_debouncer = debouncer;
			_activityLog = activityLog;
		}

		public event EventHandler<ActiveConfigurationChangedEventArgs> ActiveConfigurationChanged;

		public event EventHandler SolutionClosed;

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			if (_started)
			{
				return;
			}

			_started = true;
			_solutionBuildManager = await _package.GetServiceAsync(typeof(SVsSolutionBuildManager)).ConfigureAwait(true) as IVsSolutionBuildManager2;
			if (_solutionBuildManager != null)
			{
				var hr = _solutionBuildManager.AdviseUpdateSolutionEvents(this, out uint cookie);
				if (ErrorHandler.Succeeded(hr))
				{
					_buildEventsCookie = cookie;
				}
				else
				{
					_activityLog.Warning("Unable to subscribe to active project configuration events. HRESULT: 0x" + hr.ToString("X8"));
				}
			}
			else
			{
				_activityLog.Warning("SVsSolutionBuildManager is unavailable; configuration changes may not be detected.");
			}

			_solution = await _package.GetServiceAsync(typeof(SVsSolution)).ConfigureAwait(true) as IVsSolution;
			if (_solution != null)
			{
				var hr = _solution.AdviseSolutionEvents(this, out uint cookie);
				if (ErrorHandler.Succeeded(hr))
				{
					_solutionEventsCookie = cookie;
				}
				else
				{
					_activityLog.Warning("Unable to subscribe to solution open/close events. HRESULT: 0x" + hr.ToString("X8"));
				}
			}
			else
			{
				_activityLog.Warning("SVsSolution is unavailable; solution open/close events may not be detected.");
			}

			scheduleConfigurationEvaluation();
		}

		public async Task RefreshAsync(CancellationToken cancellationToken)
		{
			var configurationName = await _activeConfigurationProvider.GetActiveConfigurationNameAsync(cancellationToken).ConfigureAwait(false);
			ActiveConfigurationChanged?.Invoke(this, new ActiveConfigurationChangedEventArgs(configurationName));
		}

		public void Dispose()
		{
			_joinableTaskFactory.RunAsync(async delegate
			{
				await _joinableTaskFactory.SwitchToMainThreadAsync();

				if (_solutionBuildManager != null && _buildEventsCookie != 0)
				{
					try
					{
						_solutionBuildManager.UnadviseUpdateSolutionEvents(_buildEventsCookie);
					}
					catch
					{
					}
					_buildEventsCookie = 0;
				}

				if (_solution != null && _solutionEventsCookie != 0)
				{
					try
					{
						_solution.UnadviseSolutionEvents(_solutionEventsCookie);
					}
					catch
					{
					}
					_solutionEventsCookie = 0;
				}

				_solutionBuildManager = null;
				_solution = null;
			}).Task.Forget();

			_debouncer.Dispose();
		}

		public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
		{
			// A null hierarchy means the global solution configuration may have changed.
			scheduleConfigurationEvaluation();
			return VSConstants.S_OK;
		}

		public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
		{
			scheduleConfigurationEvaluation();
			return VSConstants.S_OK;
		}

		public int OnAfterCloseSolution(object pUnkReserved)
		{
			SolutionClosed?.Invoke(this, EventArgs.Empty);

			return VSConstants.S_OK;
		}

		public int UpdateSolution_Begin(ref int pfCancelUpdate)
		{
			return VSConstants.S_OK;
		}

		public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
		{
			return VSConstants.S_OK;
		}

		public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
		{
			return VSConstants.S_OK;
		}

		public int UpdateSolution_Cancel()
		{
			return VSConstants.S_OK;
		}

		public int UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
		{
			return VSConstants.S_OK;
		}

		public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
		{
			return VSConstants.S_OK;
		}

		public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
		{
			return VSConstants.S_OK;
		}

		public int OnBeforeCloseSolution(object pUnkReserved)
		{
			return VSConstants.S_OK;
		}

		private void scheduleConfigurationEvaluation()
		{
			_joinableTaskFactory.RunAsync(async delegate
			{
				try
				{
					var settings = await _settingsService.GetSettingsAsync(CancellationToken.None).ConfigureAwait(false);
					var delay = settings == null ? 250 : settings.DebounceMilliseconds;

					_debouncer.Schedule(delay, async cancellationToken =>
					{
						await RefreshAsync(cancellationToken).ConfigureAwait(false);
					});
				}
				catch (Exception ex)
				{
					_activityLog.Error("Failed to schedule active configuration evaluation.", ex);
				}
			}).Task.Forget();
		}
	}
}
