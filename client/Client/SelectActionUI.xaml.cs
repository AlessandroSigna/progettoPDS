using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Logica di interazione per SelectActionUI.xaml
    /// </summary>
    public partial class SelectActionUI : UserControl
    {
        private ClientLogic clientLogic;
        private string folderRoot;
        private MainWindow mw;
        /*
         * Costruttore
         */
        public SelectActionUI(ClientLogic client, string folder, MainWindow main)
        {
            InitializeComponent();
            mw = main;
            clientLogic = client;
            folderRoot = folder;
            App.Current.MainWindow.Width = 400;
            App.Current.MainWindow.Height = 400;
        }

        #region Download Cartella
        private void Folder_Click(object sender, RoutedEventArgs e)
        {

            DownloadFolder fs = new DownloadFolder(clientLogic, folderRoot, ((Restore)App.Current.MainWindow).mw);
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Content = fs;

        }

        private void Folder_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Folder.Background = (Brush)bc.ConvertFrom("#FFFACD");
        }

        private void Folder_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Folder.Background = (Brush)bc.ConvertFrom("#FFF5F804");
        }
        #endregion

        #region Download File
        /*
         * Callback del Button Download File che passa il controllo a FileSelection
         */
        private void File_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSelection w = new FileSelection(clientLogic, folderRoot, "", false, ((Restore)App.Current.MainWindow).mw);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = w;
            }
            catch
            {
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Close();
                App.Current.MainWindow = mw;
                //if (mw.clientLogic.clientsocket.Connected)
                //{
                //    mw.clientLogic.clientsocket.GetStream().Close();
                //    mw.clientLogic.clientsocket.Close();
                //}
                clientLogic.DisconnectAndClose();
                //MainControl main = new MainControl();
                //App.Current.MainWindow.Content = main;
                //main.messaggioErrore();
                mw.restart(true);

            }

        }

        private void File_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            File.Background = (Brush)bc.ConvertFrom("#F5FFFA");
        }

        private void File_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            File.Background = (Brush)bc.ConvertFrom("#FF44E572");
        }

        #endregion

        #region Back Button
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.Current.MainWindow is Restore)
            {
                RestoreUC main = new RestoreUC(clientLogic, ((Restore)App.Current.MainWindow).mw);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }
        }

        private void Back_MouseEnter(object sender, MouseEventArgs e)
        {
            backImage.BeginInit();
            backImage.Source = new BitmapImage(new Uri(@"Images/backLight.png", UriKind.RelativeOrAbsolute));
            backImage.EndInit();
        }

        private void Back_MouseLeave(object sender, MouseEventArgs e)
        {
            backImage.BeginInit();
            backImage.Source = new BitmapImage(new Uri(@"Images/back.png", UriKind.RelativeOrAbsolute));
            backImage.EndInit();
        }

    }
        #endregion
}
