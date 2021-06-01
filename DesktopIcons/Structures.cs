using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace DesktopIcons {
	[StructLayout(LayoutKind.Sequential)]
	public struct LVITEM {
		public ComCtl32.ListViewItemMask mask;
		public int iItem;
		public int iSubItem;
		public uint state;
		public uint stateMask;
		public IntPtr pszText;
		public uint cchTextMax;
		public int iImage;
		public IntPtr lParam;
		public int iIndent;
		public int iGroupId;
		public uint cColumns;
		public IntPtr puColumns;
		public IntPtr piColFmt;
		public int iGroup;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct POINT {
		public int x;
		public int y;
	}
}