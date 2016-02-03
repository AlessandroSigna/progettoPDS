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
    public partial class RestoreUC : UserControl
    {
        private ClientLogic clientlogic;
        private String selFolderPath;
        private MainWindow mw;

        /*
         * Costruttore
         */
        public RestoreUC(ClientLogic clientLogic, MainWindow mainw)
        {
            try
            {
                InitializeComponent();
                clientlogic = clientLogic;
                mw = mainw;
                App.Current.MainWindow.Width = 400;
                App.Current.MainWindow.Height = 400;
                clientLogic.WriteStringOnStream(ClientLogic.GETFOLDERUSER + clientLogic.username);  //chiedo al server le cartelle backuppate dall'utente
                String retFolders = clientLogic.ReadStringFromStream();
                String[] parametri = retFolders.Split('+'); //splitto la risposta in modo da ottenerne dei comandi
                String comando = parametri[1];
                if (comando.Equals("OK"))
                {
                    String[] folders = parametri[2].Split(';');
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
                    }
                }
                else
                {
                    //se il server non da OK si chiudono le risorse e si torna a MainControl
                    if (App.Current.MainWindow is Restore)
                        App.Current.MainWindow.Close();
                    if (clientLogic.clientsocket.Client.Connected)
                    {
                        clientLogic.clientsocket.GetStream().Close();
                        clientLogic.clientsocket.Close();
                    }
                    App.Current.MainWindow = mainw;
                    MainControl main = new MainControl(1);
                    App.Current.MainWindow.Content = main;
                    return;
                }
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
                App.Current.MainWindow = mainw;
                MainControl main = new MainControl(1);
                App.Current.MainWindow.Content = main;
                return;
            }


        }

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
        
    }
}
