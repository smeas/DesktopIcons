using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using Point = System.Drawing.Point;

namespace DesktopIcons {
	public partial class MainWindow : Window {
		private const string QuickSaveFile = "icon_layout.json";
		private IconManager iconManager;

		public MainWindow() {
			InitializeComponent();
		}

		private void MainWindow_OnLoaded(object sender, RoutedEventArgs e) {
			if (!TryWithError(() => iconManager = new IconManager(), "Error", Close))
				return;

			TryWithError(() => {
				Dictionary<string, IconInfo> icons = iconManager.GetDesktopIcons();
				foreach ((string name, (_, Point position)) in icons)
					TextBox.Text += $"{name} ({position.X}, {position.Y})\n";
			}, "Operation Failed");
		}

		private bool TryWithError(Action action, string caption = null, Action onError = null) {
			try {
				action();
				return true;
			}
			catch (Exception e) {
				MessageBox.Show(this, e.Message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
				onError?.Invoke();
				return false;
			}
		}

		private void ApplyIconLayout(Dictionary<string, IconInfo> layout) {
			Dictionary<string, IconInfo> icons = iconManager.GetDesktopIcons();
			foreach ((string name, (_, Point position)) in layout) {
				if (icons.TryGetValue(name, out IconInfo icon)) {
					iconManager.SetItemPosition(icon.Index, position);
				}
			}
		}

		private void QuickSave(object sender, RoutedEventArgs e) {
			TryWithError(() => {
				Dictionary<string, IconInfo> icons = iconManager.GetDesktopIcons();
				string data = JsonSerializer.Serialize(icons);
				File.WriteAllText(QuickSaveFile, data);
			}, "Operation Failed");
		}

		private void QuickLoad(object sender, RoutedEventArgs e) {
			if (!File.Exists(QuickSaveFile)) {
				MessageBox.Show("No quick save found.");
				return;
			}

			TryWithError(() => {
				string data = File.ReadAllText(QuickSaveFile);
				Dictionary<string, IconInfo>
					iconLayout = JsonSerializer.Deserialize<Dictionary<string, IconInfo>>(data);
				ApplyIconLayout(iconLayout);
			}, "Operation Failed");
		}

		private void Undo(object sender, RoutedEventArgs e) { }
	}
}