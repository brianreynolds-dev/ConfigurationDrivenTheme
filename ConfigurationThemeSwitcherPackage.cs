using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;

using ConfigurationThemeSwitcher.Options;
using ConfigurationThemeSwitcher.Services;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

namespace ConfigurationThemeSwitcher
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideOptionPage(typeof(ConfigurationThemeSwitcherOptionsPage), "Configuration Theme Switcher", "General", 0, 0, true)]
	[ProvideProfile(typeof(ConfigurationThemeSwitcherOptionsPage), "Configuration Theme Switcher", "General", 0, 0, true)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuidString)]
	public sealed class ConfigurationThemeSwitcherPackage : AsyncPackage
	{
		public const string PackageGuidString = "380dac0a-dfc5-4d81-8d22-974da3cf34fc";
		public static readonly Guid CommandSetGuid = new Guid("b3fbacdd-c6c7-4f9b-a857-cf4ca3d87b7d");
		public const int OpenOptionsCommandId = 0x0100;

		private ConfigurationThemeCoordinator _coordinator;
		private ConfigurationMonitor _configurationMonitor;

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await base.InitializeAsync(cancellationToken, progress);

			await RegisterCommandsAsync(cancellationToken);

			var activityLog = new ActivityLogService(this);
			var settingsService = new SettingsService(this, JoinableTaskFactory, activityLog);
			var themeCatalog = new VsThemeCatalogService(this, JoinableTaskFactory, activityLog);
			var themeApplication = new VsThemeApplicationService(this, JoinableTaskFactory, themeCatalog, settingsService, activityLog);
			var mappingService = new ThemeMappingService();
			var activeConfigurationProvider = new DteActiveConfigurationProvider(this, JoinableTaskFactory, activityLog);
			var debouncer = new AsyncDebouncer(activityLog);

			_configurationMonitor = new ConfigurationMonitor(
				this,
				JoinableTaskFactory,
				settingsService,
				activeConfigurationProvider,
				debouncer,
				activityLog);

			_coordinator = new ConfigurationThemeCoordinator(
				settingsService,
				mappingService,
				themeCatalog,
				themeApplication,
				_configurationMonitor,
				JoinableTaskFactory,
				activityLog);

			await _coordinator.InitializeAsync(cancellationToken);
		}

		internal void ShowConfigurationThemeSwitcherOptions()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			ShowOptionPage(typeof(ConfigurationThemeSwitcherOptionsPage));
		}

		private async Task RegisterCommandsAsync(CancellationToken cancellationToken)
		{
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			var commandService = await GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(true) as OleMenuCommandService;
			if (commandService == null)
			{
				return;
			}

			var commandId = new CommandID(CommandSetGuid, OpenOptionsCommandId);
			var command = new MenuCommand((sender, args) => ShowConfigurationThemeSwitcherOptions(), commandId);
			commandService.AddCommand(command);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_coordinator != null)
				{
					_coordinator.Dispose();
					_coordinator = null;
				}

				if (_configurationMonitor != null)
				{
					_configurationMonitor.Dispose();
					_configurationMonitor = null;
				}
			}

			base.Dispose(disposing);
		}
	}
}
