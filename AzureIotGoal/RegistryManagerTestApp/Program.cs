using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.EventHubs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace RegistryManagerTestApp
{
    class Program
    {
        private readonly string _iotHubConnectionString;
        private readonly string _iotHubHostName;
        private readonly string _eventHubClientConnectionString;

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
                _iotHubConnectionString = configuration["IotHubConnectionString"];

                var iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(_iotHubConnectionString);
                _iotHubHostName = iotHubConnectionStringBuilder.HostName;

                _eventHubClientConnectionString = configuration["EventHubClientConnectionString"];
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
            var eventHubClient = EventHubClient.CreateFromConnectionString(_eventHubClientConnectionString);
            var cts = new CancellationTokenSource();
            var receiveMessagesFromDevicesTask = ReceiveMessagesFromDevices(eventHubClient, cts.Token);

            while (true)
            {
                Console.WriteLine("1 - Display all currently registered devices");
                Console.WriteLine("2 - Add new device to hub");
                Console.WriteLine("3 - Delete existing device from hub");
                Console.WriteLine("4 - Send notification to device");
                Console.WriteLine("5 - Display device twin");
                Console.WriteLine("6 - Update desired property in device twin");
                Console.WriteLine("X - Exit");
                Console.WriteLine();

                var selection = Console.ReadKey(true);

                switch (char.ToUpperInvariant(selection.KeyChar))
                {
                    case '1':
                        await DisplayAllExistingDevices();
                        break;
                    case '2':
                        await AddNewDevice();
                        break;
                    case '3':
                        await DeleteExistingDevice();
                        break;
                    case '4':
                        await SendNotificationToDevice();
                        break;
                    case '5':
                        await DisplayDeviceTwin(cts.Token);
                        break;
                    case '6':
                        await UpdateDesiredPropertyInDeviceTwin(cts.Token);
                        break;
                    case 'X':
                        Console.WriteLine("Stopping receiving messages from devices.");
                        cts.Cancel();
                        await receiveMessagesFromDevicesTask;
                        return;
                    default:
                        Console.WriteLine("Choose again!");
                        break;
                }

                Console.WriteLine();
            }
        }

        private async Task ReceiveMessagesFromDevices(EventHubClient eventHubClient, CancellationToken cancellationToken)
        {
            var runtimeInfo = await eventHubClient.GetRuntimeInformationAsync();
            var partitions = runtimeInfo.PartitionIds;

            var tasks = new List<Task>();
            foreach (string partition in partitions)
            {
                tasks.Add(ReceiveMessagesFromDevicesAsync(eventHubClient, partition, cancellationToken));
            }

            // Wait for all the PartitionReceivers to finish.
            Task.WaitAll(tasks.ToArray());
        }

        private async Task ReceiveMessagesFromDevicesAsync(EventHubClient eventHubClient, string partition, CancellationToken cancellationToken)
        {
            // TODO: Figure out if we can get earlier than from now?
            var eventHubReceiver = eventHubClient.CreateReceiver("$Default", partition, EventPosition.FromEnqueuedTime(DateTime.Now));
            Console.WriteLine("Create receiver on partition: " + partition);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var events = await eventHubReceiver.ReceiveAsync(maxMessageCount: 100,
                    waitTime: TimeSpan.FromSeconds(1)); // Times out returning no events if there is no data.
                if (events == null) continue;

                foreach (var eventData in events)
                {
                    string data = Encoding.UTF8.GetString(eventData.Body.Array);

                    Console.WriteLine($"Message received on partition {partition} from {eventData.SystemProperties["iothub-connection-device-id"]}: {data}");

                    //Console.WriteLine("Application properties (set by device):");
                    //foreach (var prop in eventData.Properties)
                    //{
                    //    Console.WriteLine("  {0}: {1}", prop.Key, prop.Value);
                    //}
                    //Console.WriteLine("System properties (set by IoT Hub):");
                    //foreach (var prop in eventData.SystemProperties)
                    //{
                    //    Console.WriteLine("  {0}: {1}", prop.Key, prop.Value);
                    //}
                }
            }
        }

        private async Task DisplayAllExistingDevices()
        {
            Console.WriteLine("Displaying all currently registered devices:");

            using (var registryManager = RegistryManager.CreateFromConnectionString(_iotHubConnectionString))
            {

                var devicesQuery = registryManager.CreateQuery("select * from devices");

                while (devicesQuery.HasMoreResults)
                {
                    //var results = await devicesQuery.GetNextAsJsonAsync();
                    //foreach (var result in results)
                    //{
                    //    Console.WriteLine(result);
                    //}

                    var results = await devicesQuery.GetNextAsTwinAsync();
                    foreach (var result in results)
                    {
                        Console.WriteLine($"DeviceId={result.DeviceId}, Status={result.Status}, ConnectionState={result.ConnectionState}");
                    }
                }
            }

            Console.WriteLine("Done");
        }

        private async Task AddNewDevice()
        {
            Console.WriteLine("Adding new device to hub:");

            Console.WriteLine();
            Console.Write("Enter ID for device: ");
            var deviceId = Console.ReadLine();

            var newDeviceRequest = new Device(deviceId);

            Device newDevice;
            using (var registryManager = RegistryManager.CreateFromConnectionString(_iotHubConnectionString))
            {
                newDevice = await registryManager.AddDeviceAsync(newDeviceRequest);
            }

            Console.WriteLine($"Device {newDevice.Id} created.");
            Console.WriteLine();
            Console.WriteLine("To connect this device, use the following credentials:");
            Console.WriteLine($"HostName: {_iotHubHostName}");
            Console.WriteLine($"DeviceId: {deviceId}");
            Console.WriteLine($"Primary Key: {newDevice.Authentication.SymmetricKey.PrimaryKey}");
            Console.WriteLine($"Secondary Key: {newDevice.Authentication.SymmetricKey.SecondaryKey}");
            Console.WriteLine(" - or -");
            Console.WriteLine(
                $"Connection String (Primary): HostName={_iotHubHostName};DeviceId={deviceId};SharedAccessKey={newDevice.Authentication.SymmetricKey.PrimaryKey}");
            Console.WriteLine(
                $"Connection String (Secondary): HostName={_iotHubHostName};DeviceId={deviceId};SharedAccessKey={newDevice.Authentication.SymmetricKey.SecondaryKey}");
        }

        private async Task DeleteExistingDevice()
        {
            Console.WriteLine("Delete existing device from hub:");

            Console.WriteLine();
            Console.Write("Enter ID for device: ");
            var deviceId = Console.ReadLine();

            using (var registryManager = RegistryManager.CreateFromConnectionString(_iotHubConnectionString))
            {
                await registryManager.RemoveDeviceAsync(deviceId);
            }

            Console.WriteLine("Done");
        }

        private async Task SendNotificationToDevice()
        {
            Console.WriteLine("Send notification to device:");
            Console.WriteLine();

            Console.Write("Enter ID for device: ");
            var deviceId = Console.ReadLine();

            var message = $"The current time from back end is {DateTime.Now.ToLongTimeString()}.";
            var encodedMessage = new Message(Encoding.ASCII.GetBytes(message));

            Console.WriteLine($"Sending message [{message}]");

            using (var serviceClient = ServiceClient.CreateFromConnectionString(_iotHubConnectionString))
            {
                await serviceClient.SendAsync(deviceId, encodedMessage);
            }

            Console.WriteLine("Done");
        }

        private async Task DisplayDeviceTwin(CancellationToken cancellationToken)
        {
            Console.WriteLine("Displaying device twin:");
            Console.WriteLine();

            Console.Write("Enter ID for device: ");
            var deviceId = Console.ReadLine();

            using (var registryManager = RegistryManager.CreateFromConnectionString(_iotHubConnectionString))
            {
                var twin = await registryManager.GetTwinAsync(deviceId, cancellationToken);

                Console.WriteLine(twin.ToJson(Formatting.Indented));
            }
        }

        private async Task UpdateDesiredPropertyInDeviceTwin(CancellationToken cancellationToken)
        {
            Console.WriteLine("Updating desired property in device twin:");
            Console.WriteLine();

            Console.Write("Enter ID for device: ");
            var deviceId = Console.ReadLine();

            Console.Write("Enter property name: ");
            var name = Console.ReadLine();

            Console.Write("Enter value for property: ");
            var value = Console.ReadLine();

            using (var registryManager = RegistryManager.CreateFromConnectionString(_iotHubConnectionString))
            {
                var twin = await registryManager.GetTwinAsync(deviceId, cancellationToken);

                twin.Properties.Desired[name] = value;

                await registryManager.UpdateTwinAsync(deviceId, twin, twin.ETag, cancellationToken);
            }

            Console.WriteLine("Done");
        }
    }
}
