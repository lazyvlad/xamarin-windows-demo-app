using System;
using Xamarin.Forms;
using Xamarin.Forms.PlatformConfiguration;
using Xamarin.Forms.PlatformConfiguration.WindowsSpecific;

namespace ManateeShoppingCart
{
    public class App : Application
    {
        public App()
        {
            // The root page of your application
            var content = new ContentPage
            {
                Title = "ManateeShoppingCart",
                Content = new StackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    Children = {
                        new Label {
                            HorizontalTextAlignment = TextAlignment.Center,
                            Text = "Welcome to Xamarin Forms!"
                        }
                    }
                }
            };

            MainPage = new MainPage();
            //MainPage = new NavigationPage(new MainPage());
            MainPage.On<Windows>().SetToolbarPlacement(ToolbarPlacement.Top);
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
