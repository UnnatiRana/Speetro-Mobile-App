using GeoCoordinatePortable;
using Plugin.Geolocator;
using Plugin.SimpleAudioPlayer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Maps;
using Xamarin.Forms.Xaml;

namespace Speetro
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Automobile : ContentPage
    {
        //state whether automobile run or not.
        int running = 0;

        //labels of speed & distance units, add more labels here.
        string[] distUnitLabels = { "m", "km" };
        string[] speedUnitLabels = {"m/s", "km/h"};

        //current distance & speed in meter & m/s
        double curDist = 0;
        double curSpeed = 0;
        //average speed
        double avrSpeed = 0;

        //from/to address
        string fromAddr = "";
        string toAddr = "";

        //timer for measure time of journey
        Stopwatch timer = new Stopwatch();

        //beep sound player
        private ISimpleAudioPlayer _simpleAudioPlayer;

        public Automobile()
        {
            InitializeComponent();
            // set title of page
            Title = "Automobile";

            // init speed units of current/average speed and speed limit.
            initSpeedUnits();
            // locate google map to locate current position.
            updateGPSLocation();

            _simpleAudioPlayer = CrossSimpleAudioPlayer.CreateSimpleAudioPlayer();
            Stream beepStream = GetType().Assembly.GetManifestResourceStream("Speetro.alarm.wav");
            bool isSuccess = _simpleAudioPlayer.Load(beepStream);
        }

        // initialize units of speed and 
        private void initSpeedUnits()
        {
            foreach (var label in speedUnitLabels)
            {
                pckUnit.Items.Add(label);
                pckUnit.SelectedIndex = 0;
            }
        }

        // update gps vision region to current location
        private void updateGPSLocation()
        {
            Task.Run(async () =>
            {
                var locator = CrossGeolocator.Current;
                var position = await locator.GetPositionAsync(TimeSpan.FromMilliseconds(1000));

                Device.StartTimer(TimeSpan.FromMilliseconds(500), () =>
                {
                    map.MoveToRegion(MapSpan.FromCenterAndRadius(new Position(position.Latitude, position.Longitude),
                                                                 Distance.FromMiles(1)));
                    return false;
                });
            }).ConfigureAwait(false);
        }

        // update current distance and speed as well as label controls.
        private void updateDistAndSpeed(double totalDist, double deltaDist, double seconds)
        {
            curDist = totalDist;
            curSpeed = deltaDist / seconds;
            updateLabels();
        }

        // update labels with current distance & speed & status(speed over alert,..),  refer current selected unit.
        private void updateLabels()
        {
            double dist = curDist;
            double speed = curSpeed;
            if (pckUnit.SelectedIndex == 1) // km/h
            {
                dist = curDist / 1000.0;
                speed = curSpeed / 1000.0 * 3600;
            }
            lblDistance.Text = $"{dist:0.0#}";
            lblSpeed.Text = $"{speed:0.0#}";

            if (speed == 0)
            {
                icoSpeedOver.Text = "-";
                icoSpeedOver.TextColor = Color.LightSkyBlue;
                lblStatus.TextColor = Color.LightSkyBlue;
                lblStatus.Text = "Not running";
            } else if (speed > Int32.Parse(edtSpeedLimit.Text))
            {
                icoSpeedOver.TextColor = Color.Red;
                icoSpeedOver.Text = "!";
                lblStatus.TextColor = Color.Red;
                lblStatus.Text = "Speed Over";
                showAlarm();
            } else
            {
                icoSpeedOver.Text = "~";
                icoSpeedOver.TextColor = Color.Green;
                lblStatus.TextColor = Color.Green;
                lblStatus.Text = "Running Well";
                icoSpeedOver.TextColor = Color.Blue;
                if (_simpleAudioPlayer.IsPlaying)
                {
                    _simpleAudioPlayer.Stop();
                }
            }
        }

        // play speed over alarm.
        private async void showAlarm()
        {
            // beep sound
            if (!_simpleAudioPlayer.IsPlaying)
            {
                _simpleAudioPlayer.Play();
            }
            // scale animation to speed over !
            var s = await icoSpeedOver.RelScaleTo(1.5, 500);
            s = await icoSpeedOver.ScaleTo(1, 500); 
            s = await icoSpeedOver.RelScaleTo(1.5, 500);
            s = await icoSpeedOver.ScaleTo(1, 500);
        }

        // click handler for Start/Stop button.
        private void startStopJourney_Clicked(object sender, EventArgs e)
        {
            if (running == 1) //when click Stop
            {
                running = 0;
                startStopJourney.Text = "Start Journey!";

            } else if (running == 0) //when click Start
            {
                running = 1;
                startStopJourney.Text = "Stop Journey!";

                // clear map pins & path
                map.RouteCoordinates.Clear();
                map.Pins.Clear();
                curDist = 0;
                curSpeed = 0;
                updateLabels();

                //show button effect - border rotating.
                startButtonEffect();
                // start thread for journey
                startJourney();
            }
        }

        // add tracking path in map
        private void addPathInMap(Position lastPos)
        {
            map.RouteCoordinates.Add(lastPos);
        }

        // add pin for from & to position.
        private void addPinInMap(Position pos, string label)
        {
            var pin = new Pin();
            pin.Position = pos;
            pin.Label = label;
            map.Pins.Add(pin);
        }

        // show report for journey.
        private void showReport()
        {
            double dist = curDist;
            if (pckUnit.SelectedIndex == 1)
            {
                dist = dist / 1000;
                avrSpeed = avrSpeed / 1000 * 3600;
            }
            DisplayAlert("Journey Report", 
                $"Your journey finished from {fromAddr} to {toAddr}." +
                $"\nAverage speed is {avrSpeed:0.0#}{pckUnit.SelectedItem}" +
                $"\nDistance : {dist:0.0#}{distUnitLabels[pckUnit.SelectedIndex]}" +
                $"\nTime taken : {timer.Elapsed.Minutes}m {timer.Elapsed.Seconds%60}s",
                "Close");
            running = 0;
        }

        // Start thread for journey
        private void startJourney()
        {
            Task.Run(async () =>
            {
                timer.Restart();

                var totalDist = 0.0;
                var locator = CrossGeolocator.Current;
                locator.DesiredAccuracy = 100;
                var lastPosition = await locator.GetPositionAsync(TimeSpan.FromMilliseconds(1000));
                var lastAddr = await locator.GetAddressesForPositionAsync(lastPosition);
                double lastTime = 0;

                // init start address
                if (lastAddr.Count() > 0)
                {
                    fromAddr = lastAddr.ElementAt(0).Locality + " " + lastAddr.ElementAt(0).Thoroughfare;
                }
                // mark start position

                Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                {
                    addPinInMap(new Position(lastPosition.Latitude, lastPosition.Longitude), "Start");
                });

                // loop till user click Stop
                while (running == 1)
                {
                    // move map to current location
                    double curTime = timer.Elapsed.TotalSeconds;
                    double deltaTime = curTime - lastTime;
                    lastTime = curTime;
                    var position = await locator.GetPositionAsync(TimeSpan.FromMilliseconds(1000));
                    Device.StartTimer(TimeSpan.FromMilliseconds(500), () =>
                    {
                        var pos = new Position(position.Latitude, position.Longitude);
                        addPathInMap(pos);
                        map.MoveToRegion(MapSpan.FromCenterAndRadius(pos, Distance.FromMiles(1)));
                        return false;
                    });

                    // calculate distance from last position
                    var FromLocA = new GeoCoordinate(position.Latitude, position.Longitude);
                    var ToLocB = new GeoCoordinate(lastPosition.Latitude, lastPosition.Longitude);
                    var dist = FromLocA.GetDistanceTo(ToLocB);
                    lastPosition = position;

                    /*Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                    {
                        lblTime.Text = $"timing - {timer.Elapsed.TotalSeconds}s - {dist}";
                    });*/
                    // skip if not moved
                    if (dist == 0)
                    {
                        System.Threading.Thread.Sleep(500);
                        continue;
                    }
                    // calculate total distance
                    totalDist += dist;

                    // update UI and last position & time variable.
                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                    {
                        updateDistAndSpeed(totalDist, dist, deltaTime);
                    });
                    System.Threading.Thread.Sleep(500);
                }
                running = -1;
                // stop timer & calculate average speed.
                timer.Stop();
                avrSpeed = totalDist / timer.Elapsed.TotalSeconds;

                // save target address & mark position.
                lastAddr = await locator.GetAddressesForPositionAsync(lastPosition);
                if (lastAddr.Count() > 0)
                {
                    toAddr = lastAddr.ElementAt(0).Locality + " " + lastAddr.ElementAt(0).Thoroughfare;
                }
                Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                {
                    addPinInMap(new Position(lastPosition.Latitude, lastPosition.Longitude), "Arrived");
                    showReport();
                });
            }).ConfigureAwait(false);
        }

        private void startButtonEffect()
        {
            Task.Run(async () =>
            {
                var rot = 0;
                while (running == 1)
                {
                    rot += 10;
                    if (rot >= 360)
                    {
                        rot = 0;
                    }
                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                    {
                        testBtn.BorderGradientAngle = rot;
                    });
                    System.Threading.Thread.Sleep(100);
                }
            }).ConfigureAwait(false);
        }

        private void pckUnit_SelectedIndexChanged(object sender, EventArgs e)
        {
            updateLabels();
        }
    }
}