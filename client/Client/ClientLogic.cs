using MahApps.Metro.Controls;
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

        #region Costanti comandi
        private const int BUFFERSIZE = 1024;
        private const int CHALLENGESIZE = 64;
        public const string OK = "+OK+";
        public const string ERRORE = "+ERR+";
        public const string INFO = "+INFO+";
        public const string REGISTRAZIONE = "+REG+";
        public const string ECDH = "+ECDH+";   //key agreement per proteggere la registrazione
        public const string LOGIN = "+LOGIN+";
        public const string LOGOUT = "+LOGOUT+";
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

        #region Costruttori
        public ClientLogic(TcpClient clientSocketPassed, IPAddress ipPassed, int portPassed, MainWindow mainwindow, MainControl maincontrol)
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
            workertransaction.RunWorkerAsync(maincontrol);
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
                //MainControl main = new MainControl(1);  //FIXME: magicnumber!
                //App.Current.MainWindow.Content = main;
                mw.restart(true);
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

            e.Result = e.Argument;
        }


        void Workertransaction_ConnectionCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            
            MainControl mc = (MainControl)e.Result;
            mac = GetMacAddress();  //FIXME: al prof non è piaciuta sta cosa del mac

            if (errore == 1)
            {
                //MainControl main = new MainControl(errore);
                //App.Current.MainWindow.Content = main;
                //return;
                // Perché creare una nuova MainControl solo per passare l'errore?
                // Qui sono ancora nel MainControl
                mc.Esito_Connect(false);
            } else if (mac.Equals(String.Empty)) {
                //MainControl main = new MainControl(1);  //FIXME: magicnumber!
                //App.Current.MainWindow.Content = main;
                //return;
                // Perché creare una nuova MainControl solo per passare l'errore?
                // Qui sono ancora nel MainControl
                mc.Esito_Connect(false);
            } else {
                mc.Esito_Connect(true);
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

            object[] res = { ERRORE + "Errore inatteso nel login", parameters[2] }; //oggetto che verrà analizzato da Workertransaction_LoginCompleted
            e.Result = res;

            try
            {
                //comunico al server che voglio iniziare il login
                //mando LOGIN + username
                WriteStringOnStream(LOGIN + username);

                //mi aspetto OK
                String response = ReadStringFromStream();
                if (!response.Equals(OK) )
                {
                    //devo comunicare qualcosa alla login completed in e
                    res[0] = response;
                    //throw new Exception();//gestire le eccezioni 
                    return;
                }

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

                //ricevo il responso del server
                response = ReadStringFromStream();
                res[0] = response;
                if (!response.Contains(OK))
                {
                    //throw new Exception();  //gestire le eccezioni 
                    return;
                }
            }
            catch   //il backgroudworker gestisce le eccezioni in modo particolare http://stackoverflow.com/questions/1044460/unhandled-exceptions-in-backgroundworker
            {
                if (clientsocket.Connected)
                {
                    clientsocket.GetStream().Close();
                    clientsocket.Close();
                }
                //MainControl main = new MainControl(1);  //FIXME: magicnumber
                //App.Current.MainWindow.Content = main;
                mw.restart(true);
            }
            e.Result = res;
        }

        /*
         * Analizzo la risposta del server ed eventualmente passo il controllo a MenuControl
         */
        private void Workertransaction_LoginCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            object[] parameters = e.Result as object[]; //errore - http://stackoverflow.com/questions/1044460/unhandled-exceptions-in-backgroundworker
            LoginControl lc = (LoginControl)parameters[1];
            String message = (String)parameters[0];

            try
            {
                if (message.Contains(OK))
                {
                    //MenuControl main = new MenuControl();
                    //App.Current.MainWindow.Content = main;
                    lc.Esito_Login(true);
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
                        //MainControl main = new MainControl(1);
                        //App.Current.MainWindow.Content = main;
                        mw.restart(true);
                    }
                    else
                    {
                        lc.Esito_Login(false, messaggioErrore);
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
                //MainControl main = new MainControl(1);
                //App.Current.MainWindow.Content = main;
                mw.restart(true);
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

            try
            {
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
                    Console.WriteLine("Risposta inaspettata dal server");
                    throw new Exception();
                }
                String[] parametri = serverResponse.Split('+');
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
                String secretMessage = username + '+' + password;
                encryptedMessage = EffettuaCifraturaSimmetrica(clientKey, secretMessage, out iv);

                //invio al server credenziali cifrate + iv
                string ivString = BitConverter.ToString(iv);
                Console.WriteLine("IV" + ivString.Length + ": " + ivString);
                Console.WriteLine("ciphertext: " + encryptedMessage);

                String messaggio = REGISTRAZIONE + ivString + '+' + encryptedMessage;
                WriteStringOnStream(messaggio);
                Console.WriteLine("messaggio: " + messaggio);
                e.Result = parameters;
            }
            catch
            {
                if (clientsocket.Connected)
                {
                    clientsocket.GetStream().Close();
                    clientsocket.Close();
                }
                //MainControl main = new MainControl(1);  //FIXME: magicnumber
                //App.Current.MainWindow.Content = main;
                mw.restart(true);
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


        /*
         * Callback lanciata quando termina l'invio delle credenziali per la registrazione. 
         * Attende risposta da server leggendo dallo stream
         */
        private void Workertransaction_RegistrazioneCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
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
                    String messaggioErrore = message.Substring(tmp.IndexOf('+') + 2, tmp.Length - tmp.IndexOf('+') - 1);
                    //se l'autenticazione non va a buon fine torno alla finestra principale e chiude lo stream
                    rc.Registrati_Esito(false, "Registrazione fallita: " + messaggioErrore);
                    //MainControl main = new MainControl(1);  //FIXME: magicnumber
                    //App.Current.MainWindow.Content = main;
                    //mw.restart(true);
                    //if (mw.clientLogic.clientsocket.Client.Connected)   //FIXME: ma mw.clientLogic non punta a questo stesso oggetto?!
                    //{
                    //    mw.clientLogic.clientsocket.GetStream().Close();
                    //    mw.clientLogic.clientsocket.Close();
                    //}
                    //if (this.clientsocket.Client.Connected)
                    //{
                    //    this.clientsocket.GetStream().Close();
                    //    this.clientsocket.Close();
                    //}
                    //return;
                }
            }
            catch
            {
                if (mw.clientLogic.clientsocket.Connected)
                {
                    mw.clientLogic.clientsocket.GetStream().Close();
                    mw.clientLogic.clientsocket.Close();
                }
                //MainControl main = new MainControl(1);
                //App.Current.MainWindow.Content = main;
                mw.restart(true);
                return;
            }
        }

        /*callback usata sia in caso di registrazione sia in caso di login
         il discriminante è ciò che è contenuto in action dopo che l'array dei parametri viene parsificato
        */
        //private void Workertransaction_LoginRegistrazione(object sender, DoWorkEventArgs e)
        //{
        //    object[] parameters = e.Argument as object[];
        //    string[] resultArray = Array.ConvertAll(parameters, x => x.ToString());
        //    string username = resultArray[0];
        //    this.username = username;
        //    string password = resultArray[1];
        //    string action = resultArray[2];
        //    try
        //    {
        //        WriteStringOnStream(action + username + "+" + password + "+" + mac);    //invio al server le credenziali - IN CHIARO
        //    }
        //    catch
        //    {
        //        if (clientsocket.Connected)
        //        {
        //            clientsocket.GetStream().Close();
        //            clientsocket.Close();
        //        }
        //        //MainControl main = new MainControl(1);  //FIXME: magicnumber
        //        //App.Current.MainWindow.Content = main;
        //        mw.restart(true);
        //    }

        //}
        #endregion

        #region Metodi di Disconnesione e Logout
        internal void Logout(MenuControl mc)
        {
            object window = mc;
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_Logout);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_LogoutCompleted);
            workertransaction.RunWorkerAsync(mc);

        }

        private void Workertransaction_Logout(object sender, DoWorkEventArgs e)
        {
            object[] res = { ERRORE + "Errore inatteso nel logout", e.Argument }; //oggetto che verrà analizzato da Workertransaction_LoginCompleted
            e.Result = res;
            WriteStringOnStream(LOGOUT + username);
            String message = ReadStringFromStream();
            res[0] = message;
            e.Result = res;
        }

        private void Workertransaction_LogoutCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            object[] res = e.Result as object[];
            String message = (String)res[0];
            MenuControl mc = (MenuControl)res[1];
            if (message.Contains(OK))
            {
                connesso = false;
                UpdateNotifyIconDisconnesso();
                mc.Logout_Esito(true);
            }
            else
            {
                //se il logout non va a buon fine torno alla MainControl e chiudo lo stream
                mc.Logout_Esito(false, "Errore nella procedura di logout");
                mw.restart(false);
                if (this.clientsocket.Client.Connected)
                {
                    this.clientsocket.GetStream().Close();
                    this.clientsocket.Close();
                }
                return;
            }
        }

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
                //MainControl main = new MainControl();
                //App.Current.MainWindow.Content = main;
                mw.restart(true);
                return;
            }
        }

        private void Workertransaction_DisconnectCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateNotifyIconDisconnesso();
            //MainControl main = new MainControl();
            //App.Current.MainWindow.Content = main;
            mw.restart(false);
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

        #region Accesso allo stream
        /*
         * Scrive nello stream l'array di byte passato come parametro
         */
        public void WriteByteArrayOnStream(byte[] message)
        {
            TcpState statoConn = GetState(clientsocket);
            if (statoConn == TcpState.Established)
            {
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
            TcpState statoConn = GetState(clientsocket);
            if (statoConn == TcpState.Established)
            {
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
        #endregion

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
                    this.WriteStringOnStream(ClientLogic.FILE + username);  //scrivo lo username sullo stream preceduto da FILE (per avvisare il server?)
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
                if (retVal == 1)    //FIXME: magicnumber - file inviato
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
                else if (retVal == 0)   //FIXME: magicnumber - Non è stato necessario inviare il file
                {
                    workertransaction.ReportProgress(nFile, "Ultimo file sincronizzato: " + Path.GetFileName(name));
                    e.Result = true;
                }
                else if (retVal == 2)   //FIXME: magicnumber - eccezione
                {
                    e.Result = false;
                    break;
                }
                if (monitorando == false)   //monitorando vale false in modo anomalo (?) - flaggo il task come canceled
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
                MenuControl menuc = (MenuControl)mw.Content;
                menuc.pbStatus.Value = 0;
                menuc.pbStatus.Visibility = Visibility.Hidden;
                menuc.Wait.Visibility = Visibility.Hidden;
                menuc.EffettuaBackup.IsEnabled = true;
                menuc.EffettuaBackup.Visibility = Visibility.Visible;
                BrushConverter bc = new BrushConverter();
                if (e.Cancelled)
                {
                    //Se l'invio file è stato annullato  si ritorna ad uno stato stabile (senza rollback?) e lo si comunica all'utente nella TextBox FileUploading
                    // Questi comandi non dovrebbero essere qui
                    menuc.EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FF44E572");
                    menuc.EffettuaBackup.Content = "Start";
                    menuc.FolderButton.IsEnabled = true;
                    menuc.FileUploading.Text = "Non tutti i dati sono aggiornati";
                    monitorando = false;
                    MenuControl menuC = (MenuControl)App.Current.MainWindow.Content;
                    if (menuC.exit) //non capisco exit cosa gestisce
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
                        //MainControl main = new MainControl();
                        //App.Current.MainWindow.Content = main;
                        mw.restart(false);
                    }
                }
                else
                {
                    //altrimenti si comunica il 'successo'
                    menuc.FileUploading.Text = "Ultima sincronizzazione : " + DateTime.Now;
                    lavorandoInvio = false;
                    event_1.Set();

                    //se è avvenuto qualche problema nell'esecuzione del task torno a MainControl con un errore
                    if ((bool)e.Result == false)
                    {
                        //MainControl main = new MainControl(1);  //FIXME: magicnumber
                        //App.Current.MainWindow.Content = main;
                        mw.restart(true);
                        return;
                    }
                }
            }
            catch
            {
                //gestione eccezione chiudo stream e socket
                MainWindow mainw = (MainWindow)App.Current.MainWindow;
                if (mainw.clientLogic.clientsocket.Connected)
                {
                    mainw.clientLogic.clientsocket.GetStream().Close();
                    mainw.clientLogic.clientsocket.Close();
                }
                //MainControl main = new MainControl(1);
                //App.Current.MainWindow.Content = main;
                mw.restart(true);
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

                //preparo una string header da inviare al server per informarlo sul file che invierò tra poco
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

        private /*async*/ void messaggioErrore(string mess)
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //await mw.ShowMessageAsync("Errore", mess);
            MessageBoxResult result = System.Windows.MessageBox.Show(mess, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        #endregion

    }
}
