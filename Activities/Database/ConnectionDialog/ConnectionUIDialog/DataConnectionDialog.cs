//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Security;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms.Design;
using System.Security.Permissions;
using System.Runtime.InteropServices;

namespace Microsoft.Data.ConnectionUI
{
	public partial class DataConnectionDialog : Form
	{
		public DataConnectionDialog()
		{
			InitializeComponent();
			this.dataSourceTextBox.Width = 0;

			// Make sure we handle a user preference change
			components.Add(new UserPreferenceChangedHandler(this));

			// Configure initial label values
			ComponentResourceManager resources = new ComponentResourceManager(typeof(DataConnectionSourceDialog));
			this._chooseDataSourceTitle = resources.GetString("$this.Text");
			this._chooseDataSourceAcceptText = resources.GetString("okButton.Text");
			this._changeDataSourceTitle = Strings.DataConnectionDialog_ChangeDataSourceTitle;

			_dataSources = new DataSourceCollection(this);
		}

		public static DialogResult Show(DataConnectionDialog dialog)
		{

			return Show(dialog, null);
		}

		public static DialogResult Show(DataConnectionDialog dialog, IWin32Window owner)
		{
			if (dialog == null)
			{
				throw new ArgumentNullException("dialog");
			}
			if (dialog.DataSources.Count == 0)
			{
				throw new InvalidOperationException(Strings.DataConnectionDialog_NoDataSourcesAvailable);
			}
			foreach (DataSource dataSource in dialog.DataSources)
			{
				if (dataSource.Providers.Count == 0)
				{
					throw new InvalidOperationException(String.Format(Strings.DataConnectionDialog_NoDataProvidersForDataSource + dataSource.DisplayName.Replace("'", "''")));
				}
			}

			Application.ThreadException += new ThreadExceptionEventHandler(dialog.HandleDialogException);
			dialog._showingDialog = true;
			try
			{
				// If there is no selected data source or provider, show the data connection source dialog
				if (dialog.SelectedDataSource == null || dialog.SelectedDataProvider == null)
				{
					DataConnectionSourceDialog sourceDialog = new DataConnectionSourceDialog(dialog);
					sourceDialog.Title = dialog.ChooseDataSourceTitle;
					sourceDialog.HeaderLabel = dialog.ChooseDataSourceHeaderLabel;
					(sourceDialog.AcceptButton as Button).Text = dialog.ChooseDataSourceAcceptText;
					if (dialog.Container != null)
					{
						dialog.Container.Add(sourceDialog);
					}
					try
					{
						if (owner == null)
						{
							sourceDialog.StartPosition = FormStartPosition.CenterScreen;
						}
						sourceDialog.ShowDialog(owner);
						if (dialog.SelectedDataSource == null || dialog.SelectedDataProvider == null)
						{
							return DialogResult.Cancel;
						}
					}
					finally
					{
						if (dialog.Container != null)
						{
							dialog.Container.Remove(sourceDialog);
						}
						sourceDialog.Dispose();
					}
				}
				else
				{
					dialog._saveSelection = false;
				}
				if (owner == null)
				{
					dialog.StartPosition = FormStartPosition.CenterScreen;
				}
				for (; ; )
				{
					DialogResult result = dialog.ShowDialog(owner);
					if (result == DialogResult.Ignore)
					{
						DataConnectionSourceDialog sourceDialog = new DataConnectionSourceDialog(dialog);
						sourceDialog.Title = dialog.ChangeDataSourceTitle;
						sourceDialog.HeaderLabel = dialog.ChangeDataSourceHeaderLabel;
						if (dialog.Container != null)
						{
							dialog.Container.Add(sourceDialog);
						}
						try
						{
							if (owner == null)
							{
								sourceDialog.StartPosition = FormStartPosition.CenterScreen;
							}
							result = sourceDialog.ShowDialog(owner);
						}
						finally
						{
							if (dialog.Container != null)
							{
								dialog.Container.Remove(sourceDialog);
							}
							sourceDialog.Dispose();
						}
					}
					else
					{
						return result;
					}
				}
			}
			finally
			{
				dialog._showingDialog = false;
				Application.ThreadException -= new ThreadExceptionEventHandler(dialog.HandleDialogException);
			}
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
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (_headerLabel == null && (value == null || value.Length == 0))
				{
					return;
				}
				if (_headerLabel != null && value == _headerLabel.Text)
				{
					return;
				}
				if (value != null && value.Length > 0)
				{
					if (_headerLabel == null)
					{
						_headerLabel = new Label();
						_headerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
						_headerLabel.FlatStyle = FlatStyle.System;
						_headerLabel.Location = new Point(12, 12);
						_headerLabel.Margin = new Padding(3);
						_headerLabel.Name = "dataSourceLabel";
						_headerLabel.Width = this.dataSourceTableLayoutPanel.Width;
						_headerLabel.TabIndex = 100;
						this.Controls.Add(_headerLabel);
					}
					_headerLabel.Text = value;
					this.MinimumSize = Size.Empty;
					_headerLabel.Height = LayoutUtils.GetPreferredLabelHeight(_headerLabel);
					int dy =
						_headerLabel.Bottom +
						_headerLabel.Margin.Bottom +
						this.dataSourceLabel.Margin.Top -
						this.dataSourceLabel.Top;
					this.containerControl.Anchor &= ~AnchorStyles.Bottom;
					this.Height += dy;
					this.containerControl.Anchor |= AnchorStyles.Bottom;
					this.containerControl.Top += dy;
					this.dataSourceTableLayoutPanel.Top += dy;
					this.dataSourceLabel.Top += dy;
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
						this.dataSourceLabel.Top -= dy;
						this.dataSourceTableLayoutPanel.Top -= dy;
						this.containerControl.Top -= dy;
						this.containerControl.Anchor &= ~AnchorStyles.Bottom;
						this.Height -= dy;
						this.containerControl.Anchor |= AnchorStyles.Bottom;
						this.MinimumSize = this.Size;
					}
				}
			}
		}

		public bool TranslateHelpButton
		{
			get
			{
				return _translateHelpButton;
			}
			set
			{
				_translateHelpButton = value;
			}
		}

		public string ChooseDataSourceTitle
		{
			get
			{
				return _chooseDataSourceTitle;
			}
			set
			{
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (value == null)
				{
					value = String.Empty;
				}
				if (value == _chooseDataSourceTitle)
				{
					return;
				}
				_chooseDataSourceTitle = value;
			}
		}

		public string ChooseDataSourceHeaderLabel
		{
			get
			{
				return _chooseDataSourceHeaderLabel;
			}
			set
			{
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (value == null)
				{
					value = String.Empty;
				}
				if (value == _chooseDataSourceHeaderLabel)
				{
					return;
				}
				_chooseDataSourceHeaderLabel = value;
			}
		}

		public string ChooseDataSourceAcceptText
		{
			get
			{
				return _chooseDataSourceAcceptText;
			}
			set
			{
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (value == null)
				{
					value = String.Empty;
				}
				if (value == _chooseDataSourceAcceptText)
				{
					return;
				}
				_chooseDataSourceAcceptText = value;
			}
		}

		public string ChangeDataSourceTitle
		{
			get
			{
				return _changeDataSourceTitle;
			}
			set
			{
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (value == null)
				{
					value = String.Empty;
				}
				if (value == _changeDataSourceTitle)
				{
					return;
				}
				_changeDataSourceTitle = value;
			}
		}

		public string ChangeDataSourceHeaderLabel
		{
			get
			{
				return _changeDataSourceHeaderLabel;
			}
			set
			{
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (value == null)
				{
					value = String.Empty;
				}
				if (value == _changeDataSourceHeaderLabel)
				{
					return;
				}
				_changeDataSourceHeaderLabel = value;
			}
		}

		public ICollection<DataSource> DataSources
		{
			get
			{
				return _dataSources;
			}
		}

		public DataSource UnspecifiedDataSource
		{
			get
			{
				return _unspecifiedDataSource;
			}
		}

		public DataSource SelectedDataSource
		{
			get
			{
				if (_dataSources == null)
				{
					return null;
				}
				switch (_dataSources.Count)
				{
					case 0:
						Debug.Assert(_selectedDataSource == null);
						return null;
					case 1:
						// If there is only one data source, it must be selected
						IEnumerator<DataSource> e = _dataSources.GetEnumerator();
						e.MoveNext();
						return e.Current;
					default:
						return _selectedDataSource;
				}
			}
			set
			{
				if (SelectedDataSource != value)
				{
					if (_showingDialog)
					{
						throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
					}
					SetSelectedDataSource(value, false);
				}
			}
		}

		public DataProvider SelectedDataProvider
		{
			get
			{
				return GetSelectedDataProvider(SelectedDataSource);
			}
			set
			{
				if (SelectedDataProvider != value)
				{
					if (SelectedDataSource == null)
					{
						throw new InvalidOperationException(Strings.DataConnectionDialog_NoDataSourceSelected);
					}
					SetSelectedDataProvider(SelectedDataSource, value);
				}
			}
		}

		public DataProvider GetSelectedDataProvider(DataSource dataSource)
		{
			if (dataSource == null)
			{
				return null;
			}
			switch (dataSource.Providers.Count)
			{
				case 0:
					return null;
				case 1:
					// If there is only one data provider, it must be selected
					IEnumerator<DataProvider> e = dataSource.Providers.GetEnumerator();
					e.MoveNext();
					return e.Current;
				default:
					return (_dataProviderSelections.ContainsKey(dataSource)) ? _dataProviderSelections[dataSource] : dataSource.DefaultProvider;
			}
		}

		public void SetSelectedDataProvider(DataSource dataSource, DataProvider dataProvider)
		{
			if (GetSelectedDataProvider(dataSource) != dataProvider)
			{
				if (dataSource == null)
				{
					throw new ArgumentNullException("dataSource");
				}
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				SetSelectedDataProvider(dataSource, dataProvider, false);
			}
		}

		public bool SaveSelection
		{
			get
			{
				return _saveSelection;
			}
			set
			{
				_saveSelection = value;
			}
		}

		public string DisplayConnectionString
		{
			get
			{
				string s = null;
				if (ConnectionProperties != null)
				{
					try
					{
						s = ConnectionProperties.ToDisplayString();
					}
					catch { }
				}
				return (s != null) ? s : String.Empty;
			}
		}

		public string ConnectionString
		{
			get
			{
				string s = null;
				if (ConnectionProperties != null)
				{
					try
					{
						s = ConnectionProperties.ToString();
					}
					catch { }
				}
				return (s != null) ? s : String.Empty;
			}
			set
			{
				if (_showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (SelectedDataProvider == null)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_NoDataProviderSelected);
				}
				Debug.Assert(ConnectionProperties != null);
				if (ConnectionProperties != null)
				{
					ConnectionProperties.Parse(value);
				}
			}
		}

		public string AcceptButtonText
		{
			get
			{
				return acceptButton.Text;
			}
			set
			{
				acceptButton.Text = value;
			}
		}

		public event EventHandler VerifySettings;

		public event EventHandler<ContextHelpEventArgs> ContextHelpRequested;

		public event ThreadExceptionEventHandler DialogException;

		internal UserControl ConnectionUIControl
		{
			get
			{
				if (SelectedDataProvider == null)
				{
					return null;
				}
				if (!_connectionUIControlTable.ContainsKey(SelectedDataSource))
				{
					_connectionUIControlTable[SelectedDataSource] = new Dictionary<DataProvider, IDataConnectionUIControl>();
				}
				if (!_connectionUIControlTable[SelectedDataSource].ContainsKey(SelectedDataProvider))
				{
					IDataConnectionUIControl uiControl = null;
					UserControl control = null;
					try
					{
						if (SelectedDataSource == UnspecifiedDataSource)
						{
							uiControl = SelectedDataProvider.CreateConnectionUIControl();
						}
						else
						{
							uiControl = SelectedDataProvider.CreateConnectionUIControl(SelectedDataSource);
						}
						control = uiControl as UserControl;
						if (control == null)
						{
							IContainerControl ctControl = uiControl as IContainerControl;
							if (ctControl != null)
							{
								control = ctControl.ActiveControl as UserControl;
							}
						}
					}
					catch { }
					if (uiControl == null || control == null)
					{
						uiControl = new PropertyGridUIControl();
						control = uiControl as UserControl;
					}
					control.Location = Point.Empty;
					control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
					control.AutoSize = false;
					try
					{
						uiControl.Initialize(ConnectionProperties);
					}
					catch { }
					_connectionUIControlTable[SelectedDataSource][SelectedDataProvider] = uiControl;
					components.Add(control); // so that it is disposed when the form is disposed
				}
				UserControl result = _connectionUIControlTable[SelectedDataSource][SelectedDataProvider] as UserControl;
				if (result == null)
				{
					result = (_connectionUIControlTable[SelectedDataSource][SelectedDataProvider] as IContainerControl).ActiveControl as UserControl;
				}
				return result;
			}
		}

		internal IDataConnectionProperties ConnectionProperties
		{
			get
			{
				if (SelectedDataProvider == null)
				{
					return null;
				}
				if (!_connectionPropertiesTable.ContainsKey(SelectedDataSource))
				{
					_connectionPropertiesTable[SelectedDataSource] = new Dictionary<DataProvider, IDataConnectionProperties>();
				}
				if (!_connectionPropertiesTable[SelectedDataSource].ContainsKey(SelectedDataProvider))
				{
					IDataConnectionProperties properties = null;
					if (SelectedDataSource == UnspecifiedDataSource)
					{
						properties = SelectedDataProvider.CreateConnectionProperties();
					}
					else
					{
						properties = SelectedDataProvider.CreateConnectionProperties(SelectedDataSource);
					}
					if (properties == null)
					{
						properties = new BasicConnectionProperties();
					}
					properties.PropertyChanged += new EventHandler(ConfigureAcceptButton);
					_connectionPropertiesTable[SelectedDataSource][SelectedDataProvider] = properties;
				}
				return _connectionPropertiesTable[SelectedDataSource][SelectedDataProvider];
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			if (!_showingDialog)
			{
				throw new NotSupportedException(Strings.DataConnectionDialog_ShowDialogNotSupported);
			}
			ConfigureDataSourceTextBox();
			ConfigureChangeDataSourceButton();
			ConfigureContainerControl();
			ConfigureAcceptButton(this, EventArgs.Empty);
			base.OnLoad(e);
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);

			// Set focus to the connection UI control (if any)
			if (ConnectionUIControl != null)
			{
				ConnectionUIControl.Focus();
			}
		}

		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);

			this.dataSourceTableLayoutPanel.Anchor &= ~AnchorStyles.Right;
			this.containerControl.Anchor &= ~AnchorStyles.Right & ~AnchorStyles.Bottom;
			this.advancedButton.Anchor |= AnchorStyles.Top | AnchorStyles.Left;
			this.advancedButton.Anchor &= ~AnchorStyles.Right & ~AnchorStyles.Bottom;
			this.separatorPanel.Anchor |= AnchorStyles.Top;
			this.separatorPanel.Anchor &= ~AnchorStyles.Right & ~AnchorStyles.Bottom;
			this.testConnectionButton.Anchor |= AnchorStyles.Top;
			this.testConnectionButton.Anchor &= ~AnchorStyles.Bottom;
			this.buttonsTableLayoutPanel.Anchor |= AnchorStyles.Top | AnchorStyles.Left;
			this.buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Right & ~AnchorStyles.Bottom;
			Size properSize = new Size(
				this.containerControl.Right +
				this.containerControl.Margin.Right +
				this.Padding.Right,
				this.buttonsTableLayoutPanel.Bottom +
				this.buttonsTableLayoutPanel.Margin.Bottom +
				this.Padding.Bottom);
			properSize = SizeFromClientSize(properSize);
			Size dsize = this.Size - properSize;
			this.MinimumSize -= dsize;
			this.Size -= dsize;
			this.buttonsTableLayoutPanel.Anchor |= AnchorStyles.Right | AnchorStyles.Bottom;
			this.buttonsTableLayoutPanel.Anchor &= ~AnchorStyles.Top & ~AnchorStyles.Left;
			this.testConnectionButton.Anchor |= AnchorStyles.Bottom;
			this.testConnectionButton.Anchor &= ~AnchorStyles.Top;
			this.separatorPanel.Anchor |= AnchorStyles.Right | AnchorStyles.Bottom;
			this.separatorPanel.Anchor &= ~AnchorStyles.Top;
			this.advancedButton.Anchor |= AnchorStyles.Right | AnchorStyles.Bottom;
			this.advancedButton.Anchor &= ~AnchorStyles.Top & ~AnchorStyles.Left;
			this.containerControl.Anchor |= AnchorStyles.Right | AnchorStyles.Bottom;
			this.dataSourceTableLayoutPanel.Anchor |= AnchorStyles.Right;
		}

		protected virtual void OnVerifySettings(EventArgs e)
		{
			if (VerifySettings != null)
			{
				VerifySettings(this, e);
			}
		}

		protected internal virtual void OnContextHelpRequested(ContextHelpEventArgs e)
		{
			if (ContextHelpRequested != null)
			{
				ContextHelpRequested(this, e);
			}
			if (e.Handled == false)
			{
				ShowError(null, Strings.DataConnectionDialog_NoHelpAvailable);
				e.Handled = true;
			}
		}

		protected override void OnHelpRequested(HelpEventArgs hevent)
		{
			// Get the active control
			Control activeControl = this;
			ContainerControl containrControl = null;
			while ((containrControl = activeControl as ContainerControl) != null &&
				containrControl != ConnectionUIControl &&
				containrControl.ActiveControl != null)
			{
				activeControl = containrControl.ActiveControl;
			}

			// Figure out the context
			DataConnectionDialogContext context = DataConnectionDialogContext.Main;
			if (activeControl == this.dataSourceTextBox)
			{
				context = DataConnectionDialogContext.MainDataSourceTextBox;
			}
			if (activeControl == this.changeDataSourceButton)
			{
				context = DataConnectionDialogContext.MainChangeDataSourceButton;
			}
			if (activeControl == ConnectionUIControl)
			{
				context = DataConnectionDialogContext.MainConnectionUIControl;
				if (ConnectionUIControl is SqlConnectionUIControl)
				{
					context = DataConnectionDialogContext.MainSqlConnectionUIControl;
				}
				if (ConnectionUIControl is SqlFileConnectionUIControl)
				{
					context = DataConnectionDialogContext.MainSqlFileConnectionUIControl;
				}
				if (ConnectionUIControl is OracleConnectionUIControl)
				{
					context = DataConnectionDialogContext.MainOracleConnectionUIControl;
				}
				if (ConnectionUIControl is AccessConnectionUIControl)
				{
					context = DataConnectionDialogContext.MainAccessConnectionUIControl;
				}
				if (ConnectionUIControl is OleDBConnectionUIControl)
				{
					context = DataConnectionDialogContext.MainOleDBConnectionUIControl;
				}
				if (ConnectionUIControl is OdbcConnectionUIControl)
				{
					context = DataConnectionDialogContext.MainOdbcConnectionUIControl;
				}
				if (ConnectionUIControl is PropertyGridUIControl)
				{
					context = DataConnectionDialogContext.MainGenericConnectionUIControl;
				}
			}
			if (activeControl == this.advancedButton)
			{
				context = DataConnectionDialogContext.MainAdvancedButton;
			}
			if (activeControl == this.testConnectionButton)
			{
				context = DataConnectionDialogContext.MainTestConnectionButton;
			}
			if (activeControl == this.acceptButton)
			{
				context = DataConnectionDialogContext.MainAcceptButton;
			}
			if (activeControl == this.cancelButton)
			{
				context = DataConnectionDialogContext.MainCancelButton;
			}

			// Call OnContextHelpRequested
			ContextHelpEventArgs e = new ContextHelpEventArgs(context, hevent.MousePos);
			OnContextHelpRequested(e);
			hevent.Handled = e.Handled;
			if (!e.Handled)
			{
				base.OnHelpRequested(hevent);
			}
		}

		protected virtual void OnDialogException(ThreadExceptionEventArgs e)
		{
			if (DialogException != null)
			{
				DialogException(this, e);
			}
			else
			{
				ShowError(null, e.Exception);
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (DialogResult == DialogResult.OK)
			{
				try
				{
					OnVerifySettings(EventArgs.Empty);
				}
				catch (Exception ex)
				{
					ExternalException exex = ex as ExternalException;
					if (exex == null || exex.ErrorCode != NativeMethods.DB_E_CANCELED)
					{
						ShowError(null, ex);
					}
					e.Cancel = true;
				}
			}

			base.OnFormClosing(e);
		}

		[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
		protected override void WndProc(ref Message m)
		{
			if (_translateHelpButton && HelpUtils.IsContextHelpMessage(ref m))
			{
				// Force the ? in the title bar to invoke the help topic
				HelpUtils.TranslateContextHelpMessage(this, ref m);
				base.DefWndProc(ref m); // pass to the active control
				return;
			}
			base.WndProc(ref m);
		}

		internal void SetSelectedDataSourceInternal(DataSource value)
		{
			SetSelectedDataSource(value, false);
		}

		internal void SetSelectedDataProviderInternal(DataSource dataSource, DataProvider value)
		{
			SetSelectedDataProvider(dataSource, value, false);
		}

		private void SetSelectedDataSource(DataSource value, bool noSingleItemCheck)
		{
			if (!noSingleItemCheck && _dataSources.Count == 1 && _selectedDataSource != value)
			{
				IEnumerator<DataSource> e = _dataSources.GetEnumerator();
				e.MoveNext();
				if (value != e.Current)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotChangeSingleDataSource);
				}
			}
			if (_selectedDataSource != value)
			{
				if (value != null)
				{
					if (!_dataSources.Contains(value))
					{
						throw new InvalidOperationException(Strings.DataConnectionDialog_DataSourceNotFound);
					}
					_selectedDataSource = value;
					switch (_selectedDataSource.Providers.Count)
					{
						case 0:
							SetSelectedDataProvider(_selectedDataSource, null, noSingleItemCheck);
							break;
						case 1:
							IEnumerator<DataProvider> e = _selectedDataSource.Providers.GetEnumerator();
							e.MoveNext();
							SetSelectedDataProvider(_selectedDataSource, e.Current, true);
							break;
						default:
							DataProvider defaultProvider = _selectedDataSource.DefaultProvider;
							if (_dataProviderSelections.ContainsKey(_selectedDataSource))
							{
								defaultProvider = _dataProviderSelections[_selectedDataSource];
							}
							SetSelectedDataProvider(_selectedDataSource, defaultProvider, noSingleItemCheck);
							break;
					}
				}
				else
				{
					_selectedDataSource = null;
				}

				if (_showingDialog)
				{
					ConfigureDataSourceTextBox();
				}
			}
		}

		private void SetSelectedDataProvider(DataSource dataSource, DataProvider value, bool noSingleItemCheck)
		{
			Debug.Assert(dataSource != null);
			if (!noSingleItemCheck && dataSource.Providers.Count == 1 &&
				((_dataProviderSelections.ContainsKey(dataSource) && _dataProviderSelections[dataSource] != value) ||
				(!_dataProviderSelections.ContainsKey(dataSource) && value != null)))
			{
				IEnumerator<DataProvider> e = dataSource.Providers.GetEnumerator();
				e.MoveNext();
				if (value != e.Current)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotChangeSingleDataProvider);
				}
			}
			if ((_dataProviderSelections.ContainsKey(dataSource) && _dataProviderSelections[dataSource] != value) ||
				(!_dataProviderSelections.ContainsKey(dataSource) && value != null))
			{
				if (value != null)
				{
					if (!dataSource.Providers.Contains(value))
					{
						throw new InvalidOperationException(Strings.DataConnectionDialog_DataSourceNoAssociation);
					}
					_dataProviderSelections[dataSource] = value;
				}
				else if (_dataProviderSelections.ContainsKey(dataSource))
				{
					_dataProviderSelections.Remove(dataSource);
				}

				if (_showingDialog)
				{
					ConfigureContainerControl();
				}
			}
		}

		private void ConfigureDataSourceTextBox()
		{
			if (SelectedDataSource != null)
			{
				if (SelectedDataSource == UnspecifiedDataSource)
				{
					if (SelectedDataProvider != null)
					{
						dataSourceTextBox.Text = SelectedDataProvider.DisplayName;
					}
					else
					{
						dataSourceTextBox.Text = null;
					}
					dataProviderToolTip.SetToolTip(dataSourceTextBox, null);
				}
				else
				{
					dataSourceTextBox.Text = SelectedDataSource.DisplayName;
					if (SelectedDataProvider != null)
					{
						if (SelectedDataProvider.ShortDisplayName != null)
						{
							dataSourceTextBox.Text = String.Format(Strings.DataConnectionDialog_DataSourceWithShortProvider, dataSourceTextBox.Text, SelectedDataProvider.ShortDisplayName);
						}
						dataProviderToolTip.SetToolTip(dataSourceTextBox, SelectedDataProvider.DisplayName);
					}
					else
					{
						dataProviderToolTip.SetToolTip(dataSourceTextBox, null);
					}
				}
			}
			else
			{
				dataSourceTextBox.Text = null;
				dataProviderToolTip.SetToolTip(dataSourceTextBox, null);
			}
			dataSourceTextBox.Select(0, 0);
		}

		private void ConfigureChangeDataSourceButton()
		{
			this.changeDataSourceButton.Enabled = (DataSources.Count > 1 || SelectedDataSource.Providers.Count > 1);
		}

		private void ChangeDataSource(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Ignore;
			Close();
		}

		private void ConfigureContainerControl()
		{
			if (this.containerControl.Controls.Count == 0)
			{
				_initialContainerControlSize = this.containerControl.Size;
			}
			if ((this.containerControl.Controls.Count == 0 && ConnectionUIControl != null) ||
				(this.containerControl.Controls.Count > 0 && ConnectionUIControl != this.containerControl.Controls[0]))
			{
				this.containerControl.Controls.Clear();
				if (ConnectionUIControl != null && ConnectionUIControl.PreferredSize.Width > 0 && ConnectionUIControl.PreferredSize.Height > 0)
				{
					// Add it to the container control
					this.containerControl.Controls.Add(ConnectionUIControl);

					// Size dialog appropriately
					this.MinimumSize = Size.Empty;
					Size currentSize = containerControl.Size;
					containerControl.Size = _initialContainerControlSize;
					Size preferredSize = ConnectionUIControl.PreferredSize;
					containerControl.Size = currentSize;
					int minimumWidth =
						_initialContainerControlSize.Width - (this.Width - this.ClientSize.Width) -
						this.Padding.Left -
						this.containerControl.Margin.Left -
						this.containerControl.Margin.Right -
						this.Padding.Right;
					minimumWidth = Math.Max(minimumWidth,
						this.testConnectionButton.Width +
						this.testConnectionButton.Margin.Right +
						this.buttonsTableLayoutPanel.Margin.Left +
						this.buttonsTableLayoutPanel.Width +
						this.buttonsTableLayoutPanel.Margin.Right);
					preferredSize.Width = Math.Max(preferredSize.Width, minimumWidth);
					this.Size += preferredSize - containerControl.Size;
					if (containerControl.Bottom == advancedButton.Top)
					{
						this.containerControl.Margin = new Padding(
								this.containerControl.Margin.Left,
								this.dataSourceTableLayoutPanel.Margin.Bottom,
								this.containerControl.Margin.Right,
								this.advancedButton.Margin.Top);
						this.Height += this.containerControl.Margin.Bottom + this.advancedButton.Margin.Top;
						this.containerControl.Height -= this.containerControl.Margin.Bottom + this.advancedButton.Margin.Top;
					}
					Size maximumSize =
						SystemInformation.PrimaryMonitorMaximizedWindowSize -
						SystemInformation.FrameBorderSize -
						SystemInformation.FrameBorderSize;
					if (this.Width > maximumSize.Width)
					{
						this.Width = maximumSize.Width;
						if (this.Height + SystemInformation.HorizontalScrollBarHeight <= maximumSize.Height)
						{
							this.Height += SystemInformation.HorizontalScrollBarHeight;
						}
					}
					if (this.Height > maximumSize.Height)
					{
						if (this.Width + SystemInformation.VerticalScrollBarWidth <= maximumSize.Width)
						{
							this.Width += SystemInformation.VerticalScrollBarWidth;
						}
						this.Height = maximumSize.Height;
					}
					this.MinimumSize = this.Size;

					// The advanced button is only enabled for actual UI controls
					this.advancedButton.Enabled = !(ConnectionUIControl is PropertyGridUIControl);
				}
				else
				{
					// Size dialog appropriately
					this.MinimumSize = Size.Empty;
					if (containerControl.Bottom != advancedButton.Top)
					{
						this.containerControl.Height += this.containerControl.Margin.Bottom + this.advancedButton.Margin.Top;
						this.Height -= this.containerControl.Margin.Bottom + this.advancedButton.Margin.Top;
						this.containerControl.Margin = new Padding(
								this.containerControl.Margin.Left,
								0,
								this.containerControl.Margin.Right,
								0);
					}
					this.Size -= containerControl.Size - new Size(300, 0);
					this.MinimumSize = this.Size;

					// The advanced button is always enabled for no UI control
					this.advancedButton.Enabled = true;
				}
			}
			if (ConnectionUIControl != null)
			{
				// Load properties into the connection UI control
				try
				{
					this._connectionUIControlTable[SelectedDataSource][SelectedDataProvider].LoadProperties();
				}
				catch { }
			}
		}
		private Size _initialContainerControlSize;

		private void SetConnectionUIControlDockStyle(object sender, EventArgs e)
		{
			if (this.containerControl.Controls.Count > 0)
			{
				DockStyle dockStyle = DockStyle.None;
				Size containerControlSize = this.containerControl.Size;
				Size connectionUIControlMinimumSize = this.containerControl.Controls[0].MinimumSize;
				if (containerControlSize.Width >= connectionUIControlMinimumSize.Width &&
					containerControlSize.Height >= connectionUIControlMinimumSize.Height)
				{
					dockStyle = DockStyle.Fill;
				}
				if (containerControlSize.Width - SystemInformation.VerticalScrollBarWidth >= connectionUIControlMinimumSize.Width &&
					containerControlSize.Height < connectionUIControlMinimumSize.Height)
				{
					dockStyle = DockStyle.Top;
				}
				if (containerControlSize.Width < connectionUIControlMinimumSize.Width &&
					containerControlSize.Height - SystemInformation.HorizontalScrollBarHeight >= connectionUIControlMinimumSize.Height)
				{
					dockStyle = DockStyle.Left;
				}
				this.containerControl.Controls[0].Dock = dockStyle;
			}
		}

		private void ShowAdvanced(object sender, EventArgs e)
		{
			DataConnectionAdvancedDialog advancedDialog = new DataConnectionAdvancedDialog(ConnectionProperties, this);
			DialogResult dialogResult = DialogResult.None;
			try
			{
				if (Container != null)
				{
					Container.Add(advancedDialog);
				}
				dialogResult = advancedDialog.ShowDialog(this);
			}
			finally
			{
				if (Container != null)
				{
					Container.Remove(advancedDialog);
				}
				advancedDialog.Dispose();
			}
			if (dialogResult == DialogResult.OK && ConnectionUIControl != null)
			{
				try
				{
					_connectionUIControlTable[SelectedDataSource][SelectedDataProvider].LoadProperties();
				}
				catch { }
				ConfigureAcceptButton(this, EventArgs.Empty);
			}
		}

		private void TestConnection(object sender, EventArgs e)
		{
			Cursor currentCursor = Cursor.Current;
			Cursor.Current = Cursors.WaitCursor;
			try
			{
				ConnectionProperties.Test();
			}
			catch (Exception ex)
			{
				Cursor.Current = currentCursor;
				ShowError(Strings.DataConnectionDialog_TestResults, ex);
				return;
			}
			Cursor.Current = currentCursor;
			ShowMessage(Strings.DataConnectionDialog_TestResults, Strings.DataConnectionDialog_TestConnectionSucceeded);
		}

		private void ConfigureAcceptButton(object sender, EventArgs e)
		{
			try
			{
				acceptButton.Enabled = (ConnectionProperties != null) ? ConnectionProperties.IsComplete : false;
			}
			catch
			{
				acceptButton.Enabled = true;
			}
		}

		private void HandleAccept(object sender, EventArgs e)
		{
			acceptButton.Focus(); // ensures connection properties are up to date
		}

		private void PaintSeparator(object sender, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			Pen dark = new Pen(ControlPaint.Dark(BackColor, 0f));
			Pen light = new Pen(ControlPaint.Light(BackColor, 1f));
			int width = separatorPanel.Width;

			graphics.DrawLine(dark, 0, 0, width, 0);
			graphics.DrawLine(light, 0, 1, width, 1);
		}

		private void HandleDialogException(object sender, ThreadExceptionEventArgs e)
		{
			OnDialogException(e);
		}

		private void ShowMessage(string title, string message)
		{
			IUIService uiService = this.GetService(typeof(IUIService)) as IUIService;
			if (uiService != null)
			{
				uiService.ShowMessage(message);
			}
			else
			{
				RTLAwareMessageBox.Show(title, message, MessageBoxIcon.Information);
			}
		}

		private void ShowError(string title, Exception ex)
		{
			IUIService uiService = this.GetService(typeof(IUIService)) as IUIService;
			if (uiService != null)
			{
				uiService.ShowError(ex);
			}
			else
			{
				RTLAwareMessageBox.Show(title, ex.Message, MessageBoxIcon.Exclamation);
			}
		}

		private void ShowError(string title, string message)
		{
			IUIService uiService = this.GetService(typeof(IUIService)) as IUIService;
			if (uiService != null)
			{
				uiService.ShowError(message);
			}
			else
			{
				RTLAwareMessageBox.Show(title, message, MessageBoxIcon.Exclamation);
			}
		}

		private class DataSourceCollection : ICollection<DataSource>
		{
			public DataSourceCollection(DataConnectionDialog dialog)
			{
				Debug.Assert(dialog != null);

				_dialog = dialog;
			}

			public int Count
			{
				get
				{
					return _list.Count;
				}
			}

			public bool IsReadOnly
			{
				get
				{
					return _dialog._showingDialog;
				}
			}

			public void Add(DataSource item)
			{
				if (item == null)
				{
					throw new ArgumentNullException("item");
				}
				if (_dialog._showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				if (!_list.Contains(item))
				{
					_list.Add(item);
				}
			}

			public bool Contains(DataSource item)
			{
				return _list.Contains(item);
			}

			public bool Remove(DataSource item)
			{
				if (_dialog._showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				bool result = _list.Remove(item);
				if (item == _dialog.SelectedDataSource)
				{
					_dialog.SetSelectedDataSource(null, true);
				}
				return result;
			}

			public void Clear()
			{
				if (_dialog._showingDialog)
				{
					throw new InvalidOperationException(Strings.DataConnectionDialog_CannotModifyState);
				}
				_list.Clear();
				_dialog.SetSelectedDataSource(null, true);
			}

			public void CopyTo(DataSource[] array, int arrayIndex)
			{
				_list.CopyTo(array, arrayIndex);
			}

			public IEnumerator<DataSource> GetEnumerator()
			{
				return _list.GetEnumerator();
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return _list.GetEnumerator();
			}

			private List<DataSource> _list = new List<DataSource>();
			private DataConnectionDialog _dialog;
		}

		private class PropertyGridUIControl : UserControl, IDataConnectionUIControl
		{
			public PropertyGridUIControl()
			{
				this.propertyGrid = new DataConnectionAdvancedDialog.SpecializedPropertyGrid();
				this.SuspendLayout();
				// 
				// propertyGrid
				// 
				this.propertyGrid.CommandsVisibleIfAvailable = true;
				this.propertyGrid.Dock = DockStyle.Fill;
				this.propertyGrid.Location = Point.Empty;
				this.propertyGrid.Margin = new Padding(0);
				this.propertyGrid.Name = "propertyGrid";
				this.propertyGrid.TabIndex = 0;
				// 
				// DataConnectionDialog
				// 
				this.Controls.Add(this.propertyGrid);
				this.Name = "PropertyGridUIControl";
				this.ResumeLayout(false);
				this.PerformLayout();
			}

			public void Initialize(IDataConnectionProperties dataConnectionProperties)
			{
				this.connectionProperties = dataConnectionProperties;
			}

			public void LoadProperties()
			{
				this.propertyGrid.SelectedObject = connectionProperties;
			}

			public override Size GetPreferredSize(Size proposedSize)
			{
				return this.propertyGrid.GetPreferredSize(proposedSize);
			}

			private IDataConnectionProperties connectionProperties;
			private DataConnectionAdvancedDialog.SpecializedPropertyGrid propertyGrid;
		}

		private class BasicConnectionProperties : IDataConnectionProperties
		{
			public BasicConnectionProperties()
			{
			}

			public void Reset()
			{
				_s = String.Empty;
			}

			public void Parse(string s)
			{
				_s = s;
				if (PropertyChanged != null)
				{
					PropertyChanged(this, EventArgs.Empty);
				}
			}

			[Browsable(false)]
			public bool IsExtensible
			{
				get
				{
					return false;
				}
			}

			public void Add(string propertyName)
			{
				throw new NotImplementedException();
			}

			public bool Contains(string propertyName)
			{
				return (propertyName == "ConnectionString");
			}

			public object this[string propertyName]
			{
				get
				{
					if (propertyName == "ConnectionString")
					{
						return ConnectionString;
					}
					else
					{
						return null;
					}
				}
				set
				{
					if (propertyName == "ConnectionString")
					{
						ConnectionString = value as string;
					}
				}
			}

			public void Remove(string propertyName)
			{
				throw new NotImplementedException();
			}

			public event EventHandler PropertyChanged;

			public void Reset(string propertyName)
			{
				Debug.Assert(propertyName == "ConnectionString");
				_s = String.Empty;
			}

			[Browsable(false)]
			public bool IsComplete
			{
				get
				{
					return true;
				}
			}

			public string ConnectionString
			{
				get
				{
					return ToFullString();
				}
				set
				{
					Parse(value);
				}
			}

			public void Test()
			{
			}

			public string ToFullString()
			{
				return _s;
			}

			public string ToDisplayString()
			{
				return _s;
			}

			private string _s;
		}

		private bool _showingDialog;
		private Label _headerLabel;
		private bool _translateHelpButton = true;
		private string _chooseDataSourceTitle;
		private string _chooseDataSourceHeaderLabel = String.Empty;
		private string _chooseDataSourceAcceptText;
		private string _changeDataSourceTitle;
		private string _changeDataSourceHeaderLabel = String.Empty;
		private ICollection<DataSource> _dataSources;
		private DataSource _unspecifiedDataSource = DataSource.CreateUnspecified();
		private DataSource _selectedDataSource;
		private IDictionary<DataSource, DataProvider> _dataProviderSelections = new Dictionary<DataSource, DataProvider>();
		private bool _saveSelection = true;
		private IDictionary<DataSource, IDictionary<DataProvider, IDataConnectionUIControl>> _connectionUIControlTable = new Dictionary<DataSource, IDictionary<DataProvider, IDataConnectionUIControl>>();
		private IDictionary<DataSource, IDictionary<DataProvider, IDataConnectionProperties>> _connectionPropertiesTable = new Dictionary<DataSource, IDictionary<DataProvider, IDataConnectionProperties>>();
	}
}
