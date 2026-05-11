using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;

using ConfigurationThemeSwitcher.Models;
using ConfigurationThemeSwitcher.Services;

using Microsoft.VisualStudio.Shell;

namespace ConfigurationThemeSwitcher.Options
{
	internal sealed class ThemeMappingsEditor : UITypeEditor
	{
		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}

		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			var editorService = provider == null ? null : provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
			var currentValue = value as string ?? string.Empty;

			using (var dialog = new ThemeMappingsDialog(currentValue))
			{
				var result = editorService == null ? dialog.ShowDialog() : editorService.ShowDialog(dialog);
				return result == DialogResult.OK ? dialog.MappingText : value;
			}
		}

		private sealed class ThemeMappingsDialog : Form
		{
			private readonly DataGridView _grid;
			private readonly List<ThemeInfo> _themes;
			private readonly Button _addButton;
			private readonly Button _removeButton;
			private readonly Button _defaultsButton;

			public ThemeMappingsDialog(string mappingText)
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				Text = "Configuration Theme Mappings";
				StartPosition = FormStartPosition.CenterParent;
				MinimizeBox = false;
				MaximizeBox = false;
				ShowIcon = false;
				ShowInTaskbar = false;
				Width = 720;
				Height = 420;
				MinimumSize = new Size(560, 320);

				_themes = ThemeDisplayNameResolver.GetThemes().OrderBy(theme => theme.DisplayName).ToList();

				_grid = CreateGrid(mappingText);
				_addButton = new Button { Text = "Add", AutoSize = true };
				_removeButton = new Button { Text = "Remove", AutoSize = true };
				_defaultsButton = new Button { Text = "Reset defaults", AutoSize = true };
				var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
				var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };

				_addButton.Click += delegate { _grid.Rows.Add("Debug", FirstThemeName()); };
				_removeButton.Click += delegate { RemoveSelectedRows(); };
				_defaultsButton.Click += delegate { ResetDefaults(); };

				AcceptButton = okButton;
				CancelButton = cancelButton;

				var buttonPanel = new FlowLayoutPanel
				{
					Dock = DockStyle.Bottom,
					FlowDirection = FlowDirection.RightToLeft,
					Height = 42,
					Padding = new Padding(8)
				};
				buttonPanel.Controls.Add(cancelButton);
				buttonPanel.Controls.Add(okButton);
				buttonPanel.Controls.Add(_removeButton);
				buttonPanel.Controls.Add(_addButton);
				buttonPanel.Controls.Add(_defaultsButton);

				Controls.Add(_grid);
				Controls.Add(buttonPanel);
			}

			public string MappingText
			{
				get
				{
					var mappings = new List<ThemeMapping>();
					foreach (DataGridViewRow row in _grid.Rows)
					{
						if (row.IsNewRow)
						{
							continue;
						}

						var configurationName = Convert.ToString(row.Cells[0].Value).Trim();
						var themeName = Convert.ToString(row.Cells[1].Value).Trim();
						if (configurationName.Length == 0 || themeName.Length == 0)
						{
							continue;
						}

						mappings.Add(new ThemeMapping(configurationName, themeName));
					}

					return MappingLineParser.Format(mappings);
				}
			}

			protected override void OnFormClosing(FormClosingEventArgs e)
			{
				if (DialogResult == DialogResult.OK)
				{
					var errors = ValidateRows();
					if (errors.Any())
					{
						MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Invalid mappings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						e.Cancel = true;
					}
				}

				base.OnFormClosing(e);
			}

			private DataGridView CreateGrid(string mappingText)
			{
				ThreadHelper.ThrowIfNotOnUIThread();

				var grid = new DataGridView
				{
					AllowUserToAddRows = false,
					AllowUserToDeleteRows = true,
					AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
					BackgroundColor = SystemColors.Window,
					BorderStyle = BorderStyle.FixedSingle,
					Dock = DockStyle.Fill,
					EditMode = DataGridViewEditMode.EditOnEnter,
					MultiSelect = true,
					RowHeadersVisible = false,
					SelectionMode = DataGridViewSelectionMode.FullRowSelect
				};

				grid.Columns.Add(new DataGridViewTextBoxColumn
				{
					HeaderText = "Configuration",
					FillWeight = 45,
					SortMode = DataGridViewColumnSortMode.NotSortable
				});

				var themeColumn = new DataGridViewComboBoxColumn
				{
					HeaderText = "Theme",
					FillWeight = 55,
					FlatStyle = FlatStyle.System,
					SortMode = DataGridViewColumnSortMode.NotSortable
				};

				foreach (var theme in _themes)
				{
					themeColumn.Items.Add(theme.DisplayName);
				}

				grid.Columns.Add(themeColumn);

				var errors = new List<string>();
				var mappings = MappingLineParser.Parse(mappingText, errors);
				foreach (var mapping in mappings)
				{
					var themeName = ThemeDisplayNameResolver.ToDisplayName(mapping.ThemeId);
					if (!themeColumn.Items.Contains(themeName))
					{
						themeColumn.Items.Add(themeName);
					}

					grid.Rows.Add(mapping.ConfigurationName, themeName);
				}

				if (grid.Rows.Count == 0)
				{
					ResetDefaults(grid);
				}

				return grid;
			}

			private void RemoveSelectedRows()
			{
				foreach (DataGridViewRow row in _grid.SelectedRows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow).ToArray())
				{
					_grid.Rows.Remove(row);
				}
			}

			private void ResetDefaults()
			{
				ResetDefaults(_grid);
			}

			private void ResetDefaults(DataGridView grid)
			{
				grid.Rows.Clear();
				var darkTheme = _themes.FirstOrDefault(theme => theme.DisplayName.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0);
				var lightTheme = _themes.FirstOrDefault(theme => theme.DisplayName.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0);

				if (darkTheme != null)
				{
					grid.Rows.Add("Debug", darkTheme.DisplayName);
				}

				if (lightTheme != null)
				{
					grid.Rows.Add("Release", lightTheme.DisplayName);
				}
			}

			private string FirstThemeName()
			{
				return _themes.Count == 0 ? string.Empty : _themes[0].DisplayName;
			}

			private List<string> ValidateRows()
			{
				var errors = new List<string>();
				var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (DataGridViewRow row in _grid.Rows)
				{
					if (row.IsNewRow)
					{
						continue;
					}

					var configurationName = Convert.ToString(row.Cells[0].Value).Trim();
					var themeName = Convert.ToString(row.Cells[1].Value).Trim();
					if (configurationName.Length == 0 && themeName.Length == 0)
					{
						continue;
					}

					if (configurationName.Length == 0)
					{
						errors.Add("A mapping row is missing a configuration name.");
					}

					if (themeName.Length == 0)
					{
						errors.Add("Configuration '" + configurationName + "' is missing a theme.");
					}

					if (configurationName.Length > 0 && !seen.Add(configurationName))
					{
						errors.Add("Duplicate configuration mapping: " + configurationName + ".");
					}
				}

				return errors;
			}
		}
	}
}
