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
using System.Windows.Shapes;

namespace BackupServer
{
    /// <summary>
    /// Logica di interazione per SettingWindows.xaml
    /// </summary>
    public partial class SettingWindows : MetroWindow
    {

        private string pathDBtemp;

        public SettingWindows()
        {
            InitializeComponent();
        }

        public SettingWindows(ref string pathDB)
        {
            InitializeComponent();
            pathDBtemp = pathDB;
            TPathDB.Text = MainWindow.pathDB;
            BApplica.IsEnabled = false;
            BrushConverter bc = new BrushConverter();
            BApplica.Background = (Brush)bc.ConvertFrom("#FBFBFA");
        }

        private void SelezionafileDB(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog sfoglia = new Microsoft.Win32.OpenFileDialog();
            sfoglia.FileName = "DataBase";
            sfoglia.DefaultExt = ".sqlite";
            sfoglia.Filter = "File DataBase (.sqlite)|*.sqlite";
            sfoglia.CheckFileExists = false;
            Nullable<bool> result = sfoglia.ShowDialog();

            if (result == true)
            {
                string filename = sfoglia.FileName;
                TPathDB.Text = filename;
            }

        }

        private void exitNosaveclick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void applicaClick(object sender, RoutedEventArgs e)
        {
            pathDBtemp = TPathDB.Text;
            MainWindow.pathDB = pathDBtemp;
            BApplica.IsEnabled = false;
            BrushConverter bc = new BrushConverter();
            BApplica.Background = (Brush)bc.ConvertFrom("#FBFBFA");
            
        }

        private void abilitaApplica(object sender, TextChangedEventArgs e)
        {
            BApplica.IsEnabled = true;
            BApplica.Background = Brushes.LightGray;
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            MainWindow.pathDB = TPathDB.Text;
            this.Close();
        }

        private void BSfoglia_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            BSfoglia.Background = (Brush)bc.ConvertFrom("#E6E6E6");
        }

        private void BSfoglia_MouseLeave(object sender, MouseEventArgs e)
        {
            BSfoglia.Background = Brushes.LightGray;
        }

        private void BCancel_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            BCancel.Background = (Brush)bc.ConvertFrom("#E6E6E6");
        }

        private void BCancel_MouseLeave(object sender, MouseEventArgs e)
        {
            BCancel.Background = Brushes.LightGray;
        }

        private void BOk_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            BOk.Background = (Brush)bc.ConvertFrom("#E6E6E6");
        }

        private void BOk_MouseLeave(object sender, MouseEventArgs e)
        {
            BOk.Background = Brushes.LightGray;
        }

        private void BApplica_MouseEnter(object sender, MouseEventArgs e)
        {
            if (BApplica.IsEnabled)
            {
                BrushConverter bc = new BrushConverter();
                BApplica.Background = (Brush)bc.ConvertFrom("#E6E6E6");
            }
        }

        private void BApplica_MouseLeave(object sender, MouseEventArgs e)
        {
            if(BApplica.IsEnabled)
                BApplica.Background = Brushes.LightGray;
        }
    }
}
