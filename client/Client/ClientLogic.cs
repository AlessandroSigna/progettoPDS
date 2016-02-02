﻿using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows;
using System.Threading;
using System.Windows.Threading;

namespace Client
{
    public class ClientLogic
    {
        BackgroundWorker workertransaction;
        public int porta;
        public IPAddress ip;
        private int errore;
        private MainWindow mw;

        #region Costanti
        private const int BUFFERSIZE = 1024;
        public const string OK = "+OK+";
        public const string ERRORE = "+ERR+";
        public const string INFO = "+INFO+";
        public const string REGISTRAZIONE = "+REG+";
        public const string LOGIN = "+LOGIN+";
        public const string STOP = "+STOP+";
        public const string DISCONETTI = "+DISCO+";
        public const string FILE = "+FILE+";
        public const string NONINVIARE = "+NOINVIO+";
        public const string EXITDOWNLOAD = "+EXITDOWNLOAD+";
        public const string RESTORE = "+RESTORE+"; // +RESTORE+user+folderBackup+folderDestinazione (voglio anche la destinazione, così creo una nuova folderRoot) 
        public const string FOLDER = "+FOLDER+";
        public const string GETFILEV = "+GETVF+"; // +GETVF+user+file+versione -> Invio la specifica versione di un file
        public const string GETVFILE = "+GVF+"; // +GVF+user+file -> Prendo le versioni di un file
        public const string LISTFILES = "+LISTFILES+"; //+LISTFILES+user+folder+nomeLike (solo nome, senza path -> barra di ricerca. Se nullo, allora nomeLike="")
        public const string CANC = "+CANC+"; //+CANC+user+filename
        public const string RENAMEFILE = "+RENAMEFI+"; //+RENAMEFILE+user+fileNameOLD+fileNameNEW
        public const string ENDSYNC = "+ENDSYN+"; //+ENDSYN+user+folder
        public const string DISCONETTIUTENTE = "+DISCUTENTE+"; //+DISCUTENTE+user
        public const string GETFOLDERUSER = "+GETFOLDUSER+"; // +GETFOLDUSER+user
        public const string FLP = "+FLP+";
        public const string CONNESSIONE_CHIUSA_SERVER = "Connessione chiusa dal server";
        #endregion

        public volatile Boolean monitorando;    //true mentre si osserva la rootDir in attesa di cambiamenti
        public volatile Boolean lavorandoInvio; //true mentre si invia un file al server
        public AutoResetEvent event_1;
        public string folder;
        public string folderR;
        public string mac;
        private string _username;
        public bool connesso = false;
        public string username
        {
            get
            {
                return this._username;
            }
            set
            {
                this._username = value;
            }

        }
        private TcpClient _clientsocket;

        public TcpClient clientsocket
        {
            get
            {
                return this._clientsocket;
            }
            set
            {
                this._clientsocket = value;
            }

        }


        private string GetMacAddress()
        {
            const int MIN_MAC_ADDR_LENGTH = 12;
            string macAddress = string.Empty;
            long maxSpeed = -1;

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                string tempMac = nic.GetPhysicalAddress().ToString();
                if (nic.Speed > maxSpeed &&
                    !string.IsNullOrEmpty(tempMac) &&
                    tempMac.Length >= MIN_MAC_ADDR_LENGTH)
                {
                    maxSpeed = nic.Speed;
                    macAddress = tempMac;
                }
            }

            return macAddress;
        }

        #region Costruttori
        public ClientLogic(TcpClient clientSocketPassed, IPAddress ipPassed, int portPassed, MainWindow mainwindow)
        {
            mw = mainwindow;
            errore = 0;
            porta = portPassed;
            ip = ipPassed;
            clientsocket = clientSocketPassed;
            monitorando = false;
            lavorandoInvio = false;
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_Connect); //setto il metodo che deve essere eseguito all'occorrenza di workertransaction.RunWorkerAsync()
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_ConnectionCompleted);  //setto la callback da eseguire quando la backgroud operation termina
            workertransaction.RunWorkerAsync();
        }

        public ClientLogic(IPAddress iPAddress, int port, string fold, string user, string folderR)
        {
            this.clientsocket = new TcpClient();
            this.ip = iPAddress;
            this.porta = port;
            this.folder = fold;
            this.username = user;
            this.folderR = folderR;
            try
            {
                clientsocket.Connect(ip, porta);
            }
            catch
            {
                if (clientsocket.Connected)
                {
                    clientsocket.GetStream().Close();
                    clientsocket.Close();
                }
                MainControl main = new MainControl(1);  //FIXME: magicnumber!
                App.Current.MainWindow.Content = main;
            }
        }
        #endregion

        #region  Metodi di connessione
        void Workertransaction_Connect(object sender, DoWorkEventArgs e)
        {
            try
            {
                clientsocket = new TcpClient();
                clientsocket.Connect(ip, porta);
            }
            catch (SocketException)
            {
                errore = 1;
            }
        }


        void Workertransaction_ConnectionCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (errore == 1)
            {
                MainControl main = new MainControl(errore);
                App.Current.MainWindow.Content = main;
                return;
            }
            mac = GetMacAddress();  //FIXME: al prof non è piaciuta sta cosa del mac
            if (mac.Equals(String.Empty))
            {
                MainControl main = new MainControl(1);  //FIXME: magicnumber!
                App.Current.MainWindow.Content = main;
                return;
            }
            LoginRegisterControl login = new LoginRegisterControl();
            App.Current.MainWindow.Content = login;
        }

        #endregion

        #region Metodi di Login e Registrazione
        internal void Login(string username, string pass)
        {
            workertransaction = new BackgroundWorker();
            object paramAct = LOGIN;
            object paramObj = username;
            this.username = username;
            object paramObj2 = pass;
            object[] parameters = new object[] { paramObj, paramObj2, paramAct };   //incapsulo username, password e azione per poterli passare come parametro (array)

            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_LoginRegistrazione);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_LoginCompleted);
            workertransaction.RunWorkerAsync(parameters);

        }

        private void Workertransaction_LoginCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                string message = ReadStringFromStream();
                if (message.Contains(OK))
                {
                    MenuControl main = new MenuControl();
                    App.Current.MainWindow.Content = main;
                    UpdateNotifyIconConnesso();
                    connesso = true;
                }
                else
                {
                    String tmp = message.Substring(1, message.Length - 1);
                    String messaggioErrore = message.Substring(tmp.IndexOf('+') + 2, tmp.Length - tmp.IndexOf('+') - 1);
                    if (messaggioErrore == CONNESSIONE_CHIUSA_SERVER)
                    {
                        clientsocket.GetStream().Close();
                        clientsocket.Close();
                        MainControl main = new MainControl(1);
                        App.Current.MainWindow.Content = main;
                    }
                    else
                    {
                        LoginControl main = new LoginControl(messaggioErrore);
                        App.Current.MainWindow.Content = main;
                    }
                }
            }
            catch
            {
                if (clientsocket.Connected)
                {
                    clientsocket.GetStream().Close();
                    clientsocket.Close();
                }
                MainControl main = new MainControl(1);
                App.Current.MainWindow.Content = main;
            }
        }

        internal void Registrati(string username, string pass)
        {
            workertransaction = new BackgroundWorker();

            object paramAct = REGISTRAZIONE;
            object paramObj = username;
            object paramObj2 = pass;
            object[] parameters = new object[] { paramObj, paramObj2, paramAct };

            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_LoginRegistrazione);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_RegistrazioneCompleted);
            workertransaction.RunWorkerAsync(parameters);

        }

        /*
         * Callback lanciata quando termina l'invio delle credenziali per la registrazione. 
         * Attende risposta da server leggendo dallo stream
         */
        private void Workertransaction_RegistrazioneCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                string message = ReadStringFromStream();
                if (message.Contains(OK))
                {

                    MenuControl main = new MenuControl();   //il controllo passa a MenuControl
                    App.Current.MainWindow.Content = main;
                    UpdateNotifyIconConnesso();
                    connesso = true;

                }
                else
                {
                    //se l'autenticazione non va a buon fine torno alla finestra principale e chiude lo stream
                    String tmp = message.Substring(1, message.Length - 1);
                    MainControl main = new MainControl(1);  //FIXME: magicnumber
                    App.Current.MainWindow.Content = main;
                    if (mw.clientLogic.clientsocket.Client.Connected)   //FIXME: ma mw.clientLogic non punta a questo stesso oggetto?!
                    {
                        mw.clientLogic.clientsocket.GetStream().Close();
                        mw.clientLogic.clientsocket.Close();
                    }
                    return;
                }
            }
            catch
            {
                if (mw.clientLogic.clientsocket.Connected)
                {
                    mw.clientLogic.clientsocket.GetStream().Close();
                    mw.clientLogic.clientsocket.Close();
                }
                MainControl main = new MainControl(1);
                App.Current.MainWindow.Content = main;
                return;
            }
        }

        /*callback usata sia in caso di registrazione sia in caso di login
         il discriminante è cioò che è contenuto in action dopo che l'array dei parametri viene parsificato
        */
        private void Workertransaction_LoginRegistrazione(object sender, DoWorkEventArgs e)
        {
            object[] parameters = e.Argument as object[];
            string[] resultArray = Array.ConvertAll(parameters, x => x.ToString());
            string username = resultArray[0];
            this.username = username;
            string password = resultArray[1];
            string action = resultArray[2];
            try
            {
                WriteStringOnStream(action + username + "+" + password + "+" + mac);    //invio al server le credenziali
            }
            catch
            {
                if (clientsocket.Connected)
                {
                    clientsocket.GetStream().Close();
                    clientsocket.Close();
                }
                MainControl main = new MainControl(1);  //FIXME: magicnumber
                App.Current.MainWindow.Content = main;
            }

        }

        #endregion

        private async void messaggioErrore(string mess)
        {
            MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            await mw.ShowMessageAsync("Errore", mess);
        }

        #region Metodi di Disconnesione
        internal void DisconnettiServer(Boolean esci)
        {
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_Disconnect);
            if (!esci)
                workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_DisconnectCompleted);
            workertransaction.RunWorkerAsync();

        }

        private void Workertransaction_Disconnect(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (connesso)
                {
                    WriteStringOnStream(DISCONETTIUTENTE + username + "+" + mac);
                    connesso = false;
                }
                else
                    WriteStringOnStream(DISCONETTI);
                clientsocket.GetStream().Close();
                clientsocket.Close();
            }
            catch
            {
                if (clientsocket.Connected)
                {
                    clientsocket.GetStream().Close();
                    clientsocket.Close();
                }
                MainControl main = new MainControl(0);
                App.Current.MainWindow.Content = main;
                return;
            }
        }

        private void Workertransaction_DisconnectCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateNotifyIconDisconnesso();
            MainControl main = new MainControl(0);
            App.Current.MainWindow.Content = main;
        }

        public static void UpdateNotifyIconDisconnesso()
        {
            MainWindow.MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/disconnessoicon.ico");
        }

        public static void UpdateNotifyIconConnesso()
        {
            MainWindow.MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/connessoicon.ico");
        }
        #endregion



        public int WriteStringOnStream(string message)
        {
            TcpState statoConn = GetState(clientsocket);    //FIXME: istruzione inutile?! controllare statoConn
            NetworkStream stream = clientsocket.GetStream();
            stream.WriteTimeout = 30000;
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(message); //preparo i dati per l'invio sul canale
            stream.Write(data, 0, data.Length);
            return 1;   //FIXME: perchè return 1?
        }


        public string ReadStringFromStream()
        {
            TcpState statoConn = GetState(clientsocket);    //FIXME: istruzione inutile?!  oppure serve solo per lanciare una eccezione?

            NetworkStream stream = clientsocket.GetStream();
            stream.ReadTimeout = 30000;
            Byte[] data = new Byte[512];
            String responseData = String.Empty;
            Int32 bytes = stream.Read(data, 0, data.Length);
            responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);    //parsifico i byte ricevuti
            return responseData;

        }

        public TcpState GetState(TcpClient tcpClient)
        {
            var foo = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint));
            return foo != null ? foo.State : TcpState.Unknown;
        }


        #region Metodi invio file
        /*
         * Invocato da MenuControl.EffettuaBackup_Click. Riceve un array di path di file 
         * e li invia al server sfruttando diversi thread (?) di workertransaction i 
         */
        public void InvioFile(string[] Filenames)
        {
            event_1 = new AutoResetEvent(false);    //usato per la sincronizzazione dei thread
            workertransaction = new BackgroundWorker();
            workertransaction.WorkerReportsProgress = true;
            workertransaction.ProgressChanged += new ProgressChangedEventHandler(WorkerProgressChanged);    //chiamabile tramite ReportProgress se WorkerReportsProgress è true
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_InviaFile);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workertranaction_InviaFileCompleted);

            object[] objFiles = new object[Filenames.Length];
            int i = 0;
            foreach (string s in Filenames)
            {
                objFiles[i] = (object)s;    //incapsulo le stringhe come object per permetterne il passaggio all'evento come parametri
                i++;
            }
            workertransaction.RunWorkerAsync(objFiles); //per ogni filename si invoca la Workertransaction_InviaFile
        }

        private void Workertransaction_InviaFile(object sender, DoWorkEventArgs e)
        {
            lavorandoInvio = true;
            object[] parameters = e.Argument as object[];
            string[] resultArray = Array.ConvertAll(parameters, x => x.ToString()); //riottengo le stringhe con i filenames
            int nFile = 100 / resultArray.Length;   //per calcolare la percentuale di progresso
            foreach (string name in resultArray)
            {
                try
                {
                    this.WriteStringOnStream(ClientLogic.FILE + username);  //scrivo ogni filename sullo stream preceduto da FILE
                }
                catch
                {
                    //gestione eccezione: chiudo socket
                    if (this.clientsocket.Client.Connected)
                    {
                        this.clientsocket.GetStream().Close();
                        this.clientsocket.Close();
                    }
                    e.Result = false;
                }

                int retVal = InviaFile(name);

                //gestisco i codici di errore ritornati da IviaFile
                if (retVal == 1)    //FIXME: magicnumber
                {
                    try
                    {
                        workertransaction.ReportProgress(nFile, "Ultimo file sincronizzato: " + Path.GetFileName(name)); //si invoca la callback per monitorare il progresso
                        string message = ReadStringFromStream();
                        if (message == (ERRORE + "Invio file non riuscito"))
                        {
                            UpdateNotifyIconDisconnesso();
                        }
                        e.Result = true;
                    }
                    catch
                    {
                        e.Result = false;
                        break;
                    }
                }
                else if (retVal == 0)   //FIXME: magicnumber
                {
                    workertransaction.ReportProgress(nFile, "Ultimo file sincronizzato: " + Path.GetFileName(name));
                    e.Result = true;
                }
                else if (retVal == 2)   //FIXME: magicnumber
                {
                    e.Result = false;
                    break;
                }
                if (monitorando == false)
                {
                    e.Cancel = true;
                    return;
                }
            }
            try
            {
                this.WriteStringOnStream(ClientLogic.ENDSYNC + username + "+" + folder);    //comunico al server che ho terminato l'invio dei file
            }
            catch
            {
                if (this.clientsocket.Client.Connected)
                {
                    this.clientsocket.GetStream().Close();
                    this.clientsocket.Close();
                }
            }
        }

        /*
         * Callback per la fine della sincronizzazione 
         */
        private void workertranaction_InviaFileCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                MenuControl mc = (MenuControl)mw.Content;
                mc.pbStatus.Value = 0;
                mc.pbStatus.Visibility = Visibility.Hidden;
                mc.Wait.Visibility = Visibility.Hidden;
                mc.EffettuaBackup.IsEnabled = true;
                mc.EffettuaBackup.Visibility = Visibility.Visible;
                BrushConverter bc = new BrushConverter();
                if (e.Cancelled)
                {
                    mc.EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FF44E572");
                    mc.EffettuaBackup.Content = "Start";
                    mc.FolderButton.IsEnabled = true;
                    mc.FileUploading.Text = "Non tutti i dati sono aggiornati";
                    monitorando = false;
                    MenuControl menuC = (MenuControl)App.Current.MainWindow.Content;
                    if (menuC.exit)
                    {
                        MainWindow mainw = (MainWindow)App.Current.MainWindow;
                        mainw.clientLogic.monitorando = false;
                        mainw.clientLogic.lavorandoInvio = false;
                        mainw.clientLogic.WriteStringOnStream(ClientLogic.DISCONETTIUTENTE + username + "+" + mac);
                        connesso = false;
                        ClientLogic.UpdateNotifyIconDisconnesso();
                        if (mainw.clientLogic.clientsocket.Client.Connected)
                        {
                            mainw.clientLogic.clientsocket.GetStream().Close();
                            mainw.clientLogic.clientsocket.Close();
                        }
                        MainControl main = new MainControl(0);
                        App.Current.MainWindow.Content = main;
                    }
                }
                else
                {
                    mc.FileUploading.Text = "Ultima sincronizzazione : " + DateTime.Now;
                    lavorandoInvio = false;
                    event_1.Set();

                    if ((bool)e.Result == false)
                    {
                        MainControl main = new MainControl(1);
                        App.Current.MainWindow.Content = main;
                        return;
                    }
                }
            }
            catch
            {
                MainWindow mainw = (MainWindow)App.Current.MainWindow;
                if (mainw.clientLogic.clientsocket.Connected)
                {
                    mainw.clientLogic.clientsocket.GetStream().Close();
                    mainw.clientLogic.clientsocket.Close();
                }
                MainControl main = new MainControl(1);
                App.Current.MainWindow.Content = main;
            }

        }


        /*
         * Callback per monitorare il progresso dell'invio chiamata da workertransaction.ReportProgress
         * Aggiorna la ProgressBar e la TextBox di FileUploading
         */
        private void WorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            MenuControl mc = (MenuControl)mw.Content;
            mc.pbStatus.Value += e.ProgressPercentage;
            mc.FileUploading.Text = (string)e.UserState;
        }

        /*
         * Invia il file relativo a Filename al server
         */
        private int InviaFile(string Filename)
        {
            try
            {
                int bufferSize = 1024;
                byte[] buffer = null;
                byte[] header = null;
                string checksum = "";

                ReadStringFromStream();     //?? per svuotare lo stream? per lanciare eccezione?
                checksum = GetMD5HashFromFile(Filename);
                FileStream fs = new FileStream(Filename, FileMode.Open);    //apro il file da inviare come uno stream
                int bufferCount = Convert.ToInt32(Math.Ceiling((double)fs.Length / (double)bufferSize));    //numero di buffer necessari per salvare il file

                //preparo un headre da inviare al server per informarlo sul file che invierò tra poco
                string headerStr = "Content-length:" + fs.Length.ToString() + "\r\nFilename:" + Filename + "\r\nChecksum:" + checksum + "\r\n";
                header = new byte[bufferSize];
                Array.Copy(Encoding.ASCII.GetBytes(headerStr), header, Encoding.ASCII.GetBytes(headerStr).Length);  //FIXME: e se il filename è troppo lungo?!
                clientsocket.Client.Send(header);   //invio l'header tramite il canale socket
                string streamReady = ReadStringFromStream();
                if (streamReady.Equals(OK + "File ricevuto correttamente") || streamReady.Equals(INFO + "File non modificato") || streamReady.Equals(ClientLogic.INFO + "file dim 0"))
                {
                    //chiudo il FileStream visto che non è necessario inviarlo
                    fs.Close();
                    fs.Dispose();
                    return 0;
                }

                //invio tanti pezzettini di file quanto necessario
                for (int i = 0; i < bufferCount; i++)
                {
                    buffer = new byte[bufferSize];
                    int size = fs.Read(buffer, 0, bufferSize);
                    clientsocket.Client.SendTimeout = 30000;
                    clientsocket.Client.Send(buffer, size, SocketFlags.Partial);
                }
                fs.Close();
                fs.Dispose();
                return 1;   //FIXME: magicnumber
            }
            catch
            {
                return 2;   //FIXME: magicnumber
            }
        }
        #endregion
        /*
         * Calcola e ritorna l'hash md5 del file relativo a filename 
         */
        public string GetMD5HashFromFile(string fileName)
        {
            MD5 md5 = MD5.Create();
            FileStream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
            string bitret = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
            stream.Close();
            return bitret;
        }

        public T FindDescendant<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj is T)
                return obj as T;

            int childrenCount = VisualTreeHelper.GetChildrenCount(obj);
            if (childrenCount < 1)
                return null;

            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T)
                    return child as T;
            }

            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = FindDescendant<T>(VisualTreeHelper.GetChild(obj, i));
                if (child != null && child is T)
                    return child as T;
            }

            return null;
        }

    }
}
