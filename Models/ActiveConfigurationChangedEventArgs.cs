using System;

namespace ConfigurationThemeSwitcher.Models
{
	public sealed class ActiveConfigurationChangedEventArgs : EventArgs
	{
		public ActiveConfigurationChangedEventArgs(string configurationName)
		{
			ConfigurationName = configurationName;
		}

		public string ConfigurationName { get; private set; }
	}
}
