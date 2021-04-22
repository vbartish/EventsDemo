namespace VBart.EventsDemo.Inventory.Aggregator.Domain.Aggregates
{
    public partial class Vehicle : CdcBasedAggregate
    {
        public override string AggregateUuid
        {
            get => VehicleUuid;
            set => VehicleUuid = value;
        }
        public override bool IsCohesive()
        {
            return !string.IsNullOrWhiteSpace(VehicleUuid)
                   && Engine is not null;
        }
    }
}