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
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Client
{
    /// <summary>
    /// Interaction logic for WaitWindow.xaml
    /// </summary>
    public partial class WaitWindow : Window
    {
        private Window finestraChiamante;
        private Brush currentBrush;
        public WaitWindow(String messaggio)
        {
            InitializeComponent();

            finestraChiamante = Application.Current.MainWindow;
            LabelMessaggio.Content = messaggio;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = finestraChiamante;

            var blur = new BlurEffect();
            blur.Radius = 5;
            currentBrush = finestraChiamante.Background;
            finestraChiamante.Background = new SolidColorBrush(Colors.DarkGray);
            finestraChiamante.Effect = blur;
        }

        internal void Dismiss()
        {
            finestraChiamante.Effect = null;
            finestraChiamante.Background = currentBrush;
            this.Close();
        }
    }
}
