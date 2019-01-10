using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;

namespace RegistryManagerTestApp
{
    class Program
    {
        private readonly string _iotHubConnectionString;

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

            var registryManager = RegistryManager.CreateFromConnectionString(_iotHubConnectionString);

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
            Console.WriteLine("Done");
        }

        private async Task AddNewDevice()
        {
            Console.WriteLine("Adding new device to hub:");

            Console.WriteLine();
            Console.Write("Enter ID for device: ");
            var deviceId = Console.ReadLine();

            var newDeviceRequest = new Device(deviceId);

            var registryManager = RegistryManager.CreateFromConnectionString(_iotHubConnectionString);

            var newDevice = await registryManager.AddDeviceAsync(newDeviceRequest);

            Console.WriteLine($"Device {newDevice.Id} created.");
            Console.WriteLine();
            Console.WriteLine("To connect this device, use the following credentials:");
            Console.WriteLine($"Primary Key: {newDevice.Authentication.SymmetricKey.PrimaryKey}");
            Console.WriteLine($"Secondary Key: {newDevice.Authentication.SymmetricKey.SecondaryKey}");
        }
    }
}
