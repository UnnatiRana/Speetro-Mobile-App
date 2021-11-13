using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android;
using System.Collections.Generic;
using Android.Support.V4.Content;
using Android.Support.V4.App;

namespace Speetro.Droid
{
    [Activity(Label = "Speetro", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                string[] perms = new string[] { Manifest.Permission.AccessNetworkState, Manifest.Permission.AccessCoarseLocation, Manifest.Permission.AccessFineLocation,
                                                Manifest.Permission.AccessLocationExtraCommands, Manifest.Permission.Internet};
                List<string> newPerms = new List<string>();
                foreach (string perm in perms)
                {
                    if (ContextCompat.CheckSelfPermission(this, perm) != Permission.Granted)
                    {
                        newPerms.Add(perm);
                    }
                }
                ActivityCompat.RequestPermissions(this, newPerms.ToArray(), 15);
            }

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);
            LoadApplication(new App());

            //CrossMediaManager.Current.Init();
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}