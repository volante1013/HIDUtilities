using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

namespace HIDUtilities.Models
{
	public static class MouseLoop
	{
		private static readonly int BoundaryOffset = 3;

		private static bool UDLoop = false;
		private static bool LRLoop = false;

		private static int ScreenWidthSum = 0;

		private static Point pos = new Point();

		public static void Setup()
		{
			// 全スクリーンの横幅の合計を算出
			ScreenWidthSum = Screen.AllScreens.Select(screen => screen.Bounds.Width).Sum();

			// 初期値セット
			UDLoop = Properties.Settings.Default.UDLoop;
			LRLoop = Properties.Settings.Default.LRLoop;

			Properties.Settings.Default.PropertyChanged += (s, e) =>
			{
				switch (e.PropertyName)
				{
					case nameof(Properties.Settings.Default.UDLoop):
						UDLoop = Properties.Settings.Default.UDLoop;
						break;

					case nameof(Properties.Settings.Default.LRLoop):
						LRLoop = Properties.Settings.Default.LRLoop;
						break;

					default:
						return;
				}

				if(!UDLoop && !LRLoop)
				{
					MouseHook.hookEvent -= Loop;
				}
				else
				{
					MouseHook.hookEvent += Loop;
				}
			};

			if(UDLoop || LRLoop)
			{
				MouseHook.hookEvent += Loop;
			}
		}

		private static void Loop(MouseHook.StateMouse state)
		{
			bool isBoundaryX = false;
			bool isBoundaryY = false;

			pos.X = state.X; pos.Y = state.Y;

			var screen = Screen.FromPoint(pos);

			if (LRLoop)
			{
				if(pos.X >= screen.Bounds.Right - 1 && IsSameScreen(pos, screen, false))
				{
					pos.X -= ScreenWidthSum - BoundaryOffset;
					isBoundaryX = true;
				}
				else if(pos.Y <= screen.Bounds.Left && IsSameScreen(pos, screen, true))
				{
					pos.X += ScreenWidthSum - BoundaryOffset;
					isBoundaryX = true;
				}
			}
			if (UDLoop)
			{
				if(pos.Y >= screen.Bounds.Bottom - 1)
				{
					pos.Y -= screen.Bounds.Height - BoundaryOffset;
					isBoundaryY = true;
				}
				else if(pos.Y <= screen.Bounds.Top)
				{
					pos.Y += screen.Bounds.Height - BoundaryOffset;
					isBoundaryY = true;
				}
			}

			if(isBoundaryX || isBoundaryY)
			{
				var screenAfterLoop = Screen.FromPoint(pos);
				pos.Y = (pos.Y < screenAfterLoop.Bounds.Top) ?
					screenAfterLoop.Bounds.Top - 1 - BoundaryOffset : pos.Y;
				Cursor.Position = pos;
				MouseHook.Cancel();
			}

			pos.X = 0; pos.Y = 0;
		}

		private static bool IsSameScreen(Point pos, Screen screen, bool IsLeft)
		{
			pos.X += 50 * ((IsLeft) ? -1 : 1);
			return screen.Equals(Screen.FromPoint(pos));
		}
	}
}
