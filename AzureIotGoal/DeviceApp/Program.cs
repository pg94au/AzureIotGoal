using System;
using System.IO;
using System.Text;
using System.Threading;
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
            

            var cts = new CancellationTokenSource();
            var receiveMessagesFromHubTask = ReceiveMessagesFromHub(deviceClient, cts.Token);

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Exiting...");
            };

            while (true)
            {
                Console.WriteLine("1 - Send event to hub");
                Console.WriteLine("X - Exit");
                Console.WriteLine();

                var selection = Console.ReadKey(true);

                switch (char.ToUpperInvariant(selection.KeyChar))
                {
                    case '1':
                        await SendEventToHub(deviceClient, cts.Token);
                        break;
                    case 'X':
                        Console.WriteLine("Stopping receiving messages from hub.");
                        cts.Cancel();
                        await receiveMessagesFromHubTask;
                        return;
                    default:
                        Console.WriteLine("Choose again!");
                        break;
                }

                Console.WriteLine();
            }
        }

        private async Task SendEventToHub(DeviceClient deviceClient, CancellationToken cancellationToken)
        {
            Console.WriteLine("Sending event to hub.");

            var twin = await deviceClient.GetTwinAsync(cancellationToken);

            var message = $"The current time from {twin.DeviceId} is {DateTime.Now.ToLongTimeString()}.";
            var encodedMessage = new Message(Encoding.ASCII.GetBytes(message));

            Console.WriteLine($"Sending message [{message}]");
            await deviceClient.SendEventAsync(encodedMessage, cancellationToken);
        }
        
        private async Task ReceiveMessagesFromHub(DeviceClient deviceClient, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await deviceClient.ReceiveAsync(cancellationToken);
                    string data = Encoding.UTF8.GetString(message.GetBytes());

                    Console.WriteLine($"Received message: {data}");

                    // Message stays in queue until removed by a call to CompleteAsync.
                    // (Can also call AbandonAsync to explicitly requeue this message.)
                    await deviceClient.CompleteAsync(message, cancellationToken);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Stopped receiving messages.");
            }
        }
    }
}
