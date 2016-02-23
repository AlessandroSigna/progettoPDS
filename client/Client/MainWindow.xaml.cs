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
        public ClientLogic clientLogic;
        public MainWindow()
        {
            InitializeComponent();
            Console.Out.WriteLine("MainWindow: Costruttore ");
            this.Left = 0;// SystemParameters.PrimaryScreenWidth - this.Width;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 40;
            MainControl main = new MainControl();
            App.Current.MainWindow.Content = main;
            MyNotifyIcon = new System.Windows.Forms.NotifyIcon();
            MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/imageres_1040.ico");
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
            if (this.WindowState == WindowState.Minimized && !(App.Current.MainWindow.Content is LoginControl) && !(App.Current.MainWindow.Content is MainControl) && !(App.Current.MainWindow.Content is RegistratiControl))
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
         * deve gestire eventuali disconnessioni e logout in base al contenuto di MainWindow
         */
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.Out.WriteLine("MainWindow: Closing");
            var windowContent = App.Current.MainWindow.Content;
            if (windowContent is MainControl)
            {
                //se la finestra corrente è MainControl posso uscire direttmente senza comunicare nulla nè al server nè all'utente
                return;
            }

            //altrimenti prima chiedo conferma e poi disconnetto e 
            MessageBoxResult result = System.Windows.MessageBox.Show("Sicuro di volere uscire?", "Chiusura", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (windowContent is MenuControl)
                {
                    //si delega l'uscita al controllore stesso perché potrebbero essere in corso backup
                    ((MenuControl)windowContent).RichiediChiusura();
                }
                else
                {
                    //gli altri casi non richiedono controlli speciali. delego il tutto a DisconnettiServer
                    clientLogic.DisconnettiServer(true);
                }
            }

            e.Cancel = true;    //cancello questo evento. Sarà la DisconnettiServer o MenuControl a richiamare la Close in modo efficace dopo aver effettuato la disconnessione pulita
            return;

        }


        public void restart(bool error, string messaggio = null)
        {
            Console.WriteLine("Restart");
            if (clientLogic.timer != null)
            {
                clientLogic.timer.Dispose();
                clientLogic.timer = null;
            }
            MainControl main = new MainControl();
            App.Current.MainWindow.Content = main;
            App.Current.MainWindow.Title = "MyCloud";

            if (error)
                main.messaggioErrore(messaggio);
        }
        #endregion
    }
}
