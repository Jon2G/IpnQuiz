using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using ModulacionDigital.Droid.Modulacion.Audio;
namespace ModulacionDigital.Droid.Servicios
{
    public abstract class AudioService : Android.App.Service,
    HeadsetManagerListener, Android.Content.ISharedPreferencesOnSharedPreferenceChangeListener
    {
        // Internal variables:
        protected Preferences preferences = null;
        private ServiceListener listener = null;

        // Headset stuff:
        private Boolean bluetoothHeadsetSupport = null;
        private Boolean internalMicSupport = null;
        private HeadsetMode actualHeadsetMode = null;
        private HeadsetMcanager headsetManager = null;
        private AudioDeviceManager deviceManager = null;

        // Audio thread and its parameters:
        private AudioThread thread = null;
        private String threadName = null;
        private String threadPreferences = null;

        // Audio I/O devices:
        private AudioDevice input = null;
        private AudioDevice output = null;

        public void setListener(ServiceListener listener)
        {
            this.listener = listener;
        }

        public boolean isBluetoothHeadsetSupportOn()
        {
            if (bluetoothHeadsetSupport == null)
            {
                bluetoothHeadsetSupport = preferences.isBluetoothHeadsetSupportOn();
            }

            return bluetoothHeadsetSupport.booleanValue();
        }

        public void setBluetoothHeadsetSupport(boolean bluetoothHeadsetSupport)
        {
            this.bluetoothHeadsetSupport = bluetoothHeadsetSupport;
            preferences.setBluetoothHeadsetSupport(bluetoothHeadsetSupport);
        }

        public boolean isInternalMicSupportOn()
        {
            if (internalMicSupport == null)
            {
                internalMicSupport = preferences.isInternalMicSupportOn();
            }

            return internalMicSupport.booleanValue();
        }

        public void setInternalMicSupport(boolean internalMicSupport)
        {
            this.internalMicSupport = internalMicSupport;
            preferences.setInternalMicSupport(internalMicSupport);
        }

        public HeadsetMode getActualHeadsetMode()
        {
            return actualHeadsetMode;
        }

        private void setActualHeadsetMode(HeadsetMode mode)
        {
            actualHeadsetMode = mode;
        }

        protected abstract AudioThread createAudioThread(AudioDevice input, AudioDevice output);

        public String getThreadName()
        {
            return threadName;
        }

        public void setThreadName(String threadName)
        {
            this.threadName = threadName;
        }

        public String getThreadPreferences()
        {
            return threadPreferences;
        }

        public boolean hasThreadPreferences()
        {
            return threadPreferences != null && threadPreferences.length() > 0;
        }

        public void setThreadPreferences(String threadPreferences)
        {
            this.threadPreferences = threadPreferences;

            if (thread != null)
            {
                thread.configure(threadPreferences);
            }
        }

        public boolean isThreadRunning()
        {
            return (thread != null) && thread.isRunning();
        }

        public void startThread()
        {
            if (isThreadRunning()) return;

            if (isBluetoothHeadsetSupportOn())
            {
                setActualHeadsetMode(HeadsetMode.BLUETOOTH_HEADSET);
            }
            else if (isInternalMicSupportOn())
            {
                setActualHeadsetMode(HeadsetMode.WIRED_HEADPHONES);
            }
            else
            {
                setActualHeadsetMode(HeadsetMode.WIRED_HEADSET);
            }

            new Utils(this).log(
                "The actual headset mode is %s.",
                getActualHeadsetMode().toString());

            // Fallback to the wired mode if Bluetooth headset mode is set
            // but no device available or Bluetooth initialization fails
            if (getActualHeadsetMode() == HeadsetMode.BLUETOOTH_HEADSET)
            {
                if (headsetManager.isBluetoothHeadsetOn())
                {
                    if (!headsetManager.isBluetoothScoOn())
                    {
                        headsetManager.setBluetoothScoOn(true);

                        if (!headsetManager.waitForBluetoothSco())
                        {
                            headsetManager.setBluetoothScoOn(false);

                            setActualHeadsetMode(isInternalMicSupportOn()
                                ? HeadsetMode.WIRED_HEADPHONES
                                : HeadsetMode.WIRED_HEADSET);

                            new Utils(this).log(
                                "Fallback to headset mode %s.",
                                getActualHeadsetMode().toString());
                        }
                    }
                }
                else
                {
                    setActualHeadsetMode(isInternalMicSupportOn()
                        ? HeadsetMode.WIRED_HEADPHONES
                        : HeadsetMode.WIRED_HEADSET);

                    new Utils(this).log(
                        "Fallback to headset mode %s.",
                        getActualHeadsetMode().toString());
                }
            }

            // Return if wired headset mode is actually set but no device available
            if (getActualHeadsetMode() != HeadsetMode.BLUETOOTH_HEADSET
                    && !headsetManager.isWiredHeadsetOn())
            {
                if (listener != null)
                {
                    listener.onServiceFailed(ServiceFailureReason.InitialHeadsetAvailability);
                }

                return;
            }

            // Return if audio device initialization fails
            if (!initAudioDevices(getActualHeadsetMode()))
            {
                if (listener != null)
                {
                    listener.onServiceFailed(ServiceFailureReason.AudioDeviceInitialization);
                }

                return;
            }

            if (preferences.isForceVolumeLevelOn())
            {
                headsetManager.restoreVolumeLevel(getActualHeadsetMode());
            }

            thread = createAudioThread(input, output);
            thread.configure(threadPreferences);
            thread.start();
        }

        public void stopThread()
        {
            stopThread(false);
        }

        public void stopThread(boolean restarting)
        {
            if (isThreadRunning()) thread.stop();

            if (!restarting)
            {
                new Utils(this).cancelAllNotifications();
            }

            if (headsetManager.isBluetoothScoOn())
            {
                headsetManager.setBluetoothScoOn(false);
            }

            if (threadName != null && threadName.length() > 0)
            {
                preferences.setAudioThreadPreferences(
                        threadName, threadPreferences);
            }

            if (thread != null)
            {
                thread.dispose();
                thread = null;
            }

            disposeAudioDevices();
        }

        private boolean initAudioDevices(HeadsetMode mode)
        {
            new Utils(this).log("Initialising audio devices.");

            try
            {
                if (input == null)
                {
                    input = deviceManager.getInputDevice(mode);
                }

                if (output == null)
                {
                    output = deviceManager.getOutputDevice(mode);
                }
            }
            catch (IOException exception)
            {
                new Utils(this).log(exception);
                return false;
            }

            return true;
        }

        private void disposeAudioDevices()
        {
            new Utils(this).log("Disposing audio devices.");

            if (input != null)
            {
                input.dispose();
                input = null;
            }

            if (output != null)
            {
                output.dispose();
                output = null;
            }
        }

        /**
         * Shows notification, if the frontend activity become invisible while
         * thread is running.
         * */
        public void setActivityVisible(boolean isActivityVisible, Class<?> activityClass)
        {
            if (isActivityVisible)
            {
                new Utils(this).cancelAllNotifications();
            }
            else if (isThreadRunning())
            {
                new Utils(this).postNotification(
                    R.drawable.notification,
                    getString(R.string.ApplicationName),
                    getString(R.string.ServiceNotificationTitle),
                    getString(R.string.ServiceNotificationSubtitle),
                    activityClass);
            }
        }

        @Override
    public int onStartCommand(Intent intent, int flags, int startId)
        {
            return START_STICKY;
        }

        @Override
    public void onCreate()
        {
            new Utils(this).log("Creating audio service.");

            super.onCreate();

            preferences = new Preferences(getApplicationContext());
            preferences.registerOnSharedPreferenceChangeListener(this);

            if (headsetManager == null)
            {
                headsetManager = new HeadsetManager(this.getApplicationContext());
                headsetManager.setListener(this);
                headsetManager.registerHeadsetDetector();
            }

            if (deviceManager == null)
            {
                deviceManager = new AudioDeviceManager(this.getApplicationContext());
            }
        }

        @Override
    public void onDestroy()
        {
            new Utils(this).log("Destroying audio service.");

            preferences.unregisterOnSharedPreferenceChangeListener(this);

            stopThread(false);

            if (headsetManager != null)
            {
                headsetManager.unregisterHeadsetDetector();
                headsetManager.setListener(null);
                headsetManager = null;
            }

            if (deviceManager != null)
            {
                deviceManager = null;
            }

            disposeAudioDevices();

            super.onDestroy();
        }

        @Override
    public void onSharedPreferenceChanged(SharedPreferences sharedPreferences, String key)
        {
            new Utils(this).log("Preferences changed, so reinitialising audio service.");

            if (isThreadRunning())
            {
                stopThread(true);
                disposeAudioDevices();
                startThread();
            }
            else
            {
                disposeAudioDevices();
            }
        }

        public void onWiredHeadsetOff()
        {
            if (getActualHeadsetMode() != HeadsetMode.BLUETOOTH_HEADSET
                && isThreadRunning())
            {
                stopThread(false);

                if (listener != null)
                {
                    listener.onServiceFailed(ServiceFailureReason.WiredHeadsetAvailability);
                }
            }
        }

        public void onBluetoothHeadsetOff()
        {
            if (getActualHeadsetMode() == HeadsetMode.BLUETOOTH_HEADSET
                && isThreadRunning())
            {
                stopThread(false);

                if (listener != null)
                {
                    listener.onServiceFailed(ServiceFailureReason.BluetoothHeadsetAvailability);
                }
            }
        }
    }
}