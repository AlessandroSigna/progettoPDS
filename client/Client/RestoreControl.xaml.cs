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

        enum ItemType { RootFolder, Folder, File, FileVersion };
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
                if (rootFolders.Length == 0)
                {
                    noFolder.Visibility = Visibility.Visible;
                    foldersTree.Visibility = Visibility.Hidden;
                    ConfirmButton.Visibility = Visibility.Hidden;
                }
                else
                {
                    noFolder.Visibility = Visibility.Hidden;
                    foldersTree.Visibility = Visibility.Visible;
                    CreateTree(rootFolders);
                }

            }
            catch
            {
                //in caso di eccezione rilascio le risorse
                if (App.Current.MainWindow is Restore)
                {
                    App.Current.MainWindow.DialogResult = false;
                    App.Current.MainWindow.Close();

                }
                //if (clientLogic.clientsocket.Client.Connected)
                //{
                //    clientLogic.clientsocket.GetStream().Close();
                //    clientLogic.clientsocket.Close();
                //}
                clientLogic.DisconnectAndClose();
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
                    //istanzio in ItemTag con le info contenute in s
                    ItemTag rootItemTag = new ItemTag(s, ItemType.RootFolder);
                    rootItemTag.relativePath = s.Substring(s.LastIndexOf("\\") + 1);  //nome 
                    rootItemTag.nome = rootItemTag.relativePath;
                    rootItemTag.rootDir = s;
                    TreeViewItem item = new TreeViewItem();
                    item.Header = rootItemTag.nome;
                    item.Tag = rootItemTag;
                    item.FontWeight = FontWeights.Normal;
                    item.Items.Add(dummyNode);
                    item.Expanded += new RoutedEventHandler(folder_Expanded);   //callback per l'espansione
                    foldersTree.Items.Add(item);
                }
            }
        }

        /*
         * Callback chiamata quando una rootFolder viene espansa
         */
        void folder_Expanded(object sender, RoutedEventArgs e)
        {
            //il DialogResult viene esaminato da MenuControl alla chiusura di questa finestra - verrà messo a false in caso di eccezione
            App.Current.MainWindow.DialogResult = false;
            //la stessa chiamata verrà fatta sulle eventuali sottocartelle
            //ma non avranno dummynode quindi forse meglio non legare proprio la callback alle sottocartelle
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
         * Callback chiamata quando un file viene espanso
         */
        void file_Expanded(object sender, RoutedEventArgs e)
        {

            
            TreeViewItem item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] == dummyNode)
            {
                //entro qui solo quando l'oggetto viene espanso la prima volta
                item.Items.Clear();

                try
                {
                    //se invece l'oggetto espanso è un file (posso espandere un file?)
                    //domando al server le versioni del file selezionato
                    //clientLogic.WriteStringOnStream(ClientLogic.GETVFILE + clientLogic.username + "+" + folder + "+" + completePath + "+" + idFile);
                    //vedi DownloadFile.DownloadFile
                    ItemTag tag = (ItemTag)item.Tag;
                    //lista di stringhe contenenti fileinfo 
                    //NB: questa volta il primo campo della stringa è il nome del file e non il fullPath
                    List<String> fileVersions = RetrieveFileVersions(tag);
                    foreach (String s in fileVersions)
                    {

                        AddSubItem(item, s);
                    }
                }
                catch (Exception) { }   //FIXME
            }
        }


        /*
         * Callback chiamata in casi di cambio selezione nel TreeView. Abilita o meno il ConfirmButton
         */
        private void foldersItem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            
            TreeView tree = (TreeView)sender;
            TreeViewItem item = ((TreeViewItem)tree.SelectedItem);
            ItemTag tag = (ItemTag)item.Tag;
            if (tag.tipo == ItemType.FileVersion && tag.dimFile == 0)    //caso file cancellato
            {
                ConfirmButton.IsEnabled = false;
                BrushConverter bc = new BrushConverter();
                ConfirmButton.Background = (Brush)bc.ConvertFrom("#F5FFFA");
            }
            else if (tag.tipo == ItemType.FileVersion || tag.tipo == ItemType.RootFolder || tag.tipo == ItemType.Folder)
            {

                ConfirmButton.IsEnabled = true; 
                BrushConverter bc = new BrushConverter();
                ConfirmButton.Background = (Brush)bc.ConvertFrom("#FF44E572");
                
            }
            else 
            {
                ConfirmButton.IsEnabled = false;
                BrushConverter bc = new BrushConverter();
                ConfirmButton.Background = (Brush)bc.ConvertFrom("#F5FFFA");
            }
            return;

        }

        private void file_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                TreeViewItem src = e.Source as TreeViewItem;
                richiediDownloadFile(src);
                
            }
        }

        private void richiediDownloadFile(TreeViewItem file)
        {
            ItemTag tag = file.Tag as ItemTag;
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = fbd.ShowDialog();
            if (fbd.SelectedPath != "")
            {
                clientlogic.folderR = fbd.SelectedPath;      //salvo il riferimento alla folder selezionata per il restore perché serve nella StartDownload
                StartDownload main = new StartDownload(clientlogic, tag.fullPath, tag.versione, tag.rootDir, mw, tag.id, this);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }
        }

        private void richiediDownloadCartella(TreeViewItem folder)
        {
            ItemTag tag = folder.Tag as ItemTag;
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = fbd.ShowDialog();
            if (fbd.SelectedPath != "")
            {
                clientlogic.folderR = fbd.SelectedPath;      //salvo il riferimento alla folder selezionata per il restore perché serve nella DownloadFolder
                DownloadFolder main = new DownloadFolder(clientlogic, tag.rootDir, tag.fullPath, mw, this);
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Content = main;
            }
        }
        /*
         * Analizza la stringa di subFileInfo e aggiunge folder e/o file come subitem al parentItem
         * 
         */
        private void AddSubItem(TreeViewItem parentItem, String subFileInfo) 
        {
            ItemTag parentTag = (ItemTag)parentItem.Tag;
            if (parentTag.tipo == ItemType.File)
            {
                //istanzio un ItemTag con le info contenute in subFileInfo
                ItemTag fileTag = new ItemTag(subFileInfo, ItemType.FileVersion);   //in questo caso il primo campo di subFileInfo contiene solo in nome non il fullPath
                fileTag.relativePath = parentTag.relativePath;
                fileTag.nome = parentTag.nome;
                fileTag.rootDir = parentTag.rootDir;
                fileTag.fullPath = parentTag.fullPath;

                //istanzio il TreeViewItem (oggetto visibile nel TreeView) mettendogli come tag l'oggetto ItemTag appena creato
                TreeViewItem fileItem = new TreeViewItem();
                fileItem.Header = fileTag.timeStamp + SizeSuffix(fileTag.dimFile);     //FIXME: decidere cosa mostrare sulla entry con la versione del file
                fileItem.Tag = fileTag;
                fileItem.FontWeight = FontWeights.Normal;
                if (fileTag.dimFile != 0)   //se il FileVersion non è relativo ad una cancellazione
                {
                    fileItem.MouseDoubleClick += new MouseButtonEventHandler(file_DoubleClick); //callback per il doppio click - avvia restore file
                }
                parentItem.Items.Add(fileItem);
            }
            else
            {
                String itemFullPath = subFileInfo.Substring(0, subFileInfo.IndexOf("?"));
                String parentFolderPath = parentTag.fullPath;
                String itemRelativePath = MakeRelativePath(parentFolderPath, itemFullPath);

                if (!itemRelativePath.Contains(@"\"))    //è il path di un file che sta direttamente nella folder aperta
                {
                    //istanzio in ItemTag con le info contenute in subFileInfo
                    ItemTag subItemTag = new ItemTag(subFileInfo, ItemType.File);
                    subItemTag.relativePath = itemRelativePath;
                    subItemTag.nome = itemRelativePath;
                    subItemTag.rootDir = parentTag.rootDir;

                    //istanzio il TreeViewItem (oggetto visibile nel TreeView) mettendogli come tag l'oggetto ItemTag appena creato
                    TreeViewItem subItem = new TreeViewItem();
                    subItem.Header = subItemTag.nome;
                    subItem.Tag = subItemTag;
                    subItem.FontWeight = FontWeights.Normal;
                    subItem.Items.Add(dummyNode);
                    subItem.Expanded += new RoutedEventHandler(file_Expanded);        //callback per l'espansione del file
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
                        subItemTag.rootDir = parentTag.rootDir;

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
            public String rootDir;
            public String fullPath;
            public String relativePath;
            public String versione;
            public int dimFile;
            public String id;
            public String timeStamp;

            public ItemTag(string fileInfo, ItemType itemType)
            {
                this.fileInfo = fileInfo;
                tipo = itemType;
                if (tipo == ItemType.RootFolder || tipo == ItemType.Folder)
                {
                    //in questo caso fileInfo contiene solo il path della cartella
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
                    id = info[4];
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
                else if (tag.tipo == ItemType.FileVersion)
                {
                    String uriPath = tag.dimFile == 0 ? "pack://application:,,,/Images/filedel.png" : "pack://application:,,,/Images/fileadd.png";
                    Uri uri = new Uri(uriPath);
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




        #region Button
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            
            TreeViewItem item = (TreeViewItem)foldersTree.SelectedItem;
            ItemTag tag = (ItemTag)item.Tag;
            if (tag.tipo == ItemType.FileVersion)
            {
                richiediDownloadFile((TreeViewItem)foldersTree.SelectedItem);
            }
            else if (tag.tipo == ItemType.RootFolder || tag.tipo == ItemType.Folder)
            {
                richiediDownloadCartella((TreeViewItem)foldersTree.SelectedItem);
            }
        }

        private void ConfirmButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (ConfirmButton.IsEnabled)
            {
                BrushConverter bc = new BrushConverter();
                ConfirmButton.Background = (Brush)bc.ConvertFrom("#F5FFFA");
            }
        }

        private void ConfirmButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ConfirmButton.IsEnabled)
            {
                BrushConverter bc = new BrushConverter();
                ConfirmButton.Background = (Brush)bc.ConvertFrom("#FF44E572");
            }
        }


        #endregion
        /*
         * Chiede al server i path delle root folder (cartelle backuppate)
         */
        private String[] RetrieveRootFolders() 
        {
            clientlogic.WriteStringOnStream(ClientLogic.GETFOLDERUSER + clientlogic.username);  //chiedo al server le cartelle backuppate dall'utente
            String retFolders = clientlogic.ReadStringFromStream();
            String[] parametri = retFolders.Split('+'); //splitto la risposta in modo da ottenerne dei comandi
            String comando = parametri[1];
            String[] folders = null;    //conterrà i root folder path
            if (comando.Equals("OK"))
            {
                folders = parametri[2].Split(';'); //contiene i path delle root dir + un ultima stringa vuota (colpa della split)
                folders = folders.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                
            }
            else
            {
                //se il server non da OK si chiudono le risorse e si torna a MainControl
                if (App.Current.MainWindow is Restore)
                    App.Current.MainWindow.Close();
                //if (clientlogic.clientsocket.Client.Connected)
                //{
                //    clientlogic.clientsocket.GetStream().Close();
                //    clientlogic.clientsocket.Close();
                //}
                mw.clientLogic.DisconnectAndClose();
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
                    //FIXME: Situazione inattesa - eccezione?!
                    exit = true;
                    fine = true;
                }
            }
            if (fine)
            {
                //occorre un messaggio di errore prima di chiudere
                App.Current.MainWindow.Close();
            }
            //le stringhe qui sono tutti i file contenuti nella rootDir, non vengono ritornate le cartelle
            //le cartelle si devono creare logicamente esaminando le varie stringhe
            retFiles.RemoveAll(str => String.IsNullOrEmpty(str));
            return retFiles;
        }

        private List<String> RetrieveFileVersions(ItemTag fileTag)
        {
            //richiesta versioni del file
            //WriteStringOnStream(ClientLogic.GETVFILE + clientLogic.username + "+" + pathDellaRootFolderDiBackup + "+" + fullPathDelFile + "+" + idFile);

            clientlogic.WriteStringOnStream(ClientLogic.GETVFILE + clientlogic.username + "+" + fileTag.rootDir + "+" + fileTag.fullPath + "+" + fileTag.id);


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

        /*
         * convertitore da int a string per rappresentare in string la dimensione del file
         */
        static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(Int64 value)
        {
            //if (value < 0) { return "-" + SizeSuffix(-value); }
            if (value == 0) { return "\tFile Cancellato"; }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return "\t" + string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }
    }
}
