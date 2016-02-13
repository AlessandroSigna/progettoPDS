using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Threading;
using System.ComponentModel;
using System.Security.Cryptography;

namespace Client
{
    /// <summary>
    /// Logica di interazione per MenuControl.xaml
    /// </summary>
    public partial class MenuControl : System.Windows.Controls.UserControl
    {

        private CustomDialog customDialog;
        private Disconnetti disconnettiWindow;
        private FileSystemWatcher watcher;
        private MainWindow mw;
        private string path;    //cartella di backup
        private string pathR;   //cartella di restore
        public volatile bool updating;  //sto inviando qualcosa al server
        public volatile bool exit;  //mi sono disconnesso
        public volatile bool create = false;    //non so
        private string lastCheck;       //non so
        private string[] files; //filenames nella rootDir

        //lista di estensioni problematiche per le quali non voglio sincronizzare il relativo file -  FIXME?
        private List<string> extensions = new List<string>() { "", ".pub", ".pps", ".pptm", ".ppt", ".pptx", ".xlm", ".xlt", ".xls", ".docx", ".doc", ".tmp", ".lnk", ".TMP", ".docm", ".dotx", ".dotcb", ".dotm", ".accdb", ".xlsx", ".jnt" };

        /*
         * costruttore
         */
        public MenuControl()
        {
            InitializeComponent();
            //((MainWindow)App.Current.MainWindow).IsCloseButtonEnabled = true;
            exit = false;
            lastCheck = String.Empty;
            App.Current.MainWindow.Title = "Mycloud";
            mw = (MainWindow)App.Current.MainWindow;
            //mw.clientLogic.event_1 = new AutoResetEvent(false); ??
            updating = false;
            BackButtonControl.BackButton.Click += Back_Click;
            //RestoreFile.IsEnabled = true;
        }

        #region Backup
        /*
         * callback del click sul Button FolderButton
         * setta il path della cartella selezionata nella textbox e relativca logica
         */
        private void Select_Folder(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();    //finestra di sistema per selezionare la cartella
            DialogResult result = fbd.ShowDialog();
            if (fbd.SelectedPath != "")
            {
                //se il path non è vuoto lo inserisco nella TextBox e cambio colore al Button EffettuaBackup
                BackupDir.Text = fbd.SelectedPath;
                path = fbd.SelectedPath;
                //mw.clientLogic.folder = path;
                BrushConverter bc = new BrushConverter();
                EffettuaBackup.IsEnabled = true;
                EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FF44E572");
            }

        }

        /*
         * callback del click sul Button EffettuaBackup che funziona da toggle tra Start e Stop (monitoring)
         */
        private void EffettuaBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow mw = (MainWindow)App.Current.MainWindow;
                if (!mw.clientLogic.monitorando)
                {
                    //inizio a monitorare e a inviare al server lo stato attuale della cartella
                    BrushConverter bc = new BrushConverter();
                    EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FA5858");   //cambio il colore del bottone
                    EffettuaBackup.Content = "Stop";    //e la scritta
                    FolderButton.IsEnabled = false;     //disabilito il bottone folder
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FOLDER + mw.clientLogic.username + "+" + path);  //invio al server la rootfolder
                    string retFolder = mw.clientLogic.ReadStringFromStream();
                    if (retFolder == ClientLogic.OK + "RootFolder Inserita")
                        FileUploading.Text = "Cartella aggiunta: " + System.IO.Path.GetDirectoryName(path);
                    else if (retFolder == ClientLogic.OK + "Stessa RootFolder")
                        FileUploading.Text = "Cartella : " + System.IO.Path.GetDirectoryName(path);


                    pbStatus.Value = 0; //La ProgressBar in questione è Hidden in partenza
                    pbStatus.Maximum = 100;
                    files = Directory.GetFiles(path, "*.*", System.IO.SearchOption.AllDirectories); //si ricavano i nomi dei files nella rootdir
                    if (files.Length != 0)
                    {
                        pbStatus.Visibility = Visibility.Visible;
                        mw.clientLogic.InvioFile(files);    //invio al server i nomi dei files nella cartella e nelle sottocartelle
                    }
                    watcher = new System.IO.FileSystemWatcher();    //FileWatcher a cui si registrano le callback in caso di modifiche sui file
                    watcher.Path = BackupDir.Text;
                    watcher.Filter = "*.*";
                    watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                    watcher.IncludeSubdirectories = true;
                    watcher.Changed += new FileSystemEventHandler(OnChanged);
                    watcher.Created += new FileSystemEventHandler(OnCreated);
                    watcher.Deleted += new FileSystemEventHandler(OnDeleted);
                    watcher.Renamed += new RenamedEventHandler(OnRenamed);
                    watcher.EnableRaisingEvents = true;
                }
                else
                {
                    //termino di monitorare attendendo eventuali lavori in corso
                    AttendiTermineUpdate();
                }
                mw.clientLogic.monitorando = !mw.clientLogic.monitorando;
            }
            catch
            {
                //viene catturata l'eventuale eccezione lanciando un apposito thread
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }
        }

        private void EffettuaBackup_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow mw = (MainWindow)App.Current.MainWindow;
            if (path != null && !mw.clientLogic.monitorando)
            {
                BrushConverter bc = new BrushConverter();
                EffettuaBackup.Background = (Brush)bc.ConvertFrom("#F5FFFA");
            }
            else if (path != null && mw.clientLogic.monitorando)
            {
                BrushConverter bc = new BrushConverter();
                EffettuaBackup.Background = (Brush)bc.ConvertFrom("#F6CECE");
            }

        }

        private void EffettuaBackup_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow mw = (MainWindow)App.Current.MainWindow;
            if (path != null && !mw.clientLogic.monitorando)
            {
                BrushConverter bc = new BrushConverter();
                EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FF44E572");
            }
            else if (path != null && mw.clientLogic.monitorando)
            {
                BrushConverter bc = new BrushConverter();
                EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FA5858");
            }

        }
        #endregion

        #region Gestione modifiche ai file
        /*
         * callback del FileSystemWatcher in caso di cancellazione di file
         */
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.WriteLine(" ONdel");
                if (!mw.clientLogic.monitorando)    //se non sto monitorando no devo gestire l'evento
                    return;
                if (files.Length != 0)
                {
                    mw.clientLogic.event_1.WaitOne();   //aspetto che event_1 venga segnalato - sincronizzazione
                }
                string extension = System.IO.Path.GetExtension(e.FullPath);
                //se il file contiene un'estensione problematica ritorno
                if (extensions.Contains(extension))
                {
                    mw.clientLogic.event_1.Set();   //segnalo l'evento e ritorno
                    return;
                }
                WatcherChangeTypes wct = e.ChangeType;
                if (e.ChangeType == WatcherChangeTypes.Deleted) //bisogna effettivamente controllare il ChangeType? 
                {
                    updating = true;
                    //segnalo la cancellazione al server
                    mw.clientLogic.WriteStringOnStream(ClientLogic.CANC + mw.clientLogic.username + "+" + e.FullPath);
                    mw.clientLogic.ReadStringFromStream();  //consumo la risposta senza analizzarla?
                    updating = false;
                    mw.clientLogic.event_1.Set();   //segnalo l'evento
                }
            }
            catch
            {

                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();

            }
        }

        /*
         * callback del FileSystemWatcher in caso di modifica di file
         */
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.WriteLine("Oncha");
                if (files.Length != 0)
                {
                    mw.clientLogic.event_1.WaitOne();   //sincronizzazione
                }
                string extension = System.IO.Path.GetExtension(e.FullPath);
                if (extensions.Contains(extension))     //controllo estensioni problematiche
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }

                Console.WriteLine(e.FullPath);
                if (!mw.clientLogic.monitorando)        //contorllo se sto effettivamente monitorando
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                if (Directory.Exists(e.FullPath))       //controllo se il file in questione è una directory?
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                updating = true;
                InviaSingoloFile(e.FullPath);   //invio il file al server
                updating = false;
                mw.clientLogic.event_1.Set();
            }
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }


        }

        /*
         * callback del FileSystemWatcher in caso di creazione di file
         */
        private void OnCreated(object sender, FileSystemEventArgs e)
        {

            try
            {
                Console.WriteLine("Oncre");
                if (files.Length != 0)
                {
                    mw.clientLogic.event_1.WaitOne();   //sincronizzazione
                }
                string extension = System.IO.Path.GetExtension(e.FullPath);
                if (extensions.Contains(extension)) //controllo estensioni problematiche
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                Console.WriteLine(e.FullPath);
                if (Directory.Exists(e.FullPath))   //controllo se è una directory ?
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                if (!mw.clientLogic.monitorando)
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                updating = true;
                InviaSingoloFile(e.FullPath, "CREATE"); //invio il file al server
                updating = false;
                mw.clientLogic.event_1.Set();
            }
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }
        }

        /*
         * callback del FileSystemWatcher in caso di renaming di file
         */
        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                //i controlli iniziali sono gli stessi
                Console.WriteLine("Onren");
                Console.WriteLine(e.FullPath);
                if (!mw.clientLogic.monitorando)
                    return;

                if (files.Length != 0)
                {
                    mw.clientLogic.event_1.WaitOne();
                }
                string extension = System.IO.Path.GetExtension(e.FullPath);
                if (extensions.Contains(extension))
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                //invio al server il nuovo nome sia che sia un file o una directory differenziando i due casi
                if (Directory.Exists(e.FullPath))
                {
                    updating = true;
                    mw.clientLogic.WriteStringOnStream(ClientLogic.RENAMEFILE + mw.clientLogic.username + "+" + e.OldFullPath + "+" + e.FullPath + "+" + "DIR");
                    mw.clientLogic.ReadStringFromStream();
                    updating = false;
                    mw.clientLogic.event_1.Set();

                }
                else
                {
                    WatcherChangeTypes wct = e.ChangeType;
                    if (e.ChangeType == WatcherChangeTypes.Renamed)
                    {
                        updating = true;
                        mw.clientLogic.WriteStringOnStream(ClientLogic.RENAMEFILE + mw.clientLogic.username + "+" + e.OldFullPath + "+" + e.FullPath);
                        mw.clientLogic.ReadStringFromStream();
                        updating = false;
                        mw.clientLogic.event_1.Set();
                    }
                }
            }
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }
        }
        #endregion

        #region Invio file
        private void InviaSingoloFile(string fileName)
        {

            InviaSingoloFile(fileName, ""); //sfrutto la stessa funzione con un flag diverso
        }

        /*
         * Invia il file puntato da fileName al sever. Usata nel caso di creazione di file o di renaming
         */
        private void InviaSingoloFile(string fileName, string onCreate)
        {

            try
            {
                //informo il sevrer di quello che sto per fare
                if (onCreate.Equals("CREATE"))
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FILE + mw.clientLogic.username + "+" + onCreate);
                else
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FILE + mw.clientLogic.username);
                int bufferSize = 1024;
                byte[] buffer = null;
                byte[] header = null;
                string checksum = "";

                mw.clientLogic.ReadStringFromStream();  //consumo lo stream (eventuale risposta del server)
                Thread.Sleep(100);

                //le operazioni sono molto simili a quanto fatto nella ClientLogic.InviaFile
                checksum = mw.clientLogic.GetMD5HashFromFile(fileName);
                FileStream fs = new FileStream(fileName, FileMode.Open);
                int bufferCount = Convert.ToInt32(Math.Ceiling((double)fs.Length / (double)bufferSize));

                string headerStr = "Content-length:" + fs.Length.ToString() + "\r\nFilename:" + fileName + "\r\nChecksum:" + checksum + "\r\n";
                header = new byte[bufferSize];
                Array.Copy(Encoding.ASCII.GetBytes(headerStr), header, Encoding.ASCII.GetBytes(headerStr).Length);
                mw.clientLogic.clientsocket.Client.Send(header);
                string streamReady = mw.clientLogic.ReadStringFromStream();
                if (streamReady.Equals(ClientLogic.OK + "File ricevuto correttamente") || streamReady.Equals(ClientLogic.INFO + "File non modificato") || streamReady.Equals(ClientLogic.INFO + "file dim 0"))
                {
                    fs.Close();
                    return;
                }

                //delego ad un thread il setup della ProgressBar e della TextBox
                Thread t1 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, int, string, System.Windows.Controls.TextBox>(SetProgressBar), pbStatus, bufferCount, System.IO.Path.GetFileName(fileName), FileUploading); }));
                t1.Start();

                for (int i = 0; i < bufferCount; i++)
                {
                    if ((i == (bufferCount / 4)) || (i == (bufferCount / 2)) || (i == ((bufferCount * 3) / 4)) || (i == (bufferCount - 1)))
                    {
                        //delego a un altro thread la gestione del progresso della ProgressBar
                        Thread t2 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, int>(UpdateProgressBar), pbStatus, i); }));
                        t2.Start();
                    }
                    buffer = new byte[bufferSize];
                    int size = fs.Read(buffer, 0, bufferSize);
                    mw.clientLogic.clientsocket.Client.SendTimeout = 30000;
                    mw.clientLogic.clientsocket.Client.Send(buffer, size, SocketFlags.Partial);
                }

                //thread per chiudere la ProgressBar
                Thread t3 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar>(HideProgress), pbStatus); }));
                t3.Start();
                fs.Close();
                //thread per settare il messaggio di ultima sincornizzazione nella TextBox - si poteva fare direttamente nel thread precedente?
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.TextBox>(SetValue), FileUploading); }));
                t.Start();
                string message = mw.clientLogic.ReadStringFromStream();
                if (message == (ClientLogic.ERRORE + "Invio file non riuscito"))
                {
                    ClientLogic.UpdateNotifyIconDisconnesso();
                }

                return;
            }
            catch (Exception)
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }
        }
        #endregion

        #region Fine Backup
        /*
         * Chiamata da EffettuaBackup_Click quando si vuole interrompere di monitorare la cartella
         * Se il client sta ancora inviando i file appare un messaggio di attesa e il watcher viene rilasciato
         */
        private void AttendiTermineUpdate()
        {

            MainWindow mw = (MainWindow)App.Current.MainWindow;
            if (mw.clientLogic.lavorandoInvio)
            {
                //al posto del bottone EffettuaBackup compare una Label Wait
                // Ma quando succede?
                EffettuaBackup.Visibility = Visibility.Hidden;
                EffettuaBackup.IsEnabled = false;
                Wait.Visibility = Visibility.Visible;
                messaggioStop();
                if (watcher != null)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
            }
            else
            {
                BrushConverter bc = new BrushConverter();
                EffettuaBackup.Background = (Brush)bc.ConvertFrom("#F5FFFA");
                EffettuaBackup.Content = "Start";
                FolderButton.IsEnabled = true;
                if (updating)
                {
                    //non ho capito che cosa controlla updating 
                    EffettuaBackup.Visibility = Visibility.Hidden;
                    EffettuaBackup.IsEnabled = false;
                    Wait.Visibility = Visibility.Visible;
                    messaggioStop();
                    BackgroundWorker worker = new BackgroundWorker();
                    worker.DoWork += new DoWorkEventHandler(Workertransaction_Waited);
                    worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_WaitedCompleted);
                    worker.RunWorkerAsync();
                }
                else
                {
                    if (watcher != null)
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                }

            }
        }

        // Questa funzione non dovrebbe essere qui (?)
        private void Workertransaction_Waited(object sender, DoWorkEventArgs e)
        {
            mw.clientLogic.event_1.WaitOne();
        }

        // Questa funzione non dovrebbe essere qui (?)
        private void Workertransaction_WaitedCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                pbStatus.Value = 0;
                pbStatus.Visibility = Visibility.Hidden;
                Wait.Visibility = Visibility.Hidden;
                EffettuaBackup.IsEnabled = true;
                FolderButton.IsEnabled = true;
                EffettuaBackup.Visibility = Visibility.Visible;
                FileUploading.Text = "Ultima sincronizzazione : " + DateTime.Now;
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                updating = false;
                if (exit)   //se nel frattempo mi sono disconnesso pulisco e rilascio tutto
                {
                    MainWindow mainw = (MainWindow)App.Current.MainWindow;
                    mainw.clientLogic.monitorando = false;
                    mainw.clientLogic.lavorandoInvio = false;
                    mainw.clientLogic.WriteStringOnStream(ClientLogic.DISCONNETTIUTENTE + mainw.clientLogic.username + "+" + mainw.clientLogic.mac);
                    mainw.clientLogic.connesso = false;
                    ClientLogic.UpdateNotifyIconDisconnesso();
                    if (mw.clientLogic.clientsocket.Client.Connected)
                    {
                        mw.clientLogic.clientsocket.GetStream().Close();
                        mw.clientLogic.clientsocket.Close();
                    }
                    //MainControl main = new MainControl();
                    //App.Current.MainWindow.Content = main;
                    mw.restart(false);
                }
            }
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }
        }
        #endregion

        #region Restore
        /*
         * Button per selezionare la cartella di restore
         */
        // Questa andrebbe rimossa
        private void Select_FolderR(object sender, RoutedEventArgs e)
        {


        }

        /*
         * Callback per iniziare il restore
         */
        private void RestoreFile_Click(object sender, RoutedEventArgs e)
        {
            //FIXME: selezione cartella di restore da collocare meglio
            //FolderBrowserDialog fbd = new FolderBrowserDialog();
            //DialogResult result = fbd.ShowDialog();
            //if (fbd.SelectedPath != "")
            //{
            //    //RestoreDir.Text = fbd.SelectedPath;
            //    pathR = fbd.SelectedPath;
            //    mw.clientLogic.folderR = pathR;
            //}

            //verifico connessione
            if (mw.clientLogic.clientsocket.Client.Poll(1000, SelectMode.SelectRead))
            {
                //MainControl main = new MainControl(1);
                //App.Current.MainWindow.Content = main;
                //messaggioErrore("Connessione Persa");
                mw.restart(true, "Connessione persa.");
                return;

            }

            //istanzio un nuovo ClientSocket in cui aprirò un socket - perché non usare quello che già c'è? riservato al backup?
            ClientLogic clRestore = new ClientLogic(mw.clientLogic.ip, mw.clientLogic.porta, mw.clientLogic.folder, mw.clientLogic.username, mw.clientLogic.folderR);
            Window w = null;
            //Questo meccanismo crea non pochi problemi quando bisogna generare un errore o tornare alle finestre principali.
            try
            {
                //istanzio Restore e la mostro come finestra di dialogo - NO Metro!
                w = new Restore(clRestore, mw);
                w.ShowDialog();
                //da qui in poi App.Current.MainWindow è null - occorre risettarla
                //App.Current.MainWindow = mw;  //fatto nella Restore:Window_Closing
            }
            catch (Exception)
            {
                //rilascio risorse il caso di eccezione
                if (clRestore.clientsocket.Client.Connected)
                {
                    clRestore.clientsocket.GetStream().Close();
                    clRestore.clientsocket.Client.Close();
                }

                if (mw.clientLogic.clientsocket.Client.Connected)
                {
                    mw.clientLogic.clientsocket.GetStream().Close();
                    mw.clientLogic.clientsocket.Close();
                }

                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Close();
                mw.restart(true, "Connessione persa.");
                //messaggioErrore("Connessione Persa");
                //return;
            }

            //App.Current.MainWindow = mw;
            //App.Current.MainWindow.Width = 400;
            //App.Current.MainWindow.Height = 400;
        }

        private void RestoreFile_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (RestoreFile.IsEnabled)
            {
                BrushConverter bc = new BrushConverter();
                RestoreFile.Background = (Brush)bc.ConvertFrom("#99FFFF");
            }
            else
            {
                RestoreFile.Background = Brushes.LightGray;
            }
        }

        private void RestoreFile_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (RestoreFile.IsEnabled)
            {
                BrushConverter bc = new BrushConverter();
                RestoreFile.Background = (Brush)bc.ConvertFrom("#33CCFF");
            }
            else
            {
                RestoreFile.Background = Brushes.LightGray;
            }

        }
        #endregion

        #region Logout e Disconnessione

        /*
         * Back button - si deve occupare di effettuare il logout
         */
        private void Back_Click(object sender, RoutedEventArgs e)
        {

            //avverto l'utente
            MessageBoxResult result = System.Windows.MessageBox.Show("Verrà effettuato il logout.\nProcedere?", "Logout", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.OK)
            {
                //prima di chiamare la ClientLogic.Logout occorrerebbe attendere e/o interrompere eventuali operazioni in corso di backup o restore
                //vedere loro vecchia implementazione ButtonServerOnClick
                mw.clientLogic.Logout(this);
            }
        }

        /*
         * Button Logout
         */
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            //DialogDisconnetti();
        }

        public void Logout_Esito(bool success, string messaggio = null) 
        {
            if (success)
            {
                LoginControl main = new LoginControl();
                App.Current.MainWindow.Content = main;
            }
            else
            {
                messaggioErrore(messaggio);
            }
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
            //logoutImage.BeginInit();
            //logoutImage.Source = new BitmapImage(new Uri(@"Images/logoutLight.png", UriKind.RelativeOrAbsolute));
            //logoutImage.EndInit();
        }

        private void Logout_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //logoutImage.BeginInit();
            //logoutImage.Source = new BitmapImage(new Uri(@"Images/logout.png", UriKind.RelativeOrAbsolute));
            //logoutImage.EndInit();
        }


        /*
         * Callback assegnata nella DialogDisconnetti per la pressione del Button disconnetti server 
         * presente in Disconnetti.xaml
         */
        private void ButtonServerOnClick(object sender, RoutedEventArgs e)
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //mw.HideMetroDialogAsync(customDialog);
            //MainWindow mainw = (MainWindow)mw;

            //Window mw = (Window)App.Current.MainWindow;
            ////mw.HideMetroDialogAsync(customDialog);
            //MainWindow mainw = (MainWindow)mw;

            //exit = true;
            //if (mainw.clientLogic.lavorandoInvio || updating)
            //    EffettuaBackup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));    //riscatena l'evento click che stavolta porterà ad AttendiTermineUpdate
            //else
            //    mainw.clientLogic.DisconnettiServer(false);

        }

        private void ButtonCancelOnClick(object sender, RoutedEventArgs e)
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //mw.HideMetroDialogAsync(customDialog);
        }
        #endregion

        #region Gestione GUI
        private void HideProgress(System.Windows.Controls.ProgressBar obj)
        {
            obj.Visibility = Visibility.Hidden;
        }

        private void UpdateProgressBar(System.Windows.Controls.ProgressBar arg1, int arg2)
        {
            arg1.Value = arg2;
        }

        private void SetProgressBar(System.Windows.Controls.ProgressBar arg1, int arg2, string arg3, System.Windows.Controls.TextBox arg4)
        {
            arg1.Maximum = arg2;
            arg1.Minimum = 0;
            arg1.Value = 0;
            arg1.Visibility = Visibility.Visible;
            arg4.Text = "Sto sincronizzando: " + arg3;
        }

        private static void SetValue(System.Windows.Controls.TextBox txt)
        {
            txt.Text = "Ultima sincronizzazione : " + DateTime.Now;
        }
        #endregion

        #region Gestione Errori e Messaggi
        /*
         * Il controllo torna a MainControl lanciando un messaggio di errore
         */
        private void ChangeWindow(MainWindow obj)
        {
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Close();
            if (obj.clientLogic.clientsocket.Connected)
            {
                Console.WriteLine("socket CHIUSO");
                obj.clientLogic.clientsocket.GetStream().Close();
                obj.clientLogic.clientsocket.Close();
            }

            App.Current.MainWindow = obj;
            updating = false;
            //MainControl main = new MainControl();
            //mw.Content = main;
            //main.messaggioErrore();
            obj.restart(true);
        }

        private void messaggioErrore(string mess)
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //await mw.ShowMessageAsync("Errore", mess);
            MessageBoxResult result = System.Windows.MessageBox.Show(mess, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);

        }

        private /*async*/ void messaggioStop()
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //await mw.ShowMessageAsync("Attendere", "Stiamo chiudendo i canali...  un attimo di pazienza");
            //MessageBoxResult result = System.Windows.MessageBox.Show("Stiamo chiudendo i canali...  un attimo di pazienza", "Errore", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private /*async*/ void messaggioAttesa()
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //await mw.ShowMessageAsync("Attenzione", "Blocca il monitoraggio per effettuare un restore");
            MessageBoxResult result = System.Windows.MessageBox.Show("Blocca il monitoraggio per effettuare un restore", "Errore", MessageBoxButton.OK, MessageBoxImage.Stop);
        }
        #endregion


    }
}
