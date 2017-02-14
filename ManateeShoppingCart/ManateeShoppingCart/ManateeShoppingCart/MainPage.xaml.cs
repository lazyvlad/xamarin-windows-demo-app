using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;

namespace ManateeShoppingCart
{
    public partial class MainPage : MasterDetailPage
    {
        public MainPage()
        {
            InitializeComponent();

            var tgr = new TapGestureRecognizer();
            tgr.Tapped += tapEditLists;
            mainActions.slEditLists.GestureRecognizers.Add(tgr);
        }

        private void tapEditLists(object sender, EventArgs e)
        {
            Detail = new NavigationPage(new AllListsPage())
            {
                BarBackgroundColor = Color.FromHex("#1ab78d"),
                BarTextColor = Color.White,
            };

            IsPresented = false;
        }
    }
}
