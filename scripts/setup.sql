BEGIN TRANSACTION;
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Vehicles')
BEGIN
CREATE TABLE [Vehicles] (
    [VehicleUuid] uniqueidentifier NOT NULL,
    [YearOfManufacture] int NOT NULL,
    [Model] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Vehicles] PRIMARY KEY ([VehicleUuid])
    );
END;
GO


IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Engines')
BEGIN
CREATE TABLE [Engines] (
    [EngineUuid] uniqueidentifier NOT NULL,
    [VehicleUuid] uniqueidentifier NULL,
    [YearOfManufacture] int NOT NULL,
    [Manufacturer] nvarchar(max) NOT NULL,
    [MaximumEngineSpeed] int NOT NULL,
    [MaximumMileageResource] int NOT NULL,
    [RemainingMileageResource] int NOT NULL,
    CONSTRAINT [PK_Engines] PRIMARY KEY ([EngineUuid]),
    CONSTRAINT [FK_Engines_Vehicles_VehicleUuid] FOREIGN KEY ([VehicleUuid]) REFERENCES [Vehicles] ([VehicleUuid]),
    );
END;
GO

IF NOT EXISTS(SELECT * FROM sys.indexes WHERE name = 'IX_Engines_VehicleUuid' AND object_id = OBJECT_ID('Engines'))
BEGIN
EXEC(N'CREATE UNIQUE INDEX [IX_Engine_VehicleUuid] ON [Engines] ([VehicleUuid]) WHERE [VehicleUuid] IS NOT NULL');
END;
GO

COMMIT;
GO