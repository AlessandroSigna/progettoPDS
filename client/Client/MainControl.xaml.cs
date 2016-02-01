using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// Logica di interazione per MainControl.xaml
    /// </summary>
    public partial class MainControl : UserControl
    {
        private ClientLogic client;
        private TcpClient clientsocket;
        private int errore;
        public MainControl(int errorePassed)
        {
            InitializeComponent();
            App.Current.MainWindow.Width = 400;
            App.Current.MainWindow.Height = 400;
            errore = errorePassed;
            if (errore == 1)
            {
                ClientLogic.UpdateNotifyIconDisconnesso();
                messaggioErrore();
            }

        }

        #region Altri Metodi
        private void messaggioErrore()
        {
            //Window mw = (Window)App.Current.MainWindow;
            //await mw.ShowMessageAsync("Errore", "Impossibile raggiungere il server");
            MessageBoxResult result = MessageBox.Show("Impossibile raggiungere il server", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            
        }

        private void showWaitBar()
        {

            Window parentWindow = (Window)this.Parent;
            ProgressRing bar = new ProgressRing();  //FIXME: MetroWindow dependence
            bar.IsActive = true;
            bar.Width = 100;
            bar.Height = 100;
            parentWindow.Content = bar;
        }

        #endregion

        #region Button Connetti
        private void connect_button_Click(object sender, RoutedEventArgs e)
        {
            string ip = IpAddressBox.Text;
            string port = PortBox.Text;
            Boolean IpValid = IsValidIPAddress(ip);
            Boolean PortValid = IsValidPort(port);

            if (IpValid && PortValid)
            {
                showWaitBar();
                clientsocket = new TcpClient(); //FIXME: a che serve qui se poi viene reinstaziato in ClientLogic ??
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                client = new ClientLogic(clientsocket, IPAddress.Parse(ip), int.Parse(port), mw);
                mw.clientLogic = client;
            }
            else
            {
                Console.Out.WriteLine("No addr");
            }
        }

        private void Connect_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Connect.Background = (Brush)bc.ConvertFrom("#F5FFFA");

        }

        private void Connect_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Connect.Background = (Brush)bc.ConvertFrom("#FF44E572");

        }
        #endregion

        #region Controlli indirizzo e porta

        private void PortBox_LostFocus(object sender, RoutedEventArgs e)
        {
            IsValidPort(PortBox.Text);
        }

        private void IpAddressBox_LostFocus(object sender, RoutedEventArgs e)
        {
            IsValidIPAddress(IpAddressBox.Text);
        }
        //Controllo IP valido
        public bool IsValidIPAddress(string addr)
        {
            try
            {
                IPAddress address;
                if (IPAddress.TryParse(addr, out address))
                {
                    IpAddressBox.BorderBrush = Brushes.Green;
                    IpAddressBox.BorderThickness = new Thickness(1);
                    return true;
                }
                else
                {
                    IpAddressBox.BorderBrush = Brushes.Red;
                    IpAddressBox.BorderThickness = new Thickness(2);
                    return false;
                }
            }
            catch
            {
                IpAddressBox.BorderBrush = Brushes.Red;
                IpAddressBox.BorderThickness = new Thickness(2);
                return false;
            }
        }

        //Controllo Porta valida
        public bool IsValidPort(string addr)
        {

            try
            {
                int port = int.Parse(addr);
                if (port > 0 && port < 65536)
                {
                    PortBox.BorderBrush = Brushes.Green;
                    PortBox.BorderThickness = new Thickness(1);
                    return true;
                }
                else
                {
                    PortBox.BorderBrush = Brushes.Red;
                    PortBox.BorderThickness = new Thickness(2);
                    return false;
                }
            }
            catch
            {
                PortBox.BorderBrush = Brushes.Red;
                PortBox.BorderThickness = new Thickness(2);
                return false;
            }

        }
        #endregion


    }
}
