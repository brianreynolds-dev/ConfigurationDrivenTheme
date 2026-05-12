using System;

using ConfigurationThemeSwitcher.Contracts;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;

namespace ConfigurationThemeSwitcher.Services
{
	public sealed class ActivityLogService : IActivityLogService
	{
		private const string Source = "Configuration Theme Switcher";
		private readonly AsyncPackage _package;

		public ActivityLogService(AsyncPackage package)
		{
			_package = package;
		}

		public void Info(string message)
		{
			log(__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, message);
		}

		public void Warning(string message)
		{
			log(__ACTIVITYLOG_ENTRYTYPE.ALE_WARNING, message);
		}

		public void Error(string message, Exception exception = null)
		{
			var text = exception == null ? message : message + Environment.NewLine + exception;
			log(__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR, text);
		}

		private void log(__ACTIVITYLOG_ENTRYTYPE entryType, string message)
		{
			try
			{
				Task.Run(async delegate
				{
					try
					{
						if (await _package.GetServiceAsync(typeof(SVsActivityLog)).ConfigureAwait(false) is IVsActivityLog log)
						{
							ErrorHandler.ThrowOnFailure(log.LogEntry((uint)entryType, Source, message ?? string.Empty));
						}
					}
					catch
					{
						// ActivityLog failures must never affect Visual Studio or the extension.
					}
				}).Forget();
			}
			catch
			{
			}
		}
	}
}
