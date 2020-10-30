using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Lang;
using Exception = Java.Lang.Exception;

namespace ModulacionDigital.Droid.Modulacion.Audio
{
    public class HeadsetManager
    {
        private const int WIRED_HEADSET_SOURCE = (int)Stream.Music;
        private const int BLUETOOTH_HEADSET_SOURCE = 6;
        // Undocumented "6" instead of STREAM_VOICE_CALL:
        // http://stackoverflow.com/questions/4472613/android-bluetooth-earpiece-volume

        // Another undocumented Bluetooth constants:
        // http://www.netmite.com/android/mydroid/2.0/frameworks/base/core/java/android/bluetooth/BluetoothHeadset.java
        internal const string ACTION_BLUETOOTH_STATE_CHANGED = "android.bluetooth.headset.action.STATE_CHANGED";
        internal const string BLUETOOTH_STATE = "android.bluetooth.headset.extra.STATE";
        internal const int BLUETOOTH_STATE_ERROR = -1;
        internal const int BLUETOOTH_STATE_DISCONNECTED = 0;
        internal const int BLUETOOTH_STATE_CONNECTING = 1;
        internal const int BLUETOOTH_STATE_CONNECTED = 2;

        private readonly Context context;
        private readonly AudioManager audioManager;

        private HeadsetManagerListener listener = null;
        private BroadcastReceiver headsetDetector = null;

        public HeadsetManager(Context context)
        {
            this.context = context;

            audioManager = (AudioManager)
                context.GetSystemService(Context.AudioService);
        }

        public void setListener(HeadsetManagerListener listener)
        {
            this.listener = listener;
        }

        public void restoreVolumeLevel(HeadsetMode headsetMode)
        {
            int source;

            switch (headsetMode)
            {
                case HeadsetMode.WIRED_HEADPHONES:
                case HeadsetMode.WIRED_HEADSET:
                    source = WIRED_HEADSET_SOURCE;
                    break;
                case HeadsetMode.BLUETOOTH_HEADSET:
                    source = BLUETOOTH_HEADSET_SOURCE;
                    break;
                default:
                    //   new Utils(context).log(new IOException("Unknown HeadsetMode!"));
                    source = WIRED_HEADSET_SOURCE;
                    break;
            }

            try
            {
                // VOL = VOL% * (MAX / 100)
                double volumeLevel = audioManager
                    .GetStreamMaxVolume((Stream)source) / 100D;
                volumeLevel *= new Preferences(context).getVolumeLevel(headsetMode);

                audioManager.SetStreamVolume(
                        (Stream)source,
                        (int)Java.Lang.Math.Round(volumeLevel),
                        // Display the volume dialog
                        // AudioManager.FLAG_SHOW_UI);
                        // Display nothing
                        VolumeNotificationFlags.RemoveSoundAndVibrate);
            }
            catch (Exception exception)
            {
                new Utils(context).log("Cannot set audio stream volume!");
                new Utils(context).log(exception);
            }
        }

        public bool isWiredHeadsetOn()
        {
            return audioManager.WiredHeadsetOn;
        }

        public bool isBluetoothHeadsetOn()
        {
            bool isHeadsetConnected = false;

            try
            {
                BluetoothAdapter adapter = BluetoothAdapter.DefaultAdapter;
                if (adapter != null && adapter.IsEnabled)
                {
                    ICollection<BluetoothDevice> devices = adapter.BondedDevices;

                    isHeadsetConnected = devices != null
                        && devices.Count() > 0;

                    // TODO: Check device classes, what sort of devices it is
                }
            }
            catch (Exception exception)
            {
                new Utils(context).log(exception);
            }

            return isHeadsetConnected
                && audioManager.IsBluetoothScoAvailableOffCall;
        }

        public bool isBluetoothScoOn()
        {
            return audioManager.BluetoothScoOn;
        }

        public void setBluetoothScoOn(bool on)
        {
            if (audioManager.BluetoothScoOn == on) return;

            if (on)
            {
                new Utils(context).log("Starting Bluetooth SCO.");
                audioManager.StartBluetoothSco();
            }
            else
            {
                new Utils(context).log("Stopping Bluetooth SCO.");
                audioManager.StopBluetoothSco();
            }
        }

        /**
		 * Waits until Bluetooth SCO becomes available.
		 **/
        public bool waitForBluetoothSco()
        {
            long timeout = 1000 * 3;
            long idlePeriod = 50;

            long start = SystemClock.ElapsedRealtime();
            long end = start;

            while (!audioManager.BluetoothScoOn)
            {
                end = SystemClock.ElapsedRealtime();

                if (end - start > timeout)
                {
                    return false;
                }

                try
                {
                    Thread.Sleep(idlePeriod);
                }
                catch (InterruptedException exception)
                {
                }
            }

            new Utils(context).log(
                "Waited %s ms for Bluetooth SCO.",
                end - start);

            return true;
        }

        public void registerHeadsetDetector()
        {
            if (headsetDetector == null)
            {
                headsetDetector = new Reciver(this.listener);

                IntentFilter filter = new IntentFilter();
                {
                    filter.AddAction(Intent.ActionHeadsetPlug);
                    filter.AddAction(ACTION_BLUETOOTH_STATE_CHANGED);

                    // Build.VERSION.SDK_INT < 14
                    // filter.addAction(AudioManager.ACTION_SCO_AUDIO_STATE_CHANGED);
                }

                context.RegisterReceiver(headsetDetector, filter);
            }
        }
        public void unregisterHeadsetDetector()
        {
            if (headsetDetector != null)
            {
                context.UnregisterReceiver(headsetDetector);
                headsetDetector = null;
            }
        }
    }
    public class Reciver : BroadcastReceiver
    {
        private HeadsetManagerListener listener = null;
        public Reciver(HeadsetManagerListener listener)
        {
            this.listener = listener;
        }

        public override void OnReceive(Context context, Intent intent)
        {

            // WIRED HEADSET BROADCAST

            bool isWiredHeadsetBroadcast = intent.Action
                .Equals(Intent.ActionHeadsetPlug);

            if (isWiredHeadsetBroadcast)
            {
                bool isWiredHeadsetPlugged =
                    intent.GetIntExtra("state", 0) == 1;

                if (isWiredHeadsetPlugged)
                {
                    new Utils(context).log(
                        "Wired headset plugged.");
                }
                else
                {
                    new Utils(context).log(
                        "Wired headset unplugged.");

                    if (listener != null)
                    {
                        listener.onWiredHeadsetOff();
                    }
                }

                // TODO: Maybe handle the microphone indicator too
            }

            // BLUETOOTH HEADSET BROADCAST

            bool isBluetoothHeadsetBroadcast = intent.Action
                .Equals(HeadsetManager.ACTION_BLUETOOTH_STATE_CHANGED);

            if (isBluetoothHeadsetBroadcast)
            {
                int bluetoothHeadsetState = intent.GetIntExtra(
                    HeadsetManager.BLUETOOTH_STATE,
                    HeadsetManager.BLUETOOTH_STATE_ERROR);

                switch (bluetoothHeadsetState)
                {
                    case HeadsetManager.BLUETOOTH_STATE_CONNECTING:
                    case HeadsetManager.BLUETOOTH_STATE_CONNECTED:
                        new Utils(context).log(
                            "Bluetooth headset connecting or connected.");
                        break;
                    case HeadsetManager.BLUETOOTH_STATE_DISCONNECTED:
                    case HeadsetManager.BLUETOOTH_STATE_ERROR:
                    default:
                        new Utils(context).log(
                            "Bluetooth headset disconnected or error.");
                        if (listener != null)
                        {
                            listener.onBluetoothHeadsetOff();
                        }
                        break;
                }
            }

            // BLUETOOTH SCO BROADCAST (Build.VERSION.SDK_INT < 14)

            /*
            boolean isBluetoothScoBroadcast = intent.getAction()
                .equals(AudioManager.ACTION_SCO_AUDIO_STATE_CHANGED);

            if (isBluetoothScoBroadcast)
            {
                int bluetoothScoState = intent.getIntExtra(
                    AudioManager.EXTRA_SCO_AUDIO_STATE,
                    AudioManager.SCO_AUDIO_STATE_ERROR);

                switch (bluetoothScoState)
                {
                case AudioManager.SCO_AUDIO_STATE_CONNECTED:
                    new Utils(context).log(
                        "Bluetooth SCO connected.");
                    break;
                case AudioManager.SCO_AUDIO_STATE_DISCONNECTED:
                case AudioManager.SCO_AUDIO_STATE_ERROR:
                    new Utils(context).log(
                        "Bluetooth SCO disconnected or error.");
                    break;
                }
            }
            */

        }
    }
}