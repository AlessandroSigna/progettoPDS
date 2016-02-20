using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using System.Net.NetworkInformation;

namespace Client
{
    /// <summary>
    /// Logica di interazione per StartDownload.xaml
    /// </summary>
    public partial class StartDownload : UserControl
    {
        private ClientLogic clientLogic;
        private string fileName;
        private string versione;
        public volatile bool downloading;
        private string root;
        private Restore restoreWindow;
        private String completePath;
        private BackgroundWorker workertransaction;
        private string idFile;
        private RestoreControl restoreControl;

        #region Costruttore
        /*
         * costruttore
         */
        public StartDownload(ClientLogic client, string file, string versionP, string rootF, Restore main, String sIdFile, RestoreControl restoreControl)
        {
            try
            {
                InitializeComponent();
                restoreWindow = main;
                downloading = false;
                clientLogic = client;
                fileName = file;
                versione = versionP;
                root = rootF;
                App.Current.MainWindow.Width = 300;
                App.Current.MainWindow.Height = 300;
                downloadName.Content = System.IO.Path.GetFileName(file); ;
                idFile = sIdFile;
                this.restoreControl = restoreControl;       //per ripassargli il controllo
                RiceviFile();

            }
            catch
            {
                ExitStub();
            }
        }
        #endregion

        #region Ricezione File
        public void RiceviFile()
        {
            try
            {
                workertransaction = new BackgroundWorker();
                workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_RiceviFile);
                workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workertransaction_RiceviFileCompleted);
                workertransaction.RunWorkerAsync();
            }
            catch
            {
                ExitStub();
            }
        }

        private void workertransaction_RiceviFileCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null || e.Cancelled)
                {
                    if (completePath != null)
                    {
                        Directory.Delete(completePath, true);
                    }
                    ExitStub();
                    return;
                }
                downloading = false;
                string headerStr = clientLogic.ReadStringFromStream();
                if (!headerStr.Contains(ClientLogic.OK))
                {
                    Directory.Delete(completePath, true);
                    ExitStub();
                    return;
                }
                System.Diagnostics.Process.Start("explorer.exe", completePath);

                if (restoreControl != null)
                {
                    App.Current.MainWindow.Content = restoreControl;
                    App.Current.MainWindow.Width = 400;
                    App.Current.MainWindow.Height = 400;
                }
                else
                {
                    ExitStub();
                }
            }
            catch
            {
                ExitStub();
            }
        }

        private void Workertransaction_RiceviFile(object sender, DoWorkEventArgs e)
        {
            downloading = true;
            //richiedo al server la versione del file fileName
            clientLogic.WriteStringOnStream(ClientLogic.GETFILEV + clientLogic.username + "+" + root + "+" + fileName + "+" + versione + "+" + idFile);
            int bufferSize = 1024;
            byte[] buffer = null;
            string headerStr = "";
            int filesize = 0;

            string folderCreated = DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute;
            completePath = clientLogic.restoreFolder + "\\" + folderCreated;
            Directory.CreateDirectory(completePath);    //creo la cartella dove salverò il file ricevuto
            fileName = fileName.Substring(fileName.LastIndexOf(@"\"));
            string pathTmp = completePath + @"\" + fileName.Substring(fileName.LastIndexOf(@"\") + 1);
            //Creo il file usando un FileStream dentro la direttiva using 
            //in questo modo il FileStream viene chiuso anche in caso di eccezioni
            using (FileStream fs = new FileStream(pathTmp, FileMode.OpenOrCreate))
            {
                headerStr = clientLogic.ReadStringFromStream(); //leggo dallo stream la risposta del server - un header
                if (headerStr.Contains(ClientLogic.ERRORE))
                {
                    e.Cancel = true;
                    return;
                }
                string[] splitted = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                String[] str = splitted[0].Split(':');
                filesize = int.Parse(str[1]);   //parsifico la risposta e ottengo la dim del file che sto per ricevere

                clientLogic.WriteStringOnStream(ClientLogic.OK);    //mando ACK
                int sizetot = 0;
                int original = filesize;
                //delego ad un thread la gestione della ProgressBar
                Thread t1 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, int>(SetProgressBar), pbStatus, original); }));
                t1.Start();
                int bufferCount = Convert.ToInt32(Math.Ceiling((double)original / (double)bufferSize));
                int i = 0;
                while (filesize > 0)
                {
                    if (clientLogic.clientsocket.Client.Poll(10000, SelectMode.SelectRead))
                    {
                        buffer = new byte[bufferSize];
                        clientLogic.clientsocket.Client.ReceiveTimeout = 30000;
                        if (clientLogic.GetState(clientLogic.clientsocket) != TcpState.Established)
                        {
                            e.Cancel = true;
                            return;
                        }
                        //man mano che mi arrivano pezzi di file li scrivo nel FileStream e diminuisco la filesize attesa
                        int size = clientLogic.clientsocket.Client.Receive(buffer, SocketFlags.Partial);
                        fs.Write(buffer, 0, size);
                        filesize -= size;
                        sizetot += size;
                        if ((i == (bufferCount / 4)) || (i == (bufferCount / 2)) || (i == ((bufferCount * 3) / 4)) || (i == (bufferCount - 1)))
                        {
                            Thread t2 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, int>(UpdateProgressBar), pbStatus, sizetot); }));
                            t2.Start();
                        }
                        i++;
                    }
                    else
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                //ricevuto tutto. Mando ACK
                clientLogic.WriteStringOnStream(ClientLogic.OK);
            }
        }
        #endregion

        #region ProgressBar e varie
        private void ExitStub(Boolean chiusuraInattesa = true)
        {
            if (App.Current.MainWindow is Restore)
            {
                restoreWindow.chiusuraInattesa = chiusuraInattesa;
                restoreWindow.Close();
            }
        }

        private void SetProgressBar(ProgressBar arg1, int arg2)
        {
            arg1.Maximum = arg2;
            arg1.Minimum = 0;
            arg1.Value = 0;
            arg1.Visibility = Visibility.Visible;
        }

        private void UpdateProgressBar(System.Windows.Controls.ProgressBar arg1, int arg2)
        {
            arg1.Value = arg2;
        }
        #endregion

    }
}
