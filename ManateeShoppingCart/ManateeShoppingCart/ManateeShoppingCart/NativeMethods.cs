using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManateeShoppingCart
{
    public interface NativeMethods
    {
        Task ShowDialog(ListsModel _item, string _description);

        Task ShowDialog(ItemModel _item, string _description);

        void SetStatusBar(string _backgroundHexColor);
    }
}
