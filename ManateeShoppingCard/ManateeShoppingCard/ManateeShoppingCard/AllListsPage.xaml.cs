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
    public partial class AllListsPage : ContentPage
    {
        public ObservableCollection<ListsModel> allLists;
        public ListsModel list;

        public AllListsPage()
        {
            InitializeComponent();

            allLists = new ObservableCollection<ListsModel>();

            //Application.Current.Properties.Clear();
            if (Application.Current.Properties.ContainsKey("AllLists"))
            {
                string jsonList = Application.Current.Properties["AllLists"].ToString();
                allLists = JsonConvert.DeserializeObject<ObservableCollection<ListsModel>>(jsonList);
            }
        }

        private void listViewItemDisappearing(object sender, EventArgs e)
        {
            if (!entryNewList.IsEnabled)
                entryNewList.IsEnabled = true;
        }

        public async void imgDeleteTapped(object sender, EventArgs args)
        {
            try
            {
                int deleteID = int.Parse(((TappedEventArgs)args).Parameter.ToString());
                list = allLists.First(x => x.ID == deleteID);
                listViewAllLists.SelectedItem = list;

                if (list != null && list.ID > 0)
                {
                    var answer = await DisplayAlert("", "Are you sure you want do delete " + list.Name, "OK", "CANCEL");
                    if (answer)
                    {
                        //Have some problem with autofocus on entry after delete, that's why here i disable entry and after that in listViewItemDisappearing it is enabled again		                        
                        entryNewList.IsEnabled = false;

                        allLists.Remove(list);
                        Application.Current.Properties["AllLists"] = JsonConvert.SerializeObject(allLists);
                    }
                }
            }
            catch (Exception ex) { }

            listViewAllLists.SelectedItem = null;
        }

        public async void imgEditTapped(object sender, EventArgs args)
        {
            try
            {
                int editID = int.Parse(((TappedEventArgs)args).Parameter.ToString());
                list = allLists.First(x => x.ID == editID);

                if (list != null && list.ID > 0)
                {
                    listViewAllLists.ItemsSource = null;
                    await DependencyService.Get<NativeMethods>().ShowDialog(list, "Are you sure that you want to edit name for " + list.Name);
                    Application.Current.Properties["AllLists"] = JsonConvert.SerializeObject(allLists);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                listViewAllLists.ItemsSource = allLists;
            }
        }

        public void entryNewListCompleted(object sender, EventArgs args)
        {
            Entry entry = ((Entry)sender);

            list = new ListsModel();

            if (entry.Text != null && entry.Text.Trim().Length > 0)
            {
                int id = 1;
                if (allLists.Count > 0)
                    id = ((int)allLists.Max(x => x.ID)) + 1;

                list.ID = id;
                list.Name = entry.Text;

                allLists.Insert(0, list);
                Application.Current.Properties["AllLists"] = JsonConvert.SerializeObject(allLists);
            }

            entry.Text = "";
            entry.Unfocus();
        }

        public async void listViewAllListsItemTapped(object sender, ItemTappedEventArgs args)
        {
            list = (ListsModel)args.Item;

            if (list != null && list.ID > 0)
                await Navigation.PushAsync(new ItemsPage(list, allLists.IndexOf(list)));
        }

        public void listViewAllListsItemSelected(object sender, SelectedItemChangedEventArgs args)
        {
            if (args.SelectedItem == null) return;
            ((ListView)sender).SelectedItem = null;
        }

        protected override void OnAppearing()
        {
            //entryNewList.Focus();
            listViewAllLists.ItemsSource = null;
            listViewAllLists.ItemsSource = allLists;
        }
    }
}
