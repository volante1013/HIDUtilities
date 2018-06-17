using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HIDUtilities.Models
{
	public class InputKey
	{
		public enum InputType : int
		{
			WH_KEYDOWN = 0x0100,
			WH_KEYUP = 0x0101,
		}

		public int key;
		public InputType input;

		public InputKey(int key, InputType input)
		{
			this.key = key;
			this.input = input;
		}

		public static bool operator == (InputKey i1, InputKey i2)
		{
			// nullチェック

			// 両方null(参照元が同じ)か
			// i1 == i2とすると無限ループ
			if(ReferenceEquals(i1, i2))
			{
				return true;
			}
			// どちらかがnullか
			// i1 == null とすると無限ループ
			if((object)i1 == null || (object)i2 == null)
			{
				return false;
			}

			return (i1.key == i2.key) && (i1.input == i2.input);
		}

		// i1 != i2とすると無限ループ
		public static bool operator !=(InputKey i1, InputKey i2) => !(i1 == i2);

		public override bool Equals(object obj) => this == (InputKey)obj;

		/* 
		 * EqualsがtrueならGetHashCodeも同じ値になる
		 * GetHashCodeが同じでもEqualsがtrueとは限らない
		 * 
		 * 複数のオブジェクトを等価の基準にするときは、それぞれのGetHashCodeをXORした値を使うのが一般的らしい
		 * 参考：https://dobon.net/vb/dotnet/beginner/equals.html
		 */
		public override int GetHashCode() => this.key ^ this.input.GetHashCode();
	}

	/*
	 参照 ： https://qiita.com/exliko/items/3135e4413a6da067b35d
	 */
	public static class KeyHook
	{

		#region NativeMethod
		[DllImport("user32.dll")]
		private static extern IntPtr SetWindowsHookEx(int idHook, KeyHookCallback lpfn, IntPtr hInstance, int threadId);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr handle);

		[DllImport("user32.dll")]
		private static extern IntPtr CallNextHookEx(IntPtr handle, int nCode, uint msg, ref KeyLLHookStruct keyboardLLHookStruct);

		#endregion

		#region 構造体
		public struct StateKey
		{
			public Stroke Stroke;
			public Keys Key;
			public uint ScanCode;
			public uint Flags;
			public uint Time;
			public IntPtr ExtraInfo;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct KeyLLHookStruct
		{
			public uint vkCode;
			public uint scanCode;
			public uint flags;
			public uint time;
			public IntPtr dwExtraInfo;
		}

		#endregion
		public enum Stroke
		{
			KEY_DOWN,
			KEY_UP,
			SYSKEY_DOWN,
			SYSKEY_UP,
			UNKNOWN
		}

		private static StateKey state;

		private static IntPtr hHook = IntPtr.Zero;

		private delegate IntPtr KeyHookCallback(int nCode, uint msg, ref KeyLLHookStruct keyboardLLHookStruct);
		//このイベントにコールバックさせたい関数を入れて、アンマネージドコードにイベントを渡してやることで
		//GCに回収されることがなくなり、CallbackOnCollecterDelegateが発生しない
		private static event KeyHookCallback hookCallback;

		// キーが押されたイベントを発行するeventとdelegate
		public delegate void HookHandler(in StateKey state/*InputKey inputKey*/);
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

		private const int WH_KEYBORAD_LL = 0x0D; // 13

		public static void Start()
		{
			IntPtr handle = Marshal.GetHINSTANCE(typeof(KeyHook).Assembly.GetModules()[0]);
			hookCallback = KeyHookProc;
			hHook = SetWindowsHookEx(WH_KEYBORAD_LL, hookCallback, handle, 0);
			if(hHook == IntPtr.Zero)
			{
				throw new Exception("Failed SetWindowHookEx.");
			}
		}

		public static void Stop()
		{
			if(hHook != IntPtr.Zero)
			{
				bool ret = UnhookWindowsHookEx(hHook);
				if (!ret)
				{
					throw new Exception("Failed UnhookWindowsHookEx");
				}
				hHook = IntPtr.Zero;
			}
		}

		private static IntPtr KeyHookProc(int nCode, uint msg, ref KeyLLHookStruct k)
		{
			if(nCode >= 0)
			{
				//var inputKey = new InputKey((int)k.vkCode, (InputKey.InputType)GetStroke(msg));

				state.Stroke = GetStroke(msg);
				state.Key = (Keys)k.vkCode;
				state.ScanCode = k.scanCode;
				state.Flags = k.flags;
				state.Time = k.time;
				state.ExtraInfo = k.dwExtraInfo;

				hookHandler?.Invoke(in state);

				if (IsCancel)
				{
					IsCancel = false;
					return (IntPtr)1;
				}
			}

			return CallNextHookEx(hHook, nCode, msg, ref k);
		}

		private static Stroke GetStroke(uint msg)
		{
			switch (msg)
			{
				case 0x0100: // WM_KEYDOWN
					return Stroke.KEY_DOWN;
				case 0x0101: // WM_KEYUP
					return Stroke.KEY_UP;
				case 0x0104: // WM_SYSKEYDOWN
					return Stroke.SYSKEY_DOWN;
				case 0x0105: // WM_SYSKEYUP
					return Stroke.SYSKEY_UP;
				default:
					return Stroke.UNKNOWN;
			}
		}
	}
}
