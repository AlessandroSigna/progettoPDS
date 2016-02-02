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
        private string path;
        private string pathR;
        public volatile bool updating;
        public volatile bool exit;
        public volatile bool create = false;
        private string lastCheck;
        private string[] files;

        private List<string> extensions = new List<string>() { "", ".pub", ".pps", ".pptm", ".ppt", ".pptx", ".xlm", ".xlt", ".xls", ".docx", ".doc", ".tmp", ".lnk", ".TMP", ".docm", ".dotx", ".dotcb", ".dotm", ".accdb", ".xlsx", ".jnt" };
        public MenuControl()
        {
            InitializeComponent();
            //((MainWindow)App.Current.MainWindow).IsCloseButtonEnabled = true;
            exit = false;
            lastCheck = String.Empty;
            App.Current.MainWindow.Title = "Mycloud";
            mw = (MainWindow)App.Current.MainWindow;
            mw.clientLogic.event_1 = new AutoResetEvent(false);
            updating = false;
        }

        private async void messaggioStop()
        {
            MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            await mw.ShowMessageAsync("Attendere", "Stiamo chiudendo i canali...  un attimo di pazienza");
        }

        private async void messaggioAttesa()
        {
            MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            await mw.ShowMessageAsync("Attenzione", "Blocca il monitoraggio per effettuare un restore");
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
                mw.clientLogic.folder = path;
                BrushConverter bc = new BrushConverter();
                EffettuaBackup.IsEnabled = true;
                EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FF44E572");
            }

        }

        /*
         * callback del click sul Button EffettuaBackup
         */
        private void EffettuaBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow mw = (MainWindow)App.Current.MainWindow;
                if (!mw.clientLogic.monitorando)
                {
                    BrushConverter bc = new BrushConverter();
                    EffettuaBackup.Background = (Brush)bc.ConvertFrom("#FA5858");   //cambio il colore del bottone
                    EffettuaBackup.Content = "Stop";    //e la scritta
                    FolderButton.IsEnabled = false;     //disabilito il bottone folder
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FOLDER + mw.clientLogic.username + "+" + path);
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
                        mw.clientLogic.InvioFile(files);
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
                    AttendiTermineUpdate();
                }
                mw.clientLogic.monitorando = !mw.clientLogic.monitorando;
            }
            catch
            {
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

        #region Restore
        private void Select_FolderR(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            DialogResult result = fbd.ShowDialog();
            if (fbd.SelectedPath != "")
            {
                RestoreDir.Text = fbd.SelectedPath;
                pathR = fbd.SelectedPath;
                mw.clientLogic.folderR = pathR;
                BrushConverter bc = new BrushConverter();
                RestoreFile.IsEnabled = true;
                RestoreFile.Background = (Brush)bc.ConvertFrom("#33CCFF");

            }

        }

        private void RestoreFile_Click(object sender, RoutedEventArgs e)
        {
            if (mw.clientLogic.clientsocket.Client.Poll(1000, SelectMode.SelectRead))
            {
                MainControl main = new MainControl(1);
                App.Current.MainWindow.Content = main;
                messaggioErrore("Connessione Persa");
                return;

            }

            ClientLogic clRestore = new ClientLogic(mw.clientLogic.ip, mw.clientLogic.porta, mw.clientLogic.folder, mw.clientLogic.username, mw.clientLogic.folderR);
            Window w = null;
            try
            {
                w = new Restore(clRestore, mw);
                w.ShowDialog();
            }
            catch (Exception)
            {
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
                MainControl main = new MainControl(1);
                App.Current.MainWindow.Content = main;
                messaggioErrore("Connessione Persa");
                return;
            }

            App.Current.MainWindow = mw;
            App.Current.MainWindow.Width = 400;
            App.Current.MainWindow.Height = 400;
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

        #region Logout
        private void Logout_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            logoutImage.BeginInit();
            logoutImage.Source = new BitmapImage(new Uri(@"Images/logoutLight.png", UriKind.RelativeOrAbsolute));
            logoutImage.EndInit();
        }

        private void Logout_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            logoutImage.BeginInit();
            logoutImage.Source = new BitmapImage(new Uri(@"Images/logout.png", UriKind.RelativeOrAbsolute));
            logoutImage.EndInit();
        }
        #endregion

        #region Gestione modifiche
        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.WriteLine(" ONdel");
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
                WatcherChangeTypes wct = e.ChangeType;
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    updating = true;
                    mw.clientLogic.WriteStringOnStream(ClientLogic.CANC + mw.clientLogic.username + "+" + e.FullPath);
                    mw.clientLogic.ReadStringFromStream();
                    updating = false;
                    mw.clientLogic.event_1.Set();
                }
            }
            catch
            {

                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();

            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.WriteLine("Oncha");
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

                Console.WriteLine(e.FullPath);
                if (!mw.clientLogic.monitorando)
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                if (Directory.Exists(e.FullPath))
                {
                    mw.clientLogic.event_1.Set();
                    return;
                }
                updating = true;
                InviaSingoloFile(e.FullPath);
                updating = false;
                mw.clientLogic.event_1.Set();
            }
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }


        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {

            try
            {
                Console.WriteLine("Oncre");
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
                InviaSingoloFile(e.FullPath, "CREATE");
                updating = false;
                mw.clientLogic.event_1.Set();
            }
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
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
            MainControl main = new MainControl(1);
            mw.Content = main;
        }

        private static void SetValue(System.Windows.Controls.TextBox txt)
        {
            txt.Text = "Ultima sincronizzazione : " + DateTime.Now;
        }



        private void AttendiTermineUpdate()
        {

            MainWindow mw = (MainWindow)App.Current.MainWindow;
            if (mw.clientLogic.lavorandoInvio)
            {
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
                if (exit)
                {
                    MainWindow mainw = (MainWindow)App.Current.MainWindow;
                    mainw.clientLogic.monitorando = false;
                    mainw.clientLogic.lavorandoInvio = false;
                    mainw.clientLogic.WriteStringOnStream(ClientLogic.DISCONETTIUTENTE + mainw.clientLogic.username + "+" + mainw.clientLogic.mac);
                    mainw.clientLogic.connesso = false;
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
            catch
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<MainWindow>(ChangeWindow), mw); }));
                t.Start();
            }
        }

        private void Workertransaction_Waited(object sender, DoWorkEventArgs e)
        {
            mw.clientLogic.event_1.WaitOne();
        }
        private void InviaSingoloFile(string fileName)
        {

            InviaSingoloFile(fileName, "");
        }

        private void InviaSingoloFile(string fileName, string onCreate)
        {

            try
            {
                if (onCreate.Equals("CREATE"))
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FILE + mw.clientLogic.username + "+" + onCreate);
                else
                    mw.clientLogic.WriteStringOnStream(ClientLogic.FILE + mw.clientLogic.username);
                int bufferSize = 1024;
                byte[] buffer = null;
                byte[] header = null;
                string checksum = "";

                mw.clientLogic.ReadStringFromStream();
                Thread.Sleep(100);
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

                Thread t1 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, int, string, System.Windows.Controls.TextBox>(SetProgressBar), pbStatus, bufferCount, System.IO.Path.GetFileName(fileName), FileUploading); }));
                t1.Start();

                for (int i = 0; i < bufferCount; i++)
                {
                    if ((i == (bufferCount / 4)) || (i == (bufferCount / 2)) || (i == ((bufferCount * 3) / 4)) || (i == (bufferCount - 1)))
                    {
                        Thread t2 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar, int>(UpdateProgressBar), pbStatus, i); }));
                        t2.Start();
                    }
                    buffer = new byte[bufferSize];
                    int size = fs.Read(buffer, 0, bufferSize);
                    mw.clientLogic.clientsocket.Client.SendTimeout = 30000;
                    mw.clientLogic.clientsocket.Client.Send(buffer, size, SocketFlags.Partial);
                }

                Thread t3 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<System.Windows.Controls.ProgressBar>(HideProgress), pbStatus); }));
                t3.Start();
                fs.Close();
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



        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            DialogDisconnetti();
        }

        public async void DialogDisconnetti()
        {
            customDialog = new CustomDialog();
            disconnettiWindow = new Disconnetti();
            disconnettiWindow.BServer.Click += ButtonServerOnClick;
            disconnettiWindow.BCancel.Click += ButtonCancelOnClick;
            customDialog.Content = disconnettiWindow;
            MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            await mw.ShowMetroDialogAsync(customDialog);
        }

        private void ButtonServerOnClick(object sender, RoutedEventArgs e)
        {
            //MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            //mw.HideMetroDialogAsync(customDialog);
            //MainWindow mainw = (MainWindow)mw;

            Window mw = (Window)App.Current.MainWindow;
            //mw.HideMetroDialogAsync(customDialog);
            MainWindow mainw = (MainWindow)mw;

            exit = true;
            if (mainw.clientLogic.lavorandoInvio || updating)
                EffettuaBackup.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            else
                mainw.clientLogic.DisconnettiServer(false);

        }

        private void ButtonCancelOnClick(object sender, RoutedEventArgs e)
        {
            MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            mw.HideMetroDialogAsync(customDialog);
        }




        private async void messaggioErrore(string mess)
        {
            MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            await mw.ShowMessageAsync("Errore", mess);
        }

    }
}
