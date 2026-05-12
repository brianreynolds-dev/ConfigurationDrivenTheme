using System;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Services;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConfigurationThemeSwitcher.Tests
{
	[TestClass]
	public sealed class AsyncDebouncerTests
	{
		[TestMethod]
		public async Task Schedule_CoalescesRapidChangesIntoFinalApplication()
		{
			var debouncer = new AsyncDebouncer(new TestActivityLogService());
			var applied = 0;
			var finalValue = string.Empty;

			debouncer.Schedule(80, token =>
			{
				Interlocked.Increment(ref applied);
				finalValue = "Debug";
				return Task.CompletedTask;
			});

			debouncer.Schedule(80, token =>
			{
				Interlocked.Increment(ref applied);
				finalValue = "Release";
				return Task.CompletedTask;
			});

			debouncer.Schedule(20, token =>
			{
				Interlocked.Increment(ref applied);
				finalValue = "Benchmark";
				return Task.CompletedTask;
			});

			await debouncer.WhenIdleAsync().ConfigureAwait(false);

			Assert.AreEqual(1, applied);
			Assert.AreEqual("Benchmark", finalValue);
		}

		private sealed class TestActivityLogService : IActivityLogService
		{
			public void Info(string message)
			{
			}

			public void Warning(string message)
			{
			}

			public void Error(string message, Exception exception = null)
			{
			}
		}
	}
}
