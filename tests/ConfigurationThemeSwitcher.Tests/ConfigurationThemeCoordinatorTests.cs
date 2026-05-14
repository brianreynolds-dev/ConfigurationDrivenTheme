using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConfigurationThemeSwitcher.Contracts;
using ConfigurationThemeSwitcher.Models;
using ConfigurationThemeSwitcher.Services;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Threading;

namespace ConfigurationThemeSwitcher.Tests
{
	[TestClass]
	public sealed class ConfigurationThemeCoordinatorTests
	{
		[TestMethod]
		public async Task ActiveConfigurationChanged_AppliesThemeWhenInstanceIsActive()
		{
			var testContext = createCoordinator(isActive: true);
			await testContext.Coordinator.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

			testContext.ConfigurationMonitor.RaiseConfigurationChanged("Release");
			var appliedTheme = await testContext.ThemeApplication.WaitForAppliedThemeAsync().ConfigureAwait(false);

			Assert.AreEqual("Light", appliedTheme);
		}

		[TestMethod]
		public async Task ActiveConfigurationChanged_DoesNotApplyThemeWhenInstanceIsInactive()
		{
			var testContext = createCoordinator(isActive: false);
			await testContext.Coordinator.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

			testContext.ConfigurationMonitor.RaiseConfigurationChanged("Release");
			await Task.Delay(100).ConfigureAwait(false);

			Assert.AreEqual(0, testContext.ThemeApplication.AppliedThemes.Count);
		}

		[TestMethod]
		public async Task Activated_RefreshesConfigurationAndAppliesTheme()
		{
			var testContext = createCoordinator(isActive: false);
			testContext.ConfigurationMonitor.CurrentConfigurationName = "Release";
			await testContext.Coordinator.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

			testContext.ActivationMonitor.Activate();
			var appliedTheme = await testContext.ThemeApplication.WaitForAppliedThemeAsync().ConfigureAwait(false);

			Assert.AreEqual("Light", appliedTheme);
			Assert.AreEqual(1, testContext.ConfigurationMonitor.RefreshCount);
		}

		[TestMethod]
		public async Task SolutionClosed_DoesNotRestoreFallbackWhenInstanceIsInactive()
		{
			var testContext = createCoordinator(isActive: false);
			await testContext.Coordinator.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

			testContext.ConfigurationMonitor.RaiseSolutionClosed();
			await Task.Delay(100).ConfigureAwait(false);

			Assert.AreEqual(0, testContext.ThemeApplication.RestoreFallbackCount);
		}

		[TestMethod]
		public async Task SolutionClosed_RestoresFallbackWhenInstanceIsActive()
		{
			var testContext = createCoordinator(isActive: true);
			await testContext.Coordinator.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

			testContext.ConfigurationMonitor.RaiseSolutionClosed();
			await testContext.ThemeApplication.WaitForFallbackRestoreAsync().ConfigureAwait(false);

			Assert.AreEqual(1, testContext.ThemeApplication.RestoreFallbackCount);
		}

		private static CoordinatorTestContext createCoordinator(bool isActive)
		{
			#pragma warning disable VSSDK005 // Unit tests run outside the VS shell and need an isolated context.
			var joinableTaskContext = new JoinableTaskContext();
			#pragma warning restore VSSDK005
			var settings = new ExtensionSettings
			{
				IsEnabled = true,
				RestoreFallbackThemeWhenUnmapped = true,
				Mappings =
				[
					new ThemeMapping("Debug", "Dark"),
					new ThemeMapping("Release", "Light")
				]
			};

			var settingsService = new TestSettingsService(settings);
			var mappingService = new ThemeMappingService();
			var themeCatalog = new TestThemeCatalogService();
			var themeApplication = new TestThemeApplicationService();
			var configurationMonitor = new TestConfigurationMonitor();
			var activationMonitor = new TestInstanceActivationMonitor(isActive);
			var activityLog = new TestActivityLogService();
			var coordinator = new ConfigurationThemeCoordinator(
				settingsService,
				mappingService,
				themeCatalog,
				themeApplication,
				configurationMonitor,
				activationMonitor,
				joinableTaskContext.Factory,
				activityLog);

			return new CoordinatorTestContext(
				coordinator,
				configurationMonitor,
				activationMonitor,
				themeApplication);
		}

		private sealed class CoordinatorTestContext
		{
			public CoordinatorTestContext(
				ConfigurationThemeCoordinator coordinator,
				TestConfigurationMonitor configurationMonitor,
				TestInstanceActivationMonitor activationMonitor,
				TestThemeApplicationService themeApplication)
			{
				Coordinator = coordinator;
				ConfigurationMonitor = configurationMonitor;
				ActivationMonitor = activationMonitor;
				ThemeApplication = themeApplication;
			}

			public ConfigurationThemeCoordinator Coordinator { get; }

			public TestConfigurationMonitor ConfigurationMonitor { get; }

			public TestInstanceActivationMonitor ActivationMonitor { get; }

			public TestThemeApplicationService ThemeApplication { get; }
		}

		private sealed class TestConfigurationMonitor : IConfigurationMonitor
		{
			public event EventHandler<ActiveConfigurationChangedEventArgs> ActiveConfigurationChanged;

			public event EventHandler SolutionClosed;

			public string CurrentConfigurationName { get; set; } = "Debug";

			public int RefreshCount { get; private set; }

			public Task StartAsync(CancellationToken cancellationToken)
			{
				return Task.CompletedTask;
			}

			public Task RefreshAsync(CancellationToken cancellationToken)
			{
				RefreshCount++;
				RaiseConfigurationChanged(CurrentConfigurationName);
				return Task.CompletedTask;
			}

			public void Dispose()
			{
			}

			public void RaiseConfigurationChanged(string configurationName)
			{
				CurrentConfigurationName = configurationName;
				ActiveConfigurationChanged?.Invoke(this, new ActiveConfigurationChangedEventArgs(configurationName));
			}

			public void RaiseSolutionClosed()
			{
				SolutionClosed?.Invoke(this, EventArgs.Empty);
			}
		}

		private sealed class TestInstanceActivationMonitor : IInstanceActivationMonitor
		{
			public TestInstanceActivationMonitor(bool isActive)
			{
				IsActive = isActive;
			}

			public event EventHandler Activated;

			public event EventHandler Deactivated;

			public bool IsActive { get; private set; }

			public Task StartAsync(CancellationToken cancellationToken)
			{
				return Task.CompletedTask;
			}

			public void Dispose()
			{
			}

			public void Activate()
			{
				IsActive = true;
				Activated?.Invoke(this, EventArgs.Empty);
			}

			public void Deactivate()
			{
				IsActive = false;
				Deactivated?.Invoke(this, EventArgs.Empty);
			}
		}

		private sealed class TestSettingsService : ISettingsService
		{
			private readonly ExtensionSettings _settings;

			public TestSettingsService(ExtensionSettings settings)
			{
				_settings = settings;
			}

			public Task<ExtensionSettings> GetSettingsAsync(CancellationToken cancellationToken)
			{
				return Task.FromResult(_settings);
			}

			public Task SaveSettingsAsync(ExtensionSettings settings, CancellationToken cancellationToken)
			{
				return Task.CompletedTask;
			}
		}

		private sealed class TestThemeCatalogService : IThemeCatalogService
		{
			public Task<IReadOnlyList<ThemeInfo>> GetAvailableThemesAsync(CancellationToken cancellationToken)
			{
				IReadOnlyList<ThemeInfo> themes =
				[
					new ThemeInfo("Dark", "Dark", true),
					new ThemeInfo("Light", "Light", true)
				];
				return Task.FromResult(themes);
			}

			public Task<ThemeInfo> FindThemeAsync(string themeIdOrName, CancellationToken cancellationToken)
			{
				return Task.FromResult(new ThemeInfo(themeIdOrName, themeIdOrName, true));
			}
		}

		private sealed class TestThemeApplicationService : IThemeApplicationService
		{
			private TaskCompletionSource<string> _appliedTheme = createAppliedThemeSource();
			private TaskCompletionSource<object> _fallbackRestore = createFallbackRestoreSource();

			public List<string> AppliedThemes { get; } = [];

			public int RestoreFallbackCount { get; private set; }

			public Task<ThemeInfo> CaptureCurrentThemeAsync(CancellationToken cancellationToken)
			{
				return Task.FromResult(new ThemeInfo("Dark", "Dark", true));
			}

			public Task CaptureFallbackThemeAsync(CancellationToken cancellationToken)
			{
				return Task.CompletedTask;
			}

			public Task ApplyThemeAsync(string themeIdOrName, CancellationToken cancellationToken)
			{
				AppliedThemes.Add(themeIdOrName);
				_appliedTheme.TrySetResult(themeIdOrName);
				return Task.CompletedTask;
			}

			public Task RestoreFallbackThemeAsync(CancellationToken cancellationToken)
			{
				RestoreFallbackCount++;
				_fallbackRestore.TrySetResult(null);
				return Task.CompletedTask;
			}

			public async Task<string> WaitForAppliedThemeAsync()
			{
				#pragma warning disable VSTHRD003 // Test synchronization waits on local TaskCompletionSource instances.
				var completedTask = await Task.WhenAny(_appliedTheme.Task, Task.Delay(2000)).ConfigureAwait(false);
				if (completedTask != _appliedTheme.Task)
				{
					throw new TimeoutException("Timed out waiting for a theme to be applied.");
				}

				return await _appliedTheme.Task.ConfigureAwait(false);
				#pragma warning restore VSTHRD003
			}

			public async Task WaitForFallbackRestoreAsync()
			{
				#pragma warning disable VSTHRD003 // Test synchronization waits on local TaskCompletionSource instances.
				var completedTask = await Task.WhenAny(_fallbackRestore.Task, Task.Delay(2000)).ConfigureAwait(false);
				if (completedTask != _fallbackRestore.Task)
				{
					throw new TimeoutException("Timed out waiting for fallback restore.");
				}

				await _fallbackRestore.Task.ConfigureAwait(false);
				#pragma warning restore VSTHRD003
			}

			private static TaskCompletionSource<string> createAppliedThemeSource()
			{
				return new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
			}

			private static TaskCompletionSource<object> createFallbackRestoreSource()
			{
				return new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
			}
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
