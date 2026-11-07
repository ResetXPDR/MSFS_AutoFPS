using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;

namespace MSFS_AutoFPS;

public partial class NotifyIconViewModel : ObservableObject
{
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(ShowWindowCommand))]
	public bool canExecuteShowWindow = true;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(HideWindowCommand))]
	public bool canExecuteHideWindow;

	[RelayCommand(CanExecute = nameof(CanExecuteShowWindow))]
	public void ShowWindow()
	{
		Application.Current.MainWindow.WindowState = WindowState.Normal;
		WindowExtensions.Show(Application.Current.MainWindow);
		CanExecuteShowWindow = false;
		CanExecuteHideWindow = true;
	}

	[RelayCommand(CanExecute = nameof(CanExecuteHideWindow))]
	public void HideWindow()
	{
		Application.Current.MainWindow.Hide(enableEfficiencyMode: false);
		CanExecuteShowWindow = true;
		CanExecuteHideWindow = false;
	}

	[RelayCommand]
	public void ExitApplication()
	{
		if (Application.Current.MainWindow is MainWindow mainWindow)
		{
			mainWindow.Force_WindowClosing();
		}
		else
		{
			Application.Current.Shutdown();
		}
	}
}
