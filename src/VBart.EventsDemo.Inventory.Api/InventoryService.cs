using System;
using System.Threading.Tasks;
using System.Transactions;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VBart.EventsDemo.Inventory.Data.SqlServer.DataServices;
using VBart.EventsDemo.Inventory.DataModels;
using GrpcVehicle = VBart.EventsDemo.Grpc.ValueObjects.Vehicle;
using GrpcVehicles = VBart.EventsDemo.Grpc.ValueObjects.Vehicles;
using GrpcEngine = VBart.EventsDemo.Grpc.ValueObjects.Engine;
using GrpcEngines = VBart.EventsDemo.Grpc.ValueObjects.Engines;

namespace VBart.EventsDemo.Inventory.Api
{
    public class InventoryService : VBart.EventsDemo.Grpc.InventoryService.InventoryServiceBase
    {
        private readonly IMapper _mapper;
        private readonly VehicleDataService _vehicleDataService;
        private readonly EngineDataService _engineDataService;

        public InventoryService(
            IMapper mapper,
            VehicleDataService vehicleDataService,
            EngineDataService engineDataService)
        {
            _mapper = mapper;
            _vehicleDataService = vehicleDataService;
            _engineDataService = engineDataService;
        }

        public override async Task<GrpcEngine> AddEngine(
            GrpcEngine request,
            ServerCallContext context)
        {
            if (!string.IsNullOrWhiteSpace(request.EngineUuid))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "EngineUuid should not be defined."));
            }

            var engine = _mapper.Map<GrpcEngine, Engine>(request);
            var toAdd = engine with { EngineUuid = Guid.NewGuid() };
            await _engineDataService.Add(toAdd);
            return _mapper.Map<Engine, GrpcEngine>(toAdd);
        }

        public override async Task<GrpcEngines> ListEngines(
            Empty request,
            ServerCallContext context)
        {
            var result = new GrpcEngines();
            var data = await _engineDataService.List();
            data.ForEach(engine => result.Engines_.Add(_mapper.Map<Engine, GrpcEngine>(engine)));
            return result;
        }

        public override async Task<GrpcEngine> UpdateEngine(
            GrpcEngine request,
            ServerCallContext context)
        {
            if(!Guid.TryParse(request.EngineUuid, out _))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "EngineUuid is not in the correct format"));
            }

            var suggested = _mapper.Map<GrpcEngine, Engine>(request);
            await _engineDataService.UpdateByUuid(suggested!);
            return request;
        }

        public override async Task<Empty> RemoveEngine(
            GrpcEngine request,
            ServerCallContext context)
        {
            if(!Guid.TryParse(request.EngineUuid, out var idToRemove))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "EngineUuid is not in the correct format"));
            }

            await _engineDataService.RemoveByUuid(idToRemove);
            return new Empty();
        }

        public override async Task<GrpcVehicle> AddVehicle(
            GrpcVehicle request,
            ServerCallContext context)
        {
            if (!string.IsNullOrWhiteSpace(request.VehicleUuid))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "VehicleUuid should not be defined."));
            }

            var assembled = new Vehicle
            {
                VehicleUuid = Guid.NewGuid(),
                Model = request.Model,
                YearOfManufacture = request.YearOfManufacture,
            };
            await _vehicleDataService.Add(assembled);
            return _mapper.Map<Vehicle, GrpcVehicle>(assembled);
        }

        public override async Task<Empty> RemoveVehicle(
            GrpcVehicle request,
            ServerCallContext context)
        {
            if(!Guid.TryParse(request.VehicleUuid, out var idToRemove))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "VehicleUuid is not in the correct format"));
            }

            using var transactionScope = new TransactionScope(
                TransactionScopeOption.RequiresNew,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted
                }, TransactionScopeAsyncFlowOption.Enabled);

            var assignedEngine = await _engineDataService.FindByVehicleUuid(idToRemove);

            if (assignedEngine != null)
            {
                assignedEngine = assignedEngine with { VehicleUuid = null };
                await _engineDataService.UpdateByUuid(assignedEngine);
            }

            await _vehicleDataService.RemoveByUuid(idToRemove);
            transactionScope.Complete();
            return new Empty();
        }

        public override async Task<GrpcVehicles> ListVehicles(
            Empty request,
            ServerCallContext context)
        {
            var result = new GrpcVehicles();
            var data = await _vehicleDataService.List();
            data.ForEach(engine => result.Vehicles_.Add(_mapper.Map<Vehicle, GrpcVehicle>(engine)));
            return result;
        }

        public override async Task<GrpcVehicle> UpdateVehicle(
            GrpcVehicle request,
            ServerCallContext context)
        {
            if (!Guid.TryParse(request.VehicleUuid, out _))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "VehicleUuid is not in the correct format"));
            }

            var suggested = _mapper.Map<GrpcVehicle, Vehicle>(request);
            await _vehicleDataService.UpdateByUuid(suggested!);
            return request;
        }
    }
}