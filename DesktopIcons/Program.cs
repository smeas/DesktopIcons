using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Vanara.PInvoke;

namespace DesktopIcons {
	internal static class Program {
		private static void Main(string[] args) {
			using Runner runner = new();
			runner.Run();
		}
	}

	record Icon(int Index, (int x, int y) Position);

	internal class Runner : IDisposable {
		private const Kernel32.ProcessAccess ProcessAccessRWO = Kernel32.ProcessAccess.PROCESS_VM_READ |
			Kernel32.ProcessAccess.PROCESS_VM_WRITE | Kernel32.ProcessAccess.PROCESS_VM_OPERATION;

		private const int MaxText = 260;

		private Kernel32.SafeHPROCESS hExplorerProcess;
		private HWND hListView;

		private SharedMem<LVITEM> sharedLvItem;
		private SharedString sharedText;
		private SharedMem<POINT> sharedPoint;

		public void Run() {
			hListView = GetDesktopListViewHandle();

			// Open the explorer process
			User32.GetWindowThreadProcessId(hListView, out uint processId);
			hExplorerProcess = Kernel32.OpenProcess((int)ProcessAccessRWO, false, processId);

			if (hExplorerProcess.IsInvalid)
				throw new Exception("Failed to open process");

			// Allocate memory for communication
			sharedLvItem = new SharedMem<LVITEM>(hExplorerProcess);
			sharedText = new SharedString(hExplorerProcess, MaxText);
			sharedPoint = new SharedMem<POINT>(hExplorerProcess);


			int itemCount = GetItemCount();
			Console.WriteLine($"Item count: {itemCount}");

			Dictionary<string, Icon> icons = new();

			for (int i = 0; i < itemCount; i++) {
				string name = GetItemText(i);
				var pos = GetItemPosition(i);

				icons.Add(name, new Icon(i, pos));

				Console.WriteLine($"{name} {pos}");
			}

			//var rc = icons["Recycle Bin"];
			//SetItemPosition(rc.Index, (rc.Position.x + 50, rc.Position.y));
			//SetItemPosition(icons["Unreal Engine"].Index, icons["Recycle Bin"].Position);

			File.WriteAllText("icons.json", JsonSerializer.Serialize(icons));
		}

		private HWND GetDesktopListViewHandle() {
			HWND hWndDesktop = User32.FindWindow("Progman", null);
			HWND hWnd = User32.GetWindow(hWndDesktop, User32.GetWindowCmd.GW_CHILD);
			hWnd = User32.GetWindow(hWnd, User32.GetWindowCmd.GW_CHILD);

			StringBuilder sb = new();
			sb.EnsureCapacity(16);
			User32.RealGetWindowClass(hWnd, sb, (uint)sb.Capacity);
			if (sb.ToString() != "SysListView32")
				throw new Exception("Can't find desktop list view");

			return hWnd;
		}

		private int GetItemCount() {
			return (int)User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_GETITEMCOUNT);
		}

		private string GetItemText(int index) {
			LVITEM item = new() {
				mask = ComCtl32.ListViewItemMask.LVIF_TEXT,
				pszText = sharedText.RemotePtr,
				cchTextMax = MaxText,
				iItem = index,
			};

			sharedLvItem.SetValue(item);
			int nChars = (int)User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_GETITEMTEXT,
			                                     (IntPtr)index, sharedLvItem.RemotePtr);

			// TODO: pszText could have changed as per the documentation, read from it instead.
			return sharedText.GetValue(nChars);
		}

		private (int x, int y) GetItemPosition(int index) {
			if (User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_GETITEMPOSITION, (IntPtr)index,
			                       sharedPoint.RemotePtr) == IntPtr.Zero)
				throw new Exception("Failed to get item pos");

			POINT point = sharedPoint.GetValue();
			return (point.x, point.y);
		}

		private void SetItemPosition(int index, (int x, int y) pos) {
			POINT point = new() {x = pos.x, y = pos.y};
			sharedPoint.SetValue(point);

			User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_SETITEMPOSITION32, (IntPtr)index,
			                   sharedPoint.RemotePtr);
		}

		// private static string ZeroTerminatedToString(ReadOnlySpan<char> buffer) {
		// 	return buffer[..buffer.IndexOf('\0')].ToString();
		// }

		public void Dispose() {
			sharedPoint.Dispose();
			sharedText.Dispose();
			sharedLvItem.Dispose();
			hExplorerProcess.Dispose();
		}
	}

}