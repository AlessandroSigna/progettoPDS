using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Globalization;
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

namespace Client
{
    /// <summary>
    /// Logica di interazione per RestoreUC.xaml
    /// </summary>
    public partial class RestoreControl : UserControl
    {

        enum ItemType { RootFolder, Folder, File, FileVersion };
        private ClientLogic clientlogic;
        private Restore restoreWindow;
        private object dummyNode = null;
        public string SelectedImagePath { get; set; }
        public static HeaderToImageConverter ConverterInstance = new HeaderToImageConverter();
        private BackgroundWorker workertransaction;
        private WaitWindow waitWindow;

        #region Costruttore
        /*
         * Costruttore
         */
        public RestoreControl(ClientLogic clientLogic, Restore mainw)
        {
            try
            {
                InitializeComponent();
                clientlogic = clientLogic;
                restoreWindow = mainw;
                //App.Current.MainWindow.Width = 400;
                //App.Current.MainWindow.Height = 400;
                String[] rootFolders = RetrieveRootFolders();
                if (rootFolders == null)
                {
                    ExitStub();
                    return;
                }
                else if (rootFolders.Length == 0)
                {
                    noFolder.Visibility = Visibility.Visible;
                    foldersTree.Visibility = Visibility.Hidden;
                    ConfirmButton.Visibility = Visibility.Hidden;
                    AnnullaButton.Visibility = Visibility.Hidden;
                }
                else
                {
                    noFolder.Visibility = Visibility.Hidden;
                    foldersTree.Visibility = Visibility.Visible;
                    AnnullaButton.Visibility = Visibility.Visible;
                    CreateTree(rootFolders);
                }

            }
            catch
            {
                //in caso di eccezione rilascio le risorse
                ExitStub();
            }
        }

        #endregion

        #region TreeView
        /*
         * Aggiunge i campi relativi alle root folders nel tree
         */
        private void CreateTree(String[] folders)
        {
            try
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
            catch 
            {
                ExitStub();
            }
            
        }

        /*
         * Callback chiamata quando una rootFolder viene espansa
         */
        void folder_Expanded(object sender, RoutedEventArgs e)
        {
            try
            {
                TreeViewItem item = (TreeViewItem)sender;
                if (item.Items.Count == 1 && item.Items[0] == dummyNode)
                {
                    //entro qui solo quando la root folder viene espansa la prima volta
                    waitWindow = new WaitWindow("Scaricando informazioni dal server...");
                    waitWindow.Show();
                    contentGrid.IsEnabled = false;

                    item.Items.Clear();

                    //ho bisogno del worker per mantenere la UI responsive
                    workertransaction = new BackgroundWorker();

                    object paramObj1 = item.Tag;
                    object paramObj2 = item;
                    object[] parameters = new object[] { paramObj1, paramObj2 };
                    workertransaction.DoWork += new DoWorkEventHandler(Workertransaction_RootFolder);
                    workertransaction.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Workertransaction_RootFolderCompleted);
                    workertransaction.RunWorkerAsync(parameters);
                }
            }
            catch
            {
                ExitStub();
            }
            
        }

        private void Workertransaction_RootFolder(object sender, DoWorkEventArgs e)
        {
            object[] parameters = e.Argument as object[];
            ItemTag tag = (ItemTag)parameters[0];
            TreeViewItem item = (TreeViewItem)parameters[1];
            List<String> folderContent = RetrieveFolderContent(tag.fullPath);
            foreach (String s in folderContent)
            {
                Thread t = new Thread(new ThreadStart(delegate { Dispatcher.Invoke(DispatcherPriority.Normal, new Action<TreeViewItem, String>(AddSubItem), item, s); }));
                t.Start();
            }
        }
        private void Workertransaction_RootFolderCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null || e.Cancelled)
                {
                    ExitStub();
                    return;
                }

                waitWindow.Close();
                contentGrid.IsEnabled = true;
            }
            catch
            {
                ExitStub();
            }
        }
        /*
         * Callback chiamata quando un file viene espanso
         */
        void file_Expanded(object sender, RoutedEventArgs e)
        {

            try
            {
                TreeViewItem item = (TreeViewItem)sender;
                if (item.Items.Count == 1 && item.Items[0] == dummyNode)
                {
                    //entro qui solo quando l'oggetto viene espanso la prima volta
                    item.Items.Clear();

                    //se l'oggetto espanso è un file
                    //domando al server le versioni del file selezionato
                    ItemTag tag = (ItemTag)item.Tag;
                    //lista di stringhe contenenti fileinfo 
                    //NB: questa volta il primo campo della stringa è il nome del file e non il fullPath
                    List<String> fileVersions = RetrieveFileVersions(tag);
                    foreach (String s in fileVersions)
                    {
                        AddSubItem(item, s);
                    }
                }
            }
            catch
            {
                ExitStub();
            }
        }

        private void FolderSort(TreeViewItem head, TreeViewItem node, TreeViewItem parent)
        {
            // se il primo contiene il punto sarà un file
            // se il primo è un folder si procede, altrimenti andrà sicuramente prima l'oggetto attuale che è una cartella (e quindi andrà posizionato prima dei file).
            if (!head.Header.ToString().Contains("."))
            {
                // Confronta l'oggetto attuale con quello in prima posizione, se l'oggetto attuale precede alfabeticamente
                // il primo della lista, allora prenderà il suo posto, altrimenti sarà posizionato dopo.
                if (head.Header.ToString().CompareTo(node.Header.ToString()) > 0)
                {
                    parent.Items.Insert(parent.Items.CurrentPosition, node);
                }
                else
                {
                    parent.Items.MoveCurrentToNext();
                    if (!parent.Items.IsCurrentAfterLast)
                    {
                        head = (TreeViewItem)parent.Items.GetItemAt(parent.Items.CurrentPosition);
                        FolderSort(head, node, parent);
                    }
                    else
                    {
                        parent.Items.Insert(parent.Items.CurrentPosition, node);
                    }
                }
            }
            else
            {
                parent.Items.Insert(parent.Items.CurrentPosition, node);
            }
        }

        private void FileSort(TreeViewItem tail, TreeViewItem node, TreeViewItem parent)
        {
            // se contiene il punto sarà un file
            // se l'ultimo è un file si procede, altrimenti l'oggetto attuale che è un file andrà sicuramente dopo le cartelle.
            if (tail.Header.ToString().Contains("."))
            {
                // Confronta l'oggetto attuale con quello in ultima posizione, se l'oggetto attuale succede alfabeticamente
                // l'ultimo della lista, allora prenderà il suo posto, altrimenti sarà posizionato prima.
                if (tail.Header.ToString().CompareTo(node.Header.ToString()) < 0)
                {
                    parent.Items.Insert(parent.Items.CurrentPosition + 1, node);
                }
                else
                {
                    parent.Items.MoveCurrentToPrevious();
                    if (!parent.Items.IsCurrentBeforeFirst)
                    {
                        tail = (TreeViewItem)parent.Items.GetItemAt(parent.Items.CurrentPosition);
                        FileSort(tail, node, parent);
                    }
                    else
                    {
                        parent.Items.Insert(parent.Items.CurrentPosition + 1, node);
                    }
                }
            }
            else
            {
                parent.Items.Insert(parent.Items.CurrentPosition + 1, node);
            }
        }

        /*
         * Metodo principale per il popolamento del TreeView
         * Analizza la stringa di subFileInfo e aggiunge folder e/o file come subitem al parentItem
         * I vari casi sono differenziati in base al tipo di parentItem così da capire in che contesto ci si trova
         * 
         */
        private void AddSubItem(TreeViewItem parentItem, String subFileInfo)
        {
            try
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

                    if (itemRelativePath.Contains(@"\"))    //è il path di un file che sta in una sottocartella - estraggo solo il nome della cartella
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

                            parentItem.Items.MoveCurrentToFirst();

                            // Se è il primo della struttura, sarà posizionato in 0.
                            if (parentItem.Items.CurrentPosition == -1)
                            {
                                parentItem.Items.Insert(0, folderItem);
                            }
                            else
                            {
                                TreeViewItem temp = (TreeViewItem)parentItem.Items.CurrentItem;

                                FolderSort(temp, folderItem, parentItem);
                            }
                        }

                        AddSubItem(folderItem, subFileInfo);
                    }
                    else if (!itemRelativePath.Contains(@"\"))    //è il path di un file che sta direttamente nella folder aperta
                    {
                        //istanzio in ItemTag con le info contenute in subFileInfo
                        ItemTag subItemTag = new ItemTag(subFileInfo, ItemType.File);
                        subItemTag.relativePath = itemRelativePath;
                        subItemTag.nome = itemRelativePath;
                        subItemTag.rootDir = parentTag.rootDir;

                        //istanzio il TreeViewItem (oggetto visibile nel TreeView) mettendogli come tag l'oggetto ItemTag appena creato
                        TreeViewItem subItem = new TreeViewItem();
                        //itemFolderList.Add(subItem);
                        subItem.Header = subItemTag.nome;
                        subItem.Tag = subItemTag;
                        subItem.FontWeight = FontWeights.Normal;
                        subItem.Items.Add(dummyNode);
                        subItem.Expanded += new RoutedEventHandler(file_Expanded);        //callback per l'espansione del file

                        parentItem.Items.MoveCurrentToLast();

                        // Se è il primo della struttura, sarà posizionato in 0.
                        if (parentItem.Items.CurrentPosition == -1)
                        {
                            parentItem.Items.Insert(0, subItem);
                        }
                        else
                        {
                            TreeViewItem temp = (TreeViewItem)parentItem.Items.CurrentItem;

                            FileSort(temp, subItem, parentItem);
                        }

                    }
                }
            }
            catch
            {
                ExitStub();
            }

        }


        /*
         * Callback chiamata in casi di cambio selezione nel TreeView. Abilita o meno il ConfirmButton
         */
        private void foldersItem_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                TreeView tree = (TreeView)sender;
                TreeViewItem item = ((TreeViewItem)tree.SelectedItem);
                ItemTag tag = (ItemTag)item.Tag;
                if (tag.tipo == ItemType.FileVersion && tag.dimFile == 0)    //caso file cancellato
                {
                    ConfirmButton.IsEnabled = false;
                    //BrushConverter bc = new BrushConverter();
                    //ConfirmButton.Background = (Brush)bc.ConvertFrom("#F5FFFA");
                }
                else if (tag.tipo == ItemType.FileVersion || tag.tipo == ItemType.RootFolder || tag.tipo == ItemType.Folder)
                {

                    ConfirmButton.IsEnabled = true;
                    //BrushConverter bc = new BrushConverter();
                    //ConfirmButton.Background = (Brush)bc.ConvertFrom("#FF44E572");

                }
                else
                {
                    ConfirmButton.IsEnabled = false;
                    //BrushConverter bc = new BrushConverter();
                    //ConfirmButton.Background = (Brush)bc.ConvertFrom("#F5FFFA");
                }
            }
            catch
            {
                ExitStub();
            }

        }

        private void file_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    TreeViewItem src = e.Source as TreeViewItem;
                    richiediDownloadFile(src);
                }
            }
            catch
            {
                ExitStub();
            }
        }
        #endregion

        #region Richiedi Download
        private void richiediDownloadFile(TreeViewItem file)
        {
            try
            {
                ItemTag tag = file.Tag as ItemTag;
                System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
                fbd.Description = "Scegli la cartella in cui effettuare il download";
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();
                if (fbd.SelectedPath != "")
                {
                    if (clientlogic.cartellaMonitorata != null && fbd.SelectedPath.Contains(clientlogic.cartellaMonitorata))
                    {
                        System.Windows.MessageBox.Show("La risorsa è usata da un altro processo", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    clientlogic.restoreFolder = fbd.SelectedPath;      //salvo il riferimento alla folder selezionata per il restore perché serve nella StartDownload
                    StartDownload main = new StartDownload(clientlogic, tag.fullPath, tag.versione, tag.rootDir, restoreWindow, tag.id, this);
                    if (App.Current.MainWindow is Restore)
                        App.Current.MainWindow.Content = main;
                }
            }
            catch
            {
                ExitStub();
            }

        }

        private void richiediDownloadCartella(TreeViewItem folder)
        {
            try
            {
                ItemTag tag = folder.Tag as ItemTag;
                System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
                fbd.Description = "Scegli la cartella in cui effettuare il download";
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();
                if (fbd.SelectedPath != "")
                {
                    if (clientlogic.cartellaMonitorata != null && fbd.SelectedPath.Contains(clientlogic.cartellaMonitorata))
                    {
                        System.Windows.MessageBox.Show("La risorsa è usata da un altro processo", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    clientlogic.restoreFolder = fbd.SelectedPath;      //salvo il riferimento alla folder selezionata per il restore perché serve nella StartDownload
                    DownloadFolder main = new DownloadFolder(clientlogic, tag.rootDir, tag.fullPath, restoreWindow, this);
                    if (App.Current.MainWindow is Restore)
                        App.Current.MainWindow.Content = main;
                }
            }
            catch
            {
                ExitStub();
            }

        }

        #endregion

        #region Classi helper gestione Tree
        /*
         * Classe che verrà usata come TreeViewItem.Tag per ogni item dell'albero
         */
        private class ItemTag
        {
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
                try
                {
                    ItemTag tag = (ItemTag)value;
                    if (tag.tipo == ItemType.File)
                    {

                        Uri uri = new Uri("pack://application:,,,/Images/tree_file.ico");
                        BitmapImage source = new BitmapImage(uri);
                        return source;
                    }
                    else if (tag.tipo == ItemType.FileVersion)
                    {
                        String uriPath = tag.dimFile == 0 ? "pack://application:,,,/Images/tree_filered.ico" : "pack://application:,,,/Images/tree_filegreen.ico";
                        Uri uri = new Uri(uriPath);
                        BitmapImage source = new BitmapImage(uri);
                        return source;
                    }
                    else if (tag.tipo == ItemType.Folder)
                    {
                        Uri uri = new Uri("pack://application:,,,/Images/tree_folder.ico");
                        BitmapImage source = new BitmapImage(uri);
                        return source;
                    }
                    else //if (tag.tipo == ItemType.RootFolder)
                    {
                        Uri uri = new Uri("pack://application:,,,/Images/tree_disckdrive2.ico");
                        BitmapImage source = new BitmapImage(uri);
                        return source;
                    }
                }
                catch
                {
                    //in caso di eccezione in questa fase attribuisco all'item un'immagine di default
                    Uri uri = new Uri("pack://application:,,,/Images/tree_file.ico");
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

        #region Buttons
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
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
            catch
            {
                ExitStub();
            }
        }


        private void AnnullaButton_Click(object sender, RoutedEventArgs e)
        {
            restoreWindow.Close();
        }
        #endregion

        #region Metodi helper e varie

        /*
         * Cerca se in parentItem c'è una cartella con nome folderName
         * Se sì ne ritorna un riferimento
         * Se no ritorna null
         */
        private TreeViewItem searchFolderInParent(TreeViewItem parentItem, string folderName)
        {
            try
            {
                foreach (TreeViewItem item in parentItem.Items)
                {
                    ItemTag tag = (ItemTag)item.Tag;
                    bool chk = tag.tipo == ItemType.Folder && tag.nome == folderName;
                    if (chk)
                    {
                        return item;
                    }
                }
            }
            catch
            {
                ExitStub();
            }
            return null;
        }

        private void ExitStub(Boolean chiusuraInattesa = true)
        {
            if (App.Current.MainWindow is Restore)
            {
                restoreWindow.chiusuraInattesa = chiusuraInattesa;
                restoreWindow.Close();
            }
        }

        public string MakeRelativePath(string workingDirectory, string fullPath)
        {
            return fullPath.Substring(workingDirectory.Length + 1);
        }

        /*
         * convertitore da int a string per rappresentare in string la dimensione del file
         */
        static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        static string SizeSuffix(Int64 value)
        {
            try
            {
                //if (value < 0) { return "-" + SizeSuffix(-value); }
                if (value == 0) { return "\tFile Cancellato"; }

                int mag = (int)Math.Log(value, 1024);
                decimal adjustedSize = (decimal)value / (1L << (mag * 10));

                return "\t" + string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
            }
            catch
            {
                return "Dim ?";
            }
        }
        #endregion

        #region Comunicazione con il server

        /*
         * Chiede al server i path delle root folder (cartelle backuppate)
         */
        private String[] RetrieveRootFolders()
        {
            String[] folders = null;    //conterrà i root folder path
            try
            {
                clientlogic.WriteStringOnStream(ClientLogic.GETFOLDERUSER + clientlogic.username);  //chiedo al server le cartelle backuppate dall'utente
                String retFolders = clientlogic.ReadStringFromStream();
                String[] parametri = retFolders.Split('+'); //splitto la risposta in modo da ottenerne dei comandi
                String comando = parametri[1];
                if (comando.Equals("OK"))
                {
                    folders = parametri[2].Split(';'); //contiene i path delle root dir + un ultima stringa vuota (colpa della split)
                    folders = folders.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                }
                else
                {
                    //se il server non da OK si chiudono le risorse (gestite dalla window closing)
                    ExitStub();
                }
            }
            catch
            {
                ExitStub();
            }
            return folders;

        }

        private List<String> RetrieveFolderContent(String folderPath)
        {
            List<String> retFiles = new List<String>();
            try
            {
                //si chiede al server la lista dei file nella folder
                clientlogic.WriteStringOnStream(ClientLogic.LISTFILES + clientlogic.username + "+" + folderPath);

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
                        retFiles.Add(parametri[3]);     //ricevuta la stringa si aggiunge l'elemeno alla lista
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
                    //si chiudono le risorse (gestite dalla window closing)
                    ExitStub();
                }
                //le stringhe qui sono tutti i file contenuti nella rootDir, non vengono ritornate le cartelle
                //le cartelle si devono creare logicamente esaminando le varie stringhe
                retFiles.RemoveAll(str => String.IsNullOrEmpty(str));
            }
            catch
            {
                ExitStub();
            }
            return retFiles;
        }

        private List<String> RetrieveFileVersions(ItemTag fileTag)
        {
            List<String> retFiles = new List<String>();
            try
            {
                //richiesta versioni del file
                //WriteStringOnStream(ClientLogic.GETVFILE + clientLogic.username + "+" + pathDellaRootFolderDiBackup + "+" + fullPathDelFile + "+" + idFile);

                clientlogic.WriteStringOnStream(ClientLogic.GETVFILE + clientlogic.username + "+" + fileTag.rootDir + "+" + fileTag.fullPath + "+" + fileTag.id);


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
                        retFiles.Add(parametri[3]);     //ricevuta la stringa si aggiunge l'elemeno alla lista
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
                    ExitStub();
                }
                retFiles.RemoveAll(str => String.IsNullOrEmpty(str));
            }
            catch
            {
                ExitStub();
            }
            return retFiles;
        }
        #endregion

    }
}
