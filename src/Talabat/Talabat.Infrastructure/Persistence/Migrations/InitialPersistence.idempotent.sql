IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(200) NOT NULL,
        [Age] int NOT NULL,
        [PhoneNumber] nvarchar(50) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(200) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Customers_Age_Positive] CHECK ([Age] > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [DeliveryAgents] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(200) NOT NULL,
        [PhoneNumber] nvarchar(50) NULL,
        [VehicleType] int NOT NULL,
        [Status] int NOT NULL,
        [CurrentLatitude] decimal(9,6) NULL,
        [CurrentLongitude] decimal(9,6) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(200) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_DeliveryAgents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DeliveryAgents_CurrentLatitude_Range] CHECK (([CurrentLatitude] IS NULL OR ([CurrentLatitude] >= -90 AND [CurrentLatitude] <= 90))),
        CONSTRAINT [CK_DeliveryAgents_CurrentLocation_PairedNull] CHECK ((([CurrentLatitude] IS NULL AND [CurrentLongitude] IS NULL) OR ([CurrentLatitude] IS NOT NULL AND [CurrentLongitude] IS NOT NULL))),
        CONSTRAINT [CK_DeliveryAgents_CurrentLongitude_Range] CHECK (([CurrentLongitude] IS NULL OR ([CurrentLongitude] >= -180 AND [CurrentLongitude] <= 180))),
        CONSTRAINT [CK_DeliveryAgents_Status] CHECK ([Status] IN (1, 2, 3, 4)),
        CONSTRAINT [CK_DeliveryAgents_VehicleType] CHECK ([VehicleType] IN (1, 2, 3))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [Restaurants] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [ImageUrl] nvarchar(2048) NULL,
        [OpeningStart] time NOT NULL,
        [OpeningEnd] time NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(200) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Restaurants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [CustomerAddresses] (
        [Id] int NOT NULL IDENTITY,
        [Street] nvarchar(300) NOT NULL,
        [City] nvarchar(120) NOT NULL,
        [BuildingNumber] nvarchar(50) NOT NULL,
        [Floor] nvarchar(50) NULL,
        [IsDefault] bit NOT NULL,
        [CustomerId] int NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_CustomerAddresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerAddresses_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [Carts] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [RestaurantId] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(200) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Carts_Status] CHECK ([Status] IN (1, 2, 3)),
        CONSTRAINT [FK_Carts_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Carts_Restaurants_RestaurantId] FOREIGN KEY ([RestaurantId]) REFERENCES [Restaurants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [RestaurantId] int NOT NULL,
        [DeliveryStreet] nvarchar(300) NOT NULL,
        [DeliveryCity] nvarchar(120) NOT NULL,
        [DeliveryBuildingNumber] nvarchar(50) NOT NULL,
        [DeliveryFloor] nvarchar(50) NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(200) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Orders_TotalAmount_NonNegative] CHECK ([TotalAmount] >= 0),
        CONSTRAINT [FK_Orders_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Restaurants_RestaurantId] FOREIGN KEY ([RestaurantId]) REFERENCES [Restaurants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [RestaurantId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [CurrentPriceAmount] decimal(18,2) NOT NULL,
        [IsAvailable] bit NOT NULL,
        [ImageUrl] nvarchar(2048) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(200) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Products_CurrentPriceAmount_NonNegative] CHECK ([CurrentPriceAmount] >= 0),
        CONSTRAINT [FK_Products_Restaurants_RestaurantId] FOREIGN KEY ([RestaurantId]) REFERENCES [Restaurants] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [Deliveries] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [CustomerId] int NOT NULL,
        [RestaurantId] int NOT NULL,
        [AssignedAgentId] int NULL,
        [Status] int NOT NULL,
        [DeliveryStreet] nvarchar(300) NOT NULL,
        [DeliveryCity] nvarchar(120) NOT NULL,
        [DeliveryBuildingNumber] nvarchar(50) NOT NULL,
        [DeliveryFloor] nvarchar(50) NULL,
        [AssignedAt] datetime2 NULL,
        [ArrivedAtRestaurantAt] datetime2 NULL,
        [PickedUpAt] datetime2 NULL,
        [OutForDeliveryAt] datetime2 NULL,
        [DeliveredAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [FailedAt] datetime2 NULL,
        [FailureReason] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NULL,
        [ModifiedAt] datetime2 NULL,
        [ModifiedBy] nvarchar(200) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(200) NULL,
        CONSTRAINT [PK_Deliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Deliveries_Status] CHECK ([Status] IN (1, 2, 3, 4, 5, 6, 7, 8)),
        CONSTRAINT [FK_Deliveries_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Deliveries_DeliveryAgents_AssignedAgentId] FOREIGN KEY ([AssignedAgentId]) REFERENCES [DeliveryAgents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Deliveries_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Deliveries_Restaurants_RestaurantId] FOREIGN KEY ([RestaurantId]) REFERENCES [Restaurants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [CartItems] (
        [ProductId] int NOT NULL,
        [CartId] int NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [Quantity] int NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([CartId], [ProductId]),
        CONSTRAINT [CK_CartItems_Quantity_Positive] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [ProductId] int NOT NULL,
        [OrderId] int NOT NULL,
        [ProductName] nvarchar(200) NOT NULL,
        [UnitPriceAmount] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL,
        [LineTotalAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([OrderId], [ProductId]),
        CONSTRAINT [CK_OrderItems_LineTotalAmount_NonNegative] CHECK ([LineTotalAmount] >= 0),
        CONSTRAINT [CK_OrderItems_Quantity_Positive] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_OrderItems_UnitPriceAmount_NonNegative] CHECK ([UnitPriceAmount] >= 0),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'ImageUrl', N'IsActive', N'ModifiedAt', N'ModifiedBy', N'Name', N'OpeningEnd', N'OpeningStart') AND [object_id] = OBJECT_ID(N'[Restaurants]'))
        SET IDENTITY_INSERT [Restaurants] ON;
    EXEC(N'INSERT INTO [Restaurants] ([Id], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [ImageUrl], [IsActive], [ModifiedAt], [ModifiedBy], [Name], [OpeningEnd], [OpeningStart])
    VALUES (1, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, N''Charcoal grilled meals and sides.'', NULL, CAST(1 AS bit), NULL, NULL, N''Cairo Grill'', ''23:00:00'', ''10:00:00''),
    (2, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, N''Fresh pizza and baked pasta.'', NULL, CAST(1 AS bit), NULL, NULL, N''Nile Pizza'', ''01:00:00'', ''11:00:00'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'ImageUrl', N'IsActive', N'ModifiedAt', N'ModifiedBy', N'Name', N'OpeningEnd', N'OpeningStart') AND [object_id] = OBJECT_ID(N'[Restaurants]'))
        SET IDENTITY_INSERT [Restaurants] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'ImageUrl', N'IsAvailable', N'ModifiedAt', N'ModifiedBy', N'Name', N'RestaurantId', N'CurrentPriceAmount') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [CreatedAt], [CreatedBy], [DeletedAt], [DeletedBy], [Description], [ImageUrl], [IsAvailable], [ModifiedAt], [ModifiedBy], [Name], [RestaurantId], [CurrentPriceAmount])
    VALUES (101, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, N''Chicken, kofta, rice, and salad.'', NULL, CAST(1 AS bit), NULL, NULL, N''Mixed Grill Plate'', 1, 185.0),
    (102, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, N''Grilled chicken wrap with garlic sauce.'', NULL, CAST(1 AS bit), NULL, NULL, N''Chicken Shawarma'', 1, 95.0),
    (201, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, N''Tomato, mozzarella, and basil.'', NULL, CAST(1 AS bit), NULL, NULL, N''Margherita Pizza'', 2, 140.0),
    (202, ''2026-01-01T00:00:00.0000000Z'', NULL, NULL, NULL, N''Penne pasta with tomato sauce and cheese.'', NULL, CAST(1 AS bit), NULL, NULL, N''Baked Penne'', 2, 125.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'DeletedBy', N'Description', N'ImageUrl', N'IsAvailable', N'ModifiedAt', N'ModifiedBy', N'Name', N'RestaurantId', N'CurrentPriceAmount') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_CartItems_ProductId] ON [CartItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Carts_RestaurantId] ON [Carts] ([RestaurantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Carts_CustomerId_Active] ON [Carts] ([CustomerId]) WHERE [Status] = 1 AND [IsDeleted] = CAST(0 AS bit)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CustomerAddresses_CustomerId_Default] ON [CustomerAddresses] ([CustomerId]) WHERE [IsDefault] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Deliveries_CustomerId] ON [Deliveries] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Deliveries_RestaurantId] ON [Deliveries] ([RestaurantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Deliveries_AssignedAgentId_Active] ON [Deliveries] ([AssignedAgentId]) WHERE [AssignedAgentId] IS NOT NULL AND [Status] IN (2, 3, 4, 5) AND [IsDeleted] = CAST(0 AS bit)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Deliveries_OrderId] ON [Deliveries] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Orders_CustomerId_CreatedAt] ON [Orders] ([CustomerId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Orders_RestaurantId] ON [Orders] ([RestaurantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Products_RestaurantId_Name] ON [Products] ([RestaurantId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    CREATE INDEX [IX_Restaurants_IsActive] ON [Restaurants] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711171406_InitialPersistence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260711171406_InitialPersistence', N'10.0.9');
END;

COMMIT;
GO

