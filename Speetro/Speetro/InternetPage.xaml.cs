using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using Plugin.Connectivity;
using NSpeedTest;
using System.Net.Http;
using NSpeedTest.Models;
using Newtonsoft.Json;
using System.Net;
using System.Windows.Input;

namespace Speetro
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class InternetPage : ContentPage
    {
        /// <summary>
        /// Use speedtest.com api to test internet download speed.
        /// - Get IP address and location by IP.
        /// - Find nearest downloading test server.
        /// - Download test image files for several times and measure speed.
        /// - Just now, don't need to change 'Internet Speed' sub folder of this project.
        /// </summary>
        private static SpeedTestClient client;
        private static Settings settings;
        private const string DefaultCountry = "Australia";
        private static string clientCountry = null;
        private PrintableSpeed printableDownloadSpeed;

        public ICommand TestBtnClickCommand { private set; get; }

        public InternetPage()
        {
            InitializeComponent();
            Title = "Internet";
            OnTestBtnClicked(this, null);
        }

        // Test button handler
        private void OnTestBtnClicked(object sender, EventArgs e)
        {
            testBtn.IsEnabled = false;
            testBtnLabel.IsEnabled = false;
            testBtnLabel.Text = "Testing!";

            // show network strength(band-width) using CrossConnectivity Pulgin(Xamarin)
            if (CrossConnectivity.Current.Bandwidths.Count() > 0)
            {
                myNetworkBandwidth.Text = (CrossConnectivity.Current.Bandwidths.First<ulong>() / 1000000).ToString() + "Mbps";
            }

            Task.Run(async () =>
            {
                StartSpeedTestAsync();
            }).ConfigureAwait(false);


            Task.Run(async () =>
            {
                var rot = 0;
                var finished = false;
                double lastSpeed = 0;
                while (!finished)
                {
                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
                    {
                        if (testBtn.IsEnabled)
                        {
                            finished = true;
                            return;
                        }
                        rot += 10;
                        if (rot >= 360)
                        {
                            rot = 0;
                        }

                        // Update label & progress bar for download speed change.
                        var sp = new PrintableSpeed(SpeedTestClient.CurrentSpeed);
                        if (lastSpeed != SpeedTestClient.CurrentSpeed)
                        {
                            testProgress.Progress += 0.01;
                        }
                        if (testProgress.Progress >= 0.8)
                        {
                            downloadSpeedValue.Text = sp.speed.ToString();
                            downloadSpeedLabel.Text = sp.label;
                        } else
                        {
                            uploadSpeedValue.Text = sp.speed.ToString();
                            uploadSpeedLabel.Text = sp.label;
                        }
                        lastSpeed = SpeedTestClient.CurrentSpeed;
                        
                        // rotate test button border gradient.
                        testBtn.BorderGradientAngle = rot;
                    });
                    System.Threading.Thread.Sleep(100);
                }
            }).ConfigureAwait(false);
        }

        // Get IP address using icanhazip.com
        private static string getIpAddress()
        {
            string externalip = new WebClient().DownloadString("http://icanhazip.com").Replace("\n", "");
            return externalip;
        }

        // Get country of ip address using api.ipstack.com
        private static async System.Threading.Tasks.Task<string> GetClienCountryAsync(string externalip)
        {
            string url = "http://api.ipstack.com/" + externalip + "?access_key=27d7f91051ccc6ae60659f120efb0c98";
            try
            {
                var serverResponse = await SpeedTestWebClient.client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                string jsonstring = await serverResponse.Content.ReadAsStringAsync();
                CountryInfo dynObj = JsonConvert.DeserializeObject<CountryInfo>(jsonstring);

                settings.Client.Ip = dynObj.ip;
                settings.Client.Longitude = dynObj.longitude;
                settings.Client.Latitude = dynObj.latitude;
                return dynObj.country_name;
            } catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return "";
            }
        }

        // Start download speed test
        public async System.Threading.Tasks.Task<string> StartSpeedTestAsync()
        {
            string externalip = getIpAddress();
            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                myIpAddress.Text = externalip;
            });

            if (client == null)
            {
                client = new SpeedTestClient();
            }
            if (settings == null)
            {
                // load setting configuration file(speedtest-config.php.xml, -servers-static.php.xml).
                settings = await client.GetSettingsAsync();
            }

            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                UpdateIndicators("Getting Client's Country", 0.2f);
            });

            if (clientCountry == null)
            {
                // get country info by ip.
                clientCountry = await GetClienCountryAsync(externalip);
            }
            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                UpdateIndicators("Selecting Best Server by Distance", 0.3f);

            });

            // find nearest server by country.
            var servers = SelectServers();
            var bestServer = SelectBestServer(servers);
            UpdateServerInfo(bestServer);

            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                UpdateIndicators("Testing Internet Speed", 0.5f);
            });
            var downloadSpeed = client.TestDownloadSpeed(bestServer, settings.Download.ThreadsPerUrl);
            UpdateDownloadUi(downloadSpeed);
            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                UpdateIndicators("Testing Finished", 0.8f);
            });

            var uploadSpeed = client.TestUploadSpeed(bestServer, settings.Download.ThreadsPerUrl);
            UpdateUploadUi(uploadSpeed);

            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                UpdateIndicators("Testing Finished", 1.0f);
                testBtnLabel.Text = "Test Again!";
                testBtn.IsEnabled = true;
                testBtnLabel.IsEnabled = true;
            });
            return Math.Round(downloadSpeed / 1024, 2).ToString();
        }

        // Update progress bar
        private void UpdateIndicators(string info = null, float progress = 0)
        {
            testProgress.Progress = progress;
        }

        // Find the server which latency is smallest.
        private static Server SelectBestServer(IEnumerable<Server> servers)
        {
            var bestServer = servers.OrderBy(x => x.Latency).First();
            return bestServer;
        }

        // Find nearest server based on country of ip.
        private static IEnumerable<Server> SelectServers()
        {
            List<Server> servers;
            // Take 3 servers in the client's country or default country.
            if (clientCountry != null)
            {
                servers = settings.Servers.Where(s => s.Country.Equals(clientCountry)).Take(3).ToList();
            }
            else
            {
                servers = settings.Servers.Where(s => s.Country.Equals(DefaultCountry)).Take(3).ToList();
            }

            // Test ping speed for each server.
            foreach (var server in servers)
            {
                server.Latency = client.TestServerLatencyAsync(server).GetAwaiter().GetResult();
            }
            return servers;
        }

        // Update server info label when server found.
        private void UpdateServerInfo(Server info)
        {
            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                serverInfo.Text = String.Format("Server Hosted by {0} ({1}/{2})", info.Sponsor, info.Name, info.Country);
            });
        }
        
        // Update download speed and unit(kbps/mbps) labels.
        private void UpdateDownloadUi(double dnSpeed)
        {
            printableDownloadSpeed = new PrintableSpeed(dnSpeed);

            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                downloadSpeedValue.Text = printableDownloadSpeed.speed.ToString();
                downloadSpeedLabel.Text = printableDownloadSpeed.label;

                UpdateIndicators("Download Test", 1.0f);
            });
        }

        // Update upload speed and unit(kbps/mbps) labels.
        private void UpdateUploadUi(double dnSpeed)
        {
            printableDownloadSpeed = new PrintableSpeed(dnSpeed);

            Xamarin.Forms.Device.BeginInvokeOnMainThread(() =>
            {
                uploadSpeedValue.Text = printableDownloadSpeed.speed.ToString();
                uploadSpeedLabel.Text = printableDownloadSpeed.label;

                UpdateIndicators("Upload Test", 1.0f);
            });
        }

        // response structure of api.stack.com to get country and location of ip.
        private struct CountryInfo
        {
            public String ip;
            public String country_name;
            public float latitude;
            public float longitude;
        }

        // structure to format Kbps/Mbps depends on speed.
        private struct PrintableSpeed
        {
            public string label;
            public double speed;
            public PrintableSpeed(double speed)
            {
                if (speed > 1024)
                {
                    speed = Math.Round(speed / 1024, 2);
                    this.speed = speed;
                    this.label = "Mbps";
                }
                else
                {
                    speed = Math.Round(speed, 2);
                    this.speed = speed;
                    this.label = "Kbps";
                }

            }
            public string getPrintableSpeed()
            {
                return speed.ToString() + label;
            }
        }
    }
}