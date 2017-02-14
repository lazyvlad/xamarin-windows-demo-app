using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace ManateeShoppingCart.UWP
{
    public class UWPMethods : NativeMethods
    {
        public async Task ShowDialog(ListsModel _item, string _description)
        {
            UWPContentDialog contentDialog = new UWPContentDialog(_description);
            await contentDialog.ShowAsync();

            if (UWPContentDialog.CloseResult.OK == contentDialog.dialogResult)
                _item.Name = contentDialog.newName;
        }

        public async Task ShowDialog(ItemModel _item, string _description)
        {
            UWPContentDialog contentDialog = new UWPContentDialog(_description);
            await contentDialog.ShowAsync();

            if (UWPContentDialog.CloseResult.OK == contentDialog.dialogResult)
                _item.Name = contentDialog.newName;
        }

        public void SetStatusBar(string _backgroundHexColor)
        {
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();

                if (statusBar != null)
                {
                    statusBar.BackgroundOpacity = 1;
                    statusBar.BackgroundColor = HexToColor(_backgroundHexColor);
                    statusBar.ForegroundColor = Windows.UI.Colors.White;
                }
            }
        }

        private static Windows.UI.Color HexToColor(string hexColor)
        {
            //Remove # if present
            if (hexColor.IndexOf('#') != -1)
                hexColor = hexColor.Replace("#", "");
            byte alpha = 0;
            byte red = 0;
            byte green = 0;
            byte blue = 0;

            if (hexColor.Length == 8)
            {
                //#AARRGGBB
                alpha = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
                red = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
                green = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
                blue = byte.Parse(hexColor.Substring(6, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            }
            else
                if (hexColor.Length == 6)
            {
                alpha = byte.Parse("AA", System.Globalization.NumberStyles.AllowHexSpecifier);
                red = byte.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
                green = byte.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
                blue = byte.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            }

            return Windows.UI.Color.FromArgb(alpha, red, green, blue);
        }
    }
}
