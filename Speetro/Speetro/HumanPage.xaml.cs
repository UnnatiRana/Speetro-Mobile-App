using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Plugin.DeviceSensors;
using System.Diagnostics;
using Plugin.Geolocator;
using Plugin.Geolocator.Abstractions;
using Xamarin.Forms.Maps;
using GeoCoordinatePortable;

namespace Speetro
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class HumanPage : ContentPage
    {
        bool walking = false; // state whether walking or not.
        Stopwatch timer = new Stopwatch(); // timer for walking.

        // current number of steps.
        int step = 0;
        // last step position in ysValues list.
        int lastStepPos = 0;
        // current discovered step positions in following list.
        List<int> stepPoses = new List<int>(); 
        // current received y-Position & smoothed y-Position of phone.
        private List<double> yValues = new List<double>();
        private List<double> ysValues = new List<double>();

        // units for distance & speed.
        string[] distUnits = { "m", "km" }; 
        string[] speedUnitLabels = { "m/s", "km/h" };

        // current speed & distance, final average speed in meter.
        double curSpeed = 0;
        double totalDist = 0;
        double avrSpeed = 0;

        public HumanPage()
        {
            InitializeComponent();

            Title = "Human Page";

            initSpeedUnits();
        }

        // init picker for selecting units of speed.
        private void initSpeedUnits()
        {
            // set unit as m/s
            pckUnit.SelectedIndex = 0;
            foreach (var label in speedUnitLabels)
            {
                pckUnit.Items.Add(label);
                pckUnit.SelectedIndex = 0;
            }
        }

        // update dist&speed label
        private void updateLabels()
        {
            var speed = curSpeed;
            var dist = totalDist;
            if (pckUnit.SelectedIndex == 1)
            {
                speed = curSpeed / 1000 * 3600;
                dist = totalDist / 1000;
            }

            lblDistance.Text = $"{dist:0.0#}";
            lblSpeed.Text = $"{speed:0.0#}";
        }

        // show report for journey
        private void showReport()
        {
            double dist = totalDist;
            if (pckUnit.SelectedIndex == 1)
            {
                dist = totalDist / 1000;
            }
            DisplayAlert("Journey Report",
                $"Your journey finished.\nDistance : {dist:0.0#}{distUnits[pckUnit.SelectedIndex]}\nAverage speed : {avrSpeed:0.0#}{pckUnit.SelectedItem}" +
                $"\nTotal Step Taken : {lblSteps.Text}\nTaken Time: {timer.Elapsed.Minutes}m {timer.Elapsed.Seconds%60}s",
                "Close");
        }

        // Start/Stop button handler
        private void startStopJourney_Clicked(object sender, EventArgs e)
        {
            walking = !walking;
            startStopJourney.Text = walking ? "Stop Journey!" : "Start Journey!";

            if (walking) //when click Start
            {
                startJourney();
            } else
            {
                //Stop timer
                timer.Stop();
                //Stop accelerometer sensor reading.
                CrossDeviceSensors.Current.Accelerometer.StopReading();
            }
        }

        // Start GPS tracking for measure of distance & speed.
        private void startGPSTracking()
        {
            Task.Run(async () =>
            {
                var locator = CrossGeolocator.Current;
                locator.DesiredAccuracy = 100;
                var lastPosition = await locator.GetPositionAsync(TimeSpan.FromMilliseconds(1000));
                double lastTime = 0;
                while (walking)
                {
                    var nowTime = timer.Elapsed.TotalSeconds;
                    var position = await locator.GetPositionAsync(TimeSpan.FromMilliseconds(1000));

                    var FromLocA = new GeoCoordinate(position.Latitude, position.Longitude);
                    var ToLocB = new GeoCoordinate(lastPosition.Latitude, lastPosition.Longitude);
                    var dist = FromLocA.GetDistanceTo(ToLocB);
                    if (dist == 0)
                    {
                        continue;
                    }

                    curSpeed = dist / (nowTime - lastTime);
                    totalDist += dist;
                    lastTime = nowTime;

                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                    {
                        updateLabels();
                    });
                    lastPosition = position;
                    System.Threading.Thread.Sleep(1000);
                }
                avrSpeed = totalDist / timer.Elapsed.TotalSeconds;
                if (pckUnit.SelectedIndex == 1)
                {
                    avrSpeed = avrSpeed / 1000 * 3600;
                }
                Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                {
                    showReport();
                });
            }).ConfigureAwait(false);
        }

        // Start tracking of steps using accelerator sensor.
        private void addStepTracking()
        {
            if (CrossDeviceSensors.Current.Accelerometer.IsSupported)
            {
                CrossDeviceSensors.Current.Accelerometer.OnReadingChanged += (s, a) => {
                    double v = a.Reading.Y;
                    if (yValues.Count == 0 || Math.Abs(v - yValues[0]) > 0.1)
                    {
                        double y = a.Reading.Y;
                        double py = yValues.Count > 0 ? yValues[0] : 0;
                        double sp = 0.12; // smooth param
                        double sy = y * (1 - sp) + py * sp;
                        int take = 20;
                        if ((ysValues.Count - lastStepPos) > take)
                        {
                            double maxy = -10000, miny = 10000;
                            int maxi = -1, mini = -1;
                            for (int i = 0; i < take; i++)
                            {
                                if (ysValues[i] > maxy)
                                {
                                    maxi = i;
                                    maxy = ysValues[i];
                                }
                                if (ysValues[i] < miny)
                                {
                                    mini = i;
                                    miny = ysValues[i];
                                }
                            }
                            if (Math.Abs(maxi - mini) < take / 2 && Math.Abs(maxy - miny) > 2)
                            {
                                int posi = mini;
                                if (mini < 2)
                                {
                                    posi = maxi;
                                }
                                else if (maxi < 2)
                                {
                                    posi = mini;
                                }
                                else
                                {
                                    posi = Math.Min(maxi, mini);
                                }
                                lastStepPos = ysValues.Count - posi;
                                stepPoses.Add(lastStepPos);
                                lastStepPos = ysValues.Count;

                                Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                                {
                                    lblSteps.Text = ((int)(stepPoses.Count)).ToString();
                                });
                            }
                        }

                        yValues.Insert(0, a.Reading.Y);
                        ysValues.Insert(0, sy);
                        //yValuesOutput.Text = walkingLog;
                    }
                };
                CrossDeviceSensors.Current.Accelerometer.StartReading();
            }
        }

        private void startJourney()
        {
            totalDist = 0;
            step = 0;
            lastStepPos = 0;
            stepPoses.Clear();
            ysValues.Clear();
            yValues.Clear();
            timer.Restart();
            lblSteps.Text = "0";

            totalDist = 0;
            curSpeed = 0;
            updateLabels();
            startButtonEffect();

            startGPSTracking();
            addStepTracking();
        }

        // Start button border gradient rotation effect.
        private void startButtonEffect()
        {
            Task.Run(async () =>
            {
                var rot = 0;
                while (walking)
                {
                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                    {
                        rot += 10;
                        if (rot >= 360)
                        {
                            rot = 0;
                        }
                        testBtn.BorderGradientAngle = rot;
                    });
                    System.Threading.Thread.Sleep(100);
                }
            }).ConfigureAwait(false);
        }

        // Speed unit selection picker change handler.
        private void pckUnit_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update distance & speed.
            updateLabels();
        }
    }
}