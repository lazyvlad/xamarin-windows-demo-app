using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace ManateeShoppingCart.UWP
{
    public sealed partial class UWPContentDialog : ContentDialog
    {
        public string newName;
        public CloseResult dialogResult;

        public UWPContentDialog(string _description)
        {
            this.InitializeComponent();

            txtDescription.Text = _description;
            newName = "";

            dialogResult = CloseResult.CANCEL;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            newName = txtEntry.Text.Trim();

            dialogResult = CloseResult.OK;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }

        private void txtEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (((TextBox)sender).Text.Trim().Length > 0)
                IsPrimaryButtonEnabled = true;
            else
                IsPrimaryButtonEnabled = false;
        }

        private void txtEntry_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && txtEntry.Text.Trim().Length > 0)
            {
                ContentDialog_PrimaryButtonClick(this, null);
                this.Hide();
            }
        }

        public enum CloseResult
        {
            OK,
            CANCEL
        }
    }
}
