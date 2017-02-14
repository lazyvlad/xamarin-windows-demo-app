using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace ManateeShoppingCart.UWP
{
    public sealed partial class MainPage
    {
        public MainPage()
        {
            this.InitializeComponent();

            SetStatusBar();

            LoadApplication(new ManateeShoppingCart.App());

            //Set light theme only on this page, so everything else using default theme(popups, keyboards, etc..)
            this.RequestedTheme = ElementTheme.Light;
        }

        private void SetStatusBar()
        {
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();

                if (statusBar != null)
                {
                    statusBar.BackgroundOpacity = 1;
                    statusBar.BackgroundColor = Windows.UI.Color.FromArgb(1, 26, 183, 141);
                    statusBar.ForegroundColor = Windows.UI.Colors.White;
                }
            }
        }
    }
}
