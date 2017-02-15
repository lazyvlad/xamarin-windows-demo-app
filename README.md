# How to make your own Shopping Barcode Scanner Xamarin application

The Manatee Works Barcode Scanner SDK is implemented as a software library that provides a lightweight, yet powerful barcode detection and decoding API optimized for mobile platforms. The core routines are implemented in a static C library for high performance and portability, and where necessary, native wrappers are provided for the various platforms (e.g., a Windows implementation class for Xamarin).

The Barcode Scanner library supports the following barcode symbologies and sub-types:

- Aztec Code
- Codabar/Ames Code
- Code 11/USD-8
- Code 25
- Standard
- Interleaved
- ITF-14
- IATA
- Matrix
- COOP
- Inverted
- Code 39/USD-3/Alpha 39
- Code 32
- PZN
- Extended
- Code 93
- Standard
- Extended
- Code 128/EAN-14/GS1-128
- Code 128 A, B,
- SSCC-18
- UCC/EAN-128
- Data Matrix
- Square
- Rectangular
- GS1
- DotCode
- EAN
- EAN-8
- EAN-13
- ISBN
- ISMN
- ISSN
- JAN
- EAN Add-on
- GS1 Databar (Reduced Space Symbology) family of linear symbologies:
- RSS-14
- RSS-14 Stacked
- RSS Limited
- RSS Expanded
- MaxiCode
- MSI Plessey
- PDF417
- Standard o Compact/Truncated
- Postal Codes
- POSTNET
- PLANET
- Intelligent Mail Barcode
- Royal Mail
- QR Code
- Standard
- Micro
- UPC
- UPC-A
- UPC-E
- UPC-E0
- UPC-E1
- UPC Add-on

The Parser Plugin implements data parsing for the following industry standard formats:

- AAMVA CDS (Driver&#39;s License/ID Card)
- GS1/GTIN
- HIBC
- ISBT 128
- IUID
- MaxiCode

**Step 1** - [Download](https://www.visualstudio.com/downloads/) and install the latest Microsoft Visual Studio and make sure to include all Xamarin components through the installation. On this [link](https://msdn.microsoft.com/en-us/library/mt613162.aspx) you can read how to install it step by step.

After successful install visual studio, you need to install our SDK.

Open MWBarcodeLibUniversalSDK.vsix file in order to install our SDK.

Start visual studio and open ManateeShoppingCart.sln solution.

**Step 2** - Our SDK barcode needs to have a valid license in order to work, in order to purchase a license please create your Manatee Works developer account from here: [https://manateeworks.com/support/register](https://manateeworks.com/support/register)

After the registration is complete visit this link in order to generate a free trial which is valid for 30 days:

[https://manateeworks.com/admin/support/purchase/new\_evaluation\_license](https://manateeworks.com/admin/support/purchase/new_evaluation_license)

**Step 3** - In ManateeShoppingCart.UWP(Universal Windows Platform) project open the BarcodeHelper.cs and enter your license key in row 228

      int registerResult = Scanner.MWBregisterSDK("key");

Set ManateeShoppingCart.UWP(Universal Windows Platform) project as startup project, build and then rebuild ManateeShoppingCart(Portable) project and then build and rebuild ManateeShoppingCart.UWP(Universal Windows Platform).

**Overview of the solution**

This solution contains six projects:

- ManateeShoppingCart – This project is the portable class library (PCL) project that holds all of the shared code and shared UI.
- Droid – This project holds Android specific code and is the entry point for the Android application.
- iOS – This project holds iOS specific code and is the entry point for the iOS application.
- UWP – This project holds Universal Windows Platform (UWP) specific code and is the entry point for the UWP application.
- WinPhone – This project holds the Windows Phone specific code and is the entry point for the Windows Phone 8.0 application.
- WinPhone81 – This project holds the Windows Phone 8.1 specific code and is the entry point for the Windows Phone 8.1 application.

In this release we will keep only on Universal Windows Platform(UWP).

**Overview of the UWP project**

In this project we are including UWP native pages and classes that are used to work with camera, scan the image and give us the result from scanning:

- BarcodeHelper.cs

- MWOverlay.cs

- ScannerPageNative.xaml

ExtendedSplash.xaml page is used if we want to keep on splash image more time. If you want to use ExtendedSplash page you should uncomment this code in App.xaml.cs page:

    //Display an extended splash screen if app was not previously running.
    //if (e.PreviousExecutionState != ApplicationExecutionState.Running)
    //{
    //    bool loadState = (e.PreviousExecutionState == ApplicationExecutionState.Terminated);
    //    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen, loadState);
    //    rootFrame.Content = extendedSplash;
    //    Window.Current.Content = rootFrame;
    //}

ScanPageRenderer.cs is class that inherit from PageRenderer. This class helping us to show UWP native page in Xamarin.Forms page, in this case ScannerPageNative.xaml.

UWPContentDialog.xaml is a native ContentDialog which will be used in portable project.

UWPMethods.cs is class that inherit from NativeMethods interface which exists in portable project. In this class we wrote methods that can be use in portable project. To achieve this in App.xaml.cs we need to add this code at line 63.

Xamarin.Forms.DependencyService.Register&lt;UWPMethods&gt;();

DependencyService allows apps to call into platform-specific functionality from shared code. This functionality enables Xamarin.Forms apps to do anything that a native app can do.

In Package.appxmanifest we are setting app name, description, capabilities (in this case we need access to Microphone and Webcam), splash screen image, app logo, etc...

**Overview of the Portable project**

When the Splash screen is finished the first page you will see is the AllListPage.xaml.

This is Xamarin.Forms Page representing cross-platform mobile app screens.

Here we are showing our shopping lists. Every list can contain multiple items.


Every row in list view contains two action icons: pencil icon and trash icon. When you tapped or click one of them appropriate action will be executed. Also tap or click row will open the list and navigate to items for that list.

At the bottom of page is entry field. This field we are using to add new list by typing new list name.

- Add new list:
 
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

  ListsModel is a class that represent every item in list view.

  The Application base class offers the features which are exposed in our project default App subclass.

  One of that features is a static Properties dictionary which can be used to store data. This can be accessed from anywhere in Xamarin.Forms code using Application.Current.Properties.

  The Properties dictionary uses a string key and stores an object value. Because Properties dictionary can only serialize primitive types for storage in our case we need to convert our ObservableCollection&lt;ListsModel&gt; object in json format first and then to store the value.

  At the end of method we are use entry.Unfocus() to hide the keyboard.


- Edit list: By pressing on pencil icon a Pop-up window appears where you can change list name. You can cancel or make the change.

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

  As we discussed before in this topic, with DependencyService we can use platform-specific functionality from portable project. Here we are using our native content dialog that we are created in UWP project.


- Delete item: If you press on trash icon you can delete selected list.

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

  DisplayAlert is modal pop-up to alert the user or ask simple questions of them. Here we are asking from user to confirm deleting list.



After creating lists, by click or press row from list view we can add items for appropriate list. Same like in lists you can add, edit or delete every item in list.

- You can add new item in two ways:

  - First way is to add your new item by typing item name in &quot;Add new item&quot; entry field.

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

  - Second way is to press on barcode icon in the right corner. When barcode icon is pressed it&#39;s navigating to ScanPage.xaml page.

          public async void imgScanNewTapped(object sender, EventArgs args)
          {
            await Navigation.PushModalAsync(new ScanPage(selectedList.Items, selectedListIndex, -1));
          }

    Navigation is interface that manage page navigation experience. With this code we navigate from ItemsPage to ScanPage, and passing parameters in constructor of ScanPage.

    ScanPage is also Xamarin.Forms page but using ScanPageRenderer.cs from UWP project we are showing UWP native page in Xamarin.Forms page.

    ScanPageRenderer.cs is class that inherit from PageRenderer. This class helping us to show UWP native page in Xamarin.Forms page, in this case ScannerPageNative.xaml.

    After successful barcode reading a new item with appropriate type and result is entered in our list.

- Edit item: When you press item in list view, a Pop-up window appears. In this window we can edit item name. You can cancel or make the change.

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

  Also you can make a change on item Barcode type and result while pressing barcode icon which is positioned in every item in the list view.

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

- Delete item: If you press on trash icon you can delete item from list.

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



ListModel class has property ActionType that can be Edit or Check. When we are editing list items or creating new ones ActionType property for that list is Edit. When shopping icon from navigation bar is pressed, we change the property to Check, change trash icon to check icon and user can check bought items for that list.

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

While we are in shopping mode navigation bar and status bar background colors are changed.

If we want go back to edit the list, we are pressing icon Edit list from navigation bar.
