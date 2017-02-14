using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;


namespace ManateeShoppingCard
{
    public partial class ItemsPage : ContentPage
    {
        public ListsModel selectedList;

        public static int selectedListIndex;

        public ItemModel item;

        public ItemsPage(ListsModel _selectedList, int _selectedListIndex)
        {
            InitializeComponent();

            Title = _selectedList.Name;

            selectedList = _selectedList ?? new ListsModel();
            selectedListIndex = _selectedListIndex;
        }

        private void listViewItemDisappearing(object sender, EventArgs e)
        {
            if (!entryNewItem.IsEnabled)
                entryNewItem.IsEnabled = true;
        }

        public async void imgScanNewTapped(object sender, EventArgs args)
        {
            await Navigation.PushModalAsync(new ScanPage(selectedList.Items, selectedListIndex, -1));
        }

        public async void imgActionTapped(object sender, EventArgs args)
        {
            try
            {
                int actionID = int.Parse(((TappedEventArgs)args).Parameter.ToString());
                item = selectedList.Items.First(x => x.ID == actionID);
                listItemsView.SelectedItem = item;

                if (item != null && item.ID > 0)
                {
                    if (selectedList.ActionType == ItemsActionType.Edit)
                    {
                        var answer = await DisplayAlert("", "Are you sure you want do delete " + item.Name, "OK", "CANCEL");
                        if (answer)
                        {
                            //Have some problem with autofocus on entry after delete, that's why here i disable entry and after that in listViewItemDisappearing it is enabled again		                        
                            entryNewItem.IsEnabled = false;

                            selectedList.Items.Remove(item);
                            SaveListChanges();
                        }
                    }
                    else
                    if (selectedList.ActionType == ItemsActionType.Check)
                    {
                        if (item.Checked)
                        {
                            ((Image)sender).Source = "Assets/checkboxBlank36x36.png";
                            item.Checked = false;
                            item.ActionImageUrl = "Assets/checkboxBlank36x36.png";
                        }
                        else
                        {
                            ((Image)sender).Source = "Assets/checkboxMarked36x36.png";
                            item.Checked = true;
                            item.ActionImageUrl = "Assets/checkboxMarked36x36.png";
                        }
                        SaveListChanges();
                    }
                }
            }
            catch (Exception ex) { }

            listItemsView.SelectedItem = null;
        }

        public async void imgScanEditTapped(object sender, EventArgs args)
        {
            try
            {
                int selectedID = int.Parse(((TappedEventArgs)args).Parameter.ToString());
                item = selectedList.Items.First(x => x.ID == selectedID);
                listItemsView.SelectedItem = item;

                if (item != null && item.ID > 0)
                    await Navigation.PushModalAsync(new ScanPage(selectedList.Items, selectedListIndex, selectedList.Items.IndexOf(item)));
            }
            catch (Exception ex) { }

            listItemsView.SelectedItem = null;
        }

        public void entryNewItemCompleted(object sender, EventArgs args)
        {
            Entry entry = ((Entry)sender);

            item = new ItemModel();

            if (entry.Text != null && entry.Text.Trim().Length > 0)
            {
                int id = 1;
                if (selectedList.Items.Count > 0)
                    id = ((int)selectedList.Items.Max(x => x.ID)) + 1;

                item.ID = id;
                item.Name = entry.Text;
                item.BarcodeType = "Barcode type";
                item.BarcodeResult = "Result";

                selectedList.Items.Insert(0, item);
                SaveListChanges();
            }

            entry.Text = "";
            entry.Unfocus();
        }

        public async void listViewItemTapped(object sender, ItemTappedEventArgs args)
        {
            if (selectedList.ActionType == ItemsActionType.Check)
                return;

            try
            {
                item = (ItemModel)args.Item;

                if (item != null && item.ID > 0)
                {
                    listItemsView.ItemsSource = null;

                    await DependencyService.Get<NativeMethods>().ShowDialog(item, "Are you sure that you want to edit name for " + item.Name);
                    SaveListChanges();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                listItemsView.ItemsSource = selectedList.Items;
            }
        }

        public void listViewItemSelected(object sender, SelectedItemChangedEventArgs args)
        {
            if (args.SelectedItem == null) return;
            ((ListView)sender).SelectedItem = null;
        }

        protected override void OnAppearing()
        {
            if (selectedList.ActionType == ItemsActionType.Check)
            {
                gridAddNewItem.IsVisible = false;
                toolbarItemShopping.Icon = "Assets/Pencil36x36.png";
                toolbarItemShopping.Text = "Edit List";

                if (this.Parent != null)
                {
                    ((NavigationPage)this.Parent).BarBackgroundColor = Color.FromHex("#e5a82d");
                    DependencyService.Get<NativeMethods>().SetStatusBar("#e5a82d");
                }
            }

            listItemsView.ItemsSource = null;
            listItemsView.ItemsSource = selectedList.Items;
        }

        protected override void OnDisappearing()
        {
            if (selectedList.ActionType == ItemsActionType.Check)
            {
                if (this.Parent != null)
                {
                    ((NavigationPage)this.Parent).BarBackgroundColor = Color.FromHex("#1ab78d");
                    DependencyService.Get<NativeMethods>().SetStatusBar("#1ab78d");
                }
            }
        }

        private void SaveListChanges()
        {
            try
            {
                if (Application.Current.Properties.ContainsKey("AllLists"))
                {
                    string jsonList = Application.Current.Properties["AllLists"].ToString();
                    ObservableCollection<ListsModel> tempList = JsonConvert.DeserializeObject<ObservableCollection<ListsModel>>(jsonList);

                    tempList[selectedListIndex] = selectedList;

                    Application.Current.Properties["AllLists"] = JsonConvert.SerializeObject(tempList);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(" SaveListChanges Error: " + ex.Message);
            }
        }

        private void shoppingClick(object sender, EventArgs e)
        {
            listItemsView.Unfocus();

            if (selectedList.ActionType == ItemsActionType.Check)
            {
                gridAddNewItem.IsVisible = true;

                foreach (ItemModel item in selectedList.Items)
                {
                    item.ActionImageUrl = "Assets/trash36x36.png";
                    item.ScanEditImageUrl = "Assets/barcode36x36.png";
                }

                selectedList.ActionType = ItemsActionType.Edit;
                ((ToolbarItem)sender).Icon = "Assets/shopping.png";
                ((ToolbarItem)sender).Text = "Shopping";

                if (this.Parent != null)
                {
                    ((NavigationPage)this.Parent).BarBackgroundColor = Color.FromHex("#1ab78d");
                    DependencyService.Get<NativeMethods>().SetStatusBar("#1ab78d");
                }
            }
            else
            {
                gridAddNewItem.IsVisible = false;

                foreach (ItemModel item in selectedList.Items)
                {
                    if (item.Checked)
                        item.ActionImageUrl = "Assets/checkboxMarked36x36.png";
                    else
                        item.ActionImageUrl = "Assets/checkboxBlank36x36.png";

                    item.ScanEditImageUrl = "";
                }

                selectedList.ActionType = ItemsActionType.Check;
                ((ToolbarItem)sender).Icon = "Assets/Pencil36x36.png";
                ((ToolbarItem)sender).Text = "Edit List";

                if (this.Parent != null)
                {
                    ((NavigationPage)this.Parent).BarBackgroundColor = Color.FromHex("#e5a82d");
                    DependencyService.Get<NativeMethods>().SetStatusBar("#e5a82d");
                }
            }

            SaveListChanges();

            listItemsView.ItemsSource = null;
            listItemsView.ItemsSource = selectedList.Items;
        }
    }
}
