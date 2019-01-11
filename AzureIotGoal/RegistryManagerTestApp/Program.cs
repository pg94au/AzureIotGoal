using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;

namespace RegistryManagerTestApp
{
    class Program
    {
        private readonly string _iotHubConnectionString;
        private readonly string _iotHubHostName;

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
            while (true)
            {
                Console.WriteLine("1 - Display all currently registered devices");
                Console.WriteLine("2 - Add new device to hub");
                Console.WriteLine("3 - Delete existing device from hub");
                Console.WriteLine("4 - Send notification to device");
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
                    case 'X':
                        return;
                    default:
                        Console.WriteLine("Choose again!");
                        break;
                }

                Console.WriteLine();
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
            Console.WriteLine($"Connection String (Primary): HostName={_iotHubHostName};DeviceId={deviceId};SharedAccessKey={newDevice.Authentication.SymmetricKey.PrimaryKey}");
            Console.WriteLine($"Connection String (Secondary): HostName={_iotHubHostName};DeviceId={deviceId};SharedAccessKey={newDevice.Authentication.SymmetricKey.SecondaryKey}");
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
    }
}
