using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static System.Windows.Forms.NotifyIcon MyNotifyIcon;
        private CustomDialog _customDialog;
        private Esci _exitwindow;
        private Boolean closing;
        private CustomDialog customDialog;
        private Disconnetti disconnettiWindow;
        public ClientLogic clientLogic;
        private MenuControl menuContr;
        public MainWindow()
        {
            InitializeComponent();
            Console.Out.WriteLine("MainWindow: Costruttore ");
            closing = false;
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 40;
            MainControl main = new MainControl();
            App.Current.MainWindow.Content = main;
            MyNotifyIcon = new System.Windows.Forms.NotifyIcon();
            MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/uploadicon.ico");
            MyNotifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(MyNotifyIcon_MouseClick);
        }

        void MyNotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Console.Out.WriteLine("MainWindow: MouseClick ");
            this.WindowState = WindowState.Normal;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            Console.Out.WriteLine("MainWindow: StateChanged");
            if (this.WindowState == WindowState.Minimized && !(App.Current.MainWindow.Content is LoginControl) && !(App.Current.MainWindow.Content is Disconnetti) && !(App.Current.MainWindow.Content is MainControl) && !(App.Current.MainWindow.Content is RegistratiControl) && !(App.Current.MainWindow.Content is LoginRegisterControl))
            {
                this.ShowInTaskbar = false;
                MyNotifyIcon.Visible = true;
            }
            else if (this.WindowState == WindowState.Normal)
            {
                MyNotifyIcon.Visible = false;
                this.ShowInTaskbar = true;
            }
        }

        #region Chiusura finestra e richiesta conferma
        /*
         * handler della chiusura della window
         * deve gestire eventuali disconnessioni e logout
         */
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.Out.WriteLine("MainWindow: Closing");
            if (closing)
            {
                clientLogic.DisconnettiServer(true);
                e.Cancel = false;
                return;
            }

            else if ((App.Current.MainWindow.Content is LoginControl) || (App.Current.MainWindow.Content is LoginRegisterControl) || (App.Current.MainWindow.Content is RegistratiControl))
            {
                e.Cancel = true;
                messaggioDisconnetti();
                return;
            }
            else if ((App.Current.MainWindow.Content is MenuControl))
            {
                menuContr = (MenuControl)App.Current.MainWindow.Content;
                e.Cancel = true;
                DialogDisconnetti();
                return;

            }
            else if ((App.Current.MainWindow.Content is Disconnetti))
            {
                e.Cancel = true;
            }
            return;
        }

        private /*async*/ void messaggioDisconnetti()
        {
            Console.Out.WriteLine("MainWindow: messaggioDisconnetti");
            _customDialog = new CustomDialog();
            _exitwindow = new Esci();
            _exitwindow.BOk.Click += ButtonOkOnClick;
            _exitwindow.BCancel.Click += ButtonCancelOnClick;
            _customDialog.Content = _exitwindow;
            //await this.ShowMetroDialogAsync(_customDialog);
        }

        public void restart(bool error)
        {
            MainControl main = new MainControl();
            App.Current.MainWindow.Content = main;

            if (error)
                main.messaggioErrore();
        }

        private void ButtonOkOnClick(object sender, RoutedEventArgs e)
        {
            Console.Out.WriteLine("MainWindow: ButtonOnClick ");
            //this.HideMetroDialogAsync(_customDialog);
            this.Hide();
            closing = true;
            this.Close();
        }

        private void ButtonCancelOnClick(object sender, RoutedEventArgs e)
        {
            Console.Out.WriteLine("MainWindow: ButtonCancelOnClick");
            //this.HideMetroDialogAsync(_customDialog);
            this.Hide();
        }

        public /*async*/ void DialogDisconnetti()
        {
            Console.Out.WriteLine("MainWindow: DialogDisconnetti");
            customDialog = new CustomDialog();
            disconnettiWindow = new Disconnetti();
            disconnettiWindow.BServer.Click += ButtonServerOnClick;
            disconnettiWindow.BCancel.Click += ButtonCancellaOnClick;
            customDialog.Content = disconnettiWindow;
            Window mw = (Window)App.Current.MainWindow;
            //await mw.ShowMetroDialogAsync(customDialog);
        }

        private void ButtonServerOnClick(object sender, RoutedEventArgs e)
        {
            Console.Out.WriteLine("MainWindow: ButtonServerOnClick");
            Window mw = (Window)App.Current.MainWindow;
            //mw.HideMetroDialogAsync(customDialog);
            mw.Hide();
            MainWindow mainw = (MainWindow)mw;

            menuContr.exit = true;
            if (mainw.clientLogic.lavorandoInvio || menuContr.updating)
                menuContr.EffettuaBackup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            else
                mainw.clientLogic.DisconnettiServer(false);

        }

        private void ButtonCancellaOnClick(object sender, RoutedEventArgs e)
        {
            Console.Out.WriteLine("MainWindow: ButtonCancellaOnClick");
            Window mw = (Window)App.Current.MainWindow;
            //mw.HideMetroDialogAsync(customDialog);
            mw.Hide();
        }
        
        #endregion
    }
}
