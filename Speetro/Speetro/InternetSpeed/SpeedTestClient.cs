using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSpeedTest.Models;
using System.Net.Http;

namespace NSpeedTest
{
    public class SpeedTestClient : ISpeedTestClient
    {
        private const string ConfigUrl = "http://www.speedtest.net/speedtest-config.php";
        private const string ServersUrl = "http://www.speedtest.net/speedtest-servers.php";
        //private readonly int[] downloadSizes = { 35, 50, 75, 100, 150, 200, 250, 300, 350, 400, 500, 750 };
        private readonly int[] downloadSizes = { 350, 750, 1500 };
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int MaxUploadSize = 4; // 400 KB

        public static double CurrentSpeed = 0;
        #region ISpeedTestClient

        /// <summary>
        /// Download speedtest.net settings
        /// </summary>
        /// <returns>speedtest.net settings</returns>
        public async Task<Settings> GetSettingsAsync()
        {
            var settings = SpeedTestWebClient.GetConfigAsync<Settings>(ConfigUrl);
            var serversConfig = SpeedTestWebClient.GetConfigAsync<ServersList>();

            serversConfig.CalculateDistances(settings.Client.GeoCoordinate);
            settings.Servers = serversConfig.Servers.OrderBy(s => s.Distance).ToList();

            return settings;
        }

        /// <summary>
        /// Test latency (ping) to server
        /// </summary>
        /// <returns>Latency in milliseconds (ms)</returns>
        public async Task<int> TestServerLatencyAsync(Server server, int retryCount = 3)
        {
            var latencyUri = CreateTestUrl(server, "latency.txt");
            var timer = new Stopwatch();

                for (var i = 0; i < retryCount; i++)
                {
                    string testString;
                    try
                    {
                        timer.Start();
                        testString = await SpeedTestWebClient.client.GetAsync(SpeedTestWebClient.AddTimeStamp(new Uri(latencyUri)), HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult().Content.ReadAsStringAsync();
                    }
                    catch (WebException)
                    {
                        continue;
                    }
                    finally
                    {
                        timer.Stop();
                    }

                    if (!testString.StartsWith("test=test"))
                    {
                        throw new InvalidOperationException("Server returned incorrect test string for latency.txt");
                    }
                }

            return (int)timer.ElapsedMilliseconds / retryCount;
        }

        /// <summary>
        /// Test download speed to server
        /// </summary>
        /// <returns>Download speed in Kbps</returns>
        public double TestDownloadSpeed(Server server, int simultaniousDownloads = 2, int retryCount = 2)
        {
            var testData = GenerateDownloadUrls(server, retryCount);

            return TestSpeed(testData, async (url) =>
            {
                var data = await SpeedTestWebClient.client.GetAsync(SpeedTestWebClient.AddTimeStamp(new Uri(url)), HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult().Content.ReadAsStringAsync();
                return data.Length;
            }, simultaniousDownloads);
        }

        /// <summary>
        /// Test upload speed to server
        /// </summary>
        /// <returns>Upload speed in Kbps</returns>
        public double TestUploadSpeed(Server server, int simultaniousUploads = 2, int retryCount = 2)
        {
            var testData = GenerateUploadData(retryCount);
            return TestSpeed(testData, async (uploadData) =>
            {
                var stringContent = new StringContent(uploadData[uploadData.Keys.ToList()[0]]);
                await SpeedTestWebClient.client.PostAsync(server.Url, stringContent).ConfigureAwait(false);
                return uploadData[uploadData.Keys.ToList()[0]].Length;
            }, simultaniousUploads);
        }

        #endregion

        #region Helpers

        private static double TestSpeed<T>(IEnumerable<T> testData, Func<T, Task<int>> doWork, int concurencyCount = 2)
        {
            var timer = new Stopwatch();
            var throttler = new SemaphoreSlim(concurencyCount);
            var currentSize = 0;

            timer.Start();
            var downloadTasks = testData.Select(async data =>
            {
                await throttler.WaitAsync().ConfigureAwait(false);
                try
                {
                    var size = await doWork(data).ConfigureAwait(false);
                    currentSize += size;
                    CurrentSpeed = (currentSize * 8 / 1024) / ((double)timer.ElapsedMilliseconds / 1000);
                    return size;
                }
                finally
                {
                    throttler.Release();
                }
            }).ToArray();

            Task.WaitAll(downloadTasks);
            timer.Stop();

            double totalSize = downloadTasks.Sum(task => task.Result);
            return (totalSize * 8 / 1024) / ((double)timer.ElapsedMilliseconds / 1000);
        }

        private static IEnumerable<Dictionary<string, string>> GenerateUploadData(int retryCount)
        {
            var random = new Random();
            var result = new List<Dictionary<string, string>>();

            for (var sizeCounter = 1; sizeCounter < MaxUploadSize+1; sizeCounter++)
            {
                var size = sizeCounter*200*1024;
                var builder = new StringBuilder(size);

                for (var i = 0; i < size; ++i)
                    builder.Append(Chars[random.Next(Chars.Length)]);

                for (var i = 0; i < retryCount; i++)
                {
                    result.Add(new Dictionary<string, string> { { string.Format("content{0}", sizeCounter), builder.ToString() } });
                }
            }

            return result;
        }

        private static string CreateTestUrl(Server server, string file)
        {
            return new Uri(new Uri(server.Url), ".").OriginalString + file;
        }

        private IEnumerable<string> GenerateDownloadUrls(Server server, int retryCount)
        {
            var downloadUriBase = CreateTestUrl(server, "random{0}x{0}.jpg?r={1}");
            foreach (var downloadSize in downloadSizes)
            {
                for (var i = 0; i < retryCount; i++)
                {
                    yield return string.Format(downloadUriBase, downloadSize, i);
                }
            }
        }

        #endregion
    }
}
