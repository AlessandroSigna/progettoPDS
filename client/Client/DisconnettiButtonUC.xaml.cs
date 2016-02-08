using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Interaction logic for DisconnettiButtonUC.xaml
    /// </summary>
    public partial class DisconnettiButtonUC : UserControl
    {
        public DisconnettiButtonUC()
        {
            InitializeComponent();
        }

        /*
         * Button Logout
         */
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            //DialogDisconnetti();  //serve un avviso per l'utente che si sta disconnettendo dal server? oppure basta un pulsante più chiaro
            //il controllo torna a MainControl
            MainControl main = new MainControl();
            App.Current.MainWindow.Content = main;
        }

        /*
         * Metodo che gestisce un CustomDialog con dipendenza da Metro
         */
        //public async void DialogDisconnetti()
        //{
        //    customDialog = new CustomDialog();
        //    disconnettiWindow = new Disconnetti();
        //    //setto le callback per la pressione dei Button
        //    //forse è bene metterle direttamente in Disconnetti.xaml.cs e gestire la logica opportunamente
        //    disconnettiWindow.BServer.Click += ButtonServerOnClick;
        //    disconnettiWindow.BCancel.Click += ButtonCancelOnClick;
        //    customDialog.Content = disconnettiWindow;
        //    MetroWindow mw = (MetroWindow)App.Current.MainWindow;
        //    await mw.ShowMetroDialogAsync(customDialog);
        //}

        private void Logout_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            disconnectImage.BeginInit();
            disconnectImage.Source = new BitmapImage(new Uri(@"Images/logoutLight.png", UriKind.RelativeOrAbsolute));
            disconnectImage.EndInit();
        }

        private void Logout_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            disconnectImage.BeginInit();
            disconnectImage.Source = new BitmapImage(new Uri(@"Images/logout.png", UriKind.RelativeOrAbsolute));
            disconnectImage.EndInit();
        }
    }
}
