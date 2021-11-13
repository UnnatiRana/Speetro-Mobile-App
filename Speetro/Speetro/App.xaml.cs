using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Speetro
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage contentPage = new MainPage();
            MainPage = new NavigationPage(contentPage);
            contentPage.Title = "Speetro";
            Xamarin.Forms.NavigationPage.SetHasNavigationBar(contentPage, false);
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
