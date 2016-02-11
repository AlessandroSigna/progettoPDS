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

        private void messaggioerrore(string mess)
        {
            //metrowindow mw = (metrowindow)app.current.mainwindow;
            //await mw.showmessageasync("errore", mess);
            MessageBoxResult result = System.Windows.MessageBox.Show(mess, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #region Button Registrati
        private void Registrati_Click(object sender, RoutedEventArgs e)
        {

            MainWindow mw = (MainWindow)App.Current.MainWindow;

            if (Username.Text == null || Username.Text.Equals("") || Password.Password == null || Password.Password.Equals(""))
            {
                Registrati_Esito(false, "Campi username e/o password vuoti.");
            }
            else if (Username.Text.Contains("+") || Username.Text.Contains("(") || Username.Text.Contains(")") || Username.Text.Contains("{") || Username.Text.Contains("}") || Username.Text.Contains("'"))
            {
                Registrati_Esito(false, "Lo username contiene uno o piu' caratteri invalidi: + () {} '");
            }
            else if (Password.Password.Contains("+") || Password.Password.Contains("(") || Password.Password.Contains(")") || Password.Password.Contains("{") || Password.Password.Contains("}") || Password.Password.Contains("'"))
            {
                Registrati_Esito(false, "La password contiene uno o piu' caratteri invalidi: + () {} '");
            }
            else if (Username.Text.Length > 15)
            {
                Registrati_Esito(false, "La lunghezza dello username deve essere inferiore a 15 caratteri.");
            }
            else if (Password.Password.Length < 5 || Password.Password.Length > 15)
            {
                Registrati_Esito(false, "La lunghezza della password deve essere compresa tra 5 e 15 caratteri.");
            }
            else
            {
                mw.clientLogic.Registrati(Username.Text, Password.Password, this);
            }
            //MenuControl main = new MenuControl();
            //App.Current.MainWindow.Content = main;

        }

        public void Registrati_Esito(bool esito, string messaggio = null)
        {
            // If else per gestire la risposta del server, analizzata da clientLogic,
            // usata qui per decidere quale sarà la prossima finestra

            if (esito) {
                MenuControl main = new MenuControl();
                App.Current.MainWindow.Content = main;
            } else {
                messaggioerrore(messaggio);
            }
        }

        private void Registrati_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Registrati.Background = (Brush)bc.ConvertFrom("#99FFFF");
        }

        private void Registrati_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Registrati.Background = (Brush)bc.ConvertFrom("#33CCFF");

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
