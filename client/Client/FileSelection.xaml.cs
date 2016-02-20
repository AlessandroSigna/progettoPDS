using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Logica di interazione per FileSelection.xaml
    /// </summary>
    public partial class FileSelection : UserControl
    {
        private ClientLogic clientlogic;
        private String selFolderPath;   //folder da analizzare
        private String likeNome;    //stringa che il file deve contenere (inserito nella search bar)
        private Boolean search; //non ho capito :|
        private String relativePath;
        private MainWindow mw;
        List<string> listElement;

        /*
         * Costruttore
         */
        public FileSelection(ClientLogic clientLogic, string folder, string likeNomePass, Boolean searchBar, MainWindow mainw)
        {
            try
            {
                InitializeComponent();
                mw = mainw;
                listElement = new List<string>();
                search = searchBar;
                selFolderPath = folder;
                likeNome = likeNomePass;
                clientlogic = clientLogic;
                App.Current.MainWindow.Width = 600;
                App.Current.MainWindow.Height = 430;
                //si chiede al server la lista dei file nella folder (che contengono likeNome)
                clientLogic.WriteStringOnStream(ClientLogic.LISTFILES + clientLogic.username + "+" + folder + "+" + likeNome);


                String retFiles;
                Boolean exit = false;
                Boolean fine = false;
                //si parsifica opportunamente la stringa che ha inviato il server come risposta
                while (!exit)
                {
                    retFiles = clientLogic.ReadStringFromStream();
                    String[] parametri = retFiles.Split('+');
                    String comando = parametri[1];
                    if (comando.Equals("FLP"))
                    {
                        noFile.Visibility = Visibility.Hidden;
                        addElementToListbox(parametri[3]);  //ricevuta la stringa si aggiunge l'elemeno alla ListBox...
                        clientlogic.WriteStringOnStream(ClientLogic.OK);    //e si manda un ACK al server
                    }
                    else if (comando.Equals("ENDLIST") || comando.Equals("INFO"))
                    {
                        exit = true;
                    }
                    else
                    {
                        exit = true;
                        fine = true;
                    }
                }
                if (fine)
                {
                    App.Current.MainWindow.Close();
                }
                if (!likeNome.Equals(String.Empty) && !search)
                {
                    addElementToListbox("...");
                }
            }
            catch (Exception)
            {
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Close();
                //if (clientLogic.clientsocket.Client.Connected)
                //{
                //    clientLogic.clientsocket.GetStream().Close();
                //    clientLogic.clientsocket.Close();
                //}
                clientLogic.DisconnectAndClose();
                //App.Current.MainWindow = mainw;
                //MainControl main = new MainControl();
                //App.Current.MainWindow.Content = main;
                //main.messaggioErrore();
                mainw.restart(true);
                return;

            }

        }

        public string MakeRelativePath2(string workingDirectory, string fullPath)
        {
            string realtivePath = fullPath.Substring(workingDirectory.Length + 1);
            return realtivePath;
        }

        void addElementToListbox(String fileInfo)
        {
            if (fileInfo.Equals("...")) //item per tornare indietro
            {
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Image folderImg = new Image();
                BitmapImage fld = new BitmapImage();
                fld.BeginInit();
                fld.UriSource = new Uri(@"Images/folder.png", UriKind.RelativeOrAbsolute);
                fld.EndInit();
                folderImg.Source = fld;
                folderImg.Width = 40;
                folderImg.Height = 40;
                folderImg.Margin = new Thickness(10, 10, 0, 10);
                sp.Children.Add(folderImg);
                Label folderName = new Label();
                folderName.Content = "...";
                folderName.Background = Brushes.Transparent;
                folderName.BorderBrush = Brushes.Transparent;
                folderName.Margin = new Thickness(30, 15, 0, 15);
                folderName.FontSize = 14;
                folderName.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(folderName);
                ListBoxItem item = new ListBoxItem();
                item.Content = sp;
                item.MouseDoubleClick += item_MouseDoubleClickBack; //aggiungo la callback in caso di doppio click sull'item per tornare indietro
                ListBox.Items.Insert(0, item);
                return;
            }
            String[] info = fileInfo.Split('?');
            String filename = info[0];

            if (!search)
            {
                if (likeNome != "")
                {
                    relativePath = MakeRelativePath2(selFolderPath + likeNome, filename);
                }
                else
                    relativePath = MakeRelativePath2(selFolderPath, filename);
            }
            else
            {
                relativePath = MakeRelativePath2(selFolderPath, filename);
            }

            //info comuni sia a file che a cartelle (le posso mettere in una classe incapsulata dentro item.Tag)
            String versione = info[1];
            int dimFile = int.Parse(info[2]);
            String timestamp = info[3];
            if (!relativePath.Contains(@"\") && !search)    //è il path di un file che sta direttamente nella folder aperta
            {
                String idfile = info[4];
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Image folderImg = new Image();
                BitmapImage fld = new BitmapImage();
                fld.BeginInit();
                fld.UriSource = new Uri(@"Images/file.png", UriKind.RelativeOrAbsolute);
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
                folderName.FontSize = 14;
                folderName.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(folderName);
                TextBox id = new TextBox();
                id.Text = idfile;
                id.Visibility = Visibility.Hidden;
                sp.Children.Add(id);
                ListBoxItem item = new ListBoxItem();
                item.Content = sp;
                item.MouseDoubleClick += item_MouseDoubleClick; //aggiungo callback per doppio click sulla entry file
                ListBox.Items.Add(item);
            }
            else if (relativePath.Contains(@"\") && !search)    //è il path di un file che sta in una sottocartella - estraggo solo il nome della cartella
            {
                string[] subfolder = relativePath.Split('\\');
                if (listElement.Contains(subfolder[0]))
                    return;
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Image folderImg = new Image();
                BitmapImage fld = new BitmapImage();
                fld.BeginInit();
                fld.UriSource = new Uri(@"Images/folder.png", UriKind.RelativeOrAbsolute);
                fld.EndInit();
                folderImg.Source = fld;
                folderImg.Width = 40;
                folderImg.Height = 40;
                folderImg.Margin = new Thickness(10, 10, 0, 10);
                sp.Children.Add(folderImg);
                Label folderName = new Label();
                folderName.Content = subfolder[0];
                folderName.Background = Brushes.Transparent;
                folderName.BorderBrush = Brushes.Transparent;
                folderName.Margin = new Thickness(30, 15, 0, 15);
                folderName.FontSize = 14;
                folderName.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(folderName);
                ListBoxItem item = new ListBoxItem();
                item.Content = sp;
                item.MouseDoubleClick += item_MouseDoubleClickFolder;   //callback click entry cartella
                ListBox.Items.Insert(0, item);
                listElement.Add(subfolder[0]);
            }
            else if (search)
            {
                String idfile = info[4];
                StackPanel sp = new StackPanel();
                sp.Orientation = Orientation.Horizontal;
                Image folderImg = new Image();
                BitmapImage fld = new BitmapImage();
                fld.BeginInit();
                fld.UriSource = new Uri(@"Images/file.png", UriKind.RelativeOrAbsolute);
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
                folderName.FontSize = 14;
                folderName.FontFamily = new FontFamily("Tahoma");
                sp.Children.Add(folderName);
                TextBox id = new TextBox();
                id.Text = idfile;
                id.Visibility = Visibility.Hidden;
                sp.Children.Add(id);
                ListBoxItem item = new ListBoxItem();
                item.Content = sp;
                item.MouseDoubleClick += item_MouseDoubleClick;
                ListBox.Items.Add(item);
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
                Label nameBox = clientlogic.FindDescendant<Label>(selectedItem);
                likeNome = likeNome.Substring(0, likeNome.Length - 1);
                if (!likeNome.Contains("\\"))
                {
                    likeNome = "";
                }
                else
                {
                    likeNome = likeNome.Substring(0, likeNome.LastIndexOf(@"\") + 1);
                }
                if (App.Current.MainWindow is Restore)
                {
                    FileSelection main = new FileSelection(clientlogic, selFolderPath, likeNome, false, ((Restore)App.Current.MainWindow).mw);
                    if (App.Current.MainWindow is Restore)
                        App.Current.MainWindow.Content = main;
                }
            }
        }

        private void item_MouseDoubleClickFolder(object sender, MouseButtonEventArgs e)
        {
            if (ListBox.SelectedIndex == -1)
                return;

            ListBoxItem selectedItem = this.ListBox.ItemContainerGenerator.ContainerFromIndex(ListBox.SelectedIndex) as ListBoxItem;

            if (selectedItem == null)
                return;

            if (ListBox.SelectedItem != null)
            {
                Label nameBox = clientlogic.FindDescendant<Label>(selectedItem);
                likeNome += nameBox.Content.ToString() + "\\";
                FileSelection main = new FileSelection(clientlogic, selFolderPath, likeNome, false, ((Restore)App.Current.MainWindow).mw);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;

            }
        }

        void item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListBox.SelectedIndex == -1)
                return;

            ListBoxItem selectedItem = this.ListBox.ItemContainerGenerator.ContainerFromIndex(ListBox.SelectedIndex) as ListBoxItem;

            if (selectedItem == null)
                return;

            if (ListBox.SelectedItem != null && !search)
            {
                TextBox id = clientlogic.FindDescendant<TextBox>(selectedItem);
                string idfile = id.Text;
                Label nameBox = clientlogic.FindDescendant<Label>(selectedItem);
                likeNome += nameBox.Content.ToString();
                DownloadFile main = new DownloadFile(clientlogic, selFolderPath, likeNome, search, ((Restore)App.Current.MainWindow).mw, idfile);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }
            else if (ListBox.SelectedItem != null && search)
            {
                TextBox id = clientlogic.FindDescendant<TextBox>(selectedItem);
                string idfile = id.Text;
                Label nameBox = clientlogic.FindDescendant<Label>(selectedItem);
                likeNome = nameBox.Content.ToString();
                DownloadFile main = new DownloadFile(clientlogic, selFolderPath, likeNome, search, ((Restore)App.Current.MainWindow).mw, idfile);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }
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

        #region Search Button
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBar.Text.Length == 0)
            {
                likeNome = "";
                FileSelection main = new FileSelection(clientlogic, selFolderPath, likeNome, false, ((Restore)App.Current.MainWindow).mw);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }
            else
            {
                likeNome = SearchBar.Text;
                FileSelection main = new FileSelection(clientlogic, selFolderPath, likeNome, true, ((Restore)App.Current.MainWindow).mw);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }

        }

        private void SearchButton_MouseEnter(object sender, MouseEventArgs e)
        {
            searchImage.BeginInit();
            searchImage.Source = new BitmapImage(new Uri(@"Images/searchon.png", UriKind.RelativeOrAbsolute));
            searchImage.EndInit();
        }

        private void SearchButton_MouseLeave(object sender, MouseEventArgs e)
        {
            searchImage.BeginInit();
            searchImage.Source = new BitmapImage(new Uri(@"Images/search.png", UriKind.RelativeOrAbsolute));
            searchImage.EndInit();
        }
        #endregion

        #region Back Button

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            SelectActionUI main = new SelectActionUI(clientlogic, selFolderPath, ((Restore)App.Current.MainWindow).mw);
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Content = main;
        }

        private void Back_MouseLeave(object sender, MouseEventArgs e)
        {

            backImage.BeginInit();
            backImage.Source = new BitmapImage(new Uri(@"Images/back.png", UriKind.RelativeOrAbsolute));
            backImage.EndInit();
        }

        private void Back_MouseEnter(object sender, MouseEventArgs e)
        {

            backImage.BeginInit();
            backImage.Source = new BitmapImage(new Uri(@"Images/backLight.png", UriKind.RelativeOrAbsolute));
            backImage.EndInit();
        }
        #endregion


        #region Home Button
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
            likeNome = "";
            FileSelection main = new FileSelection(clientlogic, selFolderPath, likeNome, false, ((Restore)App.Current.MainWindow).mw);
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Content = main;
        }
        #endregion
 
    }
}
