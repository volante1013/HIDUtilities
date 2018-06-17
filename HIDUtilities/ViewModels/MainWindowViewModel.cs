using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

using Livet;
using Livet.Commands;
using Livet.Messaging;
using Livet.Messaging.IO;
using Livet.EventListeners;
using Livet.Messaging.Windows;

using HIDUtilities.Models;
using HIDUtilities.Views;
using System.Windows.Forms;

namespace HIDUtilities.ViewModels
{
	public class MainWindowViewModel : ViewModel
	{
		/* コマンド、プロパティの定義にはそれぞれ 
         * 
         *  lvcom   : ViewModelCommand
         *  lvcomn  : ViewModelCommand(CanExecute無)
         *  llcom   : ListenerCommand(パラメータ有のコマンド)
         *  llcomn  : ListenerCommand(パラメータ有のコマンド・CanExecute無)
         *  lprop   : 変更通知プロパティ(.NET4.5ではlpropn)
         *  
         * を使用してください。
         * 
         * Modelが十分にリッチであるならコマンドにこだわる必要はありません。
         * View側のコードビハインドを使用しないMVVMパターンの実装を行う場合でも、ViewModelにメソッドを定義し、
         * LivetCallMethodActionなどから直接メソッドを呼び出してください。
         * 
         * ViewModelのコマンドを呼び出せるLivetのすべてのビヘイビア・トリガー・アクションは
         * 同様に直接ViewModelのメソッドを呼び出し可能です。
         */

		/* ViewModelからViewを操作したい場合は、View側のコードビハインド無で処理を行いたい場合は
         * Messengerプロパティからメッセージ(各種InteractionMessage)を発信する事を検討してください。
         */

		/* Modelからの変更通知などの各種イベントを受け取る場合は、PropertyChangedEventListenerや
         * CollectionChangedEventListenerを使うと便利です。各種ListenerはViewModelに定義されている
         * CompositeDisposableプロパティ(LivetCompositeDisposable型)に格納しておく事でイベント解放を容易に行えます。
         * 
         * ReactiveExtensionsなどを併用する場合は、ReactiveExtensionsのCompositeDisposableを
         * ViewModelのCompositeDisposableプロパティに格納しておくのを推奨します。
         * 
         * LivetのWindowテンプレートではViewのウィンドウが閉じる際にDataContextDisposeActionが動作するようになっており、
         * ViewModelのDisposeが呼ばれCompositeDisposableプロパティに格納されたすべてのIDisposable型のインスタンスが解放されます。
         * 
         * ViewModelを使いまわしたい時などは、ViewからDataContextDisposeActionを取り除くか、発動のタイミングをずらす事で対応可能です。
         */

		/* UIDispatcherを操作する場合は、DispatcherHelperのメソッドを操作してください。
         * UIDispatcher自体はApp.xaml.csでインスタンスを確保してあります。
         * 
         * LivetのViewModelではプロパティ変更通知(RaisePropertyChanged)やDispatcherCollectionを使ったコレクション変更通知は
         * 自動的にUIDispatcher上での通知に変換されます。変更通知に際してUIDispatcherを操作する必要はありません。
         */

		#region CanClose変更通知プロパティ
		private bool _CanClose;

		public bool CanClose
		{
			get { return _CanClose; }
			set
			{ 
				if (_CanClose == value)	return;

				_CanClose = value;
				RaisePropertyChanged();
			}
		}
		#endregion


		#region KeyName変更通知プロパティ
		private string _KeyName;

		public string KeyName
		{
			get { return _KeyName; }
			set
			{ 
				if (_KeyName == value)	return;

				_KeyName = value;
				RaisePropertyChanged();
			}
		}
		#endregion

		private static MainWindow window;
		private NotifyIconEx notify;

		private uint keyDown = 0;
		private uint keyTrigger = 0;
		private uint keyRelease = 0;

		private bool isKeyManageCanceled = false;

		private bool IsDown(Keys key)	 => (keyDown & (1 << (int)key)) != 0;
		private bool IsTrigger(Keys key) => (keyTrigger & (1 << (int)key)) != 0;
		private bool IsRelease(Keys key) => (keyRelease & (1 << (int)key)) != 0;

		private static readonly IReadOnlyList<Keys> bracketskeys = new List<Keys> { Keys.LShiftKey, Keys.RShiftKey, Keys.D8, Keys.D9 };

		public void Initialize()
		{
			// MainWindowのインスタンス取得
			window = App.Current.MainWindow as MainWindow;

			// NotifyIconExのインスタンス生成
			var iconPath = new Uri("pack://application:,,,/Resources/HIDUtilitiesIcon.ico", UriKind.RelativeOrAbsolute);
			var menu = window.FindResource("contextmenu") as System.Windows.Controls.ContextMenu;
			notify = new NotifyIconEx(iconPath, "HID Utilities", menu);
			notify.DoubleClick += (_, __) => ShowWindow();

			// ウィンドウを非表示に
			window.Hide();

			// キーフックの設定
			setupKeyHook();
		}

		/// <summary>
		/// ウィンドウの閉じるボタンを押されたときに呼ばれる
		/// ここでウィンドウが閉じるのをキャンセルする
		/// </summary>
		public void CloseCanceledCallback()
		{
			CanClose = false;
			window.Hide();
		}

		/// <summary>
		/// ウィンドウの表示
		/// </summary>
		public void ShowWindow()
		{
			window.Show();
			window.Activate();
			window.ShowInTaskbar = true;
		}

		/// <summary>
		/// アプリの終了
		/// </summary>
		public void Exit()
		{
			App.Current.Shutdown();
		}

		/// <summary>
		/// ウィンドウが閉じたタイミングで呼ばれる
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			notify.Dispose();
		}

		private void setupKeyHook()
		{
			KeyHook.hookEvent += ManageKeyEvent;

			// キーフック開始
			KeyHook.Start();
		}

		/// <summary>
		/// 入力されたキーの状態を管理する
		/// </summary>
		/// <param name="state">入力されたキー情報</param>
		private void ManageKeyEvent(in KeyHook.StateKey state)
		{
			if (isKeyManageCanceled)
			{
				return;
			}

			// 入力されたキーの状態を管理
			uint oldKeyDown = keyDown;
			switch (state.Stroke)
			{
				case KeyHook.Stroke.KEY_DOWN:
				case KeyHook.Stroke.SYSKEY_DOWN:
					keyDown |= (uint)(1 << (int)state.Key);
					break;

				case KeyHook.Stroke.KEY_UP:
				case KeyHook.Stroke.SYSKEY_UP:
					keyDown ^= (uint)(1 << (int)state.Key);
					break;

				case KeyHook.Stroke.UNKNOWN:
				default:
					return;
			}
			keyTrigger = keyDown & ~oldKeyDown;
			keyRelease = ~keyDown & oldKeyDown;

			// 押されたキーの種類を表示
			KeyName = state.Key.ToString();

			// かっこの補完
			AutoCompleteBrackets();

			// 特定のキーに応じて特定の操作を実行
			// TODO: アプリ側から変更できるようにする
			if (state.Key == Keys.Capital)
			{
				KeyHook.Cancel();
			}
			else if (state.Key == Keys.Insert)
			{
				KeyHook.Cancel();
			}
		}

		/// <summary>
		/// かっこの入力を補完する
		/// </summary>
		/// <param name="inputKey"></param>
		private void AutoCompleteBrackets()
		{
			
		}
	}
}
