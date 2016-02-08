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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Logica di interazione per DownloadFile.xaml
    /// </summary>
    public partial class DownloadFile : UserControl
    {
        private string folder;
        private string namefile;
        private string completePath;
        private ClientLogic clientlogic;
        private MainWindow mw;
        private Boolean search; //non ho capito a che serve :|
        private string idfile;
        /*
         * Costruttore
         */
        public DownloadFile(ClientLogic clientLogic, string root, string nameFile, Boolean searchPass, MainWindow mainw, String idFile)
        {
            try
            {
                InitializeComponent();
                mw = mainw;
                clientlogic = clientLogic;
                search = searchPass;
                folder = root;
                namefile = nameFile;
                completePath = folder + @"\" + namefile;
                App.Current.MainWindow.Width = 600;
                App.Current.MainWindow.Height = 430;
                //domando al server le versioni del file selezionato
                clientLogic.WriteStringOnStream(ClientLogic.GETVFILE + clientLogic.username + "+" + folder + "+" + completePath + "+" + idFile);
                String retFiles;
                Boolean exit = false;

                //parsifico la risposta del server e popolo la ListBox
                while (!exit)
                {
                    retFiles = clientLogic.ReadStringFromStream();
                    String[] parametri = retFiles.Split('+');
                    String comando = parametri[1];
                    if (comando.Equals("FLP"))
                    {
                        addElementToListbox(parametri[3]);
                        clientlogic.WriteStringOnStream(ClientLogic.OK);
                    }
                    else if (comando.Equals("ENDLIST") || comando.Equals("INFO"))
                    {
                        exit = true;
                    }
                    else
                    {
                        exit = true;
                    }
                }
                if (!search)
                    addElementToListbox("...");
            }
            catch
            {
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Close();
                if (clientLogic.clientsocket.Client.Connected)
                {
                    clientLogic.clientsocket.GetStream().Close();
                    clientLogic.clientsocket.Close();
                }
                App.Current.MainWindow = mainw;
                MainControl main = new MainControl();
                App.Current.MainWindow.Content = main;
                main.messaggioErrore();
                return;
            }
        }

        /*
         * Si popola la ListBox in base alle info sul file ricevute dal server
         * - info dim file = 0 -> icona file cancellato + stringa + verisone + timestamp
         * - info dim file > 0 -> icona file aggiunto + dim + versione + timestamp
         * nel secondo tipo di entry viene settata anche la callback per il doppioclick
         */
        void addElementToListbox(String fileInfo)
        {
            if (fileInfo.Equals("..."))
            {
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Image folderImg = new Image();
                BitmapImage fld = new BitmapImage();
                fld.BeginInit();
                fld.UriSource = new Uri(@"Images/goback.png", UriKind.RelativeOrAbsolute);
                fld.EndInit();
                folderImg.Source = fld;
                folderImg.Width = 40;
                folderImg.Height = 40;
                folderImg.Margin = new Thickness(10, 10, 0, 10);
                sp.Children.Add(folderImg);
                Label folderName = new Label();
                folderName.Content = "Torna alla cartella";
                folderName.Background = Brushes.Transparent;
                folderName.BorderBrush = Brushes.Transparent;
                folderName.Margin = new Thickness(30, 15, 0, 15);
                folderName.FontSize = 18;
                folderName.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(folderName);
                ListBoxItem item = new ListBoxItem();
                item.Content = sp;
                item.MouseDoubleClick += item_MouseDoubleClickBack;
                ListBox.Items.Insert(0, item);
                return;
            }
            String[] info = fileInfo.Split('?');
            String filename = info[0];
            String relativePath;
            relativePath = filename;
            String versione = info[1];
            int dimFile = int.Parse(info[2]);
            String timestamp = info[3];
            if (!relativePath.Contains(@"\"))
            {
                idfile = info[4];
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Image folderImg = new Image();
                BitmapImage fld = new BitmapImage();
                fld.BeginInit();
                if (dimFile > 0)
                    fld.UriSource = new Uri(@"Images/fileadd.png", UriKind.RelativeOrAbsolute);
                else
                    fld.UriSource = new Uri(@"Images/filedel.png", UriKind.RelativeOrAbsolute);
                fld.EndInit();
                folderImg.Source = fld;
                folderImg.Width = 40;
                folderImg.Height = 40;
                folderImg.Margin = new Thickness(10, 10, 0, 10);
                sp.Children.Add(folderImg);
                Label folderName = new Label();
                folderName.Content = relativePath;
                folderName.Background = Brushes.Transparent;
                folderName.BorderBrush = Brushes.Transparent;
                folderName.Margin = new Thickness(30, 15, 0, 15);
                folderName.Width = 175;
                folderName.FontSize = 14;
                folderName.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(folderName);
                Label dimFileL = new Label();
                if (dimFile > 0)
                    dimFileL.Content = SizeSuffix(dimFile);
                else
                    dimFileL.Content = "File cancellato";
                dimFileL.Width = 75;
                dimFileL.Background = Brushes.Transparent;
                dimFileL.BorderBrush = Brushes.Transparent;
                dimFileL.Margin = new Thickness(30, 15, 0, 15);
                dimFileL.FontSize = 10;
                dimFileL.HorizontalContentAlignment = HorizontalAlignment.Right;
                dimFileL.VerticalAlignment = VerticalAlignment.Center;
                dimFileL.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(dimFileL);
                Label versioneFileL = new Label();
                versioneFileL.Content = "Versione: " + versione;
                versioneFileL.Background = Brushes.Transparent;
                versioneFileL.BorderBrush = Brushes.Transparent;
                versioneFileL.Margin = new Thickness(30, 15, 0, 15);
                versioneFileL.FontSize = 10;
                versioneFileL.Width = 60;
                versioneFileL.HorizontalContentAlignment = HorizontalAlignment.Right;
                versioneFileL.VerticalAlignment = VerticalAlignment.Center;
                versioneFileL.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(versioneFileL);
                Label tsFileL = new Label();
                tsFileL.Content = timestamp;
                tsFileL.Background = Brushes.Transparent;
                tsFileL.BorderBrush = Brushes.Transparent;
                tsFileL.Margin = new Thickness(30, 15, 0, 15);
                tsFileL.FontSize = 10;
                tsFileL.Width = 110;
                tsFileL.FontFamily = new FontFamily("Tahoma");
                tsFileL.HorizontalContentAlignment = HorizontalAlignment.Right;
                tsFileL.VerticalAlignment = VerticalAlignment.Center;
                sp.Children.Add(tsFileL);
                TextBox ver = new TextBox();
                ver.Text = versione;
                ver.Visibility = Visibility.Hidden;
                sp.Children.Add(ver);
                ListBoxItem item = new ListBoxItem();
                item.Content = sp;
                if (dimFile > 0)
                    item.MouseDoubleClick += item_MouseDoubleClick;
                ListBox.Items.Add(item);
            }
        }

        void item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListBox.SelectedIndex == -1)
                return;

            ListBoxItem selectedItem = this.ListBox.ItemContainerGenerator.ContainerFromIndex(ListBox.SelectedIndex) as ListBoxItem;

            if (selectedItem == null)
                return;

            if (ListBox.SelectedItem != null)
            {
                TextBox ver = clientlogic.FindDescendant<TextBox>(selectedItem);
                StartDownload main = new StartDownload(clientlogic, completePath, ver.Text.ToString(), folder, mw, idfile);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }
        }

        private void item_MouseDoubleClickBack(object sender, MouseButtonEventArgs e)
        {
            if (ListBox.SelectedIndex == -1)
                return;

            ListBoxItem selectedItem = this.ListBox.ItemContainerGenerator.ContainerFromIndex(ListBox.SelectedIndex) as ListBoxItem;

            if (selectedItem == null)
                return;

            if (ListBox.SelectedItem != null)
            {
                FileSelection main;
                Label nameBox = clientlogic.FindDescendant<Label>(selectedItem);

                if (namefile.Contains(@"\"))
                {
                    namefile = namefile.Substring(0, namefile.LastIndexOf(@"\") + 1);
                    main = new FileSelection(clientlogic, folder, namefile, false, mw);
                }
                else
                    main = new FileSelection(clientlogic, folder, "", false, mw);

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

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            SelectActionUI main = new SelectActionUI(clientlogic, folder, mw);
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Content = main;
        }

        private void onSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (ListBox.SelectedIndex == -1)
                return;

            ListBoxItem selectedItem = this.ListBox.ItemContainerGenerator.ContainerFromIndex(ListBox.SelectedIndex) as ListBoxItem;

            if (selectedItem == null)
                return;

            Label nameBox = clientlogic.FindDescendant<Label>(selectedItem);

        }

        private void HomeButton_MouseEnter(object sender, RoutedEventArgs e)
        {
            homeImage1.BeginInit();
            homeImage1.Source = new BitmapImage(new Uri(@"Images/homeon.png", UriKind.RelativeOrAbsolute));
            homeImage1.EndInit();
        }

        private void HomeButton_MouseLeave(object sender, RoutedEventArgs e)
        {
            homeImage1.BeginInit();
            homeImage1.Source = new BitmapImage(new Uri(@"Images/home.png", UriKind.RelativeOrAbsolute));
            homeImage1.EndInit();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            FileSelection main = new FileSelection(clientlogic, folder, "", false, mw);
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Content = main;
        }

        static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(Int64 value)
        {
            if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return "0.0 bytes"; }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }
    }
}
