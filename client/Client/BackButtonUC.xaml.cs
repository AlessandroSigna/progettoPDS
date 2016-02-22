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
    /// Interaction logic for BackButtonUC.xaml
    /// </summary>
    public partial class BackButtonUC : UserControl
    {
        public BackButtonUC()
        {
            InitializeComponent();
        }

        /*
         * il click viene gestito nelle singole finestre perchè sono loro a sapere quale è la finestra precedente
         */
        //private void Back_Click(object sender, RoutedEventArgs e)
        //{
        //    LoginRegisterControl main = new LoginRegisterControl();
        //    App.Current.MainWindow.Content = main;
        //}

        private void Back_MouseEnter(object sender, MouseEventArgs e)
        {
            sfondoImageBack.Visibility = Visibility.Visible;
        }

        private void Back_MouseLeave(object sender, MouseEventArgs e)
        {
            sfondoImageBack.Visibility = Visibility.Hidden;
        }
    }
}
