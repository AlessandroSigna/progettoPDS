using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;

namespace Client
{
    /// <summary>
    /// Logica di interazione per MainControl.xaml
    /// </summary>
    public partial class MainControl : UserControl
    {
        private ClientLogic client;
        private TcpClient clientsocket;
        private WaitWindow waitWindow;

        public MainControl()
        {
            InitializeComponent();
            //App.Current.MainWindow.Width = 400;
            //App.Current.MainWindow.Height = 400;

        }

        #region Altri Metodi
        public void messaggioErrore(string errore = null)
        {
            ClientLogic.UpdateNotifyIconDisconnesso();
            if(errore != null)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show(errore, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBoxResult result = System.Windows.MessageBox.Show("Errore durante la comunicazione con il server.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }

        private void showHideWaitBar(bool show)
        {
            if (show)
            {
                waitWindow = new WaitWindow("Connessione in corso...");
                waitWindow.Show();
                contentGrid.IsEnabled = false;
            }
            else
            {
                waitWindow.Close();
                contentGrid.IsEnabled = true;
            }
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
                showHideWaitBar(true);
                clientsocket = new TcpClient();
                MainWindow mw = (MainWindow)App.Current.MainWindow;

                client = new ClientLogic(clientsocket, IPAddress.Parse(ip), int.Parse(port), mw, this);
                mw.clientLogic = client;
            }
            else
            {
                Console.Out.WriteLine("No addr");
            }

        }

        public void Esito_Connect(bool esito)
        {
            if (esito) {
                // Il passaggio a LoginRegisterControl non avviene qui, ma in ClientLogic,
                // dopo che viene stabilita la connessione.
                // Vorrei provare a mettere tutti i passaggi tra le finestre, nella logica delle finestre stesse.
                // (come è già nella maggior parte dei casi)
                // In questo caso a connessione stabilita il ClientLogic deve comunicarlo a MainControl che poi instanzia
                // LoginRegisterControl:

                LoginRegisterControl login = new LoginRegisterControl();
                App.Current.MainWindow.Content = login;
            } else {
                messaggioErrore();
            }
            showHideWaitBar(false);
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
