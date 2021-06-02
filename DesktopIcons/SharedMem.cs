using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace DesktopIcons {
	/// <summary>
	/// Represents a block of shared memory between two processes.
	/// </summary>
	public abstract class SharedMem : IDisposable {
		protected readonly Kernel32.SafeHPROCESS hProcess;
		protected readonly int bufferSize;
		protected IntPtr remotePtr;
		protected IntPtr localPtr;
		protected bool isDisposed;

		protected SharedMem(Kernel32.SafeHPROCESS hProcess, int bufferSize) {
			this.hProcess = hProcess;
			this.bufferSize = bufferSize;

			// Allocate remote and local buffers for the data
			remotePtr = Kernel32.VirtualAllocEx(hProcess, IntPtr.Zero, bufferSize,
			                                    Kernel32.MEM_ALLOCATION_TYPE.MEM_COMMIT,
			                                    Kernel32.MEM_PROTECTION.PAGE_READWRITE);

			if (remotePtr == IntPtr.Zero)
				throw new Exception("Failed to alloc remote mem");

			localPtr = Marshal.AllocHGlobal(bufferSize);
		}

		public IntPtr RemotePtr => remotePtr;

		protected void ReadRemoteMemory(IntPtr buffer, int size) {
			if (!Kernel32.ReadProcessMemory(hProcess, remotePtr, buffer, size, out _))
				throw new Exception("Failed to read external process memory");
		}

		protected void WriteRemoteMemory(IntPtr buffer, int size) {
			if (!Kernel32.WriteProcessMemory(hProcess, remotePtr, buffer, size, out _))
				throw new Exception("Failed to write external process memory");
		}

		protected void CheckDisposed() {
			if (isDisposed)
				throw new ObjectDisposedException(nameof(SharedMem));
		}

		public void Dispose() {
			CheckDisposed();

			if (localPtr != IntPtr.Zero) {
				Marshal.FreeHGlobal(localPtr);
				localPtr = IntPtr.Zero;
			}

			if (remotePtr != IntPtr.Zero) {
				Kernel32.VirtualFreeEx(hProcess, remotePtr, 0, Kernel32.MEM_ALLOCATION_TYPE.MEM_RELEASE);
				remotePtr = IntPtr.Zero;
			}

			GC.SuppressFinalize(this);
		}

		~SharedMem() {
			if (!isDisposed)
				Dispose();
		}
	}

	/// <summary>
	/// Represents a block of shared memory between two processes.
	/// </summary>
	public class SharedMem<T> : SharedMem {
		public SharedMem(Kernel32.SafeHPROCESS hProcess) : base(hProcess, Marshal.SizeOf<T>()) { }

		public void SetValue(T value) {
			CheckDisposed();

			// Marshal the data into our local buffer
			Marshal.StructureToPtr(value, localPtr, false);

			// Copy the data into the external process
			WriteRemoteMemory(localPtr, bufferSize);
		}

		public T GetValue() {
			CheckDisposed();

			// Read the data into our local buffer
			ReadRemoteMemory(localPtr, bufferSize);

			// Marshal the data from our local buffer into managed memory
			return Marshal.PtrToStructure<T>(localPtr);
		}
	}

	/// <summary>
	/// Represents a string shared between two processes.
	/// </summary>
	public class SharedString : SharedMem {
		private readonly int bufferLengthInChars;

		public SharedString(Kernel32.SafeHPROCESS hProcess, int lengthInChars) : base(hProcess, lengthInChars * sizeof(char)) {
			bufferLengthInChars = lengthInChars;
		}

		public void SetValue(string value) {
			if (value.Length + 1 > bufferLengthInChars)
				throw new ArgumentException("String is to large to fit in buffer");

			// Copy the string bytes into a local buffer (including the null terminator)
			IntPtr localStrPtr = Marshal.StringToHGlobalUni(value);
			try {
				// Copy the contents of the local buffer into the remote buffer (+ 1 to include the null terminator)
				WriteRemoteMemory(localStrPtr, (value.Length + 1) * sizeof(char));
			}
			finally {
				Marshal.FreeHGlobal(localStrPtr);
			}
		}

		public string GetValue() {
			// Read the string data from the remote buffer into the local buffer and construct a string from it
			ReadRemoteMemory(localPtr, bufferSize);
			return Marshal.PtrToStringUni(localPtr);
		}

		public string GetValue(int length) {
			if (length < 0)
				throw new ArgumentException("Length can't be negative");

			if (length == 0)
				return string.Empty;

			int requestedSize = length * sizeof(char); // size of the string data
			if (requestedSize > bufferSize)
				throw new ArgumentException("Requested length exceeds the buffer size");

			// Read the string data from the remote buffer into the local buffer and construct a string from it
			ReadRemoteMemory(localPtr, requestedSize);
			return Marshal.PtrToStringUni(localPtr, length);
		}
	}
}