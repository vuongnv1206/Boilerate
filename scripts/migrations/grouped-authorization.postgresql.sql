START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    ALTER TABLE "Identity"."Functions" ADD "GroupId" uuid NOT NULL DEFAULT 'cd9a1000-0000-4000-8000-000000000004';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    CREATE TABLE "Identity"."FunctionGroups" (
        "Id" uuid NOT NULL,
        "Code" character varying(100) NOT NULL,
        "NormalizedCode" character varying(100) NOT NULL,
        "Name" character varying(100) NOT NULL,
        "SortOrder" integer NOT NULL,
        "Version" character varying(32) NOT NULL,
        CONSTRAINT "PK_FunctionGroups" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_FunctionGroups_SortOrder" CHECK ("SortOrder" >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    INSERT INTO "Identity"."FunctionGroups" ("Id", "Code", "Name", "NormalizedCode", "SortOrder", "Version")
    VALUES ('cd9a1000-0000-4000-8000-000000000001', 'System', 'Hệ thống', 'SYSTEM', 10, 'initial');
    INSERT INTO "Identity"."FunctionGroups" ("Id", "Code", "Name", "NormalizedCode", "SortOrder", "Version")
    VALUES ('cd9a1000-0000-4000-8000-000000000002', 'Catalog', 'Danh mục và sản phẩm', 'CATALOG', 20, 'initial');
    INSERT INTO "Identity"."FunctionGroups" ("Id", "Code", "Name", "NormalizedCode", "SortOrder", "Version")
    VALUES ('cd9a1000-0000-4000-8000-000000000003', 'Orders', 'Đơn hàng', 'ORDERS', 30, 'initial');
    INSERT INTO "Identity"."FunctionGroups" ("Id", "Code", "Name", "NormalizedCode", "SortOrder", "Version")
    VALUES ('cd9a1000-0000-4000-8000-000000000004', 'Other', 'Chức năng khác', 'OTHER', 99, 'initial');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    UPDATE "Identity"."Functions" SET "GroupId" = CASE WHEN UPPER("Code") IN ('ACTION', 'FUNCTION', 'ROLE', 'USER', 'USERS', 'HANGFIRE', 'OUTBOX', 'NOTIFICATIONS') THEN 'cd9a1000-0000-4000-8000-000000000001'::uuid WHEN UPPER("Code") IN ('PRODUCT', 'PRODUCTS', 'CATEGORY', 'CATEGORIES', 'PRODUCTTYPE', 'PRICE', 'INVENTORY', 'DIGITALINVENTORY') THEN 'cd9a1000-0000-4000-8000-000000000002'::uuid WHEN UPPER("Code") IN ('ORDER', 'PAYMENT', 'SHIPPING', 'RENEWAL', 'AFTERSALES', 'REFUND', 'RETURN') THEN 'cd9a1000-0000-4000-8000-000000000003'::uuid ELSE 'cd9a1000-0000-4000-8000-000000000004'::uuid END
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    CREATE INDEX "IX_Functions_GroupId" ON "Identity"."Functions" ("GroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    CREATE UNIQUE INDEX "IX_FunctionGroups_NormalizedCode" ON "Identity"."FunctionGroups" ("NormalizedCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    ALTER TABLE "Identity"."Functions" ADD CONSTRAINT "FK_Functions_FunctionGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "Identity"."FunctionGroups" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021042312_GroupedAuthorization') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021042312_GroupedAuthorization', '10.0.12');
    END IF;
END $EF$;
COMMIT;

