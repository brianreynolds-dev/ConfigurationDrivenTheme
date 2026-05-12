using System;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;

using EnvDTE;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class DteActiveConfigurationProvider : IActiveConfigurationProvider
	{
		private readonly AsyncPackage _package;
		private readonly JoinableTaskFactory _joinableTaskFactory;
		private readonly IActivityLogService _activityLog;

		public DteActiveConfigurationProvider(AsyncPackage package, JoinableTaskFactory joinableTaskFactory, IActivityLogService activityLog)
		{
			_package = package;
			_joinableTaskFactory = joinableTaskFactory;
			_activityLog = activityLog;
		}

		public async Task<string> GetActiveConfigurationNameAsync(CancellationToken cancellationToken)
		{
			try
			{
				await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

				if (await _package.GetServiceAsync(typeof(DTE)).ConfigureAwait(true) is not DTE dte || dte.Solution == null || !dte.Solution.IsOpen)
				{
					return null;
				}

				var solutionBuild = dte.Solution.SolutionBuild;
				if (solutionBuild == null || solutionBuild.ActiveConfiguration == null)
				{
					return null;
				}

				return solutionBuild.ActiveConfiguration.Name;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_activityLog.Warning("Unable to resolve the active solution configuration through DTE: " + ex.Message);
				return null;
			}
		}
	}
}
