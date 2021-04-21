using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VBart.EventsDemo.Inventory.DataModels;

namespace VBart.EventsDemo.Inventory.Data.SqlServer.DataServices
{
    public class VehicleDataService
    {
        private readonly IDbAdapter _adapter;

        public VehicleDataService(IDbAdapter adapter)
        {
            _adapter = adapter;
        }

        public async Task<Vehicle?> FindByUuid(Guid uuid) =>
            await _adapter.GetSingleOrDefault<Vehicle>(
                "SELECT * FROM Vehicles WHERE VehicleUuid = @uuid",
                new { uuid });

        public async Task Add(Vehicle toAdd) =>
            await _adapter.ExecuteCommand(
                @$"INSERT INTO Vehicles (VehicleUuid, YearOfManufacture, Model)
                        VALUES (@{nameof(Vehicle.VehicleUuid)}, @{nameof(Vehicle.YearOfManufacture)}, @{nameof(Vehicle.Model)})",
                toAdd);

        public async Task RemoveByUuid(Guid uuid) =>
            await _adapter.ExecuteCommand("DELETE FROM Vehicles WHERE VehicleUuid = @uuid", new { uuid });

        public async Task UpdateByUuid(Vehicle toUpdate) =>
            await _adapter.ExecuteCommand(
                @$"UPDATE Vehicles
                        SET YearOfManufacture = @{nameof(Vehicle.YearOfManufacture)},
                            Model = @{nameof(Vehicle.Model)}
                        WHERE VehicleUuid = @{nameof(Vehicle.VehicleUuid)}",
                toUpdate);

        public async Task<List<Vehicle>> List() =>
            (await _adapter.GetMany<Vehicle>(@"SELECT * FROM Vehicles")).ToList();
    }
}