//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Microsoft.Data.ConnectionUI
{
	internal partial class DataConnectionSourceDialog : Form
	{
		public DataConnectionSourceDialog()
		{
			InitializeComponent();

			// Make sure we handle a user preference change
			if (this.components == null)
			{
				this.components = new System.ComponentModel.Container();
			}
			this.components.Add(new UserPreferenceChangedHandler(this));
		}

		public DataConnectionSourceDialog(DataConnectionDialog mainDialog)
			: this()
		{
			Debug.Assert(mainDialog != null);

			_mainDialog = mainDialog;
		}

		public string Title
		{
			get
			{
				return Text;
			}
			set
			{
				Text = value;
			}
		}

		public string HeaderLabel
		{
			get
			{
				return (_headerLabel != null) ? _headerLabel.Text : String.Empty;
			}
			set
			{
				if (_headerLabel == null && (value == null || value.Length == 0))
				{
					return;
				}
				if (_headerLabel != null && value == _headerLabel.Text)
				{
					return;
				}
				if (value != null)
				{
					if (_headerLabel == null)
					{
						_headerLabel = new Label();
						_headerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
						_headerLabel.FlatStyle = FlatStyle.System;
						_headerLabel.Location = new Point(12, 12);
						_headerLabel.Margin = new Padding(3);
						_headerLabel.Name = "dataSourceLabel";
						_headerLabel.Width = this.mainTableLayoutPanel.Width;
						_headerLabel.TabIndex = 100;
						this.Controls.Add(_headerLabel);
					}
					_headerLabel.Text = value;
					this.MinimumSize = Size.Empty;
					_headerLabel.Height = LayoutUtils.GetPreferredLabelHeight(_headerLabel);
					int dy =
						_headerLabel.Bottom +
						_headerLabel.Margin.Bottom +
						this.mainTableLayoutPanel.Margin.Top -
						this.mainTableLayoutPanel.Top;
					this.mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
					this.Height += dy;
					this.mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
					this.mainTableLayoutPanel.Top += dy;
					this.MinimumSize = this.Size;
				}
				else
				{
					if (_headerLabel != null)
					{
						int dy = _headerLabel.Height;
						try
						{
							this.Controls.Remove(_headerLabel);
						}
						finally
						{
							_headerLabel.Dispose();
							_headerLabel = null;
						}
						this.MinimumSize = Size.Empty;
						this.mainTableLayoutPanel.Top -= dy;
						this.mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
						this.Height -= dy;
						this.mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
						this.MinimumSize = this.Size;
					}
				}
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			// If a main dialog was associated with this dialog, get its data sources
			if (_mainDialog != null)
			{
				foreach (DataSource dataSource in _mainDialog.DataSources)
				{
					if (dataSource == _mainDialog.UnspecifiedDataSource)
					{
						continue;
					}
					dataSourceListBox.Items.Add(dataSource);
				}
				if (_mainDialog.DataSources.Contains(_mainDialog.UnspecifiedDataSource))
				{
					// We want to put the unspecified data source at the end of the list
					dataSourceListBox.Sorted = false;
					dataSourceListBox.Items.Add(_mainDialog.UnspecifiedDataSource);
				}

				// Figure out the correct width for the data source list box and size dialog
				int dataSourceListBoxWidth = dataSourceListBox.Width - (dataSourceListBox.Width - dataSourceListBox.ClientSize.Width);
				foreach (object item in dataSourceListBox.Items)
				{
					Size size = TextRenderer.MeasureText((item as DataSource).DisplayName, dataSourceListBox.Font);
					size.Width += 3; // otherwise text is crammed up against right edge
					dataSourceListBoxWidth = Math.Max(dataSourceListBoxWidth, size.Width);
				}
				dataSourceListBoxWidth = dataSourceListBoxWidth + (dataSourceListBox.Width - dataSourceListBox.ClientSize.Width);
				dataSourceListBoxWidth = Math.Max(dataSourceListBoxWidth, dataSourceListBox.MinimumSize.Width);
				int dx = dataSourceListBoxWidth - dataSourceListBox.Size.Width;
				this.Width += dx * 2; // * 2 because the description group box resizes as well
				this.MinimumSize = this.Size;

				if (_mainDialog.SelectedDataSource != null)
				{
					dataSourceListBox.SelectedItem = _mainDialog.SelectedDataSource;
					if (_mainDialog.SelectedDataProvider != null)
					{
						dataProviderComboBox.SelectedItem = _mainDialog.SelectedDataProvider;
					}
				}

				// Configure the initial data provider selections
				foreach (DataSource dataSource in dataSourceListBox.Items)
				{
					DataProvider selectedProvider = _mainDialog.GetSelectedDataProvider(dataSource);
					if (selectedProvider != null)
					{
						_providerSelections[dataSource] = selectedProvider;
					}
				}
			}

			// Set the save selection check box
			saveSelectionCheckBox.Checked = _mainDialog.SaveSelection;

			SetOkButtonStatus();

			base.OnLoad(e);
		}

		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);

			dataProviderComboBox.Top =
				leftPanel.Height -
				leftPanel.Padding.Bottom -
				dataProviderComboBox.Margin.Bottom -
				dataProviderComboBox.Height;
			dataProviderLabel.Top =
				dataProviderComboBox.Top -
				dataProviderComboBox.Margin.Top -
				dataProviderLabel.Margin.Bottom -
				dataProviderLabel.Height;

			int dx =
				(saveSelectionCheckBox.Right + saveSelectionCheckBox.Margin.Right) -
				(buttonsTableLayoutPanel.Left - buttonsTableLayoutPanel.Margin.Left);
			if (dx > 0)
			{
				this.Width += dx;
				this.MinimumSize = new Size(this.MinimumSize.Width + dx, this.MinimumSize.Height);
			}
			mainTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
			saveSelectionCheckBox.Anchor &= ~AnchorStyles.Bottom;
			saveSelectionCheckBox.Anchor |= AnchorStyles.Top;
			buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Bottom;
			buttonsTableLayoutPanel.Anchor |= AnchorStyles.Top;
			int height =
				buttonsTableLayoutPanel.Top +
				buttonsTableLayoutPanel.Height +
				buttonsTableLayoutPanel.Margin.Bottom +
				this.Padding.Bottom;
			int dy = this.Height - SizeFromClientSize(new Size(0, height)).Height;
			this.MinimumSize = new Size(this.MinimumSize.Width, this.MinimumSize.Height - dy);
			this.Height -= dy;
			buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Top;
			buttonsTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
			saveSelectionCheckBox.Anchor &= ~AnchorStyles.Top;
			saveSelectionCheckBox.Anchor |= AnchorStyles.Bottom;
			mainTableLayoutPanel.Anchor |= AnchorStyles.Bottom;
		}

		protected override void OnRightToLeftLayoutChanged(EventArgs e)
		{
			base.OnRightToLeftLayoutChanged(e);
			if (RightToLeftLayout == true &&
				RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl(dataSourceLabel, dataSourceListBox);
				LayoutUtils.MirrorControl(dataProviderLabel, dataProviderComboBox);
			}
			else
			{
				LayoutUtils.UnmirrorControl(dataProviderLabel, dataProviderComboBox);
				LayoutUtils.UnmirrorControl(dataSourceLabel, dataSourceListBox);
			}
		}

		protected override void OnRightToLeftChanged(EventArgs e)
		{
			base.OnRightToLeftChanged(e);
			if (RightToLeftLayout == true &&
				RightToLeft == RightToLeft.Yes)
			{
				LayoutUtils.MirrorControl(dataSourceLabel, dataSourceListBox);
				LayoutUtils.MirrorControl(dataProviderLabel, dataProviderComboBox);
			}
			else
			{
				LayoutUtils.UnmirrorControl(dataProviderLabel, dataProviderComboBox);
				LayoutUtils.UnmirrorControl(dataSourceLabel, dataSourceListBox);
			}
		}

		protected override void OnHelpRequested(HelpEventArgs hevent)
		{
			// Get the active control
			Control activeControl = HelpUtils.GetActiveControl(this);

			// Figure out the context
			DataConnectionDialogContext context = DataConnectionDialogContext.Source;
			if (activeControl == dataSourceListBox)
			{
				context = DataConnectionDialogContext.SourceListBox;
			}
			if (activeControl == dataProviderComboBox)
			{
				context = DataConnectionDialogContext.SourceProviderComboBox;
			}
			if (activeControl == okButton)
			{
				context = DataConnectionDialogContext.SourceOkButton;
			}
			if (activeControl == cancelButton)
			{
				context = DataConnectionDialogContext.SourceCancelButton;
			}

			// Call OnContextHelpRequested
			ContextHelpEventArgs e = new ContextHelpEventArgs(context, hevent.MousePos);
			_mainDialog.OnContextHelpRequested(e);
			hevent.Handled = e.Handled;
			if (!e.Handled)
			{
				base.OnHelpRequested(hevent);
			}
		}

		protected override void WndProc(ref Message m)
		{
			if (_mainDialog.TranslateHelpButton && HelpUtils.IsContextHelpMessage(ref m))
			{
				// Force the ? in the title bar to invoke the help topic
				HelpUtils.TranslateContextHelpMessage(this, ref m);
			}
			base.WndProc(ref m);
		}

		private void FormatDataSource(object sender, ListControlConvertEventArgs e)
		{
			if (e.DesiredType == typeof(string))
			{
				e.Value = (e.ListItem as DataSource).DisplayName;
			}
		}

		private void ChangeDataSource(object sender, EventArgs e)
		{
			DataSource newDataSource = this.dataSourceListBox.SelectedItem as DataSource;
			this.dataProviderComboBox.Items.Clear();
			if (newDataSource != null)
			{
				foreach (DataProvider dataProvider in newDataSource.Providers)
				{
					dataProviderComboBox.Items.Add(dataProvider);
				}
				if (!_providerSelections.ContainsKey(newDataSource))
				{
					_providerSelections.Add(newDataSource, newDataSource.DefaultProvider);
				}
				dataProviderComboBox.SelectedItem = _providerSelections[newDataSource];
			}
			else
			{
				this.dataProviderComboBox.Items.Add(String.Empty);
			}
			ConfigureDescription();
			SetOkButtonStatus();
		}

		private void SelectDataSource(object sender, EventArgs e)
		{
			if (this.okButton.Enabled)
			{
				this.DialogResult = DialogResult.OK;
				DoOk(sender, e);
				Close();
			}
		}

		private void FormatDataProvider(object sender, ListControlConvertEventArgs e)
		{
			if (e.DesiredType == typeof(string))
			{
				e.Value = (e.ListItem is DataProvider) ? (e.ListItem as DataProvider).DisplayName : e.ListItem.ToString();
			}
		}

		private void SetDataProviderDropDownWidth(object sender, EventArgs e)
		{
			if (dataProviderComboBox.Items.Count > 0 &&
				!(dataProviderComboBox.Items[0] is string))
			{
				int largestWidth = 0;
				using (Graphics g = Graphics.FromHwnd(dataProviderComboBox.Handle))
				{
					foreach (DataProvider dataProvider in dataProviderComboBox.Items)
					{
						int width = TextRenderer.MeasureText(
							g,
							dataProvider.DisplayName,
							dataProviderComboBox.Font,
							new Size(Int32.MaxValue, Int32.MaxValue),
							TextFormatFlags.WordBreak
						).Width;
						if (width > largestWidth)
						{
							largestWidth = width;
						}
					}
				}
				dataProviderComboBox.DropDownWidth = largestWidth + 3; // give a little extra margin
				if (dataProviderComboBox.Items.Count > dataProviderComboBox.MaxDropDownItems)
				{
					dataProviderComboBox.DropDownWidth += SystemInformation.VerticalScrollBarWidth;
				}
			}
			else
			{
				dataProviderComboBox.DropDownWidth = dataProviderComboBox.Width;
			}
		}

		private void ChangeDataProvider(object sender, EventArgs e)
		{
			if (this.dataSourceListBox.SelectedItem != null)
			{
				_providerSelections[this.dataSourceListBox.SelectedItem as DataSource] = this.dataProviderComboBox.SelectedItem as DataProvider;
			}
			ConfigureDescription();
			SetOkButtonStatus();
		}

		private void ConfigureDescription()
		{
			if (this.dataProviderComboBox.SelectedItem is DataProvider)
			{
				if (this.dataSourceListBox.SelectedItem == _mainDialog.UnspecifiedDataSource)
				{
					descriptionLabel.Text = (this.dataProviderComboBox.SelectedItem as DataProvider).Description;
				}
				else
				{
					descriptionLabel.Text = (this.dataProviderComboBox.SelectedItem as DataProvider).GetDescription(this.dataSourceListBox.SelectedItem as DataSource);
				}
			}
			else
			{
				descriptionLabel.Text = null;
			}
		}

		private void SetSaveSelection(object sender, EventArgs e)
		{
			_mainDialog.SaveSelection = saveSelectionCheckBox.Checked;
		}

		private void SetOkButtonStatus()
		{
			this.okButton.Enabled =
				this.dataSourceListBox.SelectedItem is DataSource &&
				this.dataProviderComboBox.SelectedItem is DataProvider;
		}

		private void DoOk(object sender, EventArgs e)
		{
			_mainDialog.SetSelectedDataSourceInternal(this.dataSourceListBox.SelectedItem as DataSource);
			foreach (DataSource dataSource in dataSourceListBox.Items)
			{
				DataProvider selectedProvider = (_providerSelections.ContainsKey(dataSource)) ? _providerSelections[dataSource] : null;
				_mainDialog.SetSelectedDataProviderInternal(dataSource, selectedProvider);
			}
		}

		private Label _headerLabel;
		private Dictionary<DataSource, DataProvider> _providerSelections = new Dictionary<DataSource, DataProvider>();
		private DataConnectionDialog _mainDialog;
	}
}
