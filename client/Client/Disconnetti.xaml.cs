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
    /// Logica di interazione per Disconnetti.xaml
    /// </summary>
    public partial class Disconnetti : UserControl
    {
        public Disconnetti()
        {
            InitializeComponent();
        }

        private void BServer_MouseEnter(object sender, MouseEventArgs e)
        {
            BServer.Background = Brushes.LightGray;
        }

        private void BAnnulla_MouseEnter(object sender, MouseEventArgs e)
        {
            BCancel.Background = Brushes.LightGray;
        }



        private void BServer_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            BServer.Background = (Brush)bc.ConvertFrom("#009bEF");
        }

        private void BAnnulla_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            BCancel.Background = (Brush)bc.ConvertFrom("#009bEF");
        }





    }
}
