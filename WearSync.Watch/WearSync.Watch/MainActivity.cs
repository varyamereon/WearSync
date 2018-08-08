using Android.App;
using Android.Widget;
using Android.OS;
using Android.Support.Wearable.Activity;
using static Android.Views.View;
using Android.Views;
using Chronoir_net.Chronica.WatchfaceExtension;
using WearSync.Shared;
using Android.Graphics;

namespace WearSync.Watch
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : WearableActivity, IOnClickListener
    {
        private FrameLayout clickableFrameLayout;

        private AccentColor currentAccentColor = AccentColor.Red;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.activity_main);

            clickableFrameLayout = FindViewById<FrameLayout>(Resource.Id.clickableFrameLayout);

            clickableFrameLayout.SetBackgroundColor(GetColor(currentAccentColor));
            clickableFrameLayout.SetOnClickListener(this);

            SetAmbientEnabled();
        }

        public void OnClick(View view)
        {
            if (view.Equals(clickableFrameLayout))
            {
                currentAccentColor = AccentColors.GetNextColor(currentAccentColor);
                clickableFrameLayout.SetBackgroundColor(GetColor(currentAccentColor));
            }
        }

        #region Helper

        private int GetColorResource(string resource) => Resources.GetIdentifier(resource, "color", PackageName);

        private int GetColorResource(AccentColor accentColor) => GetColorResource(AccentColors.GetResource(accentColor));

        private Color GetColor(AccentColor accentColor) => WatchfaceUtility.ConvertARGBToColor(GetColor(GetColorResource(accentColor)));

        #endregion
    }
}


