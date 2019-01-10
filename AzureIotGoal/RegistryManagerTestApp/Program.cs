using System;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace RegistryManagerTestApp
{
    class Program
    {
        private const string IotHubConnectionString = ""; // TODO: Get from settings.

        static async Task Main(string[] args)
        {
            await new Program().Run();
        }

        private async Task Run()
        {
            while (true)
            {
                Console.WriteLine("1 - Display all currently registered devices");
                Console.WriteLine("X - Exit");
                Console.WriteLine();

                var selection = Console.ReadKey(true);

                switch (char.ToUpperInvariant(selection.KeyChar))
                {
                    case '1':
                        await DisplayAllExistingDevices();
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

            var registryManager = RegistryManager.CreateFromConnectionString(IotHubConnectionString);

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
    }
}
