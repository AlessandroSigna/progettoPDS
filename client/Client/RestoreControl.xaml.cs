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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Logica di interazione per RestoreUC.xaml
    /// </summary>
    public partial class RestoreControl : UserControl
    {
        private ClientLogic clientlogic;
        private String selFolderPath;
        private MainWindow mw;
        private object dummyNode = null;
        public string SelectedImagePath { get; set; }

        /*
         * Costruttore
         */
        public RestoreControl(ClientLogic clientLogic, MainWindow mainw)
        {
            try
            {
                InitializeComponent();
                clientlogic = clientLogic;
                mw = mainw;
                App.Current.MainWindow.Width = 400;
                App.Current.MainWindow.Height = 400;

                String[] rootFolders = RetrieveRootFolders();
                CreateTree(rootFolders);

            }
            catch
            {
                //in caso di eccezione rilascio le risorse
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Close();
                if (clientLogic.clientsocket.Client.Connected)
                {
                    clientLogic.clientsocket.GetStream().Close();
                    clientLogic.clientsocket.Close();
                }
                //App.Current.MainWindow = mainw;
                //MainControl main = new MainControl();
                //App.Current.MainWindow.Content = main;
                //main.messaggioErrore();
                mainw.restart(true);
                return;
            }


        }
        #region Explorer Tree
        private void CreateTree(String[] folders)
        {
            foreach (String s in folders)
            {
                if (s != String.Empty)
                {
                    TreeViewItem item = new TreeViewItem();
                    item.Header = s.Substring(s.LastIndexOf("\\") + 1);  //nome
                    item.Tag = s;       //fullpath
                    item.FontWeight = FontWeights.Normal;
                    item.Items.Add(dummyNode);
                    item.Expanded += new RoutedEventHandler(folder_Expanded);   //callback per l'espansione
                    foldersItem.Items.Add(item);
                }
            }
        }

        /*
         * Callback chiamata quando l'oggetto viene espanso
         */
        void folder_Expanded(object sender, RoutedEventArgs e)
        {

            //la stessa chiamata verrà fatta sulle eventuali sottocartelle
            TreeViewItem item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] == dummyNode)
            {
                //entro qui solo quando l'oggetto viene espanso la prima volta
                item.Items.Clear();

                try
                {
                    
                    //devo controllare se l'oggetto espanso è un file o una cartella (dal fullpath - item.Tag?)
                    if (true)
                    {
                        //se l'oggetto espanso è una cartella si chiede al server la lista dei file nella folder (che contengono likeNome)
                        //clientLogic.WriteStringOnStream(ClientLogic.LISTFILES + clientLogic.username + "+" + folder + "+" + likeNome);
                        //vedi FileSelection.FileSelection e FileSelection.item_MouseDoubleClickFolder
                        List<String> folderContent = RetrieveFolderContent((String)item.Tag);
                        foreach (String s in folderContent)
                        {
                            TreeViewItem subitem = new TreeViewItem();
                            subitem.Header = s.Substring(s.LastIndexOf("\\") + 1);  //nome
                            subitem.Tag = s;    //fullpath
                            subitem.FontWeight = FontWeights.Normal;
                            subitem.Items.Add(dummyNode);
                            subitem.Expanded += new RoutedEventHandler(folder_Expanded);
                            item.Items.Add(subitem);
                        }
                    }
                    else
                    {
                    //se invece l'oggetto espanso è un file (posso espandere un file?)
                    //domando al server le versioni del file selezionato
                    //clientLogic.WriteStringOnStream(ClientLogic.GETVFILE + clientLogic.username + "+" + folder + "+" + completePath + "+" + idFile);
                    //vedi DownloadFile.DownloadFile
                    }

                }
                catch (Exception) { }
            }
        }
        private void foldersItem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            return;
            TreeView tree = (TreeView)sender;
            TreeViewItem temp = ((TreeViewItem)tree.SelectedItem);

            if (temp == null)
                return;
            SelectedImagePath = "";
            string temp1 = "";
            string temp2 = "";
            while (true)
            {
                temp1 = temp.Header.ToString();
                if (temp1.Contains(@"\"))
                {
                    temp2 = "";
                }
                SelectedImagePath = temp1 + temp2 + SelectedImagePath;
                if (temp.Parent.GetType().Equals(typeof(TreeView)))
                {
                    break;
                }
                temp = ((TreeViewItem)temp.Parent);
                temp2 = @"\";
            }
            //show user selected path
            // MessageBox.Show(SelectedImagePath);
        }

        /*
         * Analizza una stringa alla volta e aggiunge folder o file come subitem all'item
         * 
         */
        private void AddSubItem(TreeViewItem item, String fileInfo, String folderPath) 
        {
            //info comuni sia a file che a cartelle (le posso mettere in una classe incapsulata dentro item.Tag)
            String[] info = fileInfo.Split('?');
            String filename = info[0];
            String versione = info[1];
            int dimFile = int.Parse(info[2]);
            String timestamp = info[3];
            String relativePath = MakeRelativePath(folderPath, filename);
            

            
            if (!relativePath.Contains(@"\"))    //è il path di un file che sta direttamente nella folder aperta
            {
                //String idfile = info[4];
                //StackPanel sp = new StackPanel();
                //sp.Orientation = Orientation.Horizontal;
                //Image folderImg = new Image();
                //BitmapImage fld = new BitmapImage();
                //fld.BeginInit();
                //fld.UriSource = new Uri(@"Images/file.png", UriKind.RelativeOrAbsolute);
                //fld.EndInit();
                //folderImg.Source = fld;
                //folderImg.Width = 40;
                //folderImg.Height = 40;
                //folderImg.Margin = new Thickness(10, 10, 0, 10);
                //sp.Children.Add(folderImg);
                //Label folderName = new Label();
                //folderName.Content = relativePath;
                //folderName.Background = Brushes.Transparent;
                //folderName.BorderBrush = Brushes.Transparent;
                //folderName.Margin = new Thickness(30, 15, 0, 15);
                //folderName.FontSize = 14;
                //folderName.FontFamily = new FontFamily("Tahoma");
                //sp.Children.Add(folderName);
                //TextBox id = new TextBox();
                //id.Text = idfile;
                //id.Visibility = Visibility.Hidden;
                //sp.Children.Add(id);
                //ListBoxItem item = new ListBoxItem();
                //item.Content = sp;
                //item.MouseDoubleClick += item_MouseDoubleClick; //aggiungo callback per doppio click sulla entry file
                //ListBox.Items.Add(item);
            }
            else if (relativePath.Contains(@"\"))    //è il path di un file che sta in una sottocartella - estraggo solo il nome della cartella
            {
                //string[] subfolder = relativePath.Split('\\');
                //if (listElement.Contains(subfolder[0]))
                //    return;
                //StackPanel sp = new StackPanel();
                //sp.Orientation = Orientation.Horizontal;
                //Image folderImg = new Image();
                //BitmapImage fld = new BitmapImage();
                //fld.BeginInit();
                //fld.UriSource = new Uri(@"Images/folder.png", UriKind.RelativeOrAbsolute);
                //fld.EndInit();
                //folderImg.Source = fld;
                //folderImg.Width = 40;
                //folderImg.Height = 40;
                //folderImg.Margin = new Thickness(10, 10, 0, 10);
                //sp.Children.Add(folderImg);
                //Label folderName = new Label();
                //folderName.Content = subfolder[0];
                //folderName.Background = Brushes.Transparent;
                //folderName.BorderBrush = Brushes.Transparent;
                //folderName.Margin = new Thickness(30, 15, 0, 15);
                //folderName.FontSize = 14;
                //folderName.FontFamily = new FontFamily("Tahoma");
                //sp.Children.Add(folderName);
                //ListBoxItem item = new ListBoxItem();
                //item.Content = sp;
                //item.MouseDoubleClick += item_MouseDoubleClickFolder;   //callback click entry cartella
                //ListBox.Items.Insert(0, item);
                //listElement.Add(subfolder[0]);
            }
            
        }

        public string MakeRelativePath(string workingDirectory, string fullPath)
        {
            return fullPath.Substring(workingDirectory.Length + 1);
        }
        #endregion
        #region ListBox
        /*
         * Aggiunge un ListBoxItem relativo alla cartella folderText alla ListBox di RestoreUC.xaml
         */
        void addElementToListbox(string folderText)
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
            folderName.Content = folderText;
            folderName.Background = Brushes.Transparent;
            folderName.BorderBrush = Brushes.Transparent;
            folderName.Margin = new Thickness(30, 15, 0, 15);
            folderName.FontSize = 14;
            folderName.FontFamily = new FontFamily("Tahoma");
            sp.Children.Add(folderName);
            ListBoxItem item = new ListBoxItem();
            item.Content = sp;
            ListBox.Items.Add(item);
        }

        /*
         * Callback della ListBox assegnata a SelectionChanged - invocata quando si seleziona un oggetto della ListBox
         */
        private void onSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (ListBox.SelectedIndex == -1)
                return;

            ListBoxItem selectedItem = this.ListBox.ItemContainerGenerator.ContainerFromIndex(ListBox.SelectedIndex) as ListBoxItem;

            if (selectedItem == null)
                return;

            FolderSelected.IsEnabled = true;
            BrushConverter bc = new BrushConverter();
            FolderSelected.Background = (Brush)bc.ConvertFrom("#FF44E572");
            Label nameBox = clientlogic.FindDescendant<Label>(selectedItem);    //ricavo la Laber con il path della cartella selezionata dall ListBox
            selFolderPath = nameBox.Content.ToString(); //salvo il path

        }
        #endregion

        #region Button
        private void FolderSelected_Click(object sender, RoutedEventArgs e)
        {
            SelectActionUI main = new SelectActionUI(clientlogic, selFolderPath, mw);
            if (App.Current.MainWindow is Restore)
                App.Current.MainWindow.Content = main;

        }

        private void FolderSelected_MouseEnter(object sender, MouseEventArgs e)
        {
            if (FolderSelected.IsEnabled)
            {
                BrushConverter bc = new BrushConverter();
                FolderSelected.Background = (Brush)bc.ConvertFrom("#F5FFFA");
            }
        }

        private void FolderSelected_MouseLeave(object sender, MouseEventArgs e)
        {
            if (FolderSelected.IsEnabled)
            {
                BrushConverter bc = new BrushConverter();
                FolderSelected.Background = (Brush)bc.ConvertFrom("#FF44E572");
            }
        }


        #endregion

        private String[] RetrieveRootFolders() 
        {
            clientlogic.WriteStringOnStream(ClientLogic.GETFOLDERUSER + clientlogic.username);  //chiedo al server le cartelle backuppate dall'utente
            String retFolders = clientlogic.ReadStringFromStream();
            String[] parametri = retFolders.Split('+'); //splitto la risposta in modo da ottenerne dei comandi
            String comando = parametri[1];
            String[] folders = null;
            if (comando.Equals("OK"))
            {
                folders = parametri[2].Split(';'); //contiene i path delle root dir + un ultima stringa vuota (colpa della split)
                folders = folders.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                //vecchia implementazione
                //
                int numParametri = folders.Length;
                if (numParametri > 1)
                {
                    noFolder.Visibility = Visibility.Hidden;
                    for (int i = 0; i < numParametri; i++)
                    {
                        if (folders[i] != string.Empty)
                        {
                            addElementToListbox(folders[i]);    //popola la ListBox con i nomi delle cartelle ritornati dal server
                        }
                    }

                }
                else
                {
                    //se non ci sono cartelle visualizza la stringa di default
                    noFolder.Visibility = Visibility.Visible;
                    folders[0] = "Empty";
                }
            }
            else
            {
                //se il server non da OK si chiudono le risorse e si torna a MainControl
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Close();
                if (clientlogic.clientsocket.Client.Connected)
                {
                    clientlogic.clientsocket.GetStream().Close();
                    clientlogic.clientsocket.Close();
                }
                //App.Current.MainWindow = mainw;
                //MainControl main = new MainControl();
                //App.Current.MainWindow.Content = main;
                //main.messaggioErrore();
                mw.restart(true);
            }
            return folders;
        }

        private List<String> RetrieveFolderContent(String folderPath)
        {
            //si chiede al server la lista dei file nella folder (che contengono likeNome)
            clientlogic.WriteStringOnStream(ClientLogic.LISTFILES + clientlogic.username + "+" + folderPath + "+" + "");


            List<String> retFiles = new List<String>();
            Boolean exit = false;
            Boolean fine = false;
            //si parsifica opportunamente la stringa che ha inviato il server come risposta
            while (!exit)
            {
                String messaggio = clientlogic.ReadStringFromStream();
                String[] parametri = messaggio.Split('+');
                String comando = parametri[1];
                if (comando.Equals("FLP"))
                {
                    retFiles.Add(parametri[3]);     //ricevuta la stringa si aggiunge l'elemeno all'array
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
            //le stringhe qui sono tutti i file contenuti nella rootDir, non vengono ritornate le cartelle
            //le cartelle si devono creare logicamente esaminando le varie stringhe
            retFiles.RemoveAll(str => String.IsNullOrEmpty(str));
            return retFiles;
        }
    }
}
