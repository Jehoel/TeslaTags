﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

using GalaSoft.MvvmLight.CommandWpf;

namespace TeslaTags.Gui
{
	public partial class MainViewModel
	{
		#region Two-way

		private String directoryPath;
		public String DirectoryPath
		{
			get { return this.directoryPath; }
			set
			{
				if( this.Set( nameof(this.DirectoryPath), ref this.directoryPath, value ) )
				{
					this.StartCommand.RaiseCanExecuteChanged();
				}
			}
		}

		private Boolean onlyValidate;
		public Boolean OnlyValidate
		{
			get { return this.onlyValidate; }
			set { this.Set( nameof(this.OnlyValidate), ref this.onlyValidate, value ); }
		}

		private String excludeLines;
		public String ExcludeLines
		{
			get { return this.excludeLines ?? String.Empty; }
			set { this.Set( nameof(this.ExcludeLines), ref this.excludeLines, value ); }
		}

		private String fileExtensionsToLoad;
		public String FileExtensionsToLoad
		{
			get { return this.fileExtensionsToLoad ?? String.Empty; }
			set { this.Set( nameof(this.FileExtensionsToLoad), ref this.fileExtensionsToLoad, value ); }
		}

		private Boolean restoreFiles;
		public Boolean RestoreFiles
		{
			get { return this.restoreFiles; }
			set { this.Set( nameof(this.RestoreFiles), ref this.restoreFiles, value ); }
		}

		private Boolean hideBoringDirectories;
		public Boolean HideBoringDirectories
		{
			get { return this.hideBoringDirectories; }
			set { this.Set( nameof(this.HideBoringDirectories), ref this.hideBoringDirectories, value ); }
		}

		public GenreRulesViewModel GenreRules { get; } = new GenreRulesViewModel();

		private readonly Dictionary<String,DirectoryViewModel> viewModelDict = new Dictionary<String,DirectoryViewModel>( StringComparer.OrdinalIgnoreCase );

		public ObservableCollection<DirectoryViewModel> DirectoriesProgress { get; } = new ObservableCollection<DirectoryViewModel>();

		private DirectoryViewModel selectedDirectory;
		public DirectoryViewModel SelectedDirectory
		{
			get { return this.selectedDirectory; }
			set { this.Set( nameof(this.SelectedDirectory), ref this.selectedDirectory, value ); }
		}

		#endregion

		#region One-way from ViewModel

		private Single progressPerc;
		public Single ProgressPerc
		{
			get { return this.progressPerc; }
			set
			{
				this.Set( nameof(this.ProgressPerc), ref this.progressPerc, value );
				this.RaisePropertyChanged( nameof(this.WindowTitle) );
			}
		}

		private ProgressState progressStatus;
		public ProgressState ProgressStatus
		{
			get { return this.progressStatus; }
			set
			{
				this.Set( nameof(this.ProgressStatus), ref this.progressStatus, value );
				this.RaisePropertyChanged( nameof(this.TaskbarProgressStatus) );
				this.RaisePropertyChanged( nameof(this.WindowTitle) );
			}
		}

		public System.Windows.Shell.TaskbarItemProgressState TaskbarProgressStatus
		{
			get
			{
				switch( this.ProgressStatus )
				{
				case ProgressState.StartingIndeterminate: return System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
				case ProgressState.Running              : return System.Windows.Shell.TaskbarItemProgressState.Normal;
				case ProgressState.Error                : return System.Windows.Shell.TaskbarItemProgressState.Error;
				case ProgressState.Canceled             : return System.Windows.Shell.TaskbarItemProgressState.Paused;

				case ProgressState.Completed:
				case ProgressState.NotStarted:
				default:
					return System.Windows.Shell.TaskbarItemProgressState.None;
				}
			}
		}

		public String WindowTitle
		{
			get
			{
				switch( this.ProgressStatus )
				{
				case ProgressState.StartingIndeterminate: return "TeslaTags - Starting...";
				case ProgressState.Running              : return "TeslaTags - " + this.ProgressPerc.ToString( "P0", CultureInfo.CurrentCulture ) + " complete.";
				case ProgressState.Error                : return "TeslaTags - Error";
				case ProgressState.Completed            : return "TeslaTags - Complete";
				case ProgressState.Canceled:
				case ProgressState.NotStarted:
				default:
					return "TeslaTags";
				}
			}
		}

		public String Version    { get; }

		public String ReadmeLink { get; }

		#endregion

		#region Commands

		public RelayCommand WindowLoadedCommand { get; }

		public RelayCommand WindowClosingCommand { get; }

		public RelayCommand StartCommand { get; }

		public RelayCommand StopCommand { get; }

		#endregion
	}
}
