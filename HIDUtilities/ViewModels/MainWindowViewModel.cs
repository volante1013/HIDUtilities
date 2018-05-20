﻿using System;
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


		private static MainWindow window;
		private NotifyIconEx notify;

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
				if (_KeyName == value)
					return;
				_KeyName = value;
				RaisePropertyChanged();
			}
		}
		#endregion


		public void Initialize()
		{
			window = App.Current.MainWindow as MainWindow;

			var iconPath = new Uri("pack://application:,,,/Resources/HIDUtilitiesIcon.ico", UriKind.RelativeOrAbsolute);
			var menu = window.FindResource("contextmenu") as ContextMenu;
			notify = new NotifyIconEx(iconPath, "HID Utilities", menu);
			notify.DoubleClick += (_, __) => ShowWindow();

			window.Hide();

			setupKeyHook();
		}

		private void setupKeyHook()
		{
			// 押されたキーの種類を表示
			KeyHook.hookEvent += (inputKey) => KeyName = KeyInterop.KeyFromVirtualKey(inputKey.key).ToString();

			// 特定のキーに応じて特定の操作を実行
			// TODO: アプリ側から変更できるようにする
			KeyHook.hookEvent += (inputKey) =>
			{
				var key = KeyInterop.KeyFromVirtualKey(inputKey.key);
				if(key == Key.Capital)
				{
					KeyHook.Cancel();
				}
				else if(key == Key.Insert)
				{
					KeyHook.Cancel();
				}
			};

			// キーフック開始
			KeyHook.Start();
		}

		public void CloseCanceledCallback()
		{
			CanClose = false;
			window.Hide();
		}

		public void ShowWindow()
		{
			window.Show();
			window.Activate();
			window.ShowInTaskbar = true;
		}

		public void Exit()
		{
			App.Current.Shutdown();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			notify.Dispose();
		}
	}
}
