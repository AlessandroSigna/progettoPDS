using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Logica di interazione per LoginControl.xaml
    /// </summary>
    public partial class LoginControl : UserControl
    {
        private static Regex sUserNameAllowedRegEx = new Regex(@"^[a-zA-Z]{1}[a-zA-Z0-9]{3,23}[^.-]$", RegexOptions.Compiled);
        private string mess;

        #region Costruttore ed Errore
        public LoginControl()
        {
            InitializeComponent();
            App.Current.MainWindow.Title = "Login";
        }

        private /*async*/ void messaggioErrore(string mess)
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //await mw.ShowMessageAsync("Errore", mess);
            MessageBoxResult result = System.Windows.MessageBox.Show(mess, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);

        }
        #endregion

        #region Login Button
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            //if (string.IsNullOrEmpty(Username.Text) || !sUserNameAllowedRegEx.IsMatch(Username.Text))  
            // controllo da fare alla fine

            if (Username.Text == null || Username.Text.Equals("") || Password.Password == null || Password.Password.Equals(""))
            {
                Esito_Login(false, "Campi username e/o password vuoti.");
            }
            else if (Username.Text.Contains("+") || Username.Text.Contains("(") || Username.Text.Contains(")") || Username.Text.Contains("{") || Username.Text.Contains("}") || Username.Text.Contains("'"))
            {
                Esito_Login(false, "Lo username contiene uno o piu' caratteri invalidi: + () {} '");
            }
            else if (Password.Password.Contains("+") || Password.Password.Contains("(") || Password.Password.Contains(")") || Password.Password.Contains("{") || Password.Password.Contains("}") || Password.Password.Contains("'"))
            {
                Esito_Login(false, "La password contiene uno o piu' caratteri invalidi: + () {} '");
            }
            else if (Username.Text.Length > 15)
            {
                Esito_Login(false, "La lunghezza dello username deve essere inferiore a 15 caratteri.");
            }
            else if (Password.Password.Length < 5 || Password.Password.Length > 15)
            {
                Esito_Login(false, "La lunghezza della password deve essere compresa tra 5 e 15 caratteri.");
            }
            else
            {
                MainWindow mw = (MainWindow)App.Current.MainWindow;
                mw.clientLogic.Login(Username.Text, Password.Password, this);
            }


            // Serve un if else per gestire la risposta del server, analizzata da clientLogic,
            // ma passata e usata qui con un valore di return per decidere quale sarà la prossima finestra
            //if (message.Contains(OK))
            //{
                //MenuControl main = new MenuControl();
                //App.Current.MainWindow.Content = main;
            //}
            //else
            //{
            //LoginControl main = new LoginControl(messaggioErrore);
            //App.Current.MainWindow.Content = main;
            //}

        }

        public void Esito_Login(bool esito, string messaggio = null)
        {
            if (esito) {
                MenuControl main = new MenuControl();
                App.Current.MainWindow.Content = main;
            } else {
                messaggioErrore(messaggio);
            }
        }

        private void Login_MouseEnter(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Login.Background = (Brush)bc.ConvertFrom("#99FFFF");

        }

        private void Login_MouseLeave(object sender, MouseEventArgs e)
        {
            //BrushConverter bc = new BrushConverter();
            //Login.Background = (Brush)bc.ConvertFrom("#33CCFF");

        }


        private void NuovoUtente_Click(object sender, RoutedEventArgs e)
        {
            RegistratiControl main = new RegistratiControl();
            App.Current.MainWindow.Content = main;
        }
        #endregion
    }
}
