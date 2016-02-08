using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
    /// Logica di interazione per UserControl1.xaml
    /// </summary>
    public partial class LoginRegisterControl : UserControl
    {
        #region Button Login
        public LoginRegisterControl()
        {
            InitializeComponent();
            App.Current.MainWindow.Title = "MyCloud - Autenticazione";
            //((MainWindow)App.Current.MainWindow).IsCloseButtonEnabled = true; ?
        }


        private void Login_Click(object sender, RoutedEventArgs e)
        {
            LoginControl main = new LoginControl();
            App.Current.MainWindow.Content = main;
        }


        private void Login_MouseEnter(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Login.Background = (Brush)bc.ConvertFrom("#F5FFFA");

        }

        private void Login_MouseLeave(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Login.Background = (Brush)bc.ConvertFrom("#FF44E572");

        }
        #endregion

        #region Button Registrati
        private void Registrati_Click(object sender, RoutedEventArgs e)
        {
            RegistratiControl main = new RegistratiControl(null);
            App.Current.MainWindow.Content = main;
        }

        private void Registrati_MouseEnter(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Registrati.Background = (Brush)bc.ConvertFrom("#FFFACD");

        }

        private void Registrati_MouseLeave(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Registrati.Background = (Brush)bc.ConvertFrom("#FFF5F804");
        }


        #endregion

        #region Button Back
        private void Back_Click(object sender, RoutedEventArgs e)
        {
        //    MainWindow mw = (MainWindow)App.Current.MainWindow;
        //    mw.clientLogic.DisconnettiServer(false);
        }

        private void Back_MouseEnter(object sender, MouseEventArgs e)
        {
            //backImage.BeginInit();
            //backImage.Source = new BitmapImage(new Uri(@"Images/backLight.png", UriKind.RelativeOrAbsolute));
            //backImage.EndInit();
        }

        private void Back_MouseLeave(object sender, MouseEventArgs e)
        {
            //backImage.BeginInit();
            //backImage.Source = new BitmapImage(new Uri(@"Images/back.png", UriKind.RelativeOrAbsolute));
            //backImage.EndInit();
        }
        #endregion

    }
}
