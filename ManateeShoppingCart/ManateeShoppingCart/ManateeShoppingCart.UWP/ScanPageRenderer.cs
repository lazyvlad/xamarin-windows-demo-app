using ManateeShoppingCart;
using ManateeShoppingCart.UWP;
using MWBCamera;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Foundation;
using Xamarin.Forms.Platform.UWP;


[assembly: ExportRenderer(typeof(ScanPage), typeof(ScanPageRenderer))]
namespace ManateeShoppingCart.UWP
{
    public class ScanPageRenderer : PageRenderer
    {
        ScannerPageNative page;

        int editListIndex;
        int editItemIndex = -1;

        protected override void OnElementChanged(ElementChangedEventArgs<Xamarin.Forms.Page> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement != null || Element == null)
                return;

            ObservableCollection<ItemModel> listItems = ((ScanPage)Element).AllItems;
            try { editListIndex = int.Parse(((ScanPage)Element).editListIndex.ToString()); } catch { }
            try { editItemIndex = int.Parse(((ScanPage)Element).editItemIndex.ToString()); } catch { }

            try
            {
                page = new ScannerPageNative(listItems, editListIndex, editItemIndex);
                SetNativeControl(page);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"      ERROR: ", ex.Message);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            try
            {
                page.Arrange(new Windows.Foundation.Rect(0, 0, finalSize.Width, finalSize.Height));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ArrangeOverride Error: " + ex.Message);
            }
            return finalSize;
        }
    }
}
