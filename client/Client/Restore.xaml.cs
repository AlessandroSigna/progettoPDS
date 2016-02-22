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
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Logica di interazione per Restore.xaml
    /// </summary>
    public partial class Restore : /*MetroWindow*/ Window
    {
        private ClientLogic clientlogic;
        public MainWindow mw;
        public bool chiusuraInattesa = false;
        /*
         * Costruttore
         * Si salvano i riferimenti al nuovo ClientLogic - quello istanziato appositamente per il restore - e alla MainWindow
         * Si passa il controllo a RestoreUC appena creato
         */
        public Restore(ClientLogic client, MainWindow mainw)
        {
            InitializeComponent();
            clientlogic = client;
            mw = mainw;
            App.Current.MainWindow = this;
            RestoreControl main = new RestoreControl(clientlogic, this);
            App.Current.MainWindow.Content = main;
        }

        /*
         * Callback di chiusura della finestra che si preoccupa di attendere se non è finito il download
         * e di rilasciare le risorse
         */
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine("RestoreWindow: Closing");

            try
            {
                if(!chiusuraInattesa)
                {//implementazione con attesa sia che sto scaricando una cartella che un file
                    if (App.Current.MainWindow.Content is DownloadFolder)
                    {
                        DownloadFolder df = (DownloadFolder)App.Current.MainWindow.Content;
                        if (df.downloading)
                        {
                            //avverto l'utente
                            MessageBoxResult result = System.Windows.MessageBox.Show("Il ripristino dei file verrà interrotto.\nProcedere?", "Disconnessione", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                            if (result == MessageBoxResult.OK)
                            {
                                df.StopRestore();
                            }
                            e.Cancel = true;
                            return;
                        }
                    }
                    else if (App.Current.MainWindow.Content is StartDownload)
                    {
                        StartDownload df = (StartDownload)App.Current.MainWindow.Content;
                        if (df.downloading)
                        {
                            MessageBoxResult result = System.Windows.MessageBox.Show("Ancora un istante...", "Attendi", MessageBoxButton.OK, MessageBoxImage.Stop);
                            e.Cancel = true;
                            return;
                        }
                    }
                    DialogResult = true;
                }
                //comunico al server che può chiudere il socket che avevamo aperto per la restore
                clientlogic.WriteStringOnStream(ClientLogic.EXITDOWNLOAD);
                //libero le risorse del timer
                clientlogic.timer.Dispose();
                //riallineo la MainWindow
                App.Current.MainWindow = mw;
                clientlogic.clientsocket.GetStream().Close();
                clientlogic.clientsocket.Close();
            }
            catch
            {
                //riallineo la MainWindow
                App.Current.MainWindow = mw;
                if (clientlogic.clientsocket.Client.Connected)
                {
                    clientlogic.clientsocket.GetStream().Close();
                    clientlogic.clientsocket.Close();
                }
            }
        }
    }
}
