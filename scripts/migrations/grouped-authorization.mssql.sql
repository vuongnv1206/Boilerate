BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    ALTER TABLE [Identity].[Functions] ADD [GroupId] uniqueidentifier NOT NULL DEFAULT 'cd9a1000-0000-4000-8000-000000000004';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    CREATE TABLE [Identity].[FunctionGroups] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [NormalizedCode] nvarchar(100) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL,
        [Version] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_FunctionGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_FunctionGroups_SortOrder] CHECK ("SortOrder" >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name', N'NormalizedCode', N'SortOrder', N'Version') AND [object_id] = OBJECT_ID(N'[Identity].[FunctionGroups]'))
        SET IDENTITY_INSERT [Identity].[FunctionGroups] ON;
    EXEC(N'INSERT INTO [Identity].[FunctionGroups] ([Id], [Code], [Name], [NormalizedCode], [SortOrder], [Version])
    VALUES (''cd9a1000-0000-4000-8000-000000000001'', N''System'', N''Hệ thống'', N''SYSTEM'', 10, N''initial''),
    (''cd9a1000-0000-4000-8000-000000000002'', N''Catalog'', N''Danh mục và sản phẩm'', N''CATALOG'', 20, N''initial''),
    (''cd9a1000-0000-4000-8000-000000000003'', N''Orders'', N''Đơn hàng'', N''ORDERS'', 30, N''initial''),
    (''cd9a1000-0000-4000-8000-000000000004'', N''Other'', N''Chức năng khác'', N''OTHER'', 99, N''initial'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Name', N'NormalizedCode', N'SortOrder', N'Version') AND [object_id] = OBJECT_ID(N'[Identity].[FunctionGroups]'))
        SET IDENTITY_INSERT [Identity].[FunctionGroups] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    UPDATE [Identity].[Functions] SET [GroupId] = CASE WHEN UPPER([Code]) IN ('ACTION', 'FUNCTION', 'ROLE', 'USER', 'USERS', 'HANGFIRE', 'OUTBOX', 'NOTIFICATIONS') THEN 'cd9a1000-0000-4000-8000-000000000001' WHEN UPPER([Code]) IN ('PRODUCT', 'PRODUCTS', 'CATEGORY', 'CATEGORIES', 'PRODUCTTYPE', 'PRICE', 'INVENTORY', 'DIGITALINVENTORY') THEN 'cd9a1000-0000-4000-8000-000000000002' WHEN UPPER([Code]) IN ('ORDER', 'PAYMENT', 'SHIPPING', 'RENEWAL', 'AFTERSALES', 'REFUND', 'RETURN') THEN 'cd9a1000-0000-4000-8000-000000000003' ELSE 'cd9a1000-0000-4000-8000-000000000004' END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    CREATE INDEX [IX_Functions_GroupId] ON [Identity].[Functions] ([GroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FunctionGroups_NormalizedCode] ON [Identity].[FunctionGroups] ([NormalizedCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    ALTER TABLE [Identity].[Functions] ADD CONSTRAINT [FK_Functions_FunctionGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Identity].[FunctionGroups] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021042258_GroupedAuthorization'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261021042258_GroupedAuthorization', N'10.0.12');
END;

COMMIT;
GO

