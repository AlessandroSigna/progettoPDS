using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
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

namespace Client
{
    /// <summary>
    /// Logica di interazione per DownloadFolder.xaml
    /// </summary>
    public partial class DownloadFolder : UserControl
    {
        private ClientLogic clientLogic;
        private string folderRoot;
        private string pathRoot;
        public volatile bool downloading;
        private Restore restoreWindow;
        private BackgroundWorker workertransaction;
        private RestoreControl restoreControl;
        private string folderToBeRestored;
        /*
         * Costruttore. 
         * rootFolder è il path della rootDirectory che è stata backuppata
         * folderToBeRestored è il path della cartella di cui si deve fare la restore
         */
        public DownloadFolder(ClientLogic clientlogic, string rootFolder, string folderToBeRestored, Restore main, RestoreControl restoreControl)
        {
            InitializeComponent();
            downloading = true;
            restoreWindow = main;
            folderRoot = rootFolder;
            clientLogic = clientlogic;
            this.folderToBeRestored = folderToBeRestored;
            this.restoreControl = restoreControl;
            App.Current.MainWindow.Width = 500;
            App.Current.MainWindow.Height = 215;
            //creo una cartella dentro la cartella scelta dall'utente dove collezionare i file che verranno ripristinati
            //questa nuova cartella avrà lo stesso nome della (sotto)cartella che si vuole ripristinare
            string folderCreated = folderToBeRestored.Substring(folderToBeRestored.LastIndexOf((@"\")) + 1);
            pathRoot = clientlogic.folderR + @"\" + folderCreated; 
            System.IO.Directory.CreateDirectory(pathRoot);

            RiceviRestore();
        }

        #region Stop Button
        /*
         * Callback del click sul Button Stop
         * Setta il boolean downloading a false. 
         * Questo booleano viene verificato nella RiceviFive per capire se l'utente vuole interrompere il download
         */
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopRestore();
        }

        private void File_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!downloading)
            {
                BrushConverter bc = new BrushConverter();
                Start.Background = (Brush)bc.ConvertFrom("#F5FFFA");
            }
            else
            {
                BrushConverter bc = new BrushConverter();
                Start.Background = (Brush)bc.ConvertFrom("#F6CECE");
            }
        }

        private void File_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!downloading)
            {
                BrushConverter bc = new BrushConverter();
                Start.Background = (Brush)bc.ConvertFrom("#FF44E572");
            }
            else
            {
                BrushConverter bc = new BrushConverter();
                Start.Background = (Brush)bc.ConvertFrom("#FA5858");
            }
        }

        public void StopRestore()
        {
            downloading = false;
            Start.IsEnabled = false;
            Start.Visibility = Visibility.Hidden;
            WaitFol.Visibility = Visibility.Visible;
        }
        #endregion

        #region Metodi di Restore
        /*
         * Comunica al server di iniziare il restore della cartella con eventualmente i file cancellati
         * delega la gestione del restore a delle opportune callback assegnate a workertransaction
         */
        private void RiceviRestore()
        {
            try
            {

                //RESTORE + username + fullPath della root directory backuppata + fullPath della cartella creata per accogliere il restore + char per file cancellati + fullPath della subdir da ripristinare
                clientLogic.WriteStringOnStream(ClientLogic.RESTORE + clientLogic.username + "+" + folderRoot + "+" + pathRoot + "+" + "N" + "+" + folderToBeRestored);

            }
            catch
            {
                //if (clientLogic.clientsocket.Client.Connected)
                //{
                //    clientLogic.clientsocket.GetStream().Close();
                //    clientLogic.clientsocket.Close();
                //}
                clientLogic.DisconnectAndClose();
                App.Current.MainWindow.Close();
            }
            //assegno le callback a workertransaction
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_RiceviRestore);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workertransaction_RiceviRestoreCompleted);
            workertransaction.RunWorkerAsync();


        }

        /*
         * Al termine dei download si apre l'explorer per mostrate quanto ripristinato
         */
        private void workertransaction_RiceviRestoreCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            downloading = false;
            System.Diagnostics.Process.Start("explorer.exe", clientLogic.folderR);
            if(restoreControl != null)
            {
                App.Current.MainWindow.Content = restoreControl;
                App.Current.MainWindow.Width = 400;
                App.Current.MainWindow.Height = 400;
            }
            else
            {
                App.Current.MainWindow.Close();
            }
        }

        private void Workertransaction_RiceviRestore(object sender, DoWorkEventArgs e)
        {
            try
            {
                //loop che itera leggendo di volta in volta la risposta del server e ricevendo eventualmente i file
                while (true)
                {
                    int bufferSize = 1024;
                    byte[] buffer = null;
                    int filesize = 0;
                    string headerStr = "";
                    headerStr = clientLogic.ReadStringFromStream(); //leggo la risposta del server
                    if (headerStr.Contains(ClientLogic.ERRORE) || headerStr.Equals(ClientLogic.OK + "Restore Avvenuto Correttamente") || headerStr.Equals(ClientLogic.INFO + "Restore interrotto dal client"))
                    {
                        break;  //esco dal loop
                    }

                    //la parsifico opportunamente per ottenere filesize, filename, checksum
                    string[] splitted = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    String[] str = splitted[0].Split('=');
                    string dim = str[1];
                    filesize = Convert.ToInt32(int.Parse(dim));
                    String[] str2 = splitted[1].Split('=');
                    string fileName = str2[1];
                    String[] str3 = splitted[2].Split('=');
                    string checksum = str3[1];

                    fileName = MakeRelativePath2(pathRoot.Substring(0, pathRoot.LastIndexOf(@"\") - 1), fileName);
                    string localpath = clientLogic.folderR + @"\" + fileName;

                    //creo la directory che conterrà il file se già non esiste. si ricava il nome della directory esaminando il filename
                    localpath = localpath.Substring(0, localpath.LastIndexOf(@"\"));
                    if (!Directory.Exists(localpath))
                    {
                        DirectoryInfo di = Directory.CreateDirectory(localpath);
                    }
                    bool exist = false;
                    //se il file esiste già ed ha la stessa checksum ricevuta dal server non devo riscriverlo
                    if (File.Exists(clientLogic.folderR + @"\" + fileName))
                    {
                        string check = clientLogic.GetMD5HashFromFile(clientLogic.folderR + @"\" + fileName);
                        if (check.Equals(checksum))
                        {
                            exist = true;
                            //thread per la progress bar
                            Thread t3 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, System.Windows.Controls.Label, string>(SetProgressBar), pbStatus, downloadName, fileName + " già esistente"); }));
                            t3.Start();
                        }

                    }
                    if (exist)
                    {
                        //comunico al server che il file non mi interessa e passo ad analizzare il file successivo
                        clientLogic.WriteStringOnStream(ClientLogic.INFO + "File gia' presente e non modificato");
                        continue;
                    }
                    if (downloading)
                        clientLogic.WriteStringOnStream(ClientLogic.OK);    //comunico l'interesse per il file
                    else
                    {
                        clientLogic.WriteStringOnStream(ClientLogic.STOP);
                        continue;
                    }

                    //ricevo il file proprio come viene fatto nella StartDownload
                    FileStream fs = new FileStream(clientLogic.folderR + @"\" + fileName, FileMode.OpenOrCreate);
                    int sizetot = 0;
                    int original = filesize;
                    Thread t1 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, int, System.Windows.Controls.Label, string>(SetProgressBar), pbStatus, original, downloadName, fileName); }));
                    t1.Start(); //progressbar
                    int bufferCount = Convert.ToInt32(Math.Ceiling((double)original / (double)bufferSize));
                    int i = 0;
                    while (filesize > 0)
                    {
                        if (clientLogic.clientsocket.Client.Poll(10000, SelectMode.SelectRead))
                        {
                            buffer = new byte[bufferSize];
                            clientLogic.clientsocket.Client.ReceiveTimeout = 30000;
                            if (clientLogic.GetState(clientLogic.clientsocket) != TcpState.Established)
                                throw new Exception();
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
                            fs.Close();
                            throw new Exception();
                        }
                    }
                    fs.Close();
                    clientLogic.WriteStringOnStream(ClientLogic.OK);
                }
            }
            catch
            {
                Thread t2 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<int>(ExitStub), 2); }));
                t2.Start();
            }
        }
        #endregion

        #region ProgressBar e vari
        private void ExitStub(int obj)
        {
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Close();
            //if (clientLogic.clientsocket.Client.Connected)
            //{
            //    clientLogic.clientsocket.GetStream().Close();
            //    clientLogic.clientsocket.Close();
            //}
            clientLogic.DisconnectAndClose();
            //App.Current.MainWindow = mw;
            //MainControl main = new MainControl();
            //mw.Content = main;
            //main.messaggioErrore();
            //********mw.restart(true);
            return;

        }

        private void SetProgressBar(ProgressBar arg1, Label arg2, string arg3)
        {
            arg1.Visibility = Visibility.Hidden;
            arg2.Content = arg3;
        }

        public string MakeRelativePath2(string workingDirectory, string fullPath)
        {
            string realtivePath = fullPath.Substring(workingDirectory.Length + 1);
            return realtivePath;
        }

        private void UpdateProgressBar(ProgressBar arg1, int arg2)
        {
            arg1.Value = arg2;
        }

        private void SetProgressBar(ProgressBar arg1, int arg2, Label arg3, string arg4)
        {
            arg1.Maximum = arg2;
            arg1.Minimum = 0;
            arg1.Value = 0;
            arg1.Visibility = Visibility.Visible;
            arg3.Content = arg4.Substring(1);
        }
        #endregion
    }
}
