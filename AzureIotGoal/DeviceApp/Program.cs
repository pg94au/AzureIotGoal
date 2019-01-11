using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;

namespace DeviceApp
{
    class Program
    {
        private readonly string _deviceConnectionString;

        static async Task Main(string[] args)
        {
            await new Program().Run();
        }

        private Program()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .Build();

            try
            {
                _deviceConnectionString = configuration["DeviceConnectionString"];
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to load app settings: {e.Message}");
                Console.Error.WriteLine("Please ensure that a valid appsettings.json file is present.");
                Environment.Exit(-1);
            }
        }

        private async Task Run()
        {
            var deviceClient = DeviceClient.CreateFromConnectionString(_deviceConnectionString, TransportType.Mqtt);
            var twin = await deviceClient.GetTwinAsync();

            Console.WriteLine("Sending periodic events to hub.  Hit Ctrl-C to terminate.");
            while (true)
            {
                var message = $"The current time from {twin.DeviceId} is {DateTime.Now.ToLongTimeString()}.";
                var encodedMessage = new Message(Encoding.ASCII.GetBytes(message));

                Console.WriteLine($"Sending message [{message}]");
                await deviceClient.SendEventAsync(encodedMessage);

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
