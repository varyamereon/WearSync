using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Gms.Common.Apis;
using Android.Gms.Wearable;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Util;

namespace WearSync.Watch
{
    /// <summary>
    /// A generic class for synchronizing <see cref="ISharedPreferences"/> between devices on a Wear OS
    /// personal-area network (PAN). More details (including instructions) at
    /// https://github.com/StringMon/prefsyncservice.
    /// </summary>
    [Service(Enabled = true, Exported = true)]
    [IntentFilter(new[] { DataApi.ActionDataChanged }, DataScheme = "wear", DataHost = "*", DataPathPrefix = "/PrefSyncService/data/settings")]
    public class PrefSyncService : WearableListenerService
    {
        #region Fields

        /// <summary>
        /// The URI prefix used to identify preference values saved to the Data API by this class.
        /// </summary>
        /// <remarks>
        /// It's public so that it can be read by other pieces of code, if necessary,
        /// but probably shouldn't be changed.
        /// </remarks>
        public static readonly string DATA_SETTINGS_PATH = "/PrefSyncService/data/settings";

        /// <summary>
        /// <see cref="PrefListener"/> waits this long before changed to <see cref="ISharedPreferences"/> are synced
        /// to the PAN. This allows multiple changed made in quick succession to be batched for more
        /// efficient use of the Wear Data API.
        /// </summary>
        public static int delayMillis = 1000;

        /// <summary>
        /// If this <see cref="string"/> is set, the preference synchronization will use a <see cref="ISharedPreferences"/>
        /// file by this name (in the app's default directory), rather than the context's default
        /// <see cref="ISharedPreferences"/> file.
        /// </summary>
        public static string sharedPrefsName = null;

        private static readonly string KEY_TIMESTAMP = "timestamp";

        private static string localNodeId;

        #endregion

        #region Interfaces

        /// <summary>
        /// A SyncFilter enables an app using <see cref="PrefListener"/> to specify which preferences should be
        /// synchronized to other nodes on the PAN.
        /// </summary>
        public interface ISyncFilter
        {
            /// <summary>
            /// Returns true or false depending on whether the supplied <paramref name="key"/> should be synced.
            /// </summary>
            /// <param name="key">The key to check.</param>
            bool ShouldSyncPref(string key);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Re-synchronizes local preferences from the Data API if no such sync has yet been run.
        /// </summary>
        /// <param name="context">The context of the preferences whose values are to be synced.</param>
        /// <param name="settings">A <see cref="ISharedPreferences"/> instance from which to retrieve
        /// values of the preferences.
        /// </param>
        public static void ResyncIfNeeded(Context context, ISharedPreferences settings)
        {
            if (!settings.Contains(PrefListener.KEY_SYNC_DONE))
                new PrefListener(context, settings).ResyncLocal();
        }

        /// <summary>
        /// This is the receiving side of <see cref="PrefSyncService"/>, listening for changes coming from the
        /// Wear Data API and saving them to the local <see cref="ISharedPreferences"/>.
        /// </summary>
        /// <param name="dataEvents">The data incoming from the Data API.</param>
        public async override void OnDataChanged(DataEventBuffer dataEvents) => await OnDataChangedAsync(dataEvents);
        private async Task OnDataChangedAsync(DataEventBuffer dataEvents)
        {
            if (localNodeId == null)
            {
                GoogleApiClient googleApiClient = new GoogleApiClient.Builder(this)
                    .AddApi(WearableClass.API)
                    .Build();
                googleApiClient.Connect();

                localNodeId = (await WearableClass.NodeApi.GetLocalNodeAsync(googleApiClient).ConfigureAwait(false)).Node.Id;
            }

            ISharedPreferences settings = string.IsNullOrEmpty(sharedPrefsName) ?
                PreferenceManager.GetDefaultSharedPreferences(this) :
                GetSharedPreferences(sharedPrefsName, FileCreationMode.Private);
            ISharedPreferencesEditor editor = settings.Edit();
            IDictionary<string, object> allPrefs = settings.All;

            try
            {
                foreach (IDataEvent ev in dataEvents)
                {
                    IDataItem dataItem = ev.DataItem;
                    Android.Net.Uri uri = dataItem.Uri;

                    var nodeId = uri.Host;
                    if (nodeId.Equals(localNodeId))
                    {
                        // Change originated on this device.
                        //continue;
                    }

                    if (uri.Path.StartsWith(DATA_SETTINGS_PATH))
                    {
                        if (ev.Type == DataEvent.TypeDeleted)
                            DeleteItem(uri, editor, allPrefs);
                        else
                            SaveItem(dataItem, editor, allPrefs);
                    }
                }
            }
            finally
            {
                // We don't use Apply() because we don't know what thread we're on.
                editor.Commit();
            }

            base.OnDataChanged(dataEvents);
        }

        private static void DeleteItem(Android.Net.Uri uri, ISharedPreferencesEditor editor, IDictionary<string, object> allPrefs)
        {
            var key = uri.LastPathSegment;
            if (allPrefs == null || allPrefs.ContainsKey(key))
                editor.Remove(key);
        }

        private static void SaveItem(IDataItem dataItem, ISharedPreferencesEditor editor, IDictionary<string, object> allPrefs)
        {
            if (dataItem == null)
                return;

            DataMap dataMap = DataMapItem.FromDataItem(dataItem).DataMap;
            if (dataMap.KeySet().Count == 0)
            {
                // Testing has shown that when an item is deleted from the Data API, it
                // will often come through as an empty TYPE_CHANGED rather than a TYPE_DELETED.
                DeleteItem(dataItem.Uri, editor, allPrefs);
            }
            else
            {
                foreach (var key in dataMap.KeySet())
                {
                    var value = dataMap.Get(key);
                    var bob = value.GetType();
                    if (value == null)
                    {
                        if (allPrefs != null && value.Equals(allPrefs.ContainsKey(key)))
                            editor.Remove(key);
                        continue;
                    }
                    if (allPrefs != null && value.Equals(allPrefs[key]))
                    {
                        // No change to value.
                        continue;
                    }
                    if (key.Equals(KEY_TIMESTAMP))
                        continue;

                    if (value is Java.Lang.Boolean)
                    {
                        editor.PutBoolean(key, (bool)value);
                    }
                    else if (value is Java.Lang.Float)
                    {
                        editor.PutFloat(key, (float)value);
                    }
                    else if (value is Java.Lang.Integer)
                    {
                        editor.PutInt(key, (int)value);
                    }
                    else if (value is Java.Lang.Long)
                    {
                        editor.PutLong(key, (long)value);
                    }
                    else if (value is Java.Lang.String)
                    {
                        editor.PutString(key, (string)value);
                    }
                    else if (value is Java.Lang.Object javaValue && javaValue.Class.SimpleName=="String[]")
                    {
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb)
                        {
                            editor.PutStringSet(key, (string[])value);
                        }
                    }
                    else
                    {
                        // Invalid cast
                    }
                }
            }
        }

        #endregion

        #region Classes

        /// <summary>
        /// The main API for preference synchronization.
        /// </summary>
        /// <remarks>
        /// Instantiate this class in the app, give it a <see cref="ISyncFilter"/>, and use it's <see cref="OnResume"/>
        /// and <see cref="OnPause"/> methods to start and stop synchronization.
        /// </remarks>
        public class PrefListener : Java.Lang.Object, ISharedPreferencesOnSharedPreferenceChangeListener
        {
            #region Fields

            public ISyncFilter syncFilter = null;

            public static readonly string KEY_SYNC_DONE = "PrefListener.sync_done";

            private ISharedPreferences settings;
            private readonly PrefHandler prefHandler;

            #endregion

            #region Constructors

            /// <summary>
            /// Simplest ctor: just supply a context and the default <see cref="ISharedPreferences"/> 
            /// for the app will be synchronized.
            /// </summary>
            /// <param name="context">The context of the preferences whose values are to be synced.</param>
            public PrefListener(Context context)
            {
                this.settings = string.IsNullOrEmpty(sharedPrefsName) ?
                    PreferenceManager.GetDefaultSharedPreferences(context) :
                    context.GetSharedPreferences(sharedPrefsName, FileCreationMode.Private);
                this.prefHandler = new PrefHandler(context);
            }

            /// <summary>
            /// If you want to synchronize a specifice <see cref="ISharedPreferences"/> file (rather than the app's
            /// default), use this ctor.
            /// </summary>
            /// <param name="context">The context of the preferences whose values are to be synced.</param>
            /// <param name="settings">An <see cref="ISharedPreferences"/> instance from which to
            /// retrieve values of the preferences.
            /// </param>
            public PrefListener(Context context, ISharedPreferences settings)
            {
                this.settings = settings;
                this.prefHandler = new PrefHandler(context);
            }

            #endregion

            #region Methods

            /// <summary>
            /// Begin listening for changes to the <see cref="ISharedPreferences"/> to synchronize.
            /// </summary>
            public void OnResume()
            {
                settings.RegisterOnSharedPreferenceChangeListener(this);

                if (!settings.Contains(KEY_SYNC_DONE))
                {
                    // It appears that no sync has been done on this device, so do one now.
                    ResyncLocal();
                }
            }

            /// <summary>
            /// Stop listening for changes to the <see cref="ISharedPreferences"/> to synchronize.
            /// </summary>
            public void OnPause() => settings.UnregisterOnSharedPreferenceChangeListener(this);

            /// <summary>
            /// Re-synchronize all <see cref="ISharedPreferences"/> values to the local node from any other
            /// attached nodes. It's up to the remote node(s) to decide which values to sync.
            /// </summary>
            public void ResyncLocal()
            {
                lock (prefHandler)
                {
                    prefHandler.RemoveMessages(PrefHandler.ACTION_SYNC_ALL);
                    prefHandler.ObtainMessage(PrefHandler.ACTION_SYNC_ALL).SendToTarget();
                }

                settings.Edit().PutBoolean(KEY_SYNC_DONE, true).Commit();
            }

            /// <summary>
            /// Re-synchronize all matching <see cref="ISharedPreferences"/> values from the local node to
            /// other attached nodes.
            /// </summary>
            /// <remarks>
            /// Note that <see cref="ISyncFilter"/> is required, otherwise we'd synchronize ALL prefs. If this is
            /// actually what you want, use a <see cref="ISyncFilter"/> that always returns true.
            /// </remarks>
            public void ResyncRemote()
            {
                if (syncFilter == null)
                    return;

                IDictionary<string, object> allPrefs = settings.All;

                lock (prefHandler)
                {
                    foreach (var key in allPrefs.Keys)
                    {
                        if (syncFilter.ShouldSyncPref(key))
                        {
                            allPrefs.TryGetValue(key, out var value);
                            prefHandler.dataQueue.Add(key, value);
                        }

                    }

                    prefHandler.clearFirst = true;
                    prefHandler.RemoveMessages(PrefHandler.ACTION_SYNC_QUEUE);
                    prefHandler.ObtainMessage(PrefHandler.ACTION_SYNC_QUEUE).SendToTarget();
                }
            }

            /// <summary>
            /// This is where changes to the local <see cref="ISharedPreferences"/> are detected, and batched for
            /// synchronization to the Wear PAN. It's called by the system whenever the <see cref="PrefListener"/>
            /// has been resumed and a change to the <see cref="ISharedPreferences"/> occurs.
            /// </summary>
            /// <remarks>
            /// Note that, if you have other work to do with <see cref="ISharedPreferences"/> changes, you're free
            /// to override this method. Just be sure to call through to this ancestor (or sync won't occur).
            /// </remarks>
            /// <param name="sharedPreferences">The <see cref="ISharedPreferences"/> that received the change.</param>
            /// <param name="key">The key of the preference that was changed, added, or removed.</param>
            public void OnSharedPreferenceChanged(ISharedPreferences sharedPreferences, string key)
            {
                if (syncFilter != null && !syncFilter.ShouldSyncPref(key) && !KEY_SYNC_DONE.Equals(key))
                    return;

                IDictionary<string, object> allPrefs = sharedPreferences.All;

                lock (prefHandler)
                {
                    allPrefs.TryGetValue(key, out var value);

                    prefHandler.dataQueue[key] = value;

                    // Wait a moment so that settings updates are batched.
                    prefHandler.RemoveMessages(PrefHandler.ACTION_SYNC_QUEUE);
                    prefHandler.SendMessageDelayed(prefHandler.ObtainMessage(PrefHandler.ACTION_SYNC_QUEUE), delayMillis);
                }
            }

            #endregion
        }

        private class PrefHandler : Handler, GoogleApiClient.IConnectionCallbacks, IResultCallback
        {
            #region Fields

            internal readonly IDictionary<string, object> dataQueue = new Dictionary<string, object>();

            internal static readonly int ACTION_NONE = -1;
            internal static readonly int ACTION_SYNC_QUEUE = 0;
            internal static readonly int ACTION_SYNC_ALL = 1;

            private static readonly Android.Net.Uri DATA_SETTINGS_URI = new Android.Net.Uri.Builder()
                .Scheme(PutDataRequest.WearUriScheme)
                .Path(DATA_SETTINGS_PATH)
                .Build();

            private readonly Context appContext;
            private readonly GoogleApiClient googleApiClient;
            internal bool clearFirst = false;
            private int pendingAction = ACTION_NONE;

            #endregion

            #region Constructors

            public PrefHandler(Context context)
            {
                appContext = context.ApplicationContext;

                googleApiClient = new GoogleApiClient.Builder(appContext)
                    .AddApi(WearableClass.API)
                    .AddConnectionCallbacks(this)
                    .Build();
                googleApiClient.Connect();

                if (localNodeId == null)
                {
                    WearableClass.NodeApi.GetLocalNode(googleApiClient).SetResultCallback<INodeApiGetLocalNodeResult>((result) =>
                    {
                        localNodeId = result.Node.Id;
                    });
                }
            }

            #endregion

            #region Methods

            public void OnConnected(Bundle connectionHint)
            {
                if (pendingAction > ACTION_NONE)
                {
                    Message msg = ObtainMessage(pendingAction);
                    HandleMessage(msg);
                    msg.Recycle();
                    pendingAction = ACTION_NONE;
                }
            }

            public void OnConnectionSuspended(int cause) { }

            public async override void HandleMessage(Message msg)
            {
                Log.Debug(nameof(PrefSyncService), $"{nameof(HandleMessage)}: {msg.What}");

                if (googleApiClient.IsConnected)
                {
                    if (msg.What == ACTION_SYNC_ALL)
                    {
                        WearableClass.DataApi.GetDataItems(googleApiClient, DATA_SETTINGS_URI, DataApi.FilterPrefix).SetResultCallback(this);
                    }
                    else
                    {
                        await ProcessQueueAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    if (msg.What >= pendingAction)
                        pendingAction = msg.What;
                }
            }

            public void OnResult(Java.Lang.Object result)
            {
                if (result is DataItemBuffer dataItems)
                {
                    // This is the callback from the GetDataItems() call in ResyncAll().
                    ISharedPreferencesEditor editor = string.IsNullOrEmpty(sharedPrefsName) ?
                        PreferenceManager.GetDefaultSharedPreferences(appContext).Edit() :
                        appContext.GetSharedPreferences(sharedPrefsName, FileCreationMode.Private).Edit();
                    try
                    {
                        if (dataItems.Status.IsSuccess)
                        {
                            for (var i = dataItems.Count - 1; i >= 0; i--)
                            {
                                if (!(dataItems.Get(i) is IDataItem))
                                {
                                    // Invalid cast
                                }

                                Log.Debug(nameof(PrefSyncService), $"Resync {nameof(OnResult)}: {dataItems.Get(i) as IDataItem}");
                                SaveItem(dataItems.Get(i) as IDataItem, editor, null);
                            }
                        }
                    }
                    finally
                    {
                        // We don't use Apply() because we don't know what thread we're on.
                        editor.Commit();
                        dataItems.Release();
                    }
                }
                else
                {
                    //Invalid cast
                }
            }

            private async Task ProcessQueueAsync()
            {
                if (clearFirst)
                {
                    // We clear items individually rather than with DataApi.DeleteDataItems() so that
                    // other clients will get notified of each deletion. Note also that we don't delete
                    // items for which we're sending a new value.
                    var newKeys = new HashSet<string>(dataQueue.Keys);
                    foreach (IDataItem item in await WearableClass.DataApi.GetDataItemsAsync(googleApiClient).ConfigureAwait(false))
                    {
                        if (item.Uri.Path.StartsWith(DATA_SETTINGS_PATH) && !newKeys.Contains(item.Uri.LastPathSegment))
                        {
                            await WearableClass.DataApi.DeleteDataItemsAsync(googleApiClient, item.Uri).ConfigureAwait(false);
                        }
                    }
                }

                if (dataQueue.Count > 0)
                {
                    // Create a data request to relay settings.
                    var path = DATA_SETTINGS_PATH;
                    if (dataQueue.Count == 1)
                    {
                        var next = string.Empty;
                        using (IEnumerator<string> enumerator = dataQueue.Keys.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                next = enumerator.Current;
                            }
                        }

                        // Only one key being sent, so add it to the URI path.
                        path += '/' + next;
                    }
                    var dataMapReq = PutDataMapRequest.Create(path);
                    DataMap dataMap = dataMapReq.DataMap;

                    if (clearFirst)
                        dataMap.PutLong(KEY_TIMESTAMP, DateTime.Now.Ticks);

                    // Add the settings.
                    lock (this)
                    {
                        foreach (var key in dataQueue.Keys)
                        {
                            dataQueue.TryGetValue(key, out var value);
                            if (value == null)
                            {
                                dataMap.Remove(key);
                                continue;
                            }

                            if (value is bool)
                            {
                                dataMap.PutBoolean(key, (bool)value);
                            }
                            else if (value is float)
                            {
                                dataMap.PutFloat(key, (float)value);
                            }
                            else if (value is int)
                            {
                                dataMap.PutInt(key, (int)value);
                            }
                            else if (value is long)
                            {
                                dataMap.PutLong(key, (long)value);
                            }
                            else if (value is string)
                            {
                                dataMap.PutString(key, (string)value);
                            }
                            else if (value is JavaCollection collection)
                            {
                                var stringArray = new string[collection.Count];
                                ((JavaCollection)value).CopyTo(stringArray, 0);

                                dataMap.PutStringArray(key, stringArray);
                            }
                            else
                            {
                                // Invalid cast
                            }
                        }
                        dataQueue.Clear();
                    }

                    // Ship it!
                    await WearableClass.DataApi.PutDataItemAsync(googleApiClient, dataMapReq.AsPutDataRequest().SetUrgent()).ConfigureAwait(false); // ensure that it arrives in a timely manner.
                }

                clearFirst = false;
            }

            #endregion
        }

        #endregion
    }
}