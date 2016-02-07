using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls.Dialogs;
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
    /// Logica di interazione per RegistratiControl.xaml
    /// </summary>
    public partial class RegistratiControl : UserControl
    {
        string mess;

        public RegistratiControl(string message)
        {
            InitializeComponent();
            App.Current.MainWindow.Title = "Registrazione";
            //((MainWindow)App.Current.MainWindow).IsCloseButtonEnabled = true;

            //mess = message;
            //if (mess != null)
            //{
            //    messaggioErrore(mess);
            //}
            //mess = null;
            //deve essere la finestra a customizzare l'evento per il click sul back button
            //perché è la finestra stessa a sapere quale è la finestra precedente
            BackButtonControl.BackButton.Click += Back_Click;
        }

        //private async void messaggioErrore(string mess)
        //{
        //    MetroWindow mw = (MetroWindow)App.Current.MainWindow;
        //    await mw.ShowMessageAsync("Errore", mess);
        //}

        #region Button Registrati
        private void Registrati_Click(object sender, RoutedEventArgs e)
        {

            //MainWindow mw = (MainWindow)App.Current.MainWindow;
            //mw.clientLogic.Registrati(Username.Text, Password.Password);

            // Serve un if else per gestire la risposta del server, analizzata da clientLogic,
            // ma passata e usata qui con un valore di return per decidere quale sarà la prossima finestra
            //if (message.Contains(OK))
            //{
            MenuControl main = new MenuControl();
            App.Current.MainWindow.Content = main;
            //}
            //else
            //{
            //    RegisterControl main = new RegisterControl(messaggioErrore);
            //    App.Current.MainWindow.Content = main;
            //}

        }

        private void Registrati_MouseEnter(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Registrati.Background = (Brush)bc.ConvertFrom("#99FFFF");
        }

        private void Registrati_MouseLeave(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Registrati.Background = (Brush)bc.ConvertFrom("#33CCFF");

        }
        #endregion

        #region Button Back
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            LoginRegisterControl main = new LoginRegisterControl();
            App.Current.MainWindow.Content = main;
        }
        #endregion

    }
}
