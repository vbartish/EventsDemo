using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Polly;
using VBart.EventsDemo.Grpc;
using VBart.EventsDemo.Grpc.ValueObjects;

namespace VBart.EventsDemo.Inventory.DataGen
{
    static class Program
    {
        private static readonly Fixture Fixture = new();

        static async Task Main(string[] args)
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, _) => { cancellationTokenSource.Cancel(); };

            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
            var uri = configuration.GetServiceUri("InventoryApi")?.AbsoluteUri ?? "http://localhost:50051";
            var vehiclesToCreate = int.Parse(configuration["vehicles"] ?? "5");
            var wait = int.Parse(configuration["wait"] ?? "1");
            using var channel = GrpcChannel.ForAddress(uri);
            var client = new InventoryService.InventoryServiceClient(channel);

            var waitAndRetryForeverAsync = Policy<(List<Vehicle> createdVehicles, List<Engine> createdEngines)>
                .Handle<RpcException>()
                .WaitAndRetryForeverAsync((_, _, _) => TimeSpan.FromSeconds(5),
                    (delegateResult, _, sleepDuration, _) =>
                    {
                        Console.WriteLine($"Got error from GRPC API: {(delegateResult.Exception as RpcException)?.Message}. Sleeping for {sleepDuration}.");
                        return Task.CompletedTask;
                    });
            var policyResult = await waitAndRetryForeverAsync.ExecuteAndCaptureAsync( _ => Initialize(client, vehiclesToCreate), cancellationTokenSource.Token);

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await RandomlyUpdateAVehicle(
                    client,
                    policyResult.Result.createdVehicles,
                    policyResult.Result.createdEngines);
                await Task.Delay(TimeSpan.FromSeconds(wait), cancellationTokenSource.Token);
            }
        }

        private static async Task RandomlyUpdateAVehicle(
            InventoryService.InventoryServiceClient client,
            List<Vehicle> createdVehicles,
            List<Engine> createdEngines)
        {
            var random = new Random();
            var vehicleIndex = random.Next(0, createdVehicles.Count - 1);
            var vehicleToUpdate = createdVehicles[vehicleIndex];
            var engineToUpdate = createdEngines.Single(engine => string.Equals(engine.VehicleUuid, vehicleToUpdate.VehicleUuid, StringComparison.Ordinal));
            var updatedVehicle = await client.UpdateVehicleAsync(Fixture
                .Build<Vehicle>()
                .With(engine => engine.VehicleUuid, vehicleToUpdate.VehicleUuid)
                .Create());
            Console.WriteLine("/////////////////////////////");
            Console.WriteLine($"Updated vehicle: {JObject.FromObject(updatedVehicle)}");
            Console.WriteLine("/////////////////////////////");
            var updatedEngine = await client.UpdateEngineAsync(Fixture
                .Build<Engine>()
                .With(engine => engine.EngineUuid, engineToUpdate.EngineUuid)
                .With(engine => engine.VehicleUuid, vehicleToUpdate.VehicleUuid)
                .Create());
            Console.WriteLine("/////////////////////////////");
            Console.WriteLine($"Updated engine: {JObject.FromObject(updatedEngine)}");
            Console.WriteLine("/////////////////////////////");
        }

        private static async Task<(List<Vehicle> vehicles, List<Engine> engines)> Initialize(
            InventoryService.InventoryServiceClient client,
            int vehiclesAmount)
        {
            var createdVehicles = new List<Vehicle>();
            for (int i = 0; i < vehiclesAmount; i++)
            {
                var createdVehicle = await client.AddVehicleAsync(Fixture
                    .Build<Vehicle>()
                    .Without(vehicle => vehicle.VehicleUuid)
                    .Create());
                createdVehicles.Add(createdVehicle);
            }

            Console.WriteLine("/////////////////////////////");
            Console.WriteLine(createdVehicles.Humanize(Environment.NewLine));
            Console.WriteLine("/////////////////////////////");

            var createdEngines = new List<Engine>();
            foreach (var createdVehicle in createdVehicles)
            {
                var createdEngine = await client.AddEngineAsync(Fixture
                    .Build<Engine>()
                    .Without(engine => engine.EngineUuid)
                    .With(engine => engine.VehicleUuid, createdVehicle.VehicleUuid)
                    .Create());
                createdEngines.Add(createdEngine);
            }

            return (createdVehicles.ToList(), createdEngines);
        }
    }
}