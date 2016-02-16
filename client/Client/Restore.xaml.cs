using MahApps.Metro.Controls;
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
using MahApps.Metro.Controls.Dialogs;

namespace Client
{
    /// <summary>
    /// Logica di interazione per Restore.xaml
    /// </summary>
    public partial class Restore : /*MetroWindow*/ Window
    {
        private ClientLogic clientlogic;
        public MainWindow mw;
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
            RestoreControl main = new RestoreControl(clientlogic, mw);
            App.Current.MainWindow = this;
            App.Current.MainWindow.Content = main;
        }

        /*
         * Callback di chiusura della finestra che si preoccupa di attendere se non è finito il download
         * e di rilasciare le risorse
         */
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //this.DialogResult = true;
            Console.WriteLine("RestoreWindow: Closing");
            //riallineo la MainWindow
            App.Current.MainWindow = mw;
            return;
            
            //vecchia implementazione - con attesa
            if (App.Current.MainWindow.Content is DownloadFolder)
            {
                DownloadFolder df = (DownloadFolder)App.Current.MainWindow.Content;
                if (df.downloading)
                {
                    messaggioAttendi("Ancora un istante...");
                    e.Cancel = true;
                    df.Start.IsEnabled = false;
                    df.Start.Visibility = Visibility.Hidden;
                    df.WaitFol.Visibility = Visibility.Visible;
                    return;
                }
            }
            else if (App.Current.MainWindow.Content is StartDownload)
            {
                StartDownload df = (StartDownload)App.Current.MainWindow.Content;
                if (df.downloading)
                {
                    messaggioAttendi("Ancora un istante...");
                    e.Cancel = true;
                    return;
                }
            }
            try
            {
                clientlogic.WriteStringOnStream(ClientLogic.EXITDOWNLOAD);
                clientlogic.clientsocket.GetStream().Close();
                clientlogic.clientsocket.Close();
            }
            catch
            {   // ?
                if (clientlogic.clientsocket.Client.Connected)
                {
                    clientlogic.clientsocket.GetStream().Close();
                    clientlogic.clientsocket.Close();
                }
            }
        }

        private /*async*/ void messaggioAttendi(string mess)
        {
            //MetroWindow mw = (MetroWindow)this;
            //await mw.ShowMessageAsync("Attendi", mess);

            MessageBoxResult result = System.Windows.MessageBox.Show("Errore durante la comunicazione con il server.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }

    }
}
