using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Point = System.Drawing.Point;

namespace DesktopIcons {
	public partial class MainWindow : Window {
		private IconManager iconManager;

		public MainWindow() {
			InitializeComponent();
		}

		private void MainWindow_OnLoaded(object sender, RoutedEventArgs e) {
			//TryWithError(() => iconManager = new IconManager(), Close, "Error");
			try {
				iconManager = new IconManager();
			}
			catch (Exception ex) {
				MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				Close();
				return;
			}

			try {
				Dictionary<string,Point> icons = iconManager.GetDesktopIcons();
				foreach ((string name, Point pos) in icons) {
					TextBox.Text += $"{name} ({pos.X}, {pos.Y})\n";
				}
			}
			catch (Exception ex) {
				MessageBox.Show(this, ex.Message, "Operation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void TryWithError(Action action, string caption, Action onError) {
			try {
				action();
			}
			catch (Exception e) {
				MessageBox.Show(this, e.Message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
				onError?.Invoke();
			}
		}
	}
}