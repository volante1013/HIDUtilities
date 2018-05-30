using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

	public static class KeyHook
	{
		private delegate int KeyHookCallback(int nCode, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern int SetWindowsHookEx(int idHook, KeyHookCallback lpfn, IntPtr hInstance, int threadId);

		[DllImport("user32.dll")]
		private static extern bool UnhookWindowsHookEx(int idHook);

		[DllImport("user32.dll")]
		private static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);

		private const int WH_KEYBORAD_LL = 0x0D; // 13

		private struct KeyboardLLHookStruct
		{
			public int vkCode;
			public int scanCode;
			public int flags;
			public int time;
			public IntPtr dwExtraInfo;
		}

		private static int hHook = 0;

		//このイベントにコールバックさせたい関数を入れて、アンマネージドコードにイベントを渡してやることで
		//GCに回収されることがなくなり、CallbackOnCollecterDelegateが発生しない
		private static event KeyHookCallback hookCallback;

		// キーが押されたイベントを発行するeventとdelegate
		public delegate void HookHandler(InputKey inputKey);
		public static event HookHandler hookEvent;

		private static bool IsCancel = false;
		public static void Cancel() => IsCancel = true;

		public static void Start()
		{
			IntPtr handle = Marshal.GetHINSTANCE(typeof(KeyHook).Assembly.GetModules()[0]);
			hookCallback = KeyHookProc;
			hHook = SetWindowsHookEx(WH_KEYBORAD_LL, hookCallback, handle, 0);
			if(hHook == 0)
			{
				throw new Exception("Failed to SetWindowHookEx.");
			}
		}

		public static void Stop()
		{
			if(hHook != 0)
			{
				bool ret = UnhookWindowsHookEx(hHook);
				if (!ret)
				{
					throw new Exception("Failed to UnhookWindowsHookEx");
				}

				hHook = 0;
			}
		}

		private static int KeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
		{
			var MyHookStruct = (KeyboardLLHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardLLHookStruct));
			if(nCode == 0)
			{
				var inputKey = new InputKey(MyHookStruct.vkCode, (InputKey.InputType)wParam);

				hookEvent?.Invoke(inputKey);

				if (IsCancel)
				{
					IsCancel = false;
					return 1;
				}
			}

			return CallNextHookEx(hHook, nCode, wParam, lParam);
		}
	}
}
