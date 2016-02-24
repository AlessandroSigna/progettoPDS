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
        private MainWindow mw;
        private MainControl mc;
        public System.Threading.Timer timer;   //timer per capire quando è opportuno mandare il messaggio di hearthbeat

        #region Costanti comandi
        private const int BUFFERSIZE = 1024;
        private const int CHALLENGESIZE = 64;
        public const string OK = ">OK>";
        public const string ERRORE = ">ERR>";
        public const string INFO = ">INFO>";
        public const string REGISTRAZIONE = ">REG>";
        public const string ECDH = ">ECDH>";   //key agreement per proteggere la registrazione
        public const string LOGIN = ">LOGIN>";
        public const string LOGOUT = ">LOGOUT>";
        public const string ECHO_REQUEST = ">ECHO_REQUEST>";
        public const string STOP = ">STOP>";
        public const string DISCONETTI = ">DISCO>";//obsoleto
        public const string DISCONNETTICLIENT = ">DISCOCLIENT>"; //chiude lo stream di comunicazione con il client (utente non loggato)
        public const string FILE = ">FILE>";
        public const string NONINVIARE = ">NOINVIO>";
        public const string EXITDOWNLOAD = ">EXITDOWNLOAD>";
        public const string RESTORE = ">RESTORE>"; // +RESTORE+user+folderBackup+folderDestinazione (voglio anche la destinazione, così creo una nuova folderRoot) 
        public const string FOLDER = ">FOLDER>";
        public const string GETFILEV = ">GETVF>"; // +GETVF+user+file+versione -> Invio la specifica versione di un file
        public const string GETVFILE = ">GVF>"; // +GVF+user+file -> Prendo le versioni di un file
        public const string LISTFILES = ">LISTFILES>"; //+LISTFILES+user+folder+nomeLike (solo nome, senza path -> barra di ricerca. Se nullo, allora nomeLike="")
        public const string CANC = ">CANC>"; //+CANC+user+filename
        public const string RENAMEFILE = ">RENAMEFI>"; //+RENAMEFILE+user+fileNameOLD+fileNameNEW
        public const string ENDSYNC = ">ENDSYN>"; //+ENDSYN+user+folder
        public const string DISCONNETTIUTENTE = ">DISCUTENTE>"; //+DISCUTENTE+user
        public const string GETFOLDERUSER = ">GETFOLDUSER>"; // +GETFOLDUSER+user
        public const string FLP = ">FLP>";
        public const string CONNESSIONE_CHIUSA_SERVER = "Connessione chiusa dal server";
        
        public const int POLLING = 30; // (secondi) uguale alla metà del timer del server

        #endregion

        public volatile Boolean monitorando;    //true mentre si osserva la rootDir in attesa di cambiamenti
        public volatile Boolean lavorandoInvio; //true mentre si inviano i file facendo il backup "grosso" - cioè quando si preme start backup
        public AutoResetEvent event_1;
        public String cartellaMonitorata;
        public string restoreFolder;
        private string _username;
        public bool connesso = false;   //true se loggato
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
        private string me;
        #region Costruttori
        public ClientLogic(TcpClient clientSocketPassed, IPAddress ipPassed, int portPassed, MainWindow mainwindow, MainControl maincontrol)
        {
            me = "Main";
            mw = mainwindow;
            mc = maincontrol;
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

        public ClientLogic(IPAddress iPAddress, int port, string backupFolder, string user)
        {
            me = "Restore";
            this.clientsocket = new TcpClient();
            this.ip = iPAddress;
            this.porta = port;
            this.cartellaMonitorata = backupFolder;
            this.username = user;
            try
            {
                clientsocket.Connect(ip, porta);
                timer = new System.Threading.Timer(TimerCallBack);
                timer.Change(TimeSpan.FromSeconds(POLLING), TimeSpan.FromSeconds(POLLING));
            }
            catch
            {
                DisconnectAndClose();
                mw.restart(true);
            }
        }
        #endregion

        #region  Metodi di connessione
        void Workertransaction_Connect(object sender, DoWorkEventArgs e)
        {
            clientsocket = new TcpClient();
            clientsocket.Connect(ip, porta);
        }


        void Workertransaction_ConnectionCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try{
                if (e.Error != null)
                {
                    mc.Esito_Connect(false);
                }
                else
                {
                    timer = new System.Threading.Timer(TimerCallBack);
                    timer.Change(TimeSpan.FromSeconds(POLLING), TimeSpan.FromSeconds(POLLING));
                    mc.Esito_Connect(true);
                }
            } 
            catch
            {
                DisconnectAndClose();
                mc.Esito_Connect(false);
            }
        }

        private void TimerCallBack(object obj)
        {
            try
            {
                Console.WriteLine(ECHO_REQUEST + me);
                WriteStringOnStream(ECHO_REQUEST);
            }
            catch
            {
                DisconnectAndClose(false);
            }
        }

        public TcpState GetState(TcpClient tcpClient)
        {
            var foo = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint));
            return foo != null ? foo.State : TcpState.Unknown;
        }

        #endregion

        #region Metodi di Login e Registrazione
        internal void Login(string username, string pass, LoginControl lc)
        {
            workertransaction = new BackgroundWorker();
            object paramObj = username;
            object paramObj2 = pass;
            object paramWindow = lc;
            object[] parameters = new object[] { paramObj, paramObj2, paramWindow };   //incapsulo username, password e azione per poterli passare come parametro (array)

            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_Login);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_LoginCompleted);
            workertransaction.RunWorkerAsync(parameters);

        }

        private void Workertransaction_Login(object sender, DoWorkEventArgs e)
        {
            object[] parameters = e.Argument as object[];
            string[] resultArray = Array.ConvertAll(parameters, x => x.ToString());
            this.username = resultArray[0];
            string password = resultArray[1];
            
            WriteStringOnStream(LOGIN + username);

            String response = ReadStringFromStream();
            if (!response.Contains(OK))
            {
                if (response.Contains(ERRORE) && response.Contains("Username"))
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    throw new Exception("Autenticazione fallita: problema di comunicazione con il server.");
                }
            }

            WriteStringOnStream(OK);

            //aspetto dal server il nonce (challenge)
            byte[] challenge = new byte[CHALLENGESIZE];
            ReadByteArrayFromStream(challenge);

            Console.WriteLine("challenge: " + BitConverter.ToString(challenge));

            //concateno pasword e challenge
            byte[] passwordChallengeBytes = new byte[password.Length + CHALLENGESIZE];
            Array.Copy(Encoding.ASCII.GetBytes(password), passwordChallengeBytes, password.Length);
            Array.Copy(challenge, 0, passwordChallengeBytes, password.Length, CHALLENGESIZE);

            //calcolo l'hash (risposta alla sfida)
            SHA256 sha = SHA256Managed.Create();
            byte[] challengeResponse = sha.ComputeHash(passwordChallengeBytes);   //sha256 ha hash di 256 bit (32 byte)


            //invio al server la risposta al challenge: sha256(nonce||password)
            WriteByteArrayOnStream(challengeResponse);
            Console.WriteLine("challengeResponse: " + BitConverter.ToString(challengeResponse));

            e.Result = parameters;
        }

        /*
         * Analizzo la risposta del server ed eventualmente passo il controllo a MenuControl
         */
        private void Workertransaction_LoginCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {

                if (e.Error != null)
                {
                    DisconnectAndClose();

                    mw.restart(true, e.Error.Message);

                } 
                else if (e.Cancelled)
                {
                    var content = App.Current.MainWindow.Content;
                    if (content is LoginControl)
                    {
                        ((LoginControl)content).Esito_Login(false, "Username e/o Password Errati");
                    }
                    else
                    {
                        MessageBoxResult result = System.Windows.MessageBox.Show("Username e/o Password Errati", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    object[] parameters = e.Result as object[]; //errore - http://stackoverflow.com/questions/1044460/unhandled-exceptions-in-backgroundworker
                    LoginControl lc = (LoginControl)parameters[2];
                    
                    string message = ReadStringFromStream();
                    String tmp = message.Substring(1, message.Length - 1);
                    String messaggioErrore = message.Substring(tmp.IndexOf('>') + 2, tmp.Length - tmp.IndexOf('>') - 1);


                    if (message.Contains(OK))
                    {
                        lc.Esito_Login(true);
                        UpdateNotifyIconConnesso();
                        connesso = true;
                    }
                    else
                    {
                        lc.Esito_Login(false, messaggioErrore);
                    }
                }
            }
            catch (Exception exc)
            {
                DisconnectAndClose();
                mw.restart(true, exc.Message);
            }
        }

        internal void Registrati(string username, string pass, RegistratiControl rc)
        {
            workertransaction = new BackgroundWorker();

            object paramAct = REGISTRAZIONE;
            object paramObj = username;
            object paramObj2 = pass;
            object paramWindow = rc;
            object[] parameters = new object[] { paramObj, paramObj2, paramAct, paramWindow };

            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_Registrazione);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_RegistrazioneCompleted);
            workertransaction.RunWorkerAsync(parameters);

        }

        /*
         * Invio delle credenziali di registrazione effettuando il KeyAgreement con DH per proteggerle
         */
        private void Workertransaction_Registrazione(object sender, DoWorkEventArgs e)
        {
            object[] parameters = e.Argument as object[];
            string[] resultArray = Array.ConvertAll(parameters, x => x.ToString());
            this.username = resultArray[0];
            string password = resultArray[1];
            byte[] clientPublicKey;
            string clientPublicKeyString;
            ECDiffieHellmanCng clientECDH = new ECDiffieHellmanCng();

            clientECDH.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            clientECDH.HashAlgorithm = CngAlgorithm.Sha256;
            clientPublicKey = clientECDH.PublicKey.ToByteArray();   //chiave pubblica in byte[]
            //devo inviare la chiave publica al server
            clientPublicKeyString = BitConverter.ToString(clientPublicKey); //converto la chiave pubblica in string. Byte separati da '-'
            Console.WriteLine("PublicKey " + clientPublicKeyString);

            WriteStringOnStream(ECDH + clientPublicKeyString);    //comunico che voglio iniziare il DH + mando la pkey
            string serverResponse = ReadStringFromStream(); //deve essere ECDH + serverPublicKeyString
            if (!serverResponse.Contains(ECDH))
            {
                e.Cancel = true;
                return;
            }
            String[] parametri = serverResponse.Split('>');
            string serverPublicKeyString = parametri[2];
            Console.WriteLine("serverPublicKey " + serverPublicKeyString);
            //la chiave del server deve essere convertita in byte[]
            String[] arr = serverPublicKeyString.Split('-');
            byte[] serverPublicKey = new byte[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                serverPublicKey[i] = Convert.ToByte(arr[i], 16);
            }
            CngKey k = CngKey.Import(serverPublicKey, CngKeyBlobFormat.EccPublicBlob);
            byte[] clientKey = clientECDH.DeriveKeyMaterial(k); //derivo la chiave simmetrica
            Console.WriteLine("ClientSimmetricKey " + BitConverter.ToString(clientKey));

            //procedo con la cifratura delle credenziali username e password
            string encryptedMessage = null;
            byte[] iv = null;
            String secretMessage = username + '>' + password;
            encryptedMessage = EffettuaCifraturaSimmetrica(clientKey, secretMessage, out iv);

            //invio al server credenziali cifrate + iv
            string ivString = BitConverter.ToString(iv);
            Console.WriteLine("IV" + ivString.Length + ": " + ivString);
            Console.WriteLine("ciphertext: " + encryptedMessage);

            String messaggio = REGISTRAZIONE + ivString + '>' + encryptedMessage;
            WriteStringOnStream(messaggio);
            Console.WriteLine("messaggio: " + messaggio);
            e.Result = parameters;
        }

        /*
         * Callback lanciata quando termina l'invio delle credenziali per la registrazione. 
         * Attende risposta da server leggendo dallo stream
         */
        private void Workertransaction_RegistrazioneCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null || e.Cancelled)
                {
                    DisconnectAndClose();
                
                    if (e.Cancelled)
                    {
                        mw.restart(true, "Registrazione fallita: problema di comunicazione con il server.");
                    }
                    else
                    {
                        mw.restart(true, e.Error.Message);

                    }
                }
                else
                {
                    // Se sono nell'else è perché la e.Result contiene un risultato consistente.
                    object[] parameters = e.Result as object[];
                    RegistratiControl rc = (RegistratiControl)parameters[3];

                    string message = ReadStringFromStream();
                    if (message.Contains(OK))
                    {
                        rc.Registrati_Esito(true);
                        UpdateNotifyIconConnesso();
                        connesso = true;

                    }
                    else
                    {
                        String tmp = message.Substring(1, message.Length - 1);
                        String messaggioErrore = message.Substring(tmp.IndexOf('>') + 2, tmp.Length - tmp.IndexOf('>') - 1);
                        //se l'autenticazione non va a buon fine torno alla finestra principale e chiude lo stream
                        rc.Registrati_Esito(false, "Registrazione fallita: " + messaggioErrore);
                    }
                }
            }
            catch (Exception exc)
            {
                DisconnectAndClose();
                mw.restart(true, exc.Message);
            }
        }


        private string EffettuaCifraturaSimmetrica(byte[] key, string secretMessage, out byte[] iv)
        {

            Aes aes = new AesCryptoServiceProvider();
            aes.Key = key;
            iv = aes.IV;

            // Encrypt the message
            using (MemoryStream ciphertext = new MemoryStream())
            using (CryptoStream cs = new CryptoStream(ciphertext, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] plaintextMessage = Encoding.UTF8.GetBytes(secretMessage);
                cs.Write(plaintextMessage, 0, plaintextMessage.Length);
                cs.Close();
                return BitConverter.ToString(ciphertext.ToArray());
            }

        }

        #endregion

        #region Metodi di Disconnesione e Logout
        
        internal void Logout(MenuControl menuc)
        {
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_Logout);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_LogoutCompleted);
            workertransaction.RunWorkerAsync(menuc);

        }

        private void Workertransaction_Logout(object sender, DoWorkEventArgs e)
        {
            WriteStringOnStream(LOGOUT + username);
            e.Result = e.Argument;
        }

        private void Workertransaction_LogoutCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    DisconnectAndClose();
                    mw.restart(true, e.Error.Message);
                }
                else
                {
                    object res = e.Result;
                    MenuControl menuc = (MenuControl)res;
                    String message = ReadStringFromStream();
                    if (message.Contains(OK))
                    {
                        connesso = false;
                        UpdateNotifyIconDisconnesso();
                        menuc.Logout_Esito(true);
                    }
                    else
                    {
                        //se il logout non va a buon fine torno alla MainControl e chiudo lo stream
                        menuc.Logout_Esito(false, "Errore nella procedura di logout");
                        mw.restart(false);
                        DisconnectAndClose(false);
                    }
                }
            }
            catch (Exception exc)
            {
                DisconnectAndClose();
                mw.restart(true, exc.Message);
            }
        }

        // Questo metodo e relativi worker veniva chiamato dal dialog che abbiamo disabilitato

        internal void DisconnettiServer(Boolean esci)
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_Disconnect);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_DisconnectCompleted);
            workertransaction.RunWorkerAsync(esci);

        }

        private void Workertransaction_Disconnect(object sender, DoWorkEventArgs e)
        {
            if (connesso)   //devo fare logout + disconnessione
            {
                WriteStringOnStream(DISCONNETTIUTENTE + username);
                connesso = false;
            }
            else            //devo solo fare disconnessione
            {
                WriteStringOnStream(DISCONNETTICLIENT);
            }

            if (clientsocket.Connected)
            {
                clientsocket.GetStream().Close();
                clientsocket.Close();
            }

            UpdateNotifyIconDisconnesso();
            e.Result = e.Argument;
        }

        private void Workertransaction_DisconnectCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                {
                    DisconnectAndClose(false);
                    mw.restart(true, e.Error.Message);
                }
                else
                {
                    mw.restart(false);
                    Boolean esci = (Boolean)e.Result;
                    if (esci)
                    {
                        mw.Close();
                    }
                }
            }
            catch (Exception exc)
            {
                DisconnectAndClose(false);
                mw.restart(true, exc.Message);
            }
        }

        public static void UpdateNotifyIconDisconnesso()
        {
            MainWindow.MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/imageres_1040.ico");
        }

        public static void UpdateNotifyIconConnesso()
        {
            MainWindow.MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/imageres_1040.ico");
        }

        /*
         * Se disconnect è true comunica al server la disconnessione del client, eventualmente effettua il logout
         * e chiude il socket - tutto gestito nel worker della DisconnettiServer
         * Se disconnect è false chiude solo il socket senza comunicare col server - utile per situazioni delicate
         * come errori durante il logout
         */
        public void DisconnectAndClose(bool disconnect = true)
        {

            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
            if (clientsocket.Connected)
            {
                if (disconnect && System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    DisconnettiServer(false);
                }
                else
                {
                    clientsocket.GetStream().Close();
                    clientsocket.Close();
                }
            }
        }

        #endregion

        #region Accesso allo stream
        /*
         * Scrive nello stream l'array di byte passato come parametro
         */
        public void WriteByteArrayOnStream(byte[] message)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                if (timer != null)
                {
                    timer.Change(TimeSpan.FromSeconds(POLLING), TimeSpan.FromSeconds(POLLING)); //rinvio l'esecuzione della callback del timer di un minuto
                }
                NetworkStream stream = clientsocket.GetStream();
                stream.WriteTimeout = 30000;
                stream.Write(message, 0, message.Length);
            }
            else
            {
                throw new Exception("Network exception");
            }
        }

        /*
         * Legge dallo stream tanti byte quanti buffer.Length e li inserisce in buffer
         */
        public void ReadByteArrayFromStream(byte[] buffer)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                if (timer != null)
                {
                    timer.Change(TimeSpan.FromSeconds(POLLING), TimeSpan.FromSeconds(POLLING)); //rinvio l'esecuzione della callback del timer di un minuto
                }
                NetworkStream stream = clientsocket.GetStream();
                stream.ReadTimeout = 30000;
                if (stream.Read(buffer, 0, buffer.Length) == 0)
                {
                    throw new IOException("No data is available");
                }
            }
            else
            {
                throw new Exception("Network exception");
            }
        }

        public void WriteStringOnStream(string message)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                if (timer != null)
                {
                    timer.Change(TimeSpan.FromSeconds(POLLING), TimeSpan.FromSeconds(POLLING)); //rinvio l'esecuzione della callback del timer di un minuto
                }
                NetworkStream stream = clientsocket.GetStream();
                stream.WriteTimeout = 30000;
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(message); //preparo i dati per l'invio sul canale
                stream.Write(data, 0, data.Length);
            }
            else
            {
                throw new Exception("Network exception");
            }
        }

        // Riscrivere come ReadArrayByte
        public string ReadStringFromStream()
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                if (timer != null)
                {
                    timer.Change(TimeSpan.FromSeconds(POLLING), TimeSpan.FromSeconds(POLLING)); //rinvio l'esecuzione della callback del timer di un minuto
                }
                NetworkStream stream = clientsocket.GetStream();
                stream.ReadTimeout = 30000;
                Byte[] data = new Byte[512];
                String responseData = String.Empty;
                Int32 bytes = stream.Read(data, 0, data.Length);
                if (bytes == 0)
                {
                    throw new IOException("No data is available");
                }
                responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);    //parsifico i byte ricevuti
                return responseData;
            }
            else
            {
                throw new Exception("Network exception");
            }
        }
        #endregion

        #region Metodi invio file
        /*
         * Invocato da MenuControl.EffettuaBackup_Click. Riceve un array di path di file 
         * e li invia al server sfruttando diversi thread di workertransaction 
         */
        public void InvioFile(string[] Filenames)
        {
            event_1 = new AutoResetEvent(false);    //usato per la sincronizzazione dei thread
            workertransaction = new BackgroundWorker();
            workertransaction.WorkerReportsProgress = true;
            workertransaction.ProgressChanged += new ProgressChangedEventHandler(WorkerProgressChanged);    //chiamabile tramite ReportProgress se WorkerReportsProgress è true
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_InviaFile);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workertransaction_InviaFileCompleted);

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
            bool inviato = false;
            lavorandoInvio = true;
            object[] parameters = e.Argument as object[];
            string[] resultArray = Array.ConvertAll(parameters, x => x.ToString()); //riottengo le stringhe con i filenames
            int nFile = 100 / resultArray.Length;   //per calcolare la percentuale di progresso
            foreach (string name in resultArray)
            {
                WriteStringOnStream(ClientLogic.FILE + username);  //scrivo lo username sullo stream preceduto da FILE

                InviaFile(name, ref inviato);

                //gestisco i codici di errore ritornati da IviaFile
                if (inviato)
                {
                        workertransaction.ReportProgress(nFile, "Ultimo file sincronizzato: " + Path.GetFileName(name)); //si invoca la callback per monitorare il progresso
                        string message = ReadStringFromStream();
                        if (message == (ERRORE + "Invio file non riuscito"))
                        {
                            UpdateNotifyIconDisconnesso();
                        }
                        e.Result = true;
                }
                else   
                {
                    workertransaction.ReportProgress(nFile, "Ultimo file sincronizzato: " + Path.GetFileName(name));
                    e.Result = true;
                }
                if (monitorando == false)   //se monitorando vale false in questo contesto vuol dire che ho voluto interrompere da MenuControl il backup
                {
                    e.Cancel = true;
                    return;
                }
            }
            this.WriteStringOnStream(ClientLogic.ENDSYNC + username + ">" + cartellaMonitorata);    //comunico al server che ho terminato l'invio dei file

        }

        /*
         * Callback per la fine della sincronizzazione 
         */
        private void workertransaction_InviaFileCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                MenuControl menuc = (MenuControl)mw.Content;
                menuc.pbStatus.Value = 0;
                menuc.pbStatus.Visibility = Visibility.Hidden;
                menuc.EffettuaBackup.IsEnabled = true;
                menuc.EffettuaBackup.Visibility = Visibility.Visible;
                menuc.RestoreFile.IsEnabled = true;
                BrushConverter bc = new BrushConverter();
                lavorandoInvio = false;
                event_1.Set();

                if (e.Error != null)
                {
                    DisconnectAndClose();
                    mw.restart(true, e.Error.Message);
                }
                else if (e.Cancelled)
                {
                    //Se l'invio file è stato annullato  lo si comunica all'utente nella TextBox FileUploading
                    menuc.EffettuaBackup.Content = "Sincronizza";
                    menuc.EffettuaBackup.Foreground = (Brush)bc.ConvertFrom("Black");
                    menuc.FolderButton.IsEnabled = true;
                    menuc.FileUploading.Text = "Non tutti i dati sono aggiornati";

                }
                else
                {
                    //altrimenti si comunica il 'successo'
                    menuc.FileUploading.Text = "Ultima sincronizzazione : " + DateTime.Now;

                }
            }
            catch
            {
                DisconnectAndClose();
                mw.restart(true);
            }
        }

        /*
         * Callback per monitorare il progresso dell'invio chiamata da workertransaction.ReportProgress
         * Aggiorna la ProgressBar e la TextBox di FileUploading
         */
        private void WorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            MenuControl menuc = (MenuControl)mw.Content;
            menuc.pbStatus.Value += e.ProgressPercentage;
            menuc.FileUploading.Text = (string)e.UserState;
        }

        /*
         * Invia un header al server con info sul file tra cui una checksum
         * Confrontando la checksum il server comunica se è interessato alla versione corrente del file o meno
         * Se necessario si invia il file relativo a Filename al server
         */
        private void InviaFile(string Filename, ref bool inviato)
        {
            byte[] buffer = null;
            byte[] header = null;
            string checksum = "";

            ReadStringFromStream();     //?? per svuotare lo stream? per lanciare eccezione?
            checksum = GetMD5HashFromFile(Filename);
            FileStream fs = new FileStream(Filename, FileMode.Open);    //apro il file da inviare come uno stream
            int bufferCount = Convert.ToInt32(Math.Ceiling((double)fs.Length / (double)BUFFERSIZE));    //numero di buffer necessari per salvare il file

            //preparo una string header da inviare al server per informarlo sul file che invierò tra poco
            string headerStr = "Content-length:" + fs.Length.ToString() + "\r\nFilename:" + Filename + "\r\nChecksum:" + checksum + "\r\n";
            header = new byte[BUFFERSIZE];
            Array.Copy(Encoding.ASCII.GetBytes(headerStr), header, Encoding.ASCII.GetBytes(headerStr).Length);  //FIXME: e se il filename è troppo lungo?!
            clientsocket.Client.Send(header);   //invio l'header tramite il canale socket
            string streamReady = ReadStringFromStream();

            if (streamReady.Equals(OK + "File ricevuto correttamente") || streamReady.Equals(INFO + "File non modificato") || streamReady.Equals(ClientLogic.INFO + "file dim 0"))
            {
                //chiudo il FileStream visto che non è necessario inviarlo
                fs.Close();
                fs.Dispose();
                inviato = false;
            }
            else
            {
                //invio tanti pezzettini di file quanto necessario
                for (int i = 0; i < bufferCount; i++)
                {
                    buffer = new byte[BUFFERSIZE];
                    int size = fs.Read(buffer, 0, BUFFERSIZE);
                    clientsocket.Client.SendTimeout = 30000;
                    clientsocket.Client.Send(buffer, size, SocketFlags.Partial);
                }
                fs.Close();
                fs.Dispose();
                inviato = true;
            }
        }
        #endregion

        #region Utils
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

        /*
         * Metodo chiamato da RestoreUC.onSelectionChanged
         * obj è un oggetto della UI e ritorna il primo figlio (ricorsivo) che è di tipo T
         * usata per estrarre una Label da un ListBoxItem che contiene StackPanel
         */
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
        #endregion

    }
}
