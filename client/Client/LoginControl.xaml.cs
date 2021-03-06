﻿using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Logica di interazione per LoginControl.xaml
    /// </summary>
    public partial class LoginControl : UserControl
    {
        private static Regex sUserNameAllowedRegEx = new Regex(@"^[a-zA-Z]{1}[a-zA-Z0-9]{3,23}[^.-]$", RegexOptions.Compiled);
        private string mess;
        public LoginControl(string message)
        {
            InitializeComponent();
            ((MainWindow)App.Current.MainWindow).IsCloseButtonEnabled = true;
            App.Current.MainWindow.Title = "Login";
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

        private void Login_MouseEnter(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Login.Background = (Brush)bc.ConvertFrom("#99FFFF");

        }

        private void Login_MouseLeave(object sender, MouseEventArgs e)
        {
            BrushConverter bc = new BrushConverter();
            Login.Background = (Brush)bc.ConvertFrom("#33CCFF");

        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            //if (string.IsNullOrEmpty(Username.Text) || !sUserNameAllowedRegEx.IsMatch(Username.Text))  
            // controllo da fare alla fine
            MainWindow mw = (MainWindow)App.Current.MainWindow;
            mw.clientLogic.Login(Username.Text, Password.Password);

        }
    }
}
