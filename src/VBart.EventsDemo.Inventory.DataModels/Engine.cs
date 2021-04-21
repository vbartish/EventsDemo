using System;

namespace VBart.EventsDemo.Inventory.DataModels
{
    public record Engine
    {
        public Guid EngineUuid { get; init; }

        public Guid? VehicleUuid { get; init; }

        public int YearOfManufacture { get; init; }

        public string Manufacturer { get; init; } = string.Empty;

        public int MaximumEngineSpeed { get; init; }

        public int MaximumMileageResource { get; init; }

        public int RemainingMileageResource { get; init; }
    }
}