using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManateeShoppingCard
{
    public class ListsModel
    {
        public int ID { get; set; }
        public string ImageUrl { get { return "Assets/listBulleted36x36.png"; } }
        public string Name { get; set; }
        public ObservableCollection<ItemModel> Items { get; set; }
        public ItemsActionType ActionType { get; set; }

        public ListsModel()
        {
            this.Items = new ObservableCollection<ItemModel>();
            this.ActionType = ItemsActionType.Edit;
        }
    }
}