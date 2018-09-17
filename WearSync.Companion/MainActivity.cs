using Android.App;
using Android.Widget;
using Android.OS;
using Android.Support.V7.App;
using WearSync.Shared;
using Android.Content.Res;
using Android.Graphics;
using Android.Content;
using static WearSync.Companion.PrefSyncService;
using Android.Preferences;
using Chronoir_net.Chronica.WatchfaceExtension;

namespace WearSync.Companion
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ISharedPreferencesOnSharedPreferenceChangeListener
    {
        private PrefListener prefListener;
        private ISharedPreferences settings;
        private ISharedPreferencesEditor editor;

        private const string ACCENT_COLOR_KEY = "accent_color";
        private const string ACCENT_COLOR_DEFAULT = nameof(Resource.Color.accent_red);

        private Spinner colorSpinner;
        private FrameLayout colorLayout;

        private AccentColor currentAccentColor;

        private bool ignoreSelection = false;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.activity_main);

            settings = PreferenceManager.GetDefaultSharedPreferences(this);
            editor = settings.Edit();
            //editor.Clear().Commit();
            prefListener = new PrefListener(this);

            colorSpinner = FindViewById<Spinner>(Resource.Id.colorSpinner);
            colorLayout = FindViewById<FrameLayout>(Resource.Id.colorLayout);

            currentAccentColor = AccentColors.GetIdFromResource(settings.GetString(ACCENT_COLOR_KEY, ACCENT_COLOR_DEFAULT));

            AccentColor[] colors = AccentColors.GetAccentColors().ToArray();
            colorSpinner.Adapter = new ArrayAdapter<AccentColor>(this, Resource.Layout.colorspinner_item, colors);

            colorLayout.SetBackgroundColor(GetColor(currentAccentColor));
            colorSpinner.ItemSelected += ColorSpinner_ItemSelected;
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

        private void ColorSpinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            AccentColor selectedColor = AccentColors.GetAccentColors()[e.Position];

            if (selectedColor != currentAccentColor && !ignoreSelection)
            {
                currentAccentColor = selectedColor;

                colorLayout.SetBackgroundColor(GetColor(currentAccentColor));

                editor.PutString(ACCENT_COLOR_KEY, AccentColors.GetResource(currentAccentColor)).Commit();
            }

            ignoreSelection = false;
        }

        public void OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
        {
            switch (key)
            {
                case ACCENT_COLOR_KEY:
                    AccentColor newColor = AccentColors.GetIdFromResource(sharedPreferences.GetString(key, ACCENT_COLOR_DEFAULT));

                    if (newColor != currentAccentColor)
                    {
                        currentAccentColor = newColor;

                        colorLayout.SetBackgroundColor(GetColor(currentAccentColor));
                        ignoreSelection = true;
                        colorSpinner.SetSelection(AccentColors.GetAccentColors().IndexOf(currentAccentColor));
                    }

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

