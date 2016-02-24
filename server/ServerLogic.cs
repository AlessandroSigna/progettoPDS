using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BackupServer
{
    public class ServerLogic
    {
        BackgroundWorker workertransaction;
        private TcpListener serverSocket;
        private int port;
        private MainWindow mainWindow;
        private int counterClient = 0;

        #region Costanti
        private const int BUFFERSIZE = 1024;
        private const int CHALLENGESIZE = 64;
        public const string OK = ">OK>";
        public const string RESTART = ">RESTART>";
        public const string ERRORE = ">ERR>";
        public const string STOP = ">STOP>";
        public const string REGISTRAZIONE = ">REG>";
        public const string ECDH = ">ECDH>";   //key agreement per proteggere la registrazione
        public const string LOGIN = ">LOGIN>";
        public const string LOGOUT = ">LOGOUT>";
        public const string DISCONETTI = ">DISCO>"; //obsoleto
        public const string DISCONNETTICLIENT = ">DISCOCLIENT>"; //chiude lo stream di comunicazione con il client (utente non loggato)
        public const string EXITDOWNLOAD = ">EXITDOWNLOAD>";
        public const string INFO = ">INFO>";
        public const string GETVFILE = ">GVF>"; // +GVF+user+file -> Prendo le versioni di un file
        public const string GETFILEV = ">GETVF>"; // +GETVF+user+file+versione -> Invio la specifica versione di un file
                                                  // public const string GETFILE = ">GETF>"; // +GETF+user+file ??non serve??
        public const string GETFOLDERUSER = ">GETFOLDUSER>"; // +GETFOLDUSER+user
        public const string FOLDER = ">FOLDER>";
        public const string LISTFILES = ">LISTFILES>"; //+LISTFILES+user+folder+nomeLike (solo nome, senza path -> barra di ricerca. Se nullo, allora nomeLike="")
        public const string RESTORE = ">RESTORE>"; // +RESTORE+user+folderBackup+folderDestinazione (voglio anche la destinazione, così creo una nuova folderRoot) 
        public const string FILE = ">FILE>";
        public const string DISCONNETTIUTENTE = ">DISCUTENTE>"; //+DISCUTENTE+user
        public const string RENAMEFILE = ">RENAMEFI>"; //+RENAMEFILE+user+fileNameOLD+fileNameNEW
        public const string CANC = ">CANC>"; //+CANC+user+filename
        public const string ENDSYNC = ">ENDSYN>"; //+ENDSYN+username
        public const string NUMFILE = ">NUMFL>";
        public const string FLP = ">FLP>";
        public const string ENDLIST = ">ENDLIST>"; //+ENDSYN+username
        public const string ECHO_REQUEST = ">ECHO_REQUEST>";

        public const int TIME_OUT = 60000; //(ms) timeout per la read sullo stream (mi aspetto un messaggio di hearthbeat ogni 20 sec)

        #endregion

        public static bool serverKO = true;
        public static ReaderWriterLockSlim _readerWriterLock = new ReaderWriterLockSlim();

        public ServerLogic(ref TcpListener serverSocketPassed, int portPassed, MainWindow mw)
        {
            mainWindow = mw;
            port = portPassed;
            serverSocket = mw.serverSocket; //serverSocket come argomento del costruttore, preso comunque direttamente da MainWindow
            
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(workertransaction_DoWork);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workertransaction_RunWorkerCompleted);
            workertransaction.RunWorkerAsync();
        }

        #region Metodi di connessione
        void workertransaction_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Run worker completed. ");
        }
        
        /*
         * Thread che attende connessioni dai client.
         * 
         * Task principale del server: loop in attesa di connessioni. 
         * Quando avviene la connessione con un client la comunicazione è delegata a un BackgroundWorker
         * e il thread si rimette in attesa di altri client
         */
        void workertransaction_DoWork(object sender, DoWorkEventArgs e)
        {
            TcpClient clientsocket;

            while (true)
            {
                try
                {
                    // Apre il socket e si mette in ascolto
                    clientsocket = (mainWindow.serverSocket).AcceptTcpClient();
                }
                catch
                {
                    mainWindow.tb.Dispatcher.Invoke(new BackupServer.MainWindow.UpdateTextCallback(mainWindow.UpdateText), new object[] { DateTime.Now + " - Connessione chiusa dal server\n" });
                    break;
                }
                counterClient++;
                mainWindow.listaClient.Add(clientsocket);

                mainWindow.tb.Dispatcher.Invoke(new BackupServer.MainWindow.UpdateTextCallback(mainWindow.UpdateText), new object[] { DateTime.Now + " - Cliente connesso n: " + counterClient + "\n" });
                Console.WriteLine("Cliente connesso n: " + counterClient + "\n");

                // Operazioni eseguite in thread separati per client
                BackgroundWorker nuovoClientConnesso = new BackgroundWorker();
                object paramAct = clientsocket;
                object[] parameters = new object[] { paramAct };
                nuovoClientConnesso.DoWork += new DoWorkEventHandler(nuovoClientConnesso_DoWork);
                nuovoClientConnesso.RunWorkerCompleted += new RunWorkerCompletedEventHandler(nuovoClientConnesso_RunWorkerCompleted);
                nuovoClientConnesso.RunWorkerAsync(parameters);
    }
        }

        private void nuovoClientConnesso_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Run Worker Completed.");
        }

        /*
         * Thread generato per ogni client connesso.
         * Ritorna quando la comunicazione deve essere interrotta (stream chiuso)
         */
        private void nuovoClientConnesso_DoWork(object sender, DoWorkEventArgs e)
        {
            object[] parameters = e.Argument as object[];
            TcpClient clientsocket = (TcpClient)parameters[0];
            readStringFromStream(clientsocket);
        }

        #endregion

        #region Metodi handler comandi

        private string comandoECDH(string responseData, TcpClient clientsocket, out String username)
        {
            username = null;
            Console.WriteLine(responseData);
            try
            {
                //mi aspetto dal client: ECDH + chiave pubblica client
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri != 3)
                {
                    return ERRORE + "Numero di paramentri passati per ECDH errato: " + numParametri;
                }

                String clientPublicKeyString = parametri[2];    //ricavo stringa con la chiave pubblica inviata dal client
                Console.WriteLine("ClientPublicKey " + clientPublicKeyString);

                //converto la chiave pubblica del client da string in byte[]
                String[] arr = clientPublicKeyString.Split('-');
                byte[] clientPublicKey = new byte[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                {
                    clientPublicKey[i] = Convert.ToByte(arr[i], 16);
                }

                byte[] serverPublicKey;
                string serverPublicKeyString;
                ECDiffieHellmanCng serverECDH = new ECDiffieHellmanCng();   //istanzio la struttua dati per l'ECDH

                serverECDH.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
                serverECDH.HashAlgorithm = CngAlgorithm.Sha256;
                serverPublicKey = serverECDH.PublicKey.ToByteArray();       //chiave pubblica del server in byte[]
                serverPublicKeyString = BitConverter.ToString(serverPublicKey);     //chiave convertita in string
                writeStringOnStream(clientsocket, ECDH + serverPublicKeyString);    //invio al client la mia chiave pubblica
                Console.WriteLine("ServerPublicKey " + serverPublicKeyString);
                
                //derivo la chiave simmetrica del server per questa connessione
                CngKey k = CngKey.Import(clientPublicKey, CngKeyBlobFormat.EccPublicBlob);
                byte[] serverKey = serverECDH.DeriveKeyMaterial(k);
                Console.WriteLine("ServerSimmetricKey " + BitConverter.ToString(serverKey));
                return registrazioneSicura(serverKey, clientsocket, out username);
            }
            catch
            {
                return ERRORE + "Errore durante DH";
            }
        }

        private string registrazioneSicura(byte[] simmetricKey, TcpClient clientsocket, out String username)
        {
            username = null;
            SQLiteTransaction transazioneReg = mainWindow.m_dbConnection.BeginTransaction();

            try
            {

                //devo ricevere dal client REG + IV + {username + password}chiave simmetrica
                String responseData = ReadStringFromStream(clientsocket);
                Console.WriteLine("messaggio: " + responseData);

                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri != 4)
                {
                    transazioneReg.Rollback();
                    transazioneReg.Dispose();
                    return ERRORE + "Numero di paramentri passati per la registrazione sicura errato";
                }

                String comando = parametri[1];
                String ivString = parametri[2];
                String ciphertextString = parametri[3];

                if (comando == null || !comando.Equals(REGISTRAZIONE.Replace('>', ' ').Trim()))
                {
                    transazioneReg.Rollback();
                    transazioneReg.Dispose();
                    return ERRORE + "Comando errato";
                }

                //converto IV e ciphertext in byte[]
                String[] arr = ivString.Split('-');
                byte[] iv = new byte[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                {
                    iv[i] = Convert.ToByte(arr[i], 16);
                }

                String[] arr2 = ciphertextString.Split('-');
                byte[] ciphertext = new byte[arr2.Length];
                for (int i = 0; i < arr2.Length; i++)
                {
                    ciphertext[i] = Convert.ToByte(arr2[i], 16);
                }
                Console.WriteLine("IV: " + ivString);
                Console.WriteLine("ciphertext: " + ciphertextString);

                //decifro
                String plaintext = EffettuaDecifraturaSimmetrica(ciphertext, iv, simmetricKey);
                String[] credenziali = plaintext.Split('>');
                String user = credenziali[0].ToUpper();
                String pass = credenziali[1];
                Console.WriteLine("user: " + user);
                Console.WriteLine("pass: " + pass);


                SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP.CommandText = "SELECT * FROM UTENTI WHERE username=@username";
                comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP.Transaction = transazioneReg;

                try
                {
                     _readerWriterLock.EnterReadLock();
            
                    if (comandoP.ExecuteScalar() != null)
                    {
                        transazioneReg.Rollback();
                        transazioneReg.Dispose();
                        return ERRORE + "Utente gia' registrato";
                    }
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                SQLiteCommand comandoP2 = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP2.CommandText = "INSERT INTO UTENTI (username,password) VALUES(@username,@password)";
                comandoP2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP2.Parameters.Add("@password", System.Data.DbType.String, pass.Length).Value = pass;
                comandoP2.Transaction = transazioneReg;

                bool isBroken = false;
                do
                {
                    try
                    {
                        _readerWriterLock.EnterWriteLock();
                        if (_readerWriterLock.WaitingReadCount > 0)
                        {
                            isBroken = true;
                        }
                        else
                        {
                            if (comandoP2.ExecuteNonQuery().Equals(0))
                            {
                                transazioneReg.Rollback();
                                transazioneReg.Dispose();
                                return ERRORE + "Errore durante la registrazione";
                            }
                        }
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                    if (isBroken)
                    {
                        Thread.Sleep(10);
                    }
                    else
                        isBroken = false;
                } while (isBroken);

                SQLiteCommand comandoP4 = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP4.CommandText = "INSERT INTO UTENTILOGGATI (username,lastUpdate) VALUES (@username,@lastUpdate)";
                comandoP4.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP4.Parameters.Add("@lastUpdate", System.Data.DbType.String, DateTime.Now.ToString().Length).Value = DateTime.Now.ToString();
                comandoP4.Transaction = transazioneReg;

                bool isBroken2 = false;
                do
                {
                    try
                    {
                        _readerWriterLock.EnterWriteLock();
                        if (_readerWriterLock.WaitingReadCount > 0)
                        {
                            isBroken2 = true;
                        }
                        else
                        {
                            if (comandoP4.ExecuteNonQuery() != 1)
                            {
                                transazioneReg.Rollback();
                                transazioneReg.Dispose();
                                return ERRORE + "Errore durante la registrazione";
                            }
                        }
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                    if (isBroken2)
                    {
                        Thread.Sleep(10);
                    }
                    else
                        isBroken2 = false;
                } while (isBroken2);

                //throw new Exception("Eccezione generata manualmente.");
                transazioneReg.Commit();
                transazioneReg.Dispose();
                username = user;
                return OK + "Registrazione avvenuta correttamente!";
            }
            catch
            {
                transazioneReg.Rollback();
                transazioneReg.Dispose();

                return ERRORE + "Errore durante la registrazione";
            }
        }

        private string EffettuaDecifraturaSimmetrica(byte[] encryptedMessage, byte[] iv, byte[] key)
        {
            Aes aes = new AesCryptoServiceProvider();
            aes.Key = key;
            aes.IV = iv;
            // Decrypt the message
            using (MemoryStream plaintext = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(plaintext, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(encryptedMessage, 0, encryptedMessage.Length);
                    cs.Close();
                    string message = Encoding.UTF8.GetString(plaintext.ToArray());
                    Console.WriteLine("Plaintext: " + message);
                    return message;
                }
            }
            
        }


        #region Comandi Login/Logout
        // Chiamato anche da comando disconnetti.
        private string comandoLogout(string responseData)
        {
            SQLiteTransaction transazioneLogout = mainWindow.m_dbConnection.BeginTransaction();
            try
            {
                //mi aspetto LOGOUT + username
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri != 3)
                {
                    transazioneLogout.Commit();
                    transazioneLogout.Dispose();
                    return ERRORE + "Numero di paramentri passati per il logout errato";
                }

                String user = parametri[2].ToUpper();

                SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP.CommandText = "DELETE FROM UTENTILOGGATI WHERE username=@username";
                comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP.Transaction = transazioneLogout;
                bool isBroken = false;
                do
                {
                    try
                    {
                        _readerWriterLock.EnterWriteLock();
                        if (_readerWriterLock.WaitingReadCount > 0)
                        {
                            isBroken = true;
                        }
                        else
                        {
                            if (comandoP.ExecuteNonQuery() != 1)
                            {
                                transazioneLogout.Rollback();
                                transazioneLogout.Dispose();
                                return ERRORE + "Errore durante la disconnessione";
                            }
                        }
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                    if (isBroken)
                    {
                        Thread.Sleep(10);
                    }
                    else
                        isBroken = false;
                } while (isBroken);

                transazioneLogout.Commit();
                transazioneLogout.Dispose();

                return OK + "Utente Disconnesso";
            }
            catch
            {
                transazioneLogout.Rollback();
                transazioneLogout.Dispose();
                return ERRORE + "Errore durante la disconnessione";
            }
        }

        private string comandoLogin(string responseData, TcpClient clientsocket)
        {

            SQLiteTransaction transazioneLogin = mainWindow.m_dbConnection.BeginTransaction();

            SQLiteDataReader reader = null;

            try
            {
                //mi aspetto LOGIN + username
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;

                if (numParametri != 3)
                {
                    transazioneLogin.Rollback();
                    transazioneLogin.Dispose();
                    return ERRORE + "Numero di paramentri passati per il login errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String pass = String.Empty;

                if (comando == null || !comando.Equals(LOGIN.Replace('>', ' ').Trim()))
                {
                    transazioneLogin.Rollback();
                    transazioneLogin.Dispose();
                    return ERRORE + "Comando errato";
                }

                if (user == null || user.Equals(""))
                {
                    transazioneLogin.Rollback();
                    transazioneLogin.Dispose();
                    return ERRORE + "User non valido";
                }
                

                SQLiteCommand comandoP0= new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP0.CommandText = "SELECT * FROM UTENTI WHERE username=@username";
                comandoP0.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP0.Transaction = transazioneLogin;


                try
                {
                    _readerWriterLock.EnterReadLock();
                    reader = comandoP0.ExecuteReader();
                    if (reader.Read()) 
                    {
                        //throw new Exception("Eccezione manuale.");
                        pass = reader["password"].ToString();
                    }
                    reader.Close();
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                if (pass == String.Empty)
                {
                    transazioneLogin.Rollback();
                    transazioneLogin.Dispose();

                    //utente non presente nel DB da gestire
                    return ERRORE + "Username e/o Password Errati ";
                }

                //pass contiene la password del supplicant
                //do l'OK al client perché l'utente esiste
                writeStringOnStream(clientsocket, OK);
                string chk = ReadStringFromStream(clientsocket);
                if (!chk.Equals(OK))
                {
                    transazioneLogin.Rollback();
                    transazioneLogin.Dispose();
                    return ERRORE + "Problema nella connesione durante il login";
                }

                //posso procedere con l'invio della sfida
                byte[] challenge = new byte[CHALLENGESIZE];
                Random random = new Random();
                random.NextBytes(challenge);
                WriteByteArrayOnStream(clientsocket, challenge);
                //leggo la risposta al challenge del client
                byte[] challengeResponse = new byte[32];    //dimensione dell'hash sha256
                ReadByteArrayFromStream(clientsocket, challengeResponse);
                //calcolo la risposta corretta
                //concateno pasword e challenge
                byte[] passwordChallengeBytes = new byte[pass.Length + CHALLENGESIZE];
                Array.Copy(Encoding.ASCII.GetBytes(pass), passwordChallengeBytes, pass.Length);
                Array.Copy(challenge, 0, passwordChallengeBytes, pass.Length, CHALLENGESIZE);
                SHA256 sha = SHA256Managed.Create();
                byte[] challengeResponseCorrect = sha.ComputeHash(passwordChallengeBytes);  //32 byte

                //valuto la risposta del client
                Console.WriteLine("RispostaClient: " + BitConverter.ToString(challengeResponse));
                Console.WriteLine("RispostaServer: " + BitConverter.ToString(challengeResponseCorrect));
                if (!BitConverter.ToString(challengeResponseCorrect).Equals(BitConverter.ToString(challengeResponse))) 
                {
                    transazioneLogin.Rollback();
                    transazioneLogin.Dispose();
                    return ERRORE + "Username e/o Password Errati";
                }
                
                SQLiteCommand comandoP3 = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP3.CommandText = "SELECT * FROM UTENTILOGGATI WHERE username=@username";
                comandoP3.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP3.Transaction = transazioneLogin;
                string utenteLog = String.Empty;
                try
                {
                    _readerWriterLock.EnterReadLock();
                    utenteLog = (string)comandoP3.ExecuteScalar();
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                if (utenteLog != null)
                {
                    transazioneLogin.Rollback();
                    transazioneLogin.Dispose();
                    return ERRORE + "Utente gia' loggato";  //dove si elimina l'utente al logout?
                }

                SQLiteCommand comandoP4 = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP4.CommandText = "INSERT INTO UTENTILOGGATI (username,lastUpdate) VALUES (@username,@lastUpdate)";
                comandoP4.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP4.Parameters.Add("@lastUpdate", System.Data.DbType.String, DateTime.Now.ToString().Length).Value = DateTime.Now.ToString();
                comandoP4.Transaction = transazioneLogin;
                bool isBroken = false;
                do
                {
                    try
                    {
                        _readerWriterLock.EnterWriteLock();
                        if (_readerWriterLock.WaitingReadCount > 0)
                        {
                            isBroken = true;
                        }
                        else
                        {
                            if (comandoP4.ExecuteNonQuery() != 1)
                            {
                                transazioneLogin.Rollback();
                                transazioneLogin.Dispose();
                                return ERRORE + "Errore durante il login";
                            }
                        }
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                    if (isBroken)
                    {
                        Thread.Sleep(10);
                    }
                } while (isBroken);

                //throw new Exception("Eccezione generata manualmente.");
                transazioneLogin.Commit();
                transazioneLogin.Dispose();

                return OK + "Login effettuato correttamente";

            }
            catch (Exception)
            {
                if (reader != null && !reader.IsClosed)
                    reader.Close();

                transazioneLogin.Rollback();
                transazioneLogin.Dispose();
                return ERRORE + "Eccezione durante il login";
            }
        }
        #endregion

        private Boolean comandoDisconnetti(string responseData)
        {
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 4 || numParametri < 4)
                    return false;

                String user = parametri[2].ToUpper();
                if (user != null && !user.Equals(""))
                {
                    string logout = comandoLogout(responseData);
                    if (logout.Contains(ERRORE))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string comandoECHO(string responseData, TcpClient clientsocket)
        {
            try
            {
                writeStringOnStream(clientsocket, OK);
                return RESTART;
            }
            catch
            {
                return ERRORE + "Errore durante l'echo reply";
            }
        }

        private string comandoFolder(string responseData)
        {
            SQLiteTransaction transazioneFold = mainWindow.m_dbConnection.BeginTransaction();
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 4 || numParametri < 4)
                {
                    transazioneFold.Rollback();
                    transazioneFold.Dispose();
                    return ERRORE + "Numero di paramentri passati per il settaggio del RootFolder errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String folder = parametri[3];

                if (comando == null || !comando.Equals(FOLDER.Replace('>', ' ').Trim()))
                {
                    transazioneFold.Rollback();
                    transazioneFold.Dispose();
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    transazioneFold.Rollback();
                    transazioneFold.Dispose();
                    return ERRORE + "User non valido";
                }
                if (folder == null || folder.Equals(""))
                {
                    transazioneFold.Rollback();
                    transazioneFold.Dispose();
                    return ERRORE + "Path RootFolder non valida";
                }
                SQLiteCommand comandoP1 = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP1.CommandText = "SELECT * FROM LASTFOLDERUTENTE WHERE username=@username";
                comandoP1.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP1.Transaction = transazioneFold;
                object q1 = null;

                try
                {
                    _readerWriterLock.EnterReadLock();

                    q1 = comandoP1.ExecuteScalar();
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                if (q1 == null)
                {
                    SQLiteCommand comandoP2 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comandoP2.CommandText = "INSERT INTO LASTFOLDERUTENTE (username,folderBackup,lastUpdate) VALUES(@username,@folderBackup,@lastUpdate)";
                    comandoP2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comandoP2.Parameters.Add("@folderBackup", System.Data.DbType.String, folder.Length).Value = folder;
                    comandoP2.Parameters.Add("@lastUpdate", System.Data.DbType.String, DateTime.Now.ToString().Length).Value = DateTime.Now.ToString();
                    comandoP2.Transaction = transazioneFold;

                    bool isBroken = false;
                    do
                    {
                        try
                        {
                            _readerWriterLock.EnterWriteLock();
                            if (_readerWriterLock.WaitingReadCount > 0)
                            {
                                isBroken = true;
                            }
                            else
                            {
                                if (comandoP2.ExecuteNonQuery() == 1)
                                {
                                    //throw new Exception("Eccezione generata manualmente.");
                                    transazioneFold.Commit();
                                    transazioneFold.Dispose();
                                    return OK + "RootFolder Inserita";
                                }
                            }
                        }
                        finally
                        {
                            _readerWriterLock.ExitWriteLock();
                        }
                        if (isBroken)
                        {
                            Thread.Sleep(10);
                        }
                        else
                            isBroken = false;
                    } while (isBroken);

                    transazioneFold.Rollback();
                    transazioneFold.Dispose();

                    return ERRORE + "Errore durante l'inserimento della RootFolder";

                }
                else
                {
                    SQLiteCommand comandoP2 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comandoP2.CommandText = "SELECT * FROM LASTFOLDERUTENTE WHERE username=@username AND folderBackup=@folderBackup";
                    comandoP2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comandoP2.Parameters.Add("@folderBackup", System.Data.DbType.String, folder.Length).Value = folder;
                    comandoP2.Transaction = transazioneFold;
                    object q2 = null;

                    try
                    {
                        _readerWriterLock.EnterReadLock();

                        q2 = comandoP2.ExecuteScalar();

                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                    if (q2 == null)
                    {
                        SQLiteCommand comandoP3 = new SQLiteCommand(mainWindow.m_dbConnection);
                        comandoP3.CommandText = "UPDATE LASTFOLDERUTENTE SET folderBackup=@folderBackup WHERE username=@username";
                        comandoP3.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                        comandoP3.Parameters.Add("@folderBackup", System.Data.DbType.String, folder.Length).Value = folder;
                        comandoP3.Transaction = transazioneFold;

                        bool isBroken = false;
                        do
                        {
                            try
                            {
                                _readerWriterLock.EnterWriteLock();
                                if (_readerWriterLock.WaitingReadCount > 0)
                                {
                                    isBroken = true;
                                }
                                else
                                {

                                    if (comandoP3.ExecuteNonQuery() == 1)
                                    {
                                        transazioneFold.Commit();
                                        transazioneFold.Dispose();
                                        return OK + "RootFolder Aggiornata";
                                    }
                                }
                            }
                            finally
                            {
                                _readerWriterLock.ExitWriteLock();
                            }
                            if (isBroken)
                            {
                                Thread.Sleep(10);
                            }
                            else
                                isBroken = false;
                        } while (isBroken);

                        transazioneFold.Rollback();
                        transazioneFold.Dispose();
                        return ERRORE + "Errore durante l'aggiornamento della RootFolder";

                    }
                    else
                    {
                        transazioneFold.Commit();
                        transazioneFold.Dispose();
                        return OK + "Stessa RootFolder";
                    }
                }

            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                transazioneFold.Rollback();
                transazioneFold.Dispose();
                return ERRORE + "Errore durante la gestione della RootFolder";
            }
        }

        #region Comandi per gestione file e liste file

        // Chiama riceviFile
        private string comandoFile(string responseData, TcpClient clientsocket, LinkedList<string> fileList)
        {
            SQLiteTransaction transazioneFile = mainWindow.m_dbConnection.BeginTransaction();
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 4 || numParametri < 3)
                {
                    transazioneFile.Rollback();
                    transazioneFile.Dispose();

                    return ERRORE + "Numero di parametri passati per il trasferimento file errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String ext = null;
                if (numParametri > 3)
                    ext = parametri[3];

                if (comando == null || !comando.Equals(FILE.Replace('>', ' ').Trim()))
                {
                    transazioneFile.Rollback();
                    transazioneFile.Dispose();
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    transazioneFile.Rollback();
                    transazioneFile.Dispose();
                    return ERRORE + "User non valido";
                }

                string risp = RiceviFile(clientsocket, user, fileList, null);

                //throw new Exception("Eccezione generata manualmente.");
                transazioneFile.Commit();
                transazioneFile.Dispose();

                return risp;

            }
            catch (Exception e)
            {
                //qui genera out of memory per cartelle con grandi files (la mia cartella era da 2.3 GB con file da 100MB )
                Console.WriteLine(e.Message);
                transazioneFile.Rollback();
                transazioneFile.Dispose();
                return ERRORE + "Invio file non riuscito";
            }
        }

        private string comandoGetListaFile(TcpClient clientsocket, string responseData)
        {
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                string listaFiles = string.Empty;

                if (numParametri != 4)
                {
                    return ERRORE + "Numero di paramentri passati per lista dei files errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String folderRoot = parametri[3];

                if (comando == null || !comando.Equals(LISTFILES.Replace('>', ' ').Trim()))
                {
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    {
                        return ERRORE + "User non valido";
                    }
                }
                if (folderRoot == null || folderRoot.Equals(""))
                {
                    {
                        return ERRORE + "folderRoot non valida";
                    }
                }

                SQLiteCommand comando1 = new SQLiteCommand(mainWindow.m_dbConnection);
                comando1.CommandText = "SELECT percorsoFile,versione,dimFile,timestamp,idfile FROM BACKUPHISTORY bh1 WHERE bh1.username=@username AND bh1.folderBackup=@folderBackup and not exists (select 1 from RENAMEFILEMATCH rfm where bh1.idfile=rfm.idfile and bh1.username=rfm.username and bh1.folderBackup=rfm.folderBackup and bh1.percorsoFile=rfm.percorsoFileOLD) and bh1.versione=(SELECT MAX(versione) FROM BACKUPHISTORY bh2 WHERE bh1.username=bh2.username and bh1.folderbackup=bh2.folderbackup and bh1.percorsofile = bh2.percorsofile and bh1.idfile=bh2.idfile)";
                comando1.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comando1.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                SQLiteDataReader rdr;

                try
                {
                    _readerWriterLock.EnterReadLock();

                    rdr = comando1.ExecuteReader();

                    while (rdr.Read())
                    {
                        listaFiles += Convert.ToString(rdr["percorsoFile"]) + "?" + Convert.ToString(rdr["versione"]) + "?" + Convert.ToString(rdr["dimFile"]) + "?" + Convert.ToString(rdr["timestamp"]) + "?" + Convert.ToString(rdr["idfile"]) + "<";
                    }
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                if (listaFiles.Equals(""))
                {
                    return INFO + "Nessuna file trovato";
                }

                String[] splittato = listaFiles.Split('<');
                int numFile = splittato.Length;

                for (int i = 0; i < numFile - 1; i++)
                {
                    writeStringOnStream(clientsocket, FLP + i + ">" + splittato[i]);
                    ReadStringFromStream(clientsocket);
                }

                return ">ENDLIST>";

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return ERRORE + "Invio lista file non riuscito";
            }
        }

        private string comandoGetVersioniFile(TcpClient clientsocket, string responseData)
        {
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 6 || numParametri < 6)
                {
                    return ERRORE + "Numero di paramentri passati per il trasferimento lista versioni file errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String folderRoot = parametri[3];
                String fileName = parametri[4];
                String idfile = parametri[5];

                if (comando == null || !comando.Equals(GETVFILE.Replace('>', ' ').Trim()))
                {
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    return ERRORE + "User non valido";
                }
                if (folderRoot == null || folderRoot.Equals(""))
                {
                    return ERRORE + "folderRoot non valido";
                }
                if (fileName == null || fileName.Equals(""))
                {
                    return ERRORE + "fileName non valido";
                }
                if (idfile == null || idfile.Equals(""))
                {
                    return ERRORE + "idfile non valido";
                }

                String versioni = "";

                do
                {
                    SQLiteCommand comando1 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comando1.CommandText = "SELECT percorsoFile,versione,dimFile,timestamp FROM BACKUPHISTORY WHERE username=@username AND folderBackup=@folderBackup AND percorsoFile=@percorsoFile and idfile=@idfile ORDER BY versione DESC";
                    comando1.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comando1.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comando1.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileName.Length).Value = fileName;
                    comando1.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = Int32.Parse(idfile);
                    SQLiteDataReader rdr;

                    try
                    {
                        _readerWriterLock.EnterReadLock();

                        rdr = comando1.ExecuteReader();


                        while (rdr.Read())
                        {
                            string path = Convert.ToString(rdr["percorsoFile"]);
                            string nomeFile = path.Substring(path.LastIndexOf('\\') + 1);
                            versioni += nomeFile + "?" + Convert.ToString(rdr["versione"]) + "?" + Convert.ToString(rdr["dimFile"]) + "?" + Convert.ToString(rdr["timestamp"]) + "?" + idfile + "<";
                        }


                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                    SQLiteCommand comando2 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comando2.CommandText = "SELECT percorsoFileOLD FROM RENAMEFILEMATCH WHERE username=@username AND folderBackup=@folderBackup AND percorsoFileNEW=@percorsoFileNEW and idfile=@idfile ORDER BY lastVersionOLD DESC";
                    comando2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comando2.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comando2.Parameters.Add("@percorsoFileNEW", System.Data.DbType.String, fileName.Length).Value = fileName;
                    comando2.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = Int32.Parse(idfile);
                    
                    try
                    {
                        _readerWriterLock.EnterReadLock();

                        fileName = (string)comando2.ExecuteScalar();
                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                } while (fileName != null);

                if (versioni.Equals(""))
                {
                    return INFO + "Nessuna versione del file";
                }

                String[] splittato = versioni.Split('<');
                int numFile = splittato.Length;

                for (int i = 0; i < numFile - 1; i++)
                {
                    writeStringOnStream(clientsocket, FLP + i + ">" + splittato[i]);
                    ReadStringFromStream(clientsocket);
                }

                return ">ENDLIST>";

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return ERRORE + "Invio versioni file non riuscito";
            }
        }

        private string comandoGetFileVersione(TcpClient clientsocket, string responseData)
        {
            string token = RandomString(30);
            String tmpFileName = null;
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 7 || numParametri < 7)
                {
                    return ERRORE + "Numero di paramentri passati per il trasferimento versione file errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String folderRoot = parametri[3];
                String fileName = parametri[4];
                String versione = parametri[5];
                String idfile = parametri[6];

                if (comando == null || !comando.Equals(GETFILEV.Replace('>', ' ').Trim()))
                {
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    return ERRORE + "User non valido";
                }
                if (folderRoot == null || folderRoot.Equals(""))
                {
                    return ERRORE + "folderRoot non valido";
                }
                if (fileName == null || fileName.Equals(""))
                {
                    return ERRORE + "fileName non valido";
                }
                if (versione == null || versione.Equals(""))
                {
                    return ERRORE + "versione non valida";
                }
                if (idfile == null || idfile.Equals(""))
                {
                    return ERRORE + "idfile non valida";
                }

                SQLiteCommand comando2 = new SQLiteCommand(mainWindow.m_dbConnection);
                comando2.CommandText = "SELECT file,dimFile,checksum FROM BACKUPHISTORY WHERE username=@username AND folderBackup=@folderBackup AND versione=@versione and idfile=@idfile"; //AND percorsoFile=@percorsoFile 
                comando2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comando2.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                comando2.Parameters.Add("@versione", System.Data.DbType.Int64, 10).Value = Convert.ToInt64(versione);
                comando2.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = Int32.Parse(idfile);
                SQLiteDataReader dr;

                Byte[] file = null;
                byte[] buffer = null;
                int dimFile = 0;
                int dimFile2 = 0;
                int bufferSize = 1024;
                string checksum = string.Empty;

                try
                {
                    _readerWriterLock.EnterReadLock();
                    dr = comando2.ExecuteReader();

                    while (dr.Read())
                    {
                        file = (Byte[])dr["file"];
                        dimFile = Convert.ToInt32(dr["dimFile"]);
                        checksum = Convert.ToString(dr["checksum"]);
                    }
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
                if (file == null && dimFile == 0)
                {
                    SQLiteCommand comando3 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comando3.CommandText = "SELECT file,dimFile,checksum FROM BACKUPHISTORY WHERE username=@username AND folderBackup=@folderBackup AND percorsoFile=@percorsoFile AND versione=@versione and idfile=@idfile";
                    comando3.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comando3.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comando3.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileName.Length).Value = fileName;
                    comando3.Parameters.Add("@versione", System.Data.DbType.Int64, 10).Value = Convert.ToInt64(versione) - 1;
                    comando3.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = Int32.Parse(idfile);
                    SQLiteDataReader dr2;

                    try
                    {
                        _readerWriterLock.EnterReadLock();
                        dr2 = comando3.ExecuteReader();

                        while (dr2.Read())
                        {
                            file = (Byte[])dr["file"];
                            dimFile2 = Convert.ToInt32(dr["dimFile"]);
                            checksum = Convert.ToString(dr["checksum"]);
                        }
                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }
                }
                else if ((file == null || dimFile == 0))
                {
                    return ERRORE + "Errore durante il recupero della Versione del file";
                }

                Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\tmp");
                tmpFileName = Directory.GetCurrentDirectory() + "\\tmp\\" + idfile + token;
                using (FileStream fs = new FileStream(tmpFileName, FileMode.Create))
                { 
                    fs.Write(file, 0, file.Length);
                }
                using (FileStream fs = new FileStream(tmpFileName, FileMode.Open, FileAccess.Read))
                {
                    string headerStr = "Content-length:" + fs.Length.ToString() + "\r\nFilename:" + fileName + "\r\nChecksum:" + checksum + "\r\n";
                    writeStringOnStream(clientsocket, headerStr);
                    ReadStringFromStream(clientsocket);
                    int bufferCount = Convert.ToInt32(Math.Ceiling((double)fs.Length / (double)bufferSize));
                    Console.WriteLine("inizio invio");
                    for (int i = 0; i < bufferCount; i++)
                    {
                        buffer = new byte[bufferSize];
                        int size = fs.Read(buffer, 0, bufferSize);
                        clientsocket.Client.Send(buffer, size, SocketFlags.Partial);
                    }
                }
                
                ReadStringFromStream(clientsocket);
                
                return OK + "Versione file inviata";

            }
            catch (Exception)
            {
                return ERRORE + "Invio file non riuscito";
            }
            finally
            {
                if (tmpFileName != null)
                {
                    File.Delete(tmpFileName);
                }
            }
        }

        private string comandoRenameFile(string responseData)
        {
            SQLiteTransaction transazioneRename = mainWindow.m_dbConnection.BeginTransaction();
            SQLiteDataReader dr= null;
            SQLiteDataReader dr2 = null;
            SQLiteDataReader drD = null;
            SQLiteDataReader drD2 = null;

            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 6 || numParametri < 5)
                {
                    transazioneRename.Rollback();
                    transazioneRename.Dispose();
                    return ERRORE + "Numero di paramentri passati per la rinonima del file errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String fileNameOLD = parametri[3];
                String fileNameNEW = parametri[4];
                String dir = null;
                if (numParametri>5)
                    dir = parametri[5];

                if (comando == null || !comando.Equals(RENAMEFILE.Replace('>', ' ').Trim()))
                {
                    transazioneRename.Rollback();
                    transazioneRename.Dispose();
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    transazioneRename.Rollback();
                    transazioneRename.Dispose();
                    return ERRORE + "User non valido";
                }
                if (fileNameOLD == null || fileNameOLD.Equals(""))
                {
                    transazioneRename.Rollback();
                    transazioneRename.Dispose();
                    return ERRORE + "fileNameOLD non valido";
                }
                if (fileNameNEW == null || fileNameNEW.Equals(""))
                {
                    transazioneRename.Rollback();
                    transazioneRename.Dispose();
                    return ERRORE + "fileNameNEW non valida";
                }

                string folderRoot = getFolderRoot(user);
                if (folderRoot == null)
                {
                    transazioneRename.Rollback();
                    transazioneRename.Dispose();
                    return ERRORE + "impossibile recuperare FolderRoot";
                }

                if (dir != null && dir.Equals("DIR"))
                {
                    SQLiteCommand comandoPD = new SQLiteCommand(mainWindow.m_dbConnection);
                    comandoPD.CommandText = "SELECT idfile,percorsoFile from BACKUPHISTORY bh1 where username=@username and folderBackup=@folderBackup and percorsoFile like @percorsoFile and timestamp = (select max(bh2.timestamp) from backuphistory bh2 where bh1.username=bh2.username and bh1.folderBackup=bh2.folderBackup and bh1.percorsoFile=bh2.percorsoFile and bh1.versione=bh2.versione)";
                    comandoPD.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comandoPD.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comandoPD.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileNameOLD.Length).Value = fileNameOLD + "\\%";
                    comandoPD.Transaction = transazioneRename;
                    String idfileD = "0";

                    try
                    {
                        _readerWriterLock.EnterReadLock();
                        drD = comandoPD.ExecuteReader();
                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                    while (drD.Read())
                    {
                        idfileD = Convert.ToString(drD["idfile"]);
                        String fileNameOLDD = Convert.ToString(drD["percorsoFile"]);
                        String fileNameNEWD = fileNameNEW + "\\" + fileNameOLDD.Substring(fileNameOLD.Length + 1);
                        int lastVersionD = getLastVersion(user, folderRoot, fileNameOLDD);

                        SQLiteCommand comandoPD1 = new SQLiteCommand(mainWindow.m_dbConnection);
                        comandoPD1.CommandText = "INSERT INTO RENAMEFILEMATCH (username, folderBackup, idfile, percorsoFileOLD, percorsoFileNEW, lastVersionOLD, lastUpdate)" +
                                            "VALUES (@username,@folderBackup,@idfile,@fileNameOLD,@fileNameNEW,@lastVersionOLD,@lastUpdate)";
                        comandoPD1.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                        comandoPD1.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                        comandoPD1.Parameters.Add("@fileNameOLD", System.Data.DbType.String, fileNameOLD.Length).Value = fileNameOLDD;
                        comandoPD1.Parameters.Add("@fileNameNEW", System.Data.DbType.String, fileNameNEW.Length).Value = fileNameNEWD;
                        comandoPD1.Parameters.Add("@lastVersionOLD", System.Data.DbType.Int64, 5).Value = lastVersionD;
                        comandoPD1.Parameters.Add("@lastUpdate", System.Data.DbType.String, DateTime.Now.ToString().Length).Value = DateTime.Now.ToString();
                        comandoPD1.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = Int32.Parse(idfileD);
                        comandoPD1.Transaction = transazioneRename;

                        bool isBrokenD = false;
                        do
                        {
                            try
                            {
                                _readerWriterLock.EnterWriteLock();
                                if (_readerWriterLock.WaitingReadCount > 0)
                                {
                                    isBrokenD = true;
                                }
                                else
                                {
                                    if (comandoPD1.ExecuteNonQuery() != 1)
                                    {
                                        transazioneRename.Rollback();
                                        transazioneRename.Dispose();
                                        return ERRORE + "impossibile inserire match rinomina file";
                                    }
                                }
                            }
                            finally
                            {
                                _readerWriterLock.ExitWriteLock();

                            }
                            if (isBrokenD)
                            {
                                Thread.Sleep(10);
                            }
                            else
                                isBrokenD = false;
                        } while (isBrokenD);

                        SQLiteCommand comandoD2 = new SQLiteCommand(mainWindow.m_dbConnection);
                        comandoD2.CommandText = "SELECT idfile,file,dimFile,checksum FROM BACKUPHISTORY WHERE username=@username AND folderBackup=@folderBackup AND percorsoFile=@percorsoFile AND versione=@versione";
                        comandoD2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                        comandoD2.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                        comandoD2.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileNameNEW.Length).Value = fileNameOLDD;
                        comandoD2.Parameters.Add("@versione", System.Data.DbType.Int64, 10).Value = lastVersionD;
                        comandoD2.Transaction = transazioneRename;
        
                        try
                        {
                            _readerWriterLock.EnterReadLock();
                            drD2 = comandoD2.ExecuteReader();
                        }
                        finally
                        {
                            _readerWriterLock.ExitReadLock();
                        }

                        Byte[] fileD = null;
                        int dimFileD = 0;
                        string checksum2D = string.Empty;
                        while (drD2.Read())
                        {
                            //throw new Exception("Eccezione generata manualmente.");
                            fileD = (Byte[])drD2["file"];
                            dimFileD = Convert.ToInt32(drD2["dimFile"]);
                            checksum2D = Convert.ToString(drD2["checksum"]);

                        }
                        drD2.Close();

                        if (!inserisciFile(user, folderRoot, fileNameNEWD, lastVersionD + 1, fileD, dimFileD, checksum2D, null, idfileD))
                        {
                            drD.Close();
                            transazioneRename.Rollback();
                            transazioneRename.Dispose();
                            Console.WriteLine(ERRORE + "Impossibile inserire file su DB");
                            return ERRORE + "Impossibile inserire file su DB";
                        }

                        Console.WriteLine(OK + "Inserito match rinomina file");

                    }
                    drD.Close();
                    transazioneRename.Commit();
                    transazioneRename.Dispose();
                    return OK + "Inserito match rinomina files";

                }
                else
                {
                    int lastVersion = getLastVersion(user, folderRoot, fileNameOLD);

                    SQLiteCommand comandoP0 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comandoP0.CommandText = "select idfile from BACKUPHISTORY bh1 where username=@username and folderBackup=@folderBackup and percorsoFile=@percorsoFile and timestamp = (select max(bh2.timestamp) from backuphistory bh2 where bh1.username=bh2.username and bh1.folderBackup=bh2.folderBackup and bh1.percorsoFile=bh2.percorsoFile and bh1.versione=bh2.versione)";
                    comandoP0.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comandoP0.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comandoP0.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileNameOLD.Length).Value = fileNameOLD;
                    String idfile = "0";
                    try
                    {
                        _readerWriterLock.EnterReadLock();
                        dr2 = comandoP0.ExecuteReader();

                        while (dr2.Read())
                        {
                            //throw new Exception("Eccezione generata manualmente.");
                            idfile = Convert.ToString(dr2["idfile"]);
                        }

                        dr2.Close();
                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                    if (idfile.Equals("0"))
                    {
                        transazioneRename.Commit();
                        transazioneRename.Dispose();
                        return INFO + "File dim Zero Rename";
                    }

                    SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
                    comandoP.CommandText = "INSERT INTO RENAMEFILEMATCH (username, folderBackup, idfile, percorsoFileOLD, percorsoFileNEW, lastVersionOLD, lastUpdate)" +
                                        "VALUES (@username,@folderBackup,@idfile,@fileNameOLD,@fileNameNEW,@lastVersionOLD,@lastUpdate)";
                    comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comandoP.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comandoP.Parameters.Add("@fileNameOLD", System.Data.DbType.String, fileNameOLD.Length).Value = fileNameOLD;
                    comandoP.Parameters.Add("@fileNameNEW", System.Data.DbType.String, fileNameNEW.Length).Value = fileNameNEW;
                    comandoP.Parameters.Add("@lastVersionOLD", System.Data.DbType.Int64, 5).Value = lastVersion;
                    comandoP.Parameters.Add("@lastUpdate", System.Data.DbType.String, DateTime.Now.ToString().Length).Value = DateTime.Now.ToString();
                    comandoP.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = Int32.Parse(idfile);
                    comandoP.Transaction = transazioneRename;

                    bool isBroken = false;
                    do
                    {
                        try
                        {
                            _readerWriterLock.EnterWriteLock();
                            if (_readerWriterLock.WaitingReadCount > 0)
                            {
                                isBroken = true;
                            }
                            else
                            {
                                if (comandoP.ExecuteNonQuery() != 1)
                                {
                                    transazioneRename.Rollback();
                                    transazioneRename.Dispose();
                                    return ERRORE + "impossibile inserire match rinomina file";
                                }
                            }
                        }
                        finally
                        {
                            _readerWriterLock.ExitWriteLock();

                        }
                        if (isBroken)
                        {
                            Thread.Sleep(10);
                        }
                        else
                            isBroken = false;
                    } while (isBroken);

                    SQLiteCommand comando2 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comando2.CommandText = "SELECT idfile,file,dimFile,checksum FROM BACKUPHISTORY WHERE username=@username AND folderBackup=@folderBackup AND percorsoFile=@percorsoFile AND versione=@versione";
                    comando2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comando2.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comando2.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileNameNEW.Length).Value = fileNameOLD;
                    comando2.Parameters.Add("@versione", System.Data.DbType.Int64, 10).Value = lastVersion;
                    comando2.Transaction = transazioneRename;
                    
                    try
                    {
                        _readerWriterLock.EnterReadLock();
                        dr = comando2.ExecuteReader();
                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                    Byte[] file = null;
                    int dimFile = 0;
                    string checksum2 = string.Empty;
                    while (dr.Read())
                    {
                        //throw new Exception("Eccezione generata manualmente.");
                        file = (Byte[])dr["file"];
                        dimFile = Convert.ToInt32(dr["dimFile"]);
                        checksum2 = Convert.ToString(dr["checksum"]);
                    }

                    dr.Close();

                    if (!inserisciFile(user, folderRoot, fileNameNEW, lastVersion + 1, file, dimFile, checksum2, null, idfile))
                    {
                        transazioneRename.Rollback();
                        transazioneRename.Dispose();
                        return ERRORE + "Impossibile inserire file su DB";
                    }

                    //throw new Exception("Eccezione generata manualmente.");
                    transazioneRename.Commit();
                    transazioneRename.Dispose();
                    return OK + "Inserito match rinomina file";
                }
            }
            catch
            {
                if (dr != null && !dr.IsClosed)
                    dr.Close();

                if (dr2 != null && !dr2.IsClosed)
                    dr2.Close();

                if (drD != null && !drD.IsClosed)
                    drD.Close();

                if (drD2 != null && !drD2.IsClosed)
                    drD2.Close();

                transazioneRename.Rollback();
                transazioneRename.Dispose();
                return ERRORE + "impossibile inserire match rinomina file";
            }
        }

        private string comandoFileCancellato(string responseData)
        {
            SQLiteTransaction transazioneDelete = mainWindow.m_dbConnection.BeginTransaction();

            SQLiteDataReader dr2 = null;
            SQLiteDataReader dr2D = null;

            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 4 || numParametri < 4)
                {
                    transazioneDelete.Rollback();
                    transazioneDelete.Dispose();
                    return ERRORE + "Numero di paramentri passati per la cancellazione del file errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String fileName = parametri[3];

                if (comando == null || !comando.Equals(CANC.Replace('>', ' ').Trim()))
                {
                    transazioneDelete.Rollback();
                    transazioneDelete.Dispose();
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    transazioneDelete.Rollback();
                    transazioneDelete.Dispose();
                    return ERRORE + "User non valido";
                }
                if (fileName == null || fileName.Equals(""))
                {
                    transazioneDelete.Rollback();
                    transazioneDelete.Dispose();
                    return ERRORE + "fileName non valido";
                }

                string folderRoot = getFolderRoot(user);
                if (folderRoot == null)
                {
                    transazioneDelete.Rollback();
                    transazioneDelete.Dispose();
                    return ERRORE + "impossibile recuperare FolderRoot";
                }

                SQLiteCommand comandoP0 = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP0.CommandText = "SELECT idfile from BACKUPHISTORY bh1 where username=@username and folderBackup=@folderBackup and percorsoFile=@percorsoFile and not exists (select 1 from BACKUPHISTORY bh2 where bh1.username=bh2.username and bh1.folderBackup=bh2.folderBackup and bh1.idfile=bh2.idfile and bh1.percorsoFile=bh2.percorsoFile and dimFile=0 and isDelete='S') and bh1.timestamp = (select max(bh3.timestamp) from BACKUPHISTORY bh3 where bh1.username=bh3.username and bh1.folderBackup=bh3.folderBackup and bh1.idfile=bh3.idfile and bh1.percorsoFile=bh3.percorsoFile)";
                comandoP0.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP0.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                comandoP0.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileName.Length).Value = fileName;
                comandoP0.Transaction = transazioneDelete;
                
                String idfile = "0";
                try
                {
                    _readerWriterLock.EnterReadLock();
                    dr2 = comandoP0.ExecuteReader();

                    while (dr2.Read())
                    {
                        idfile = Convert.ToString(dr2["idfile"]);
                    }

                    dr2.Close();
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                if (idfile.Equals("0"))
                {

                    SQLiteCommand comandoP0D = new SQLiteCommand(mainWindow.m_dbConnection);
                    comandoP0D.CommandText = "SELECT idfile,percorsoFile from BACKUPHISTORY bh1 where username=@username and folderBackup=@folderBackup and percorsoFile like @percorsoFile and not exists (select 1 from BACKUPHISTORY bh2 where bh1.username=bh2.username and bh1.folderBackup=bh2.folderBackup and bh1.idfile=bh2.idfile and bh1.percorsoFile=bh2.percorsoFile and dimFile=0 and isDelete='S') and bh1.timestamp = (select max(bh3.timestamp) from BACKUPHISTORY bh3 where bh1.username=bh3.username and bh1.folderBackup=bh3.folderBackup and bh1.idfile=bh3.idfile and bh1.percorsoFile=bh3.percorsoFile)";
                    comandoP0D.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comandoP0D.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                    comandoP0D.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileName.Length).Value = fileName + "\\%";
                    comandoP0D.Transaction = transazioneDelete;
                    String idfileD = "0";

                    try
                    {
                        _readerWriterLock.EnterReadLock();
                        dr2D = comandoP0D.ExecuteReader();
                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                    while (dr2D.Read())
                    {
                        idfileD = Convert.ToString(dr2D["idfile"]);
                        String fileNameD = Convert.ToString(dr2D["percorsoFile"]);

                        int lastVersionD = getLastVersion(user, folderRoot, fileNameD) + 1;

                        if (inserisciFile(user, folderRoot, fileNameD, lastVersionD, null, 0, "", null, idfileD, "S"))
                        {
                            Console.WriteLine(OK + "File cancellatto correttamente");
                        }
                        else
                        {
                            dr2D.Close();
                            transazioneDelete.Rollback();
                            transazioneDelete.Dispose();
                            Console.WriteLine(ERRORE + "impossibile cancellare il file");
                            return ERRORE + "impossibile cancellare il file";
                        }
                    }

                    dr2D.Close();
                    //throw new Exception("Eccezione generata manualmente.");
                    transazioneDelete.Commit();
                    transazioneDelete.Dispose();
                    return OK + "File cancellatti correttamente";
                }
                else
                {

                    int lastVersion = getLastVersion(user, folderRoot, fileName) + 1;

                    if (inserisciFile(user, folderRoot, fileName, lastVersion, null, 0, "", null, idfile, "S"))
                    {

                        //throw new Exception("Eccezione generata manualmente.");
                        transazioneDelete.Commit();
                        transazioneDelete.Dispose();
                        return OK + "File cancellatto correttamente";
                    }
                    else
                    {
                        transazioneDelete.Rollback();
                        transazioneDelete.Dispose();
                        return ERRORE + "impossibile cancellare il file";
                    }

                }
            }
            catch
            {
                if (dr2 != null && !dr2.IsClosed)
                    dr2.Close();

                if (dr2D != null && !dr2D.IsClosed)
                    dr2D.Close();

                transazioneDelete.Rollback();
                transazioneDelete.Dispose();
                return ERRORE + "impossibile cancellare il file";
            }

        }

        private string comandoGetFoldersUser(string responseData)
        {
            try
            {
                string folders = string.Empty;
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;

                if (numParametri != 3)
                    return ERRORE + "Numero di paramentri passati per il login errato";

                String comando = parametri[1];
                String user = parametri[2].ToUpper();

                if (comando == null || !comando.Equals(GETFOLDERUSER.Replace('>', ' ').Trim()))
                    return ERRORE + "Comando errato";
                if (user == null || user.Equals(""))
                {
                    return ERRORE + "User non valido";
                }

                SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
                comandoP.CommandText = "SELECT DISTINCT(folderBackup) FROM BACKUPHISTORY WHERE username=@username";
                comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                SQLiteDataReader reader;

                try
                {
                    _readerWriterLock.EnterReadLock();
                    reader = comandoP.ExecuteReader();

                    while (reader.Read())
                    {
                        folders += reader["folderBackup"].ToString();
                        folders += "<";
                    }
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                if (folders == string.Empty)
                    return OK;

                return OK + folders;

            }
            catch
            {
                return ERRORE + "Errore durante get user folder";
            }

        }
        #endregion

        // Riceve una lista di file. Interroga il DB per una lista di file cancellati.
        // Toglie dalla lista di file cancellati i file nella list passata.
        // Per ogni file rimasto nella lista cancella l'ultima versione (con il meccanismo di inserimento file nullo)
        private string comandoEndSync(string responseData, LinkedList<string> fileList)
        {
            LinkedList<string> listFileCancellati = new LinkedList<string>();
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;
                if (numParametri > 4 || numParametri < 4)
                {
                    return ERRORE + "Numero di paramentri passati per la cancellazione del file errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String folderroot = parametri[3];

                if (comando == null || !comando.Equals(ENDSYNC.Replace('>', ' ').Trim()))
                {
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    return ERRORE + "User non valido";
                }
                if (folderroot == null || folderroot.Equals(""))
                {
                    return ERRORE + "folderroot non valida";
                }

                SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection); ;
                comandoP.CommandText = "SELECT percorsoFile FROM BACKUPHISTORY bh1 WHERE username=@username AND folderBackup=@folderBackup and not exists(select 1 from RENAMEFILEMATCH rfm where rfm.idfile=bh1.idfile and rfm.username=bh1.username and rfm.folderBackup=bh1.folderBackup and rfm.percorsoFileOLD=bh1.percorsoFile) and versione=(SELECT MAX(bh2.versione) FROM BACKUPHISTORY bh2 WHERE bh1.idfile=bh2.idfile and bh1.username=bh2.username and bh1.folderbackup=bh2.folderbackup and bh1.percorsofile = bh2.percorsofile) and dimFile>0";
                comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP.Parameters.Add("@folderBackup", System.Data.DbType.String, folderroot.Length).Value = folderroot;
                SQLiteDataReader rdr;

                try
                {
                    _readerWriterLock.EnterReadLock();
                    rdr = comandoP.ExecuteReader();

                    while (rdr.Read())
                    {
                        if (!listFileCancellati.Contains(Convert.ToString(rdr["percorsoFile"])))
                            listFileCancellati.AddFirst(Convert.ToString(rdr["percorsoFile"]));
                    }
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                foreach (string file in fileList)
                {
                    if (listFileCancellati.Contains(file))
                        listFileCancellati.Remove(file);
                }

                foreach (string file in listFileCancellati)
                {
                    int lastVersion = getLastVersion(user, folderroot, file) + 1;

                    SQLiteCommand comandoP0 = new SQLiteCommand(mainWindow.m_dbConnection);
                    comandoP0.CommandText = "select idfile from BACKUPHISTORY bh1 where username=@username and folderBackup=@folderBackup and percorsoFile=@percorsoFile and bh1.timestamp = (select max(bh3.timestamp) from BACKUPHISTORY bh3 where bh1.username=bh3.username and bh1.folderBackup=bh3.folderBackup and bh1.idfile=bh3.idfile and bh1.percorsoFile=bh3.percorsoFile)";
                    comandoP0.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                    comandoP0.Parameters.Add("@folderBackup", System.Data.DbType.String, folderroot.Length).Value = folderroot;
                    comandoP0.Parameters.Add("@percorsoFile", System.Data.DbType.String, file.Length).Value = file;
                    SQLiteDataReader dr2;
                    String idfile = "0";
                    try
                    {
                        _readerWriterLock.EnterReadLock();
                        dr2 = comandoP0.ExecuteReader();

                        while (dr2.Read())
                        {
                            idfile = Convert.ToString(dr2["idfile"]);
                        }
                    }
                    finally
                    {
                        _readerWriterLock.ExitReadLock();
                    }

                    if (inserisciFile(user, folderroot, file, lastVersion, null, 0, "", null, idfile,"S"))
                        Console.WriteLine(OK + "File cancellato correttamente");
                    else
                        Console.WriteLine(ERRORE + "impossibile cancellare il file");
                }
                return OK;
            }
            catch
            {
                return ERRORE;
            }

        }

        //Debuggare
        private string comandoRestore(TcpClient clientsocket, string responseData)
        {
            try
            {
                String[] parametri = responseData.Split('>');
                int numParametri = parametri.Length;

                if (numParametri != 6)
                {
                    return ERRORE + "Numero di paramentri passati per lista dei files errato";
                }

                String comando = parametri[1];
                String user = parametri[2].ToUpper();
                String folderRoot = parametri[3];   //path della rootDirectory backuppata
                String newFolderRoot = parametri[4];    //cartella dove il client salverà i file ripristinati
                String folderToBeRestored = parametri[5];   //(sub)directory di cui effettuare il restore

                if (comando == null || !comando.Equals(RESTORE.Replace('>', ' ').Trim()))
                {
                    return ERRORE + "Comando errato";
                }
                if (user == null || user.Equals(""))
                {
                    return ERRORE + "User non valido";
                }
                if (folderRoot == null || folderRoot.Equals(""))
                {
                    return ERRORE + "folderRoot non valida";
                }
                if (newFolderRoot == null)
                {
                    return ERRORE + "newFolderRoot non valida";
                }
                if (!folderToBeRestored.Contains(folderRoot))
                {
                    return ERRORE + "sotto cartella non valida";
                }

                SQLiteCommand comando2 = new SQLiteCommand(mainWindow.m_dbConnection);
                comando2.CommandText = "SELECT percorsoFile,versione,file,dimFile,checksum,idfile,isDelete FROM BACKUPHISTORY bh1 WHERE bh1.username=@username AND bh1.folderBackup=@folderBackup AND versione=(SELECT MAX(bh2.versione) FROM BACKUPHISTORY bh2 WHERE bh1.username=bh2.username and bh1.folderbackup=bh2.folderbackup and bh1.percorsofile = bh2.percorsofile and bh1.idfile=bh2.idfile) and not exists(select 1 from RENAMEFILEMATCH rfm where bh1.username=rfm.username and bh1.idfile=rfm.idfile and bh1.folderBackup=rfm.folderBackup and bh1.percorsoFile=rfm.percorsoFileOLD)";
                comando2.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comando2.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                SQLiteDataReader dr;

                try
                {
                    _readerWriterLock.EnterReadLock();
                    dr = comando2.ExecuteReader();

                    while (dr.Read())
                    {
                        Byte[] file = null;
                        byte[] buffer = null;
                        int dimFile = 0;
                        int bufferSize = 1024;
                        String tmpFileName = null;
                        string idfile = Convert.ToString(dr["idfile"]);
                        string checksum = Convert.ToString(dr["checksum"]);
                        string fileName = Convert.ToString(dr["percorsoFile"]);
                        string isDelete = Convert.ToString(dr["isDelete"]);
                        if (DBNull.Value.Equals(dr["file"]))
                            file = null;
                        else
                            file = (Byte[])dr["file"];
                        dimFile = Convert.ToInt32(dr["dimFile"]);

                        if (file == null)
                        {
                            continue;
                        }
                        //se il nome del file correntemente del reader non contiene il nome della cartella da ripristinare lo salto
                        if (!fileName.Contains(folderToBeRestored + "\\"))
                        {
                            continue;
                        }

                        if (isDelete.Equals("S"))   //se il file puntato dal reader è cancellato ignoralo
                            continue;
                        
                        idfile = idfile + RandomString(30); //??

                        Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\tmp");
                        tmpFileName = Directory.GetCurrentDirectory() + "\\tmp\\" + idfile;
                        using (FileStream fs = new FileStream(tmpFileName, FileMode.Create))
                        {
                            for (int i = 0; i < file.Length; i++)
                                fs.WriteByte(file[i]);
                        }

                        using (FileStream fs = new FileStream(tmpFileName, FileMode.Open, FileAccess.Read))
                        {
                            if (!folderRoot.Equals(newFolderRoot))
                            {
                                string nomefileSenzaRoot = fileName.Substring(folderToBeRestored.Length + 1);
                                fileName = newFolderRoot + "\\" + nomefileSenzaRoot;
                            }
                            int bufferCount = Convert.ToInt32(Math.Ceiling((double)fs.Length / (double)bufferSize));


                            string headerStr = "Content-length=" + fs.Length.ToString() + "\r\nFilename=" + fileName + "\r\nChecksum=" + checksum + "\r\n";

                            writeStringOnStream(clientsocket, headerStr); //Breakpoint

                            string streamReady = ReadStringFromStream(clientsocket);

                            if (streamReady.Contains(ERRORE))
                            {
                                return ERRORE + "Errore durante il restore";
                            }

                            if (streamReady.Contains(STOP))
                            {
                                return INFO + "Restore interrotto dal client";
                            }

                            if (streamReady.Equals(INFO + "File gia' presente e non modificato"))
                            {
                                continue;
                            }

                            for (int i = 0; i < bufferCount; i++)
                            {
                                buffer = new byte[bufferSize];
                                int size = fs.Read(buffer, 0, bufferSize);
                                clientsocket.Client.Send(buffer, size, SocketFlags.Partial);
                            }

                        }

                        ReadStringFromStream(clientsocket);
                        if (tmpFileName != null)
                        {
                            File.Delete(tmpFileName);
                        }
                    }
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }

                return OK + "Restore Avvenuto Correttamente";

            }
            catch
            {
                return ERRORE + "Errore durante il restore";
            }
            finally
            {

            }
        }

        #endregion

        public TcpState GetState(TcpClient tcpClient)
        {
            var foo = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint));
            return foo != null ? foo.State : TcpState.Unknown;
        }

        #region Metodi ricezione file

        public string RiceviFile(TcpClient client, String user, LinkedList<string> listFile, SQLiteTransaction transazioneFile)
        {
            return RiceviFile(client, user, listFile, transazioneFile, null);
        }

        public string RiceviFile(TcpClient client, String user, LinkedList<string> listFile, SQLiteTransaction transazioneFile,String ext)
        {
            String pathTmp = null;
            try
            {
                writeStringOnStream(client, OK);
                byte[] buffer = null;
                byte[] header = null;
                string headerStr = "";
                string filename = "";
                string checksumFileInArrivo = "";
                int filesize = 0;
                String token = RandomString(10);

                header = new byte[BUFFERSIZE];
                client.Client.Receive(header);
                headerStr = Encoding.ASCII.GetString(header);

                string[] splitted = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                Dictionary<string, string> headers = new Dictionary<string, string>();
                foreach (string s in splitted)
                {
                    if (s.Contains(":"))
                    {
                        headers.Add(s.Substring(0, s.IndexOf(":")), s.Substring(s.IndexOf(":") + 1));
                    }

                }
                //Dimensione prelevata dall'header
                filesize = Convert.ToInt32(headers["Content-length"]);
                int filesizeC = filesize;
                if (filesizeC == 0)
                {
                    return INFO + "file dim 0";
                }
                filename = headers["Filename"];
                checksumFileInArrivo = headers["Checksum"];
                listFile.AddFirst(filename);
                // Controllo se il file è presente identico nel db
                if (!(ext != null && ext.Equals("CREATE")) && controlloCheck(checksumFileInArrivo, user, filename))
                {
                    return INFO + "File non modificato";
                }

                writeStringOnStream(client, OK);


                pathTmp = Directory.GetCurrentDirectory() + "\\" + token; // Il token serve solo per non avere path temporanei coincidenti
                using (FileStream fs = new FileStream(pathTmp, FileMode.OpenOrCreate))
                {
                    while (filesize > 0)
                    {
                        buffer = new byte[BUFFERSIZE];
                        int size = client.Client.Receive(buffer, SocketFlags.Partial);
                        fs.Write(buffer, 0, size);
                        filesize -= size;
                    }
                }

                using (FileStream fs = new FileStream(pathTmp, FileMode.Open, FileAccess.Read))
                {
                    byte[] file = new byte[fs.Length];
                    fs.Read(file, 0, System.Convert.ToInt32(fs.Length));
                    string checksumFileRicevuto = GetMD5HashFromFile(pathTmp);

                    if (incasellaFile(user, null, filename, file, filesizeC, checksumFileRicevuto, transazioneFile, ext))
                    {
                        return OK + "File ricevuto correttamente";
                    }
                    else
                    {
                        return ERRORE + "Invio file non riuscito";
                    }
                }
            }
            catch
            {
                return "FORCE_ERRORE";
            }
            finally
            {
                if (pathTmp != null)
                {
                    File.Delete(pathTmp);
                }
            }
        }

        private bool controlloCheck(string checksumFileInArrivo, string user, string fileName)
        {
            string folderRoot = getFolderRoot(user);
            int lastVersion = getLastVersion(user, folderRoot, fileName);
            SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
            comandoP.CommandText = "SELECT COUNT(*) FROM BACKUPHISTORY WHERE username=@username AND  percorsoFile=@filename AND folderBackup=@folderBackup AND checksum=@checksum AND versione=@versione";
            comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
            comandoP.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
            comandoP.Parameters.Add("@filename", System.Data.DbType.String, fileName.Length).Value = fileName;
            comandoP.Parameters.Add("@checksum", System.Data.DbType.String, checksumFileInArrivo.Length).Value = checksumFileInArrivo;
            comandoP.Parameters.Add("@versione", System.Data.DbType.Int64, 5).Value = lastVersion;
            object tmp;

            try
            {
                _readerWriterLock.EnterReadLock();
                tmp = comandoP.ExecuteScalar();

            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            if (tmp.ToString() == "0")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        protected string GetMD5HashFromFile(string fileName)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(fileName))
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
        }

        #endregion

        #region Metodi di elaborazione e inserimento file

        private Boolean incasellaFile(string user, string folderRoot, string fileName, Byte[] file, int filesizeC, string checkSum, SQLiteTransaction transazioneFile,String ext)
        {

            if (folderRoot == null)
            {
                folderRoot = getFolderRoot(user);
                if (folderRoot == null)
                    return false;
            }

            int sLastVersion = getLastVersion(user, folderRoot, fileName);
            int idFile = 0;

            if (sLastVersion == 0)
            {
                sLastVersion = getLastVersion2(user, folderRoot, fileName) + 1;
            }
            else
            {
                sLastVersion++;
                if (ext != null && ext.Equals("CREATE"))
                    idFile = 0;
                else
                    idFile = getIdFile(user, folderRoot, fileName);
            }
            string sidFile = null;
            if (idFile > 0)
                sidFile = "" + idFile;

            return inserisciFile(user, folderRoot, fileName, sLastVersion, file, filesizeC, checkSum, transazioneFile, sidFile);
        }
        
        private string getFolderRoot(string user)
        {
            SQLiteCommand comandoP1 = new SQLiteCommand(mainWindow.m_dbConnection);
            comandoP1.CommandText = "SELECT folderBackup FROM LASTFOLDERUTENTE WHERE username=@username";
            comandoP1.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
            string ret;

            try
            {
                _readerWriterLock.EnterReadLock();
                ret = (string)comandoP1.ExecuteScalar();
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            return ret;

        }

        private int getLastVersion(string user, string folderRoot, string fileName)
        {
            SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
            comandoP.CommandText = "SELECT MAX(versione) FROM BACKUPHISTORY WHERE username=@username AND folderBackup=@folderBackup AND percorsoFile=@percorsoFile";
            comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
            comandoP.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
            comandoP.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileName.Length).Value = fileName;
            object tmp;

            try
            {
                _readerWriterLock.EnterReadLock();
                tmp = comandoP.ExecuteScalar();
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            if (DBNull.Value.Equals(tmp))
                return 0;
            else
            {
                int tmp2 = Convert.ToInt32(tmp);
                return tmp2;
            }
        }

        private int getLastVersion2(string user, string folderRoot, string fileName)
        {
            SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
            comandoP.CommandText = "SELECT lastVersionOLD FROM RENAMEFILEMATCH WHERE username=@username AND folderBackup=@folderBackup AND percorsoFileNEW=@percorsoFileNEW";
            comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
            comandoP.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
            comandoP.Parameters.Add("@percorsoFileNEW", System.Data.DbType.String, fileName.Length).Value = fileName;
            object tmp;

            try
            {
                _readerWriterLock.EnterReadLock();
                tmp = comandoP.ExecuteScalar();
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            if (DBNull.Value.Equals(tmp))
                return 0;
            else
            {
                int tmp2 = Convert.ToInt32(tmp);
                return tmp2;
            }
        }

        private int getIdFile(string user, string folderRoot, string fileName)
        {

            SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
            comandoP.CommandText = "SELECT idfile FROM BACKUPHISTORY WHERE username=@username AND folderBackup=@folderBackup AND percorsoFile=@percorsoFile";
            comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
            comandoP.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
            comandoP.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileName.Length).Value = fileName;
            object tmp;

            try
            {
                _readerWriterLock.EnterReadLock();
                tmp = comandoP.ExecuteScalar();
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
            if (DBNull.Value.Equals(tmp))
                return 0;
            else
            {
                int tmp2 = Convert.ToInt32(tmp);
                return tmp2;
            }
        }

        private int getSequence()
        {
            SQLiteCommand comand = new SQLiteCommand(mainWindow.m_dbConnection);
            comand.CommandText = "SELECT MAX(idfile) FROM IDFILESEQ";
            object tmp;
            int seq;
            try
            {
                _readerWriterLock.EnterReadLock();
                tmp = comand.ExecuteScalar();
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            if (DBNull.Value.Equals(tmp))
                seq = 1;
            else
            {
                int tmp2 = Convert.ToInt32(tmp);
                seq = tmp2 + 1;
            }

            SQLiteCommand comand2 = new SQLiteCommand(mainWindow.m_dbConnection);
            comand2.CommandText = "INSERT INTO IDFILESEQ (idfile) VALUES (@idfile)";
            comand2.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = seq;


            bool isBroken = false;
            do
            {
                try
                {
                    _readerWriterLock.EnterWriteLock();
                    if (_readerWriterLock.WaitingReadCount > 0)
                    {
                        isBroken = true;
                    }
                    else
                    {
                        if (comand2.ExecuteNonQuery() != 1)
                            return 0;
                    }
                }
                finally
                {
                    _readerWriterLock.ExitWriteLock();
                }
                if (isBroken)
                {
                    Thread.Sleep(10);
                }
                else
                    isBroken = false;
            } while (isBroken);

            return seq;
        }

        private bool inserisciFile(string user, string folderRoot, string fileName, int versione, byte[] file, int filesizeC, string checkSum, SQLiteTransaction transazioneFile, String idfileT)
        {
            return inserisciFile(user, folderRoot, fileName, versione, file, filesizeC, checkSum, transazioneFile, idfileT, "N");
        }

        private bool inserisciFile(string user, string folderRoot, string fileName, int versione, byte[] file, int filesizeC, string checkSum, SQLiteTransaction transazioneFile, String idfileT, String delete)
        {
            SQLiteCommand comandoP = new SQLiteCommand(mainWindow.m_dbConnection);
            comandoP.CommandText = "INSERT INTO BACKUPHISTORY (idfile,username, folderBackup, percorsoFile, versione, file, dimFile,isDelete, checksum, timestamp)" +
                                "VALUES (@idfile,@username,@folderBackup,@percorsoFile,@versione,@file,@dimFile,@delete,@checksum,@timestamp)";
            if (idfileT == null)
                comandoP.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = getSequence();
            else
                comandoP.Parameters.Add("@idfile", System.Data.DbType.Int32, 10).Value = Int32.Parse(idfileT);
                comandoP.Parameters.Add("@username", System.Data.DbType.String, user.Length).Value = user;
                comandoP.Parameters.Add("@folderBackup", System.Data.DbType.String, folderRoot.Length).Value = folderRoot;
                comandoP.Parameters.Add("@percorsoFile", System.Data.DbType.String, fileName.Length).Value = fileName;
                comandoP.Parameters.Add("@versione", System.Data.DbType.Int64, 5).Value = versione;
                comandoP.Parameters.Add("@checksum", System.Data.DbType.String, checkSum.Length).Value = checkSum;
                comandoP.Parameters.Add("@timestamp", System.Data.DbType.String, DateTime.Now.ToString().Length).Value = DateTime.Now.ToString();
                comandoP.Parameters.Add("@delete", System.Data.DbType.String, delete.Length).Value = delete;
            if (file == null)
                comandoP.Parameters.Add("@file", System.Data.DbType.Binary, 1).Value = file;
            else
                comandoP.Parameters.Add("@file", System.Data.DbType.Binary, file.Length).Value = file;
            comandoP.Parameters.Add("@dimFile", System.Data.DbType.Int64, filesizeC.ToString().Length).Value = filesizeC;
            if (transazioneFile != null)
                comandoP.Transaction = transazioneFile;

            bool isBroken = false;
            do
            {
                try
                {
                    _readerWriterLock.EnterWriteLock();
                    if (_readerWriterLock.WaitingReadCount > 0)
                    {
                        isBroken = true;
                    }
                    else
                    {
                        if (comandoP.ExecuteNonQuery() != 1)
                            return false;
                    }
                }
                finally
                {
                    _readerWriterLock.ExitWriteLock();
                }
                if (isBroken)
                {
                    Thread.Sleep(10);
                }
                else
                    isBroken = false;
            } while (isBroken);

            // throw new Exception("Eccezione generata manualmente.");
            return true;
        }

        #endregion

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        #region Metodi di lettura e scrittura stringa su stream

        public bool WriteByteArrayOnStream(TcpClient clientsocket, byte[] message)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                NetworkStream stream = clientsocket.GetStream();
                stream.WriteTimeout = 30000;
                stream.Write(message, 0, message.Length);
                return true;
            }
            return false;
        }

        public bool ReadByteArrayFromStream(TcpClient clientsocket, byte[] buffer)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                NetworkStream stream = clientsocket.GetStream();
                stream.ReadTimeout = TIME_OUT;
                Int32 bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public string ReadStringFromStream(TcpClient clientsocket)
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                NetworkStream stream = clientsocket.GetStream();
                Byte[] data = new Byte[512];
                String responseData = String.Empty;
                Int32 bytes = stream.Read(data, 0, data.Length);
                responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                return responseData;
            }
            else
            {
                return ERRORE + "Connessione chiusa dal client";
            }

        }

        private void writeStringOnStream(TcpClient clientsocket, string message)
        {
            if (!serverKO)
            {
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
                NetworkStream stream = clientsocket.GetStream();
                stream.Write(data, 0, data.Length);
            }
        }

        private void readStringFromStream(TcpClient clientsocket)
        {
            NetworkStream stream = clientsocket.GetStream();
            bool chiudere = false;  //condizione di uscita dal loop
            bool nowrite = false;   //true se NON si deve scrivere al client una risposta
            String username = null; //conterrà lo username associato a clientsocket quando il client farà login
            LinkedList<string> fileReceived = new LinkedList<string>();
            
            while (!serverKO && !chiudere)
            {
                Byte[] data = new Byte[512];
                String responseData = String.Empty;
                Int32 bytes = 0;
                String risposta = "";
                String tmp;
                String comando = "";

                stream.ReadTimeout = TIME_OUT;

                try
                {
                    bytes = stream.Read(data, 0, data.Length);
                    if (bytes == 0)
                    {
                        throw new Exception("Network Exception");
                    }
                    responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                    if (responseData.Contains(ECHO_REQUEST))
                    {
                        mainWindow.tb.Dispatcher.Invoke(new BackupServer.MainWindow.UpdateTextCallback(mainWindow.UpdateText), new object[] { DateTime.Now + " - " + ECHO_REQUEST + "\n" });
                        responseData = responseData.Replace(ECHO_REQUEST, "");     //pulisco response data nel caso in cui ECHO_REQUEST si sia accodata o premessa ad altri comandi
                    }
                    if (responseData == "")
                    {
                        comando = "";
                    }
                    else
                    {
                        tmp = responseData.Substring(1, responseData.Length - 1);
                        comando = responseData.Substring(0, tmp.IndexOf('>') + 2);
                    }
                }
                catch (Exception e)
                {
                    if (username != null)
                    {
                        comandoLogout(DISCONNETTIUTENTE + username);    //serve per eliminare l'utente crashato dagli utenti loggati
                    }
                    risposta = ERRORE + e.Message;
                    chiudere = true;        //si comunica di chiudere il socket
                }

                if (!comando.Equals(""))
                {
                    switch (comando)
                    {
                        case ECHO_REQUEST:
                            risposta = comandoECHO(responseData, clientsocket);
                            nowrite = false;
                            break;
                        case ECDH:
                            risposta = comandoECDH(responseData, clientsocket, out username);
                            nowrite = false;
                            break;
                        case LOGIN:
                            risposta = comandoLogin(responseData, clientsocket);
                            if (risposta.Contains(OK))
                            {
                                username = responseData.Substring(responseData.LastIndexOf((">")) + 1);
                            }
                            nowrite = false;
                            break;
                        case LOGOUT:
                            risposta = comandoLogout(responseData);
                            if (risposta.Contains(OK))
                            {
                                username = null;
                            }
                            nowrite = false;
                            break;
                        case DISCONETTI:
                            Boolean risultato = comandoDisconnetti(responseData);
                            if (risultato)
                            {
                                chiudere = true;
                                risposta = OK + "Client Disconnesso";
                            }
                            else
                                risposta = ERRORE + "Errore durante la  Disconnessione";

                            break;
                        case FOLDER:
                            fileReceived = new LinkedList<string>();
                            risposta = comandoFolder(responseData);
                            nowrite = false;
                            break;
                        case FILE:
                            risposta = comandoFile(responseData, clientsocket, fileReceived);
                            nowrite = false;
                            break;
                        case LISTFILES:
                            risposta = comandoGetListaFile(clientsocket, responseData);
                            nowrite = false;
                            break;
                        case GETVFILE:
                            risposta = comandoGetVersioniFile(clientsocket, responseData);
                            nowrite = false;
                            break;
                        case GETFILEV:
                            risposta = comandoGetFileVersione(clientsocket, responseData);
                            nowrite = false;
                            break;
                        case RENAMEFILE:
                            risposta = comandoRenameFile(responseData);
                            nowrite = false;
                            break;
                        case CANC:
                            risposta = comandoFileCancellato(responseData);
                            nowrite = false;
                            break;
                        case ENDSYNC:
                            risposta = ENDSYNC + "OK - Fine Sync";
                            comandoEndSync(responseData, fileReceived);
                            nowrite = true;
                            break;
                        case GETFOLDERUSER:
                            risposta = comandoGetFoldersUser(responseData);
                            nowrite = false;
                            break;
                        case DISCONNETTIUTENTE:  //chiusura dello stream con logout - Utile per disconnetti da MenuControl
                            risposta = comandoLogout(responseData);
                            chiudere = true;
                            nowrite = true;
                            break;
                        case DISCONNETTICLIENT:  //chiusura dello stream senza logout
                            risposta = "Client richiede la disconnessione";
                            chiudere = true;
                            nowrite = true;
                            break;
                        case RESTORE:
                            risposta = comandoRestore(clientsocket, responseData);
                            nowrite = false;
                            break;
                        case EXITDOWNLOAD:
                            risposta = "Download terminato";
                            nowrite = true;
                            chiudere = true;
                            break;
                        default:
                            risposta = ERRORE + "Comando non valido TOP";
                            nowrite = false;
                            break;
                    }

                }
                else
                {
                    continue;
                }

                mainWindow.tb.Dispatcher.Invoke(new BackupServer.MainWindow.UpdateTextCallback(mainWindow.UpdateText), new object[] { DateTime.Now + " - " + risposta + "\n" });
                if (!nowrite)
                {
                    if (!risposta.Equals("FORCE_ERRORE"))
                    {
                        try
                        {
                            writeStringOnStream(clientsocket, risposta);
                        }
                        catch (Exception)
                        {
                            chiudere = true;
                        }
                    }
                }
            }

            mainWindow.tb.Dispatcher.Invoke(new BackupServer.MainWindow.UpdateTextCallback(mainWindow.UpdateText), new object[] { DateTime.Now + " - Client Disconnesso" + "\n" });
            stream.Close();
            stream.Dispose();
            mainWindow.listaClient.Remove(clientsocket);
            if (File.Exists(Directory.GetCurrentDirectory() + "\\tmp"))
                Directory.Delete(Directory.GetCurrentDirectory() + "\\tmp", true);
            clientsocket.Close();
            counterClient--;

        }

        #endregion
    }
}
