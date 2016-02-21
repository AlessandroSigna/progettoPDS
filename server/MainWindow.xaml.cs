using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.ComponentModel;
using System.IO;
using System.Data.SQLite;
using System.Collections.Generic;
using System.Threading;

namespace BackupServer
{
    public partial class MainWindow
    {
        private System.Windows.Forms.NotifyIcon MyNotifyIcon;
        public Boolean avviato = false;
        public static String pathDB = "";
        public ServerLogic server;
        public SQLiteConnection m_dbConnection;

        public List<TcpClient> listaClient = new List<TcpClient>();

        public MainWindow()
        {
            InitializeComponent();
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 100;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 500;
            TPorta.Text = "1010";
            MyNotifyIcon = new System.Windows.Forms.NotifyIcon();
            MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/applogoIcon.ico");
            MyNotifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(MyNotifyIcon_MouseClick);
        }

        private void startStopClick(object sender, RoutedEventArgs e)
        {
            if (avviato == false)
            {
                string pathDBtemp = TPathDB.Text;
                pathDB = pathDBtemp;
                startServer();
            }
            else
            {
                stopServer();
            }
        }

        #region UI e controlli su input dell'utente
        //Metodo Che controlla se  sono inseriti solo numeri non lettere o punteggiatura.
        private void NumericText_Validate(object sender, TextCompositionEventArgs e)
        {
            if (Char.IsNumber(e.Text, 0))
            {
                ErrorMessage.Text = "";

            }
            else
            {
                ErrorMessage.Text = "Inserire solo valori numerici nel campo della porta.";
            }
        }

        private void clear_Click(object sender, RoutedEventArgs e)
        {
            tb.Text = "";
        }

        #endregion

        private void stopServer()
        {
            MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/applogoIcon.ico");

            ServerLogic.serverKO = true;
            TPorta.IsEnabled = true;
            TPathDB.IsEnabled = true;
            TPorta.Background = Brushes.White;
            TPathDB.Background = Brushes.White;

            foreach (TcpClient client in listaClient)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(ServerLogic.INFO + "Connessione chiusa dal server");
                    if (stream.CanWrite)
                        stream.Write(data, 0, data.Length);

                    stream.Close();
                    stream.Dispose();
                    client.Close();
                }
                catch
                {
                    tb.Text += DateTime.Now + " - errore durante la chiusura dello stream\n";
                }
            }

            try
            {
                SQLiteCommand comandoP = new SQLiteCommand(m_dbConnection);
                comandoP.CommandText = "DELETE FROM UTENTILOGGATI";
                bool isBroken = false;
                do
                {
                    try
                    {
                        ServerLogic._readerWriterLock.EnterWriteLock();
                        if (ServerLogic._readerWriterLock.WaitingReadCount > 0)
                        {
                            isBroken = true;
                        }
                        else
                        {
                            comandoP.ExecuteNonQuery();
                        }
                    }
                    finally
                    {
                        ServerLogic._readerWriterLock.ExitWriteLock();
                    }
                    if (isBroken)
                    {
                        Thread.Sleep(10);
                    }
                    else
                        isBroken = false;
                } while (isBroken);
            }
            catch
            {
                tb.Text += DateTime.Now + " - errore durante la DELETE utenti loggati\n";
            }

            serverSocket.Stop();
            m_dbConnection.Dispose();
            m_dbConnection.Close();
            tb.Text += DateTime.Now + " - Connessione al DB chiusa\n";
            tb.Text += DateTime.Now + " - Server arrestato\n";
            avviato = false;
            statusImage.BeginInit();
            statusImage.Source = new BitmapImage(new Uri(@"Images/startpremuto.png", UriKind.RelativeOrAbsolute));
            statusImage.EndInit();
        }


        private void startServer()
        {
            try
            {
                porta = int.Parse(TPorta.Text);
            }
            catch
            {
                ErrorMessage.Text = "Inserire solo valori numerici nel campo della porta.";
                TPorta.Text = "";
                TPorta.BorderBrush = Brushes.Red;
                return;
            }

            if (pathDB.Equals("") || pathDB == null)
            {
                ErrorMessage.Text = "Seleziona il database.";
                TPathDB.Text = "";
                TPathDB.BorderBrush = Brushes.Red;
                return;
            }


            #region Controlli e creazione Database
            if (!File.Exists(pathDB)) 
            {
                try 
                {
                    SQLiteConnection.CreateFile(pathDB);
                }
                catch
                {
                    ErrorMessage.Text = "Errore nell'url del database.";
                    TPathDB.Text = "";
                    TPathDB.BorderBrush = Brushes.Red;
                    return;
                }
            }

            m_dbConnection = new SQLiteConnection("Data Source=" + pathDB + ";Version=3;Journal Mode=Off;Synchronous=Full;");
            m_dbConnection.Open();
            SQLiteTransaction transazioneINIT = m_dbConnection.BeginTransaction();
            try
            {

                bool isBroken = false;
                do
                {
                    try
                    {
                        ServerLogic._readerWriterLock.EnterWriteLock();
                        if (ServerLogic._readerWriterLock.WaitingReadCount > 0)
                        {
                            isBroken = true;
                        }
                        else
                        {
                            String queryCheck = "SELECT name FROM sqlite_master WHERE type='table' AND name='UTENTI'";
                            SQLiteCommand comand = new SQLiteCommand(queryCheck, m_dbConnection, transazioneINIT);
                            if (comand.ExecuteScalar() == null)
                            {
                                String queryCreaTab1 = "CREATE TABLE UTENTI ( username VARCHAR(50)  PRIMARY KEY, password VARCHAR(20))";
                                comand = new SQLiteCommand(queryCreaTab1, m_dbConnection, transazioneINIT);
                                comand.ExecuteNonQuery();
                            }
                            queryCheck = "SELECT name FROM sqlite_master WHERE type='table' AND name='BACKUPHISTORY'";
                            comand = new SQLiteCommand(queryCheck, m_dbConnection, transazioneINIT);
                            if (comand.ExecuteScalar() == null)
                            {
                                String queryCreaTab2 = "CREATE TABLE BACKUPHISTORY (idfile INTEGER, username VARCHAR(50), folderBackup VARCHAR(2000), percorsoFile VARCHAR(2000), versione INTEGER, file BLOB, dimFile INTEGER, isDelete VARCHAR(5), checksum VARCHAR(2000), timestamp VARCHAR(50), PRIMARY KEY(username,folderBackup,versione,percorsoFile,idfile))";
                                comand = null;
                                comand = new SQLiteCommand(queryCreaTab2, m_dbConnection, transazioneINIT);
                                comand.ExecuteNonQuery();
                            }

                            queryCheck = "SELECT name FROM sqlite_master WHERE type='table' AND name='LASTFOLDERUTENTE'";
                            comand = new SQLiteCommand(queryCheck, m_dbConnection, transazioneINIT);
                            if (comand.ExecuteScalar() == null)
                            {
                                String queryCreaTab3 = "CREATE TABLE LASTFOLDERUTENTE ( username VARCHAR(50) PRIMARY KEY, folderBackup VARCHAR(2000), lastUpdate VARCHAR(50))";
                                comand = null;
                                comand = new SQLiteCommand(queryCreaTab3, m_dbConnection, transazioneINIT);
                                comand.ExecuteNonQuery();
                            }

                            queryCheck = "SELECT name FROM sqlite_master WHERE type='table' AND name='RENAMEFILEMATCH'";
                            comand = new SQLiteCommand(queryCheck, m_dbConnection, transazioneINIT);
                            if (comand.ExecuteScalar() == null)
                            {
                                String queryCreaTab3 = "CREATE TABLE RENAMEFILEMATCH ( username VARCHAR(50), folderBackup VARCHAR(2000),idfile INTEGER,percorsoFileOLD VARCHAR(2000),percorsoFileNEW VARCHAR(2000),lastVersionOLD INTEGER, lastUpdate VARCHAR(50), PRIMARY KEY(username,folderBackup,percorsoFileOLD,idfile))";
                                comand = null;
                                comand = new SQLiteCommand(queryCreaTab3, m_dbConnection, transazioneINIT);
                                comand.ExecuteNonQuery();
                            }

                            queryCheck = "SELECT name FROM sqlite_master WHERE type='table' AND name='UTENTILOGGATI'";
                            comand = new SQLiteCommand(queryCheck, m_dbConnection, transazioneINIT);
                            if (comand.ExecuteScalar() == null)
                            {
                                String queryCreaTab3 = "CREATE TABLE UTENTILOGGATI ( username VARCHAR(50), lastUpdate VARCHAR(50),PRIMARY KEY (username))";
                                comand = null;
                                comand = new SQLiteCommand(queryCreaTab3, m_dbConnection, transazioneINIT);
                                comand.ExecuteNonQuery();
                            }
                            queryCheck = "SELECT name FROM sqlite_master WHERE type='table' AND name='IDFILESEQ'";
                            comand = new SQLiteCommand(queryCheck, m_dbConnection, transazioneINIT);
                            if (comand.ExecuteScalar() == null)
                            {
                                String queryCreaTab3 = "CREATE TABLE IDFILESEQ ( idfile INTEGER PRIMARY KEY)";
                                comand = null;
                                comand = new SQLiteCommand(queryCreaTab3, m_dbConnection, transazioneINIT);
                                comand.ExecuteNonQuery();
                            }

                            Console.WriteLine("Fine creazione e controlli tabelle. ");
                        }
                    }
                    finally
                    {
                        ServerLogic._readerWriterLock.ExitWriteLock();
                    }
                    if (isBroken)
                    {
                        Thread.Sleep(10);
                    }
                    else
                        isBroken = false;
                } while (isBroken);


                try
                {
                    SQLiteCommand comandoP = new SQLiteCommand("", m_dbConnection, transazioneINIT);
                    comandoP.CommandText = "DELETE FROM UTENTILOGGATI";
                    bool isBroken2 = false;
                    do
                    {
                        try
                        {
                            ServerLogic._readerWriterLock.EnterWriteLock();
                            if (ServerLogic._readerWriterLock.WaitingReadCount > 0)
                            {
                                isBroken2 = true;
                            }
                            else
                            {
                                comandoP.ExecuteNonQuery();
                            }
                        }
                        finally
                        {
                            ServerLogic._readerWriterLock.ExitWriteLock();
                        }
                        if (isBroken2)
                        {
                            Thread.Sleep(10);
                        }
                        else
                            isBroken2 = false;
                    } while (isBroken2);
                }
                catch
                {
                    tb.Text += DateTime.Now + " - errore durante la DELETE utenti loggati\n";
                }

            }
            catch
            {
                transazioneINIT.Rollback();
                transazioneINIT.Dispose();
                tb.Text += DateTime.Now + " - ERRORE durante l'inizializzazione del DB\n";
                return;
            }

            transazioneINIT.Commit();
            transazioneINIT.Dispose();

            statusImage.BeginInit();
            statusImage.Source = new BitmapImage(new Uri(@"Images/stoppremuto.png", UriKind.RelativeOrAbsolute));
            statusImage.EndInit();

            tb.Text += DateTime.Now + " - DB Inizializzato correttamente\n";
            #endregion


            #region Inizializzazione connessione tcp
            try
            {
                serverSocket = new TcpListener(IPAddress.Any, porta);
                serverSocket.Start();

                server = new ServerLogic(ref serverSocket, porta, this);
                ServerLogic.serverKO = false;
            }
            catch
            {
                ErrorMessage.Text = "Il valore della porta non è corretto.";
                TPorta.Text = "";
                TPorta.BorderBrush = Brushes.Red;
                return;
            }
            #endregion

            tb.Text += DateTime.Now + " - Server avviato su porta " + porta + "\n";
            MyNotifyIcon.Icon = new System.Drawing.Icon(@"Images/applogoIcon.ico");
            avviato = true;


            ErrorMessage.Text = "";
            TPorta.BorderBrush = Brushes.Transparent;
            TPathDB.BorderBrush = Brushes.Transparent;
            TPorta.IsEnabled = false;
            TPathDB.IsEnabled = false;
            TPorta.Background = Brushes.LightGray;
            TPathDB.Background = Brushes.LightGray;

            tb.Text += DateTime.Now + " - ***DB selezionato: " + pathDB + "***\n";
        }

        internal void UpdateText(string message)
        {
            tb.Text += message;
        }

        public delegate void UpdateTextCallback(string message);

        public TcpListener serverSocket;

        public int porta;

        #region Chiusura UI
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (avviato)
            {
                e.Cancel = true;

            }
            else
            {
                Application.Current.Shutdown();
            }
        }
        #endregion

        private void SelezionafileDB(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog sfoglia = new Microsoft.Win32.OpenFileDialog();
            sfoglia.FileName = "DataBase";
            sfoglia.DefaultExt = ".sqlite";
            sfoglia.Filter = "File DataBase (.sqlite)|*.sqlite";
            sfoglia.CheckFileExists = false;
            Nullable<bool> result = sfoglia.ShowDialog();

            if (result == true)
            {
                string filename = sfoglia.FileName;
                TPathDB.Text = filename;
            }

        }

        #region UI di MainWindow
        private void ButtonOkOnClick(object sender, RoutedEventArgs e)
        {
            stopServer();
            this.Close();
        }


        void MyNotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            this.WindowState = WindowState.Normal;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                MyNotifyIcon.Visible = true;
            }
            else if (this.WindowState == WindowState.Normal)
            {
                MyNotifyIcon.Visible = false;
                this.ShowInTaskbar = true;
            }
        }

        private void clear_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            BClear.Background = (Brush)bc.ConvertFrom("#FFFFCC");

        }

        private void clear_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            BClear.Background = (Brush)bc.ConvertFrom("#FFFF99");

        }

        private void startStop_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!avviato)
            {
                statusImage.BeginInit();
                statusImage.Source = new BitmapImage(new Uri(@"Images/startpremuto.png", UriKind.RelativeOrAbsolute));
                statusImage.EndInit();
            }
            else
            {
                statusImage.BeginInit();
                statusImage.Source = new BitmapImage(new Uri(@"Images/stoppremuto.png", UriKind.RelativeOrAbsolute));
                statusImage.EndInit();
            }
        }

        private void startStop_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!avviato)
            {
                statusImage.BeginInit();
                statusImage.Source = new BitmapImage(new Uri(@"Images/start.png", UriKind.RelativeOrAbsolute));
                statusImage.EndInit();
            }
            else
            {
                statusImage.BeginInit();
                statusImage.Source = new BitmapImage(new Uri(@"Images/stop.png", UriKind.RelativeOrAbsolute));
                statusImage.EndInit();
            }
        }

        private void autoScroll(object sender, TextChangedEventArgs e)
        {
            tb.ScrollToEnd();
        }
        #endregion
    }

}
