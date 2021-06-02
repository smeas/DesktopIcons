using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using Vanara.PInvoke;

namespace DesktopIcons {
	public class IconManager {
		private const Kernel32.ProcessAccess ProcessAccessRWO = Kernel32.ProcessAccess.PROCESS_VM_READ |
			Kernel32.ProcessAccess.PROCESS_VM_WRITE | Kernel32.ProcessAccess.PROCESS_VM_OPERATION;

		private const int MaxText = 260;

		private Kernel32.SafeHPROCESS hExplorerProcess;
		private HWND hListView;

		private SharedMem<LVITEM> sharedLvItem;
		private SharedString sharedText;
		private SharedMem<POINT> sharedPoint;

		public IconManager() {
			hListView = GetDesktopListViewHandle();
			if (hListView == HWND.NULL)
				throw new Exception("Failed to find the desktop list view");

			// Open the desktop (explorer) process
			User32.GetWindowThreadProcessId(hListView, out uint processId);
			hExplorerProcess = Kernel32.OpenProcess((int)ProcessAccessRWO, false, processId);

			if (hExplorerProcess.IsInvalid)
				throw new Exception("Failed to open process");

			// Allocate memory in the process for communication
			sharedLvItem = new SharedMem<LVITEM>(hExplorerProcess);
			sharedText = new SharedString(hExplorerProcess, MaxText);
			sharedPoint = new SharedMem<POINT>(hExplorerProcess);
		}

		private HWND GetDesktopListViewHandle() {
			// Get the desktop list view from known location relative to the main desktop window
			HWND hWndDesktop = User32.FindWindow("Progman", null);
			HWND hWnd = User32.GetWindow(hWndDesktop, User32.GetWindowCmd.GW_CHILD);
			hWnd = User32.GetWindow(hWnd, User32.GetWindowCmd.GW_CHILD);

			if (hWnd == HWND.NULL)
				return HWND.NULL;

			// Make sure we got the right window
			StringBuilder sb = new();
			sb.EnsureCapacity(16);
			User32.RealGetWindowClass(hWnd, sb, (uint)sb.Capacity);
			if (sb.ToString() != "SysListView32")
				return HWND.NULL;

			return hWnd;
		}

		public int GetItemCount() {
			return (int)User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_GETITEMCOUNT);
		}

		public string GetItemText(int index) {
			LVITEM item = new() {
				mask = ComCtl32.ListViewItemMask.LVIF_TEXT,
				pszText = sharedText.RemotePtr,
				cchTextMax = MaxText,
				iItem = index,
			};

			sharedLvItem.SetValue(item);
			int nChars = (int)User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_GETITEMTEXT,
			                                     (IntPtr)index, sharedLvItem.RemotePtr);

			// Assuming that desktop icons always have a name
			if (nChars == 0)
				throw new Exception("Failed to get item text");

			// TODO: pszText could have changed as per the documentation, read from it instead.
			return sharedText.GetValue(nChars);
		}

		public Point GetItemPosition(int index) {
			if (User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_GETITEMPOSITION, (IntPtr)index,
			                       sharedPoint.RemotePtr) == IntPtr.Zero)
				throw new Exception("Failed to get item pos");

			POINT point = sharedPoint.GetValue();
			return new Point(point.x, point.y);
		}

		public void SetItemPosition(int index, Point pos) {
			POINT point = new() {x = pos.X, y = pos.Y};
			sharedPoint.SetValue(point);

			User32.SendMessage(hListView, (uint)ComCtl32.ListViewMessage.LVM_SETITEMPOSITION32, (IntPtr)index,
			                   sharedPoint.RemotePtr);
		}

		public Dictionary<string, Point> GetDesktopIcons() {
			int itemCount = GetItemCount();
			Dictionary<string, Point> icons = new();

			try {
				for (int i = 0; i < itemCount; i++) {
					string iconName = GetItemText(i);
					Point iconPos = GetItemPosition(i);
					icons.Add(iconName, iconPos);
				}
			}
			catch (ArgumentException) {
				throw new Exception("Duplicate item names");
			}

			return icons;
		}

		public void Dispose() {
			sharedPoint.Dispose();
			sharedText.Dispose();
			sharedLvItem.Dispose();
			hExplorerProcess.Dispose();
		}
	}
}