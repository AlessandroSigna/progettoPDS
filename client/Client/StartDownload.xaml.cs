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
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
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
        private MainWindow mw;
        private string completePath;
        private BackgroundWorker workertransaction;
        private string idFile;
        public StartDownload(ClientLogic client, string file, string versionP, string rootF, MainWindow main, String sIdFile)
        {
            InitializeComponent();
            mw = main;
            downloading = false;
            clientLogic = client;
            fileName = file;
            versione = versionP;
            root = rootF;
            App.Current.MainWindow.Width = 300;
            App.Current.MainWindow.Height = 300;
            downloadName.Content = System.IO.Path.GetFileName(file); ;
            idFile = sIdFile;
            RiceviFile();
        }

        public void RiceviFile()
        {
            workertransaction = new BackgroundWorker();
            workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_RiceviFile);
            workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(workertranaction_RiceviFileCompleted);
            workertransaction.RunWorkerAsync();
        }

        private void workertranaction_RiceviFileCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                downloading = false;
                string headerStr = clientLogic.ReadStringFromStream();
                System.Diagnostics.Process.Start("explorer.exe", completePath);
                //clientLogic.WriteStringOnStream(ClientLogic.EXITDOWNLOAD);
                //clientLogic.clientsocket.GetStream().Close();
                //clientLogic.clientsocket.Close();
                App.Current.MainWindow.Close();
            }
            catch
            {
                App.Current.MainWindow.Close();
            }
        }

        private void Workertransaction_RiceviFile(object sender, DoWorkEventArgs e)
        {
            try
            {
                downloading = true;
                clientLogic.WriteStringOnStream(ClientLogic.GETFILEV + clientLogic.username + "+" + root + "+" + fileName + "+" + versione + "+" + idFile);
                int bufferSize = 1024;
                byte[] buffer = null;
                string headerStr = "";
                int filesize = 0;

                string folderCreated = DateTime.Now.Year + "_" + DateTime.Now.Month + "_" + DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute;
                //completePath = @"C:\MyCloud\" + folderCreated;
                completePath = clientLogic.folderR + "\\" + folderCreated;
                //System.IO.Directory.CreateDirectory(@"C:\MyCloud");
                System.IO.Directory.CreateDirectory(clientLogic.folderR + "\\" + folderCreated);
                fileName = fileName.Substring(fileName.LastIndexOf(@"\"));
                string pathTmp = completePath + @"\" + fileName.Substring(fileName.LastIndexOf(@"\") + 1);
                FileStream fs = new FileStream(pathTmp, FileMode.OpenOrCreate);

                headerStr = clientLogic.ReadStringFromStream();
                if (headerStr.Contains(ClientLogic.ERRORE))
                {
                    if (clientLogic.clientsocket.Client.Connected)
                    {
                        clientLogic.clientsocket.GetStream().Close();
                        clientLogic.clientsocket.Close();
                    }
                    return;
                }
                string[] splitted = headerStr.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                String[] str = splitted[0].Split(':');
                filesize = int.Parse(str[1]);

                clientLogic.WriteStringOnStream(ClientLogic.OK);
                int sizetot = 0;
                int original = filesize;
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
            catch
            {
                Thread t2 = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<int>(ExitStub), 2); }));
                t2.Start();
            }
        }

        private void ExitStub(int obj)
        {
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Close();
            if (clientLogic.clientsocket.Client.Connected)
            {
                clientLogic.clientsocket.GetStream().Close();
                clientLogic.clientsocket.Close();
            }
            App.Current.MainWindow = mw;
            MainControl main = new MainControl(1);
            mw.Content = main;
            return;
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
    }
}
