using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
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

        enum ItemType { RootFolder, Folder, File };
        private ClientLogic clientlogic;
        private String selFolderPath;
        private MainWindow mw;
        private object dummyNode = null;
        public string SelectedImagePath { get; set; }
        public static HeaderToImageConverter ConverterInstance = new HeaderToImageConverter();
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
                mainw.restart(true);
                return;
            }


        }
        #region Explorer Tree
        /*
         * Aggiunge i campi relativi alle root folders nel tree
         */
        private void CreateTree(String[] folders)
        {
            foreach (String s in folders)
            {
                if (s != String.Empty)
                {
                    //istanzio in ItemTag con le info contenute in subFileInfo
                    ItemTag rootItemTag = new ItemTag(s, ItemType.RootFolder);
                    rootItemTag.relativePath = s.Substring(s.LastIndexOf("\\") + 1);  //nome 
                    rootItemTag.nome = rootItemTag.relativePath;
                    TreeViewItem item = new TreeViewItem();
                    item.Header = rootItemTag.nome;
                    item.Tag = rootItemTag;
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
                    
                    //se l'oggetto espanso è una cartella si chiede al server la lista dei file nella folder (che contengono likeNome)
                    //clientLogic.WriteStringOnStream(ClientLogic.LISTFILES + clientLogic.username + "+" + folder + "+" + likeNome);
                    //vedi FileSelection.FileSelection e FileSelection.item_MouseDoubleClickFolder

                    //se invece l'oggetto espanso è un file (posso espandere un file?)
                    //domando al server le versioni del file selezionato
                    //clientLogic.WriteStringOnStream(ClientLogic.GETVFILE + clientLogic.username + "+" + folder + "+" + completePath + "+" + idFile);
                    //vedi DownloadFile.DownloadFile
                    ItemTag tag = (ItemTag)item.Tag;
                    List<String> folderContent = RetrieveFolderContent(tag.fullPath);   //lista di stringhe contenenti fileinfo
                    foreach (String s in folderContent)
                    {

                        AddSubItem(item, s);
                    }
                }
                catch (Exception) { }   //FIXME
            }
        }
        /*
         * Callback chiamata in casi di cambio selezione nel TreeView, magari ci serve
         */
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
         * Analizza la stringa di subFileInfo e aggiunge folder e/o file come subitem al parentItem
         * 
         */
        private void AddSubItem(TreeViewItem parentItem, String subFileInfo) 
        {

            String itemFullPath = subFileInfo.Substring(0, subFileInfo.IndexOf("?"));
            String parentFolderPath = ((ItemTag)parentItem.Tag).fullPath;
            String itemRelativePath = MakeRelativePath(parentFolderPath, itemFullPath);


            if (!itemRelativePath.Contains(@"\"))    //è il path di un file che sta direttamente nella folder aperta
            {
                //istanzio in ItemTag con le info contenute in subFileInfo
                ItemTag subItemTag = new ItemTag(subFileInfo, ItemType.File);
                subItemTag.relativePath = itemRelativePath;
                subItemTag.nome = itemRelativePath;

                //istanzio il TreeViewItem (oggetto visibile nel TreeView) mettendogli come tag l'oggetto ItemTag appena creato
                TreeViewItem subItem = new TreeViewItem();
                subItem.Header = subItemTag.nome;
                subItem.Tag = subItemTag;
                subItem.FontWeight = FontWeights.Normal;
                subItem.Items.Add(dummyNode);
                subItem.Expanded += new RoutedEventHandler(folder_Expanded);        //file expanded
                parentItem.Items.Add(subItem);

            }
            else if (itemRelativePath.Contains(@"\"))    //è il path di un file che sta in una sottocartella - estraggo solo il nome della cartella
            {

                String folderName = itemRelativePath.Substring(0, itemRelativePath.IndexOf("\\"));
                //cerco se esiste già una cartella adibita a contenere il file
                TreeViewItem folderItem = searchFolderInParent(parentItem, folderName);

                //se la cartella esiste già devo chiamare la addSubItem su di essa
                //se non esiste devo crearla, aggiungerla e chiamare la addSubItem
                if (folderItem == null)
                {

                    //istanzio in ItemTag con le info contenute in subFileInfo
                    String folderFullPath = parentFolderPath + "\\" + folderName;
                    ItemTag subItemTag = new ItemTag(folderFullPath, ItemType.Folder);
                    subItemTag.relativePath = itemRelativePath;
                    subItemTag.nome = folderName;

                    //istanzio il TreeViewItem (oggetto visibile nel TreeView) mettendogli come tag l'oggetto ItemTag appena creato
                    folderItem = new TreeViewItem();
                    folderItem.Header = subItemTag.nome;
                    folderItem.Tag = subItemTag;
                    folderItem.FontWeight = FontWeights.Normal;
                    //folderItem.Items.Add(dummyNode);  //qui il dummyNode non serve perchè gestisco a mano l'inserimento del primo nodo
                    folderItem.Expanded += new RoutedEventHandler(folder_Expanded);        //file expanded
                    parentItem.Items.Add(folderItem);
                }
                AddSubItem(folderItem, subFileInfo);
            }
            
        }

        /*
         * Cerca se in parentItem c'è una cartella con nome folderName
         * Se sì ne ritorna un riferimento
         * Se no ritorna null
         */
        private TreeViewItem searchFolderInParent(TreeViewItem parentItem, string folderName)
        {
            foreach(TreeViewItem item in parentItem.Items)
            {
                ItemTag tag = (ItemTag)item.Tag;
                bool chk = tag.tipo == ItemType.Folder && tag.nome == folderName;
                if (chk)
                {
                    return item;
                }
            }
            return null;
        }

        public string MakeRelativePath(string workingDirectory, string fullPath)
        {
            return fullPath.Substring(workingDirectory.Length + 1);
        }

        private class ItemTag {
            public ItemType tipo;
            public String nome;
            public String fileInfo;
            public String fullPath;
            public String relativePath;
            public String versione;
            public int dimFile;
            public String timeStamp;

            public ItemTag(string fileInfo, ItemType itemType)
            {
                this.fileInfo = fileInfo;
                tipo = itemType;
                if (tipo != ItemType.File)
                {
                    //in questo caso fileInfo contiene solo il path della rootFolder
                    this.fullPath = fileInfo;
                }
                else 
                {
                    //fileInfo ha info sul file 
                    String[] info = fileInfo.Split('?');
                    fullPath = info[0];
                    versione = info[1];
                    dimFile = int.Parse(info[2]);
                    timeStamp = info[3];
                }
            }
        }

        /*
         * Converter per ottenere l'immagine corretta in base al tipo di value
         * value è bindato nello xaml ad essere il tag del TreeViewItem (quindi un oggetto ItemTag)
         */
        public class HeaderToImageConverter : IValueConverter
        {

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                ItemTag tag = (ItemTag)value;
                // this is a diskdrive
                if (tag.tipo == ItemType.File)
                {

                    Uri uri = new Uri("pack://application:,,,/Images/file.png");
                    BitmapImage source = new BitmapImage(uri);
                    return source;
                }
                else //if (tag.tipo == ItemType.Folder)
                {
                    Uri uri = new Uri("pack://application:,,,/Images/folder.png");
                    BitmapImage source = new BitmapImage(uri);
                    return source;
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotSupportedException("Cannot convert back");
            }
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
