using System;

namespace VBart.EventsDemo.Inventory.DataModels
{
    public record Vehicle
    {
        public Guid VehicleUuid { get; init; }

        public int YearOfManufacture { get; init; }

        public string Model { get; init; } = string.Empty;
    }
}