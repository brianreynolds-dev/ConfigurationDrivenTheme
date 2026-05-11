using System;

namespace ConfigurationThemeSwitcher.Contracts
{
	public interface IActivityLogService
	{
		void Info(string message);

		void Warning(string message);

		void Error(string message, Exception exception = null);
	}
}
