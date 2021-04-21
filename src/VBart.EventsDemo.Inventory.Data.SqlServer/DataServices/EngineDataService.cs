using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VBart.EventsDemo.Inventory.DataModels;

namespace VBart.EventsDemo.Inventory.Data.SqlServer.DataServices
{
    public class EngineDataService
    {
        private readonly IDbAdapter _adapter;

        public EngineDataService(IDbAdapter adapter)
        {
            _adapter = adapter;
        }

        public async Task<Engine?> FindByUuid(Guid uuid) =>
            await _adapter.GetSingleOrDefault<Engine>(
                "SELECT * FROM Engines WHERE EngineUuid = @uuid",
                new { uuid });

        public async Task<Engine?> FindByVehicleUuid(Guid uuid) =>
            await _adapter.GetSingleOrDefault<Engine>(
                "SELECT * FROM Engines WHERE VehicleUuid = @uuid",
                new { uuid });

        public async Task Add(Engine toAdd) =>
            await _adapter.ExecuteCommand(
                @$"INSERT INTO Engines (EngineUuid, VehicleUuid, YearOfManufacture, Manufacturer,
                                             MaximumEngineSpeed, MaximumMileageResource, RemainingMileageResource)
                        VALUES (@{nameof(Engine.EngineUuid)}, @{nameof(Engine.VehicleUuid)}, @{nameof(Engine.YearOfManufacture)}, @{nameof(Engine.Manufacturer)},
                                @{nameof(Engine.MaximumEngineSpeed)}, @{nameof(Engine.MaximumMileageResource)}, @{nameof(Engine.RemainingMileageResource)})",
                toAdd);

        public async Task RemoveByUuid(Guid uuid) =>
            await _adapter.ExecuteCommand("DELETE FROM Engines WHERE EngineUuid = @uuid", new { uuid });

        public async Task UpdateByUuid(Engine toUpdate) =>
            await _adapter.ExecuteCommand(
                @$"UPDATE Engines
                        SET YearOfManufacture = @{nameof(Engine.YearOfManufacture)},
                            VehicleUuid = @{nameof(Engine.VehicleUuid)},
                            Manufacturer = @{nameof(Engine.Manufacturer)},
                            MaximumEngineSpeed = @{nameof(Engine.MaximumEngineSpeed)},
                            MaximumMileageResource = @{nameof(Engine.MaximumMileageResource)},
                            RemainingMileageResource = @{nameof(Engine.RemainingMileageResource)}
                        WHERE EngineUuid = @{nameof(Engine.EngineUuid)}",
                toUpdate);

        public async Task<List<Engine>> List() =>
            (await _adapter.GetMany<Engine>(@"SELECT * FROM Engines")).ToList();
    }
}