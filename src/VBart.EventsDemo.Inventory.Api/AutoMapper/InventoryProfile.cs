using AutoMapper;
using VBart.EventsDemo.Inventory.DataModels;
using GrpcVehicle = VBart.EventsDemo.Grpc.ValueObjects.Vehicle;
using GrpcEngine = VBart.EventsDemo.Grpc.ValueObjects.Engine;

namespace VBart.EventsDemo.Inventory.Api.AutoMapper
{
    public class InventoryProfile : Profile
    {
        public InventoryProfile()
        {
            CreateMap<GrpcVehicle, Vehicle>()
                .ForMember(
                    vehicle => vehicle.VehicleUuid,
                    opt => opt.ConvertUsing(new GrpcGuidValueConverter()));
            CreateMap<Vehicle, GrpcVehicle>();
            CreateMap<GrpcEngine, Engine>()
                .ForMember(
                    engine => engine.EngineUuid,
                    opt => opt.ConvertUsing(new GrpcGuidValueConverter()))
                .ForMember(
                    engine => engine.VehicleUuid,
                    opt => opt.ConvertUsing(new GrpcNullableGuidValueConverter()));
            CreateMap<Engine, GrpcEngine>();
        }
    }
}