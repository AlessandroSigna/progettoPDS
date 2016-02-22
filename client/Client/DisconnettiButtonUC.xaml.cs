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

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {

            //devo disconnettermi dal server ma prima evnetualmente devo sloggare
            //delego la decisione a ClientLogic piochè conosce lo stato della connessione
            //avverto l'utente
            MessageBoxResult result = System.Windows.MessageBox.Show("Verrai disconnesso dal server.\nProcedere?", "Disconnessione", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                //prima di chiamare la ClientLogic.DisconnettiServer occorrerebbe attendere e/o interrompere eventuali operazioni in corso di backup o restore
                //vedere vecchia implementazione su MenuControl.ButtonServerOnClick

                MainWindow mw = (MainWindow)App.Current.MainWindow;
                var windowContent = App.Current.MainWindow.Content;
                if (windowContent is MenuControl)
                {
                    //si delega la disconnessione al controllore stesso perché potrebbero essere in corso backup
                    ((MenuControl)windowContent).RichiediDisconnessione();
                }
                else
                {
                    //gli altri casi non richiedono controlli speciali. delego il tutto a DisconnettiServer
                    mw.clientLogic.DisconnettiServer(false);
                }
            }

        }

        private void Disconnect_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            sfondoImageDisconnect.Visibility = Visibility.Visible;
        }

        private void Disconnect_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            sfondoImageDisconnect.Visibility = Visibility.Hidden;
        }
    }
}
