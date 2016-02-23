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

        #region Costruttore ed Errore
        public LoginControl()
        {
            InitializeComponent();
            App.Current.MainWindow.Title = "Login";
        }

        private void messaggioErrore(string mess)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show(mess, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);

        }
        #endregion

        #region Controlli username e password
        private Boolean IsValidUsername(String username)
        {
            if (username == null || username.Equals("") )
            {
                mostraErroreUsername("Campo username vuoto.");
                return false;
            }
            else if (Username.Text.Contains(">") || Username.Text.Contains("<") || Username.Text.Contains("(") || Username.Text.Contains(")") || Username.Text.Contains("{") || Username.Text.Contains("}") || Username.Text.Contains("'"))
            {
                mostraErroreUsername("Lo username contiene uno o piu' caratteri invalidi: <> () {} '");
                return false;
            }
            else if (Username.Text.Length > 15)
            {
                mostraErroreUsername("La lunghezza dello username deve essere inferiore a 15 caratteri.");
                return false;
            }
            else
            {
                BrushConverter bc = new BrushConverter();
                Username.BorderBrush = (Brush)bc.ConvertFrom("#FFABADB3");
                Username.BorderThickness = new Thickness(1);
                return true; 
            }
        }

        private Boolean IsValidPassword(String password)
        {
            if (password == null || password.Equals("") || password.Length < 5 || password.Length > 15)
            {
                mostraErrorePassword("La lunghezza della password deve essere compresa tra 5 e 15 caratteri.");
                return false;
            }
            else if (password.Contains(">") || password.Contains("(") || password.Contains(")") || password.Contains("{") || password.Contains("}") || password.Contains("'"))
            {
                mostraErrorePassword("La password contiene uno o piu' caratteri invalidi: + () {} '");
                return false;
            }
            else
            {
                BrushConverter bc = new BrushConverter();
                Password.BorderBrush = (Brush)bc.ConvertFrom("#FFABADB3");
                Password.BorderThickness = new Thickness(1);
                return true;
            }
        }

        private void mostraErroreUsername(String errore)
        {

            Username.BorderBrush = Brushes.Red;
            Username.BorderThickness = new Thickness(1);
            erroreUsername.Content = errore;
            erroreUsername.Visibility = Visibility.Visible;
        }

        private void mostraErrorePassword(String errore)
        {

            Password.BorderBrush = Brushes.Red;
            Password.BorderThickness = new Thickness(1);
            errorePassword.Content = errore;
            errorePassword.Visibility = Visibility.Visible;
        }

        private void Username_GotFocus(object sender, RoutedEventArgs e)
        {
            erroreUsername.Visibility = Visibility.Hidden;
        }

        private void Username_LostFocus(object sender, RoutedEventArgs e)
        {
            IsValidUsername(Username.Text);
        }

        private void Password_GotFocus(object sender, RoutedEventArgs e)
        {
            errorePassword.Visibility = Visibility.Hidden;
        }

        private void Password_LostFocus(object sender, RoutedEventArgs e)
        {
            IsValidPassword(Password.Password);
        }
        #endregion


        #region Login Button
        private void Login_Click(object sender, RoutedEventArgs e)
        {

            string username = Username.Text;
            string password = Password.Password;
            Boolean usernameIsValid = IsValidUsername(username);
            Boolean passwordIsValid = IsValidPassword(password);

            if (usernameIsValid && passwordIsValid)
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
                mostraErroreUsername("");
                mostraErrorePassword(messaggio);
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
