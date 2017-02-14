using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ManateeShoppingCart
{
    public partial class ScanPage : ContentPage
    {
        public ObservableCollection<ItemModel> AllItems;
        public int editListIndex;
        public int editItemIndex;

        public ScanPage(ObservableCollection<ItemModel> _items, int _editListIndex, int _editItemIndex)
        {
            InitializeComponent();

            AllItems = _items ?? new ObservableCollection<ItemModel>();
            editListIndex = _editListIndex;
            editItemIndex = _editItemIndex;
        }
    }
}
