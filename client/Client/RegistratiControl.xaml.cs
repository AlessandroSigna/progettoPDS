﻿using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls.Dialogs;
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
    /// Logica di interazione per RegistratiControl.xaml
    /// </summary>
    public partial class RegistratiControl : UserControl
    {
        string mess;

        public RegistratiControl(string message)
        {
            InitializeComponent();
            App.Current.MainWindow.Title = "Registrazione";
            ((MainWindow)App.Current.MainWindow).IsCloseButtonEnabled = true;
            mess = message;
            if (mess != null)
            {
                messaggioErrore(mess);
            }
            mess = null;
        }

        private async void messaggioErrore(string mess)
        {
            MetroWindow mw = (MetroWindow)App.Current.MainWindow;
            await mw.ShowMessageAsync("Errore", mess);
        }

        private void Registrati_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Registrati.Background = (Brush)bc.ConvertFrom("#99FFFF");
        }

        private void Registrati_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Registrati.Background = (Brush)bc.ConvertFrom("#33CCFF");

        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            LoginRegisterControl main = new LoginRegisterControl();
            App.Current.MainWindow.Content = main;
        }

        private void Back_MouseEnter(object sender, MouseEventArgs e)
        {
            backImage.BeginInit();
            backImage.Source = new BitmapImage(new Uri(@"Images/backLight.png", UriKind.RelativeOrAbsolute));
            backImage.EndInit();
        }

        private void Back_MouseLeave(object sender, MouseEventArgs e)
        {
            backImage.BeginInit();
            backImage.Source = new BitmapImage(new Uri(@"Images/back.png", UriKind.RelativeOrAbsolute));
            backImage.EndInit();
        }

        private void Registrati_Click(object sender, RoutedEventArgs e)
        {

            MainWindow mw = (MainWindow)App.Current.MainWindow;
            mw.clientLogic.Registrati(Username.Text, Password.Password);

        }

    }
}
