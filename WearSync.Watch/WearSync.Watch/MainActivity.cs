using Android.App;
using Android.Widget;
using Android.OS;
using Android.Support.Wearable.Activity;
using static Android.Views.View;
using Android.Views;
using Chronoir_net.Chronica.WatchfaceExtension;
using WearSync.Shared;
using Android.Graphics;
using static WearSync.Watch.PrefSyncService;
using Android.Content;
using Android.Preferences;

namespace WearSync.Watch
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : WearableActivity, IOnClickListener, ISharedPreferencesOnSharedPreferenceChangeListener
    {
        private PrefListener prefListener;
        private ISharedPreferences settings;
        private ISharedPreferencesEditor editor;

        private const string ACCENT_COLOR_KEY = "accent_color";
        private const string ACCENT_COLOR_DEFAULT = nameof(Resource.Color.accent_red);

        private FrameLayout clickableFrameLayout;

        private AccentColor currentAccentColor;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.activity_main);

            settings = PreferenceManager.GetDefaultSharedPreferences(this);
            editor = settings.Edit();
            //editor.Clear().Commit();
            prefListener = new PrefListener(this);

            clickableFrameLayout = FindViewById<FrameLayout>(Resource.Id.clickableFrameLayout);

            currentAccentColor = AccentColors.GetIdFromResource(settings.GetString(ACCENT_COLOR_KEY, ACCENT_COLOR_DEFAULT));

            clickableFrameLayout.SetBackgroundColor(GetColor(currentAccentColor));
            clickableFrameLayout.SetOnClickListener(this);

            SetAmbientEnabled();
        }

        protected override void OnResume()
        {
            base.OnResume();

            settings.RegisterOnSharedPreferenceChangeListener(this);
            prefListener.OnResume();
        }

        protected override void OnPause()
        {
            base.OnPause();

            settings.UnregisterOnSharedPreferenceChangeListener(this);
            prefListener.OnPause();
        }

        public void OnClick(View view)
        {
            if (view.Equals(clickableFrameLayout))
            {
                editor.PutString(ACCENT_COLOR_KEY, AccentColors.GetResource(AccentColors.GetNextColor(currentAccentColor))).Commit();
            }
        }

        public void OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
        {
            switch (key)
            {
                case ACCENT_COLOR_KEY:
                    currentAccentColor=AccentColors.GetIdFromResource(sharedPreferences.GetString(key, ACCENT_COLOR_DEFAULT));
                    clickableFrameLayout.SetBackgroundColor(GetColor(currentAccentColor));
                    break;
            }
        }

        #region Helper

        private int GetColorResource(string resource) => Resources.GetIdentifier(resource, "color", PackageName);

        private int GetColorResource(AccentColor accentColor) => GetColorResource(AccentColors.GetResource(accentColor));

        private Color GetColor(AccentColor accentColor) => WatchfaceUtility.ConvertARGBToColor(GetColor(GetColorResource(accentColor)));

        #endregion
    }
}


