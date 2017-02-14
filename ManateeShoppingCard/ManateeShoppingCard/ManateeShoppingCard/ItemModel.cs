using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ManateeShoppingCard
{
    public class ItemModel
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string BarcodeType { get; set; }
        public string BarcodeResult { get; set; }
        public string Barcode { get { return (BarcodeType != "" ? BarcodeType + ": " : "") + BarcodeResult; } }
        public bool Checked { get; set; }
        public int ListID { get; set; }

        public string ScanEditImageUrl { get; set; }
        public string ActionImageUrl { get; set; }

        public ItemModel()
        {
            this.ScanEditImageUrl = "Assets/barcode36x36.png";
            this.ActionImageUrl = "Assets/trash36x36.png";
        }
    }
}