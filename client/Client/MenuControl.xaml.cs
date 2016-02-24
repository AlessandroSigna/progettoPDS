using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private FileSystemWatcher watcher;
        private MainWindow mw;
        private string backupFolder;    //cartella di backup
        public volatile bool updating;  //il watcher sta comunicando un aggiornamento o un file nuovo o modificato - può essere true solo dopo lavorando invio e solo durante monitorando
        public volatile bool wantToExit;            //voglio chiudere la finestra
        private volatile bool wantToLogout;         //voglio effettuare il logout -> LoginControl
        private volatile bool wantToDisconnect;     //voglio effettuare la disconnessione -> MainControl
        public volatile bool create = false;    //non so
        private string lastCheck;       //non so
        private string[] files; //filenames nella rootDir

        public const string WARNING_SINCRONIZZAZIONE = "L'operazione non è stata sincronizzata.";

        //lista di estensioni problematiche per le quali non voglio sincronizzare il relativo file -  FIXME?
        private List<string> extensions = new List<string>() { "", ".pub", ".pps", ".pptm", ".ppt", ".pptx", ".xlm", ".xlt", ".xls", ".docx", ".doc", ".tmp", ".lnk", ".TMP", ".docm", ".dotx", ".dotcb", ".dotm", ".accdb", ".xlsx", ".jnt" };

        /*
         * costruttore
         */
        public MenuControl()
        {
            InitializeComponent();
            wantToExit = wantToDisconnect = wantToLogout = false;
            lastCheck = String.Empty;
            App.Current.MainWindow.Title = "BackApp";
            mw = (MainWindow)App.Current.MainWindow;
            updating = false;

            BrushConverter bc = new BrushConverter();
            EffettuaBackup.Foreground = (Brush)bc.ConvertFrom("#dadada");
            EffettuaBackup.IsEnabled = false;

            BackButtonControl.BackButton.Click += Back_Click;
        }

        #region Backup
        /*
         * callback del click sul Button FolderButton
         * setta il path della cartella selezionata nella textbox e relativca logica
         */
        private void Select_Folder(object sender, RoutedEventArgs e)
        {
            if (!BackupError.Text.Equals(""))
            {
                BackupError.Text = "";
                BackupDir.BorderBrush = Brushes.Gray;
            }

            try
            {
                FolderBrowserDialog fbd = new FolderBrowserDialog();    //finestra di sistema per selezionare la cartella
                fbd.ShowDialog();
                if (fbd.SelectedPath != "")
                {
                    //se il path non è vuoto lo inserisco nella TextBox e cambio colore al Button EffettuaBackup
                    BackupDir.Text = fbd.SelectedPath;
                    backupFolder = fbd.SelectedPath;
                    //mw.clientLogic.backupFolder = path;
                    BrushConverter bc = new BrushConverter();
                    EffettuaBackup.Foreground = (Brush)bc.ConvertFrom("Black");
                    EffettuaBackup.IsEnabled = true;
                }
            }
            catch
            {
                mw.clientLogic.DisconnectAndClose();
                mw.restart(true);
            }

        }

        /*
         * callback del click sul Button EffettuaBackup che funziona da toggle tra Start e Stop (monitoring)
         */
        private void EffettuaBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!FolderURLCheck(BackupDir.Text))
                return;

            try
            {
                MainWindow mw = (MainWindow)App.Current.MainWindow;
                if (!mw.clientLogic.monitorando)
                {
                    BackupDir.BorderBrush = Brushes.Transparent;
                    BackupDir.IsEnabled = false;
                    BackupDir.Background = Brushes.Transparent;
                    Monitor.Text = "Monitorando: ";

                    //inizio a monitorare e a inviare al server lo stato attuale della cartella
                    new BrushConverter();
                    EffettuaBackup.Content = "Arresta";    //e la scritta
                    FolderButton.IsEnabled = false;     //disabilito il bottone folder
                    mw.clientLogic.cartellaMonitorata = backupFolder;
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FOLDER + mw.clientLogic.username + ">" + backupFolder);  //invio al server la rootfolder
                    string retFolder = mw.clientLogic.ReadStringFromStream();
                    if (retFolder == ClientLogic.OK + "RootFolder Inserita")
                    {
                        FileUploading.Text = "Cartella aggiunta: " + System.IO.Path.GetDirectoryName(backupFolder);
                    }
                    else if (retFolder == ClientLogic.OK + "Stessa RootFolder")
                    {
                        FileUploading.Text = "Cartella : " + System.IO.Path.GetDirectoryName(backupFolder);
                    }

                    pbStatus.Value = 0; //La ProgressBar in questione è Hidden in partenza
                    pbStatus.Maximum = 100;
                    files = Directory.GetFiles(backupFolder, "*.*", System.IO.SearchOption.AllDirectories); //si ricavano i nomi dei files nella rootdir
                    if (files.Length != 0)
                    {
                        pbStatus.Visibility = Visibility.Visible;
                        mw.clientLogic.InvioFile(files);    //se necessario invio al server i files nella cartella e nelle sottocartelle - lavorandoinvio true nel frattempo
                        RestoreFile.IsEnabled = false;
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
                //toggle sul bool monitorando
                mw.clientLogic.monitorando = !mw.clientLogic.monitorando;
            }
            catch
            {
                ExitStub();
            }
        }

        #endregion

        #region Gestione modifiche ai file - Watcher
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
                if (extensions.Contains(extension) && !extension.Equals(""))
                {
                    mw.clientLogic.event_1.Set();   //segnalo l'evento e ritorno
                    return;
                }

                if (e.ChangeType == WatcherChangeTypes.Deleted) //bisogna effettivamente controllare il ChangeType? 
                {
                    updating = true;
                    //segnalo la cancellazione al server
                    mw.clientLogic.WriteStringOnStream(ClientLogic.CANC + mw.clientLogic.username + ">" + e.FullPath);
                    String risposta = mw.clientLogic.ReadStringFromStream();  //consumo la risposta senza analizzarla?
                    if (risposta.Contains("ERR"))
                    {
                        System.Windows.MessageBox.Show(WARNING_SINCRONIZZAZIONE, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    updating = false;
                    mw.clientLogic.event_1.Set();   //segnalo l'evento
                }
            }
            catch
            {
                //devo lanciare un apposito thread per gestire l'eccezione: The calling thread must be STA, because many UI components require this.
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<Boolean>(ExitStub), true); }));
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
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<Boolean>(ExitStub), true); }));
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
                if (Directory.Exists(e.FullPath))
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
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<Boolean>(ExitStub), true); }));
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
                string risposta = null;
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
                if (extensions.Contains(extension) && !extension.Equals(""))
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                //invio al server il nuovo nome sia che sia un file o una directory differenziando i due casi
                if (Directory.Exists(e.FullPath))
                {
                    updating = true;
                    mw.clientLogic.WriteStringOnStream(ClientLogic.RENAMEFILE + mw.clientLogic.username + ">" + e.OldFullPath + ">" + e.FullPath + ">" + "DIR");
                    risposta = mw.clientLogic.ReadStringFromStream();
                    if (risposta.Contains("ERR"))
                    {
                        System.Windows.MessageBox.Show(WARNING_SINCRONIZZAZIONE, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    updating = false;
                    mw.clientLogic.event_1.Set();

                }
                else
                {
                    if (e.ChangeType == WatcherChangeTypes.Renamed)
                    {
                        updating = true;
                        mw.clientLogic.WriteStringOnStream(ClientLogic.RENAMEFILE + mw.clientLogic.username + ">" + e.OldFullPath + ">" + e.FullPath);
                        risposta = mw.clientLogic.ReadStringFromStream();
                        if (risposta.Contains("ERR"))
                        {
                            System.Windows.MessageBox.Show(WARNING_SINCRONIZZAZIONE, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        updating = false;
                        mw.clientLogic.event_1.Set();
                    }
                }
            }
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<Boolean>(ExitStub), true); }));
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
         * Invia il file puntato da fileName al sever.
         * Usata nel caso di creazione di file o di renaming
         */
        private void InviaSingoloFile(string fileName, string onCreate)
        {

            try
            {
                //informo il sevrer di quello che sto per fare
                if (onCreate.Equals("CREATE"))
                {
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FILE + mw.clientLogic.username + ">" + onCreate);
                }
                else
                {
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FILE + mw.clientLogic.username);
                }
                int bufferSize = 1024;
                byte[] buffer = null;
                byte[] header = null;
                string checksum = "";

                string risposta = mw.clientLogic.ReadStringFromStream();  //consumo lo stream (eventuale risposta del server)
                if (risposta.Contains("ERR"))
                {
                    System.Windows.MessageBox.Show(WARNING_SINCRONIZZAZIONE, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                //le operazioni sono molto simili a quanto fatto nella ClientLogic.InviaFile
                checksum = mw.clientLogic.GetMD5HashFromFile(fileName);
                using (FileStream fs = new FileStream(fileName, FileMode.Open))
                {
                    int bufferCount = Convert.ToInt32(Math.Ceiling((double)fs.Length / (double)bufferSize));

                    string headerStr = "Content-length:" + fs.Length.ToString() + "\r\nFilename:" + fileName + "\r\nChecksum:" + checksum + "\r\n";
                    header = new byte[bufferSize];
                    Array.Copy(Encoding.ASCII.GetBytes(headerStr), header, Encoding.ASCII.GetBytes(headerStr).Length);
                    mw.clientLogic.clientsocket.Client.Send(header);
                    string streamReady = mw.clientLogic.ReadStringFromStream();
                    if (streamReady.Equals(ClientLogic.OK + "File ricevuto correttamente") || streamReady.Equals(ClientLogic.INFO + "File non modificato") || streamReady.Equals(ClientLogic.INFO + "file dim 0"))
                    {
                        return;
                    }
                    else if (streamReady.Contains("ERR"))
                    {
                        System.Windows.MessageBox.Show(WARNING_SINCRONIZZAZIONE, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                }

                //thread per chiudere la ProgressBar
                Thread t3 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar>(HideProgress), pbStatus); }));
                t3.Start();
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
                ExitStub();
            }
        }
        #endregion

        #region Fine Backup
        /*
         * Chiamata da EffettuaBackup_Click quando si vuole interrompere di monitorare la cartella
         * Se il client sta ancora inviando i file appare un messaggio di attesa e il watcher viene rilasciato
         * Altrimenti se non ci sono lavori in corso 
         */
        private void AttendiTermineUpdate()
        {
            try
            {
                if (mw.clientLogic.lavorandoInvio || updating)
                {
                    //sono qui se sto facendo il backup "grosso" - lavorandoInvio
                    //oppure se il watcher mentre monitorava comunica qualcosa al server - updating
                    //al posto del bottone EffettuaBackup compare una Label Wait
                    //attendo di accedere alle risorse in modo pulito - Workertransaction_Waited
                    //per poi operare in base al contesto verificando il valore di alcuni flag - Workertransaction_WaitedCompleted

                    BrushConverter bc = new BrushConverter();
                    EffettuaBackup.Foreground = (Brush)bc.ConvertFrom("#dadada");
                    EffettuaBackup.IsEnabled = false;
                    BackgroundWorker worker = new BackgroundWorker();
                    worker.DoWork += new DoWorkEventHandler(Workertransaction_Waited);
                    worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_WaitedCompleted);
                    worker.RunWorkerAsync();
                }
                else
                {
                    EffettuaBackup.Content = "Sincronizza";
                    BackupDir.IsEnabled = true;
                    BackupDir.Background = Brushes.White;
                    BackupDir.BorderBrush = Brushes.Gray;
                    Monitor.Text = "Cartella da monitorare: ";
                    FolderButton.IsEnabled = true;
                    if (watcher != null)
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                }
            }
            catch
            {
                ExitStub();
            }
        }

        /*
         * sincronizzazione con la ClientLogic.InvioFile e con i vari eventi del watcher
         */
        private void Workertransaction_Waited(object sender, DoWorkEventArgs e)
        {
            mw.clientLogic.event_1.WaitOne();
        }

        /*
         * finita l'attesa ha il compito di verificare alcuni flag per capire cosa fare tra disconnessione logout ecc
         */
        private void Workertransaction_WaitedCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled)
            {
                ExitStub();
                return;
            }
            try
            {
                pbStatus.Value = 0;
                pbStatus.Visibility = Visibility.Hidden;
                EffettuaBackup.IsEnabled = true;
                FolderButton.IsEnabled = true;
                EffettuaBackup.Visibility = Visibility.Visible;
                FileUploading.Text = "Ultima sincronizzazione : " + DateTime.Now;
                mw.clientLogic.cartellaMonitorata = null;
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                updating = false;
                if (wantToLogout)   //se ho interrotto il backup in seguito al click su back
                {
                    mw.clientLogic.Logout(this);
                }
                else if (wantToExit)    //se ho interrotto il backup in seguito al click sulla X
                {
                    mw.clientLogic.DisconnettiServer(true);
                }
                else if (wantToDisconnect)   //se ho interrotto il backup in seguito al click su disconnetti
                {
                    mw.clientLogic.DisconnettiServer(false);
                }
            }
            catch
            {
                ExitStub();
            }
        }
        #endregion

        #region Restore

        /*
         * Callback per iniziare il restore
         */
        private void RestoreFile_Click(object sender, RoutedEventArgs e)
        {
            ClientLogic clRestore = null;
            try
            {
                //verifico connessione
                if (mw.clientLogic.clientsocket.Client.Poll(1000, SelectMode.SelectRead))
                {
                    mw.clientLogic.DisconnectAndClose();
                    mw.restart(true, "Connessione persa.");
                    return;

                }
                //istanzio un nuovo ClientSocket in cui aprirò un socket - quello che già c'è è riservato a backup e accounting
                String monitoredDir = mw.clientLogic.monitorando ? backupFolder : null;
                clRestore = new ClientLogic(mw.clientLogic.ip, mw.clientLogic.porta, monitoredDir, mw.clientLogic.username);
                Restore windowRestore = null;
                windowRestore = new Restore(clRestore, mw);
                if (windowRestore.chiusuraInattesa == true) //intercetto errori nella costruzione della Restore e RestoreControl
                {
                    mw.clientLogic.DisconnectAndClose();
                    mw.restart(true, "Connessione persa. Impossibile effettuare il restore.");
                    return;
                }
                //il pattern che seguo per gestire problemi nella nuova window è: una volta chiusa la window Restore checkare il valore dei DialogResult
                //se è true è andato tutto bene altrimenti si è chiusa con un eccezione e MenuControl deve capire che è successo
                //se un'eccezione occorre in Restore si deve chiudere il nuovo socket aperto e chiudere la nuova window - è compito di Restore stessa
                if (windowRestore.ShowDialog() == false) {  //istanzio Restore e la mostro come finestra di dialogo
                    Console.WriteLine("chiusura inattesa");
                    //verifico connessione
                    if (mw.clientLogic.clientsocket.Client.Poll(1000, SelectMode.SelectRead))
                    {
                        //errore non recuperabile: resetto
                        mw.clientLogic.DisconnectAndClose();
                        mw.restart(true, "Connessione persa.");
                        return;

                    }
                    else
                    {
                        //errore recuperabile: il controllo resta su MenuControl
                        System.Windows.MessageBox.Show("Errore durante durante la fase di restore.\nRiprovare.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception)
            {
                //rilascio risorse in caso di eccezione
                mw.clientLogic.DisconnectAndClose();

                if (App.Current.MainWindow is Restore)
                {
                    App.Current.MainWindow.Close();
                }
                mw.restart(true, "Connessione persa.");
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
                //prima di chiamare la ClientLogic.Logout occorreattendere e/o interrompere eventuali operazioni in corso di backup
                wantToLogout = true;
                if (mw.clientLogic.lavorandoInvio || updating)
                {
                    EffettuaBackup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));    //riscatena l'evento click che stavolta porterà ad AttendiTermineUpdate
                }
                else
                {
                    mw.clientLogic.Logout(this);
                }
                
            }
        }

        public void RichiediChiusura()
        {  
            //prima di chiamare la DisconnettiServer occorre attendere e/o interrompere eventuali operazioni in corso di backup
            wantToExit = true;
            if (mw.clientLogic.lavorandoInvio || updating)
            {
                EffettuaBackup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));    //riscatena l'evento click che stavolta porterà ad AttendiTermineUpdate
            }
            else
            {
                mw.clientLogic.DisconnettiServer(true);
            }
        }


        public void RichiediDisconnessione()
        {  
            //prima di chiamare la DisconnettiServer occorre attendere e/o interrompere eventuali operazioni in corso di backup
            wantToDisconnect = true;
            if (mw.clientLogic.lavorandoInvio || updating)
            {
                EffettuaBackup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));    //riscatena l'evento click che stavolta porterà ad AttendiTermineUpdate
            }
            else
            {
                mw.clientLogic.DisconnettiServer(false);
            }
        }
        /*
         * Button Logout
         */

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

        private void Backup_LostFocus(object sender, RoutedEventArgs e)
        {
            if (FolderURLCheck(BackupDir.Text))
                BackupDir.BorderBrush = Brushes.Gray;
        }

        private void Backup_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!BackupError.Text.Equals(""))
                BackupError.Text = "";
        }

        private void Folder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            sfondoFolder.Visibility = System.Windows.Visibility.Visible;
        }

        private void Folder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            sfondoFolder.Visibility = System.Windows.Visibility.Hidden;
        }


        private void RestoreFile_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            sfondoRestore.Visibility = System.Windows.Visibility.Visible;
        }

        private void RestoreFile_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            sfondoRestore.Visibility = System.Windows.Visibility.Hidden;
        }
        #endregion

        #region Gestione Errori e Messaggi

        private bool FolderURLCheck(string url)
        {

            if (url.Equals("") || url == null)
            {
                BackupError.Text = "Seleziona la cartella da sincronizzare.";
                BackupDir.BorderBrush = Brushes.Red;
                return false;
            }

            if (!Directory.Exists(url))
            {
                BackupError.Text = "La cartella selezionata non esiste.";
                BackupDir.BorderBrush = Brushes.Red;
                return false;
            }

            return true;
        }

        /*
         * Il controllo torna a MainControl lanciando un messaggio di errore
         */
        private void ExitStub(Boolean error = true)
        {
            mw.clientLogic.DisconnectAndClose();
            mw.restart(error);
        }

        private void messaggioErrore(string mess)
        {
            System.Windows.MessageBox.Show(mess, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion
    }
}
