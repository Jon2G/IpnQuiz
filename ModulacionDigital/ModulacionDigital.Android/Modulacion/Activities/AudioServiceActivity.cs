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

namespace ModulacionDigital.Droid.Modulacion.Activities
{
	public abstract class AudioServiceActivity<T extends AudioService> extends
	GDActivity
{
	// Service parameters:
	private final Class<T>		serviceClass;
	private T					serviceInstance		= null;
	private Context				serviceContext		= null;
	private Intent				serviceIntent		= null;
	private ServiceConnection	serviceConnection	= null;

	protected AudioServiceActivity(Class<T> serviceClass)
	{
		this.serviceClass = serviceClass;
	}

	@Override
	protected void onCreate(Bundle savedInstanceState)
	{
		super.onCreate(savedInstanceState);

		addActionBarItem(ActionBarItem.Type.Settings);
	}

	protected T getService()
	{
		new Utils(this).assertTrue(serviceInstance != null,
			"Service %s wasn't properly instantiated!",
			serviceClass.getName());

		return serviceInstance;
	}

	private void startService()
	{
		if (serviceContext == null)
		{
			serviceContext = this.getApplicationContext();
		}

		if (serviceIntent == null)
		{
			serviceIntent = new Intent(
				serviceContext,
				serviceClass);
		}

		if (!new Utils(serviceContext).isServiceRunning(serviceClass))
		{
			if (startService(serviceIntent) == null)
			{
				new Utils(this).toast("Unable to start audio service %s!",
					serviceClass.getName());
			}
		}
	}

	private void stopService()
	{
		if (serviceIntent != null)
		{
			stopService(serviceIntent);
		}
	}

	private void bindService()
	{
		if (serviceConnection == null)
		{
			serviceConnection = new ServiceConnection()
			{
				public void onServiceConnected(ComponentName component, IBinder _binder)
				{
					@SuppressWarnings("unchecked")
					ServiceBinder<T> binder = (ServiceBinder<T>) _binder;
					serviceInstance = binder.getServiceInstance();

					// Notify the subclass
					AudioServiceActivity.this.onServiceConnected();
				}

				public void onServiceDisconnected(ComponentName component)
				{
					// Notify the subclass
					AudioServiceActivity.this.onServiceDisconnected();
					serviceInstance = null;
				}
			};
		}

		if (serviceInstance == null)
		{
			if (!bindService(serviceIntent,
				serviceConnection, Context.BIND_AUTO_CREATE))
			{
				new Utils(this).toast("Unable to bind service %s!",
					serviceClass.getName());
			}
		}
	}

	private void unbindService()
	{
		if (serviceConnection != null)
		{
			unbindService(serviceConnection);

			// Notify the subclass
			onServiceDisconnected();
			serviceInstance = null;
		}
	}

	/**
	 * The reference to the service instance is now available.
	 */
	protected void onServiceConnected()
	{
	}

	/**
	 * The reference to the service instance is no longer available.
	 */
	protected void onServiceDisconnected()
	{
	}

	@Override
	protected void onResume()
	{
		super.onResume();

		startService();
		bindService();
	}

	@Override
	protected void onPause()
	{
		super.onPause();

		unbindService();
	}

	@Override
	public boolean onHandleActionBarItemClick(ActionBarItem item, int position)
	{
		// Show preference activity
		if (position == 0)
		{
			Intent intent = new Intent(this, PreferenceActivity.class);
			intent.putExtra("caller", this.getClass().getName());
			startActivity(intent);
		}

		return super.onHandleActionBarItemClick(item, position);
	}
}

}