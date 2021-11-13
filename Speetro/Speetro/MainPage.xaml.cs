using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Speetro
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void Automobile_Click(object sender, EventArgs e)
        {
            Navigation.PushAsync(new Automobile());
        }

        private void Internet_Click(object sender, EventArgs e)
        {
            Navigation.PushAsync(new InternetPage());
        }

        private void Human_Click(object sender, EventArgs e)
        {
            Navigation.PushAsync(new HumanPage());
        }
    }
}
