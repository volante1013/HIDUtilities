using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HIDUtilities.Models
{
	/*
	 参照 ： https://qiita.com/exliko/items/3135e4413a6da067b35d
	 */
	public static class MouseHook
	{
		#region NativeMethod
		[DllImport("user32.dll")]
		private static extern IntPtr SetWindowsHookEx(int idHook, MouseHookCallback lpfn, IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr hhk);

		[DllImport("user32.dll")]
		private static extern int CallNextHookEx(IntPtr hhk, int nCode, uint msg, ref MSLLHOOKSTRUCT msllhookstruct);
		#endregion

		#region 構造体
		public struct StateMouse
		{
			public Stroke stroke;
			public int X;
			public int Y;
			public uint Data;
			public uint Flags;
			public uint Time;
			public IntPtr ExtraInfo;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct POINT
		{
			public int x;
			public int y;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MSLLHOOKSTRUCT
		{
			public POINT pt;
			public uint mouseData;
			public uint flags;
			public uint time;
			public IntPtr dwExtraInfo;
		}
		#endregion

		public enum Stroke
		{
			MOVE,
			LEFT_DOWN,
			LEFT_UP,
			RIGHT_DOWN,
			RIGHT_UP,
			MIDDLE_DOWN,
			MIDDLE_UP,
			WHEEL_DOWN,
			WHEEL_UP,
			X1_DOWN,
			X1_UP,
			X2_DOWN,
			X2_UP,
			UNKNOWN,
		}

		public static StateMouse state;

		private static IntPtr hHook = IntPtr.Zero;

		private delegate int MouseHookCallback(int nCode, uint msg, ref MSLLHOOKSTRUCT msllhookstruct);
		private static event MouseHookCallback hookCallback;

		public delegate void HookHandler(StateMouse state);
		private static HookHandler hookHandler;
		public static event HookHandler hookEvent
		{
			add
			{
				// hookHandlerがNullでない かつ valueが含まれているときはaddしない
				if (hookHandler?.GetInvocationList().Contains(value) ?? false)
					return;

				hookHandler += value;
			}
			remove
			{
				// hookHandlerがNull または valueが含まれていないときはremoveしない
				if (!hookHandler?.GetInvocationList().Contains(value) ?? true)
					return;

				hookHandler -= value;
			}
		}

		private static bool IsCancel = false;
		public static void Cancel() => IsCancel = true;

		private const int WH_MOUSE_LL = 0x0E; // 14

		public static void Start()
		{
			IntPtr handle = Marshal.GetHINSTANCE(typeof(MouseHook).Assembly.GetModules()[0]);
			hookCallback = MouseHookProc;
			hHook = SetWindowsHookEx(WH_MOUSE_LL, hookCallback, handle, 0);
			if(hHook == IntPtr.Zero)
			{
				throw new Win32Exception("Failed SetWindowsHookEx.");
			}
		}

		public static void Stop()
		{
			if(hHook != IntPtr.Zero)
			{
				bool ret = UnhookWindowsHookEx(hHook);
				if (!ret)
				{
					throw new Win32Exception("Failed UnhookWindowsHookEx.");
				}
				hHook = IntPtr.Zero;
			}
		}

		private static int MouseHookProc(int nCode, uint msg, ref MSLLHOOKSTRUCT s)
		{
			if(nCode > 0)
			{
				state.stroke = GetStroke(msg, ref s);
				state.X = s.pt.x;
				state.Y = s.pt.y;
				state.Data = s.mouseData;
				state.Flags = s.flags;
				state.Time = s.time;
				state.ExtraInfo = s.dwExtraInfo;

				hookHandler?.Invoke(state);

				if (IsCancel)
				{
					IsCancel = false;
					return 1;
				}
			}

			return CallNextHookEx(hHook, nCode, msg, ref s);
		}

		private static Stroke GetStroke(uint msg, ref MSLLHOOKSTRUCT s)
		{
			switch (msg)
			{
				case 0x0200: // WM_MOUSEMOVE
					return Stroke.MOVE;
				case 0x0201: // WM_LBUTTONDOWN
					return Stroke.LEFT_DOWN;
				case 0x0202: // WM_LBUTTONUP
					return Stroke.LEFT_UP;
				case 0x0204: // WM_RBUTTONDOWN
					return Stroke.RIGHT_DOWN;
				case 0x0205: // WM_RBUTTONUP
					return Stroke.RIGHT_UP;
				case 0x0207: // WM_MBUTTONDOWN
					return Stroke.MIDDLE_DOWN;
				case 0x0208: // WM_MBUTTONUP
					return Stroke.MIDDLE_UP;
				case 0x020A: // WM_MOUSEWHEEL
					return ((short)((s.mouseData >> 16) & 0xffff) > 0) ? Stroke.WHEEL_UP : Stroke.WHEEL_DOWN;
				case 0x020B: // WM_XBUTTONDOWN
					switch (s.mouseData >> 16)
					{
						case 1:
							return Stroke.X1_DOWN;
						case 2:
							return Stroke.X2_DOWN;
						default:
							return Stroke.UNKNOWN;
					}
				case 0x020C: // WM_XBUTTONUP
					switch (s.mouseData >> 16)
					{
						case 1:
							return Stroke.X1_UP;
						case 2:
							return Stroke.X2_UP;
						default:
							return Stroke.UNKNOWN;
					}
				default:
					return Stroke.UNKNOWN;
			}
		}
	}
}
