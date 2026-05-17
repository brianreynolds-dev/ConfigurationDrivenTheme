using System;

namespace ConfigurationThemeSwitcher.Models
{
	public sealed class DebuggingStateChangedEventArgs : EventArgs
	{
		public DebuggingStateChangedEventArgs(bool isDebugging)
		{
			IsDebugging = isDebugging;
		}

		public bool IsDebugging { get; }
	}
}
