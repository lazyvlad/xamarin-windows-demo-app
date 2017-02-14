using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;

namespace ManateeShoppingCart
{
    public partial class MainActions : ContentPage
    {
        public StackLayout slEditLists { get { return layoutEditLists; } }

        public MainActions()
        {
            InitializeComponent();
        }
    }
}
