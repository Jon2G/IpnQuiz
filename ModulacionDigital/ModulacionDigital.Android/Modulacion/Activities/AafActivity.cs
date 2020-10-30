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

import java.beans.PropertyChangeEvent;
import java.beans.PropertyChangeListener;

import android.os.Bundle;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.CheckBox;
import android.widget.CompoundButton;
import android.widget.CompoundButton.OnCheckedChangeListener;
import de.jurihock.voicesmith.AAF;
import de.jurihock.voicesmith.R;
import de.jurihock.voicesmith.Utils;
import de.jurihock.voicesmith.services.AafService;
import de.jurihock.voicesmith.services.ServiceFailureReason;
import de.jurihock.voicesmith.services.ServiceListener;
import de.jurihock.voicesmith.widgets.AafPicker;
import de.jurihock.voicesmith.widgets.ColoredToggleButton;
import de.jurihock.voicesmith.widgets.IntervalPicker;

public sealed class AafActivity : AudioServiceActivity<AafService>:
	PropertyChangeListener, OnClickListener,
	OnCheckedChangeListener, ServiceListener
{
	// Relevant activity widgets:
private AafPicker viewAafPicker = null;
private IntervalPicker viewIntervalPicker = null;
private CheckBox viewBluetoothHeadset = null;
private CheckBox viewInternalMic = null;
private ColoredToggleButton viewStartStopButton = null;

public AafActivity()
{
    super(AafService.class);
	}

	/**
	 * Initializes the activity, its layout and widgets.
	 * */
	@Override
    protected void onCreate(Bundle savedInstanceState)
{
    super.onCreate(savedInstanceState);
    this.setActionBarContentView(R.layout.aaf);

    viewAafPicker = (AafPicker)this.findViewById(R.id.viewAafPicker);
    viewAafPicker.setPropertyChangeListener(this);

    viewIntervalPicker = (IntervalPicker)this.findViewById(
        R.id.viewIntervalPicker);
    viewIntervalPicker.setPropertyChangeListener(this);

    viewBluetoothHeadset = (CheckBox)this.findViewById(
        R.id.viewBluetoothHeadset);
    viewBluetoothHeadset.setOnCheckedChangeListener(this);

    viewInternalMic = (CheckBox)this.findViewById(
            R.id.viewInternalMic);
    viewInternalMic.setOnCheckedChangeListener(this);

    viewStartStopButton = (ColoredToggleButton)this.findViewById(
        R.id.viewStartStopButton);
    viewStartStopButton.setOnClickListener(this);
}

@Override
    protected void onServiceConnected()
{
    new Utils(this).log("AafActivity founds the audio service.");

    getService().setActivityVisible(true, this.getClass());
    getService().setListener(this);

    // Update widgets
    viewAafPicker.setAaf(getService().getAaf());
    viewBluetoothHeadset.setChecked(getService().isBluetoothHeadsetSupportOn());
    viewInternalMic.setChecked(getService().isInternalMicSupportOn());
    viewStartStopButton.setChecked(getService().isThreadRunning());

    if (getService().getAaf() == AAF.FAF)
    {
        viewIntervalPicker.setVisibility(View.VISIBLE);

        if (getService().hasThreadPreferences())
        {
            int interval = Integer.parseInt(getService().getThreadPreferences());
            viewIntervalPicker.setInterval(interval);
        }
    }
    else
    {
        viewIntervalPicker.setVisibility(View.GONE);
    }
}

@Override
    protected void onServiceDisconnected()
{
    new Utils(this).log("AafActivity losts the audio service.");

    if (!this.isFinishing())
    {
        getService().setActivityVisible(false, this.getClass());
    }

    getService().setListener(null);
}

public void onClick(View view)
{
    if (getService().isThreadRunning())
    {
        if (viewStartStopButton.isChecked())
            viewStartStopButton.setChecked(false);

        getService().stopThread(false);
    }
    else
    {
        if (!viewStartStopButton.isChecked())
            viewStartStopButton.setChecked(true);

        getService().startThread();
    }

    // BZZZTT!!1!
    viewStartStopButton.performHapticFeedback(0);
}

public void onCheckedChanged(CompoundButton view, boolean value)
{
    if (view == viewBluetoothHeadset)
    {
        if (getService().isBluetoothHeadsetSupportOn() != value)
        {
            getService().setBluetoothHeadsetSupport(value);
        }
    }
    else if (view == viewInternalMic)
    {
        if (getService().isInternalMicSupportOn() != value)
        {
            getService().setInternalMicSupport(value);
        }
    }
}

@Override
    public void propertyChange(PropertyChangeEvent event)
{
    if (event.getSource().equals(viewAafPicker))
		{
    AAF aaf = viewAafPicker.getAaf();

    getService().setAaf(aaf);

    if (aaf == AAF.FAF)
    {
        viewIntervalPicker.setVisibility(View.VISIBLE);

        if (getService().hasThreadPreferences())
        {
            int interval = Integer.parseInt(getService().getThreadPreferences());
            viewIntervalPicker.setInterval(interval);
        }
    }
    else
    {
        viewIntervalPicker.setVisibility(View.GONE);
    }
}

        else if (event.getSource().equals(viewIntervalPicker))

        {
    int interval = viewIntervalPicker.getInterval();
    getService().setThreadPreferences(Integer.toString(interval));
}
}

public void onServiceFailed(ServiceFailureReason reason)
{
    if (viewStartStopButton.isChecked())
        viewStartStopButton.setChecked(false);

    new Utils(this).toast(getString(R.string.ServiceFailureMessage));

    // BZZZTT!!1!
    viewStartStopButton.performHapticFeedback(0);
}
}

namespace ModulacionDigital.Droid.Modulacion.Activities
{
    class AafActivity
    {
    }
}
