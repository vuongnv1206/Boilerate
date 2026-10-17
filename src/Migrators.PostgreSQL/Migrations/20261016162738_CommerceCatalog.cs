using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Boilerate.Migrators.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class CommerceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DO $$ BEGIN IF EXISTS (SELECT 1 FROM "Catalog"."Products") OR EXISTS (SELECT 1 FROM "Catalog"."Categories") THEN RAISE EXCEPTION 'CommerceCatalog requires an empty Catalog; migrate existing data explicitly.'; END IF; END $$;""");
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Price",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.EnsureSchema(
                name: "Inventory");

            migrationBuilder.EnsureSchema(
                name: "Media");

            migrationBuilder.EnsureSchema(
                name: "Messaging");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                schema: "Catalog",
                table: "Products",
                newName: "ProductTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoryId",
                schema: "Catalog",
                table: "Products",
                newName: "IX_Products_ProductTypeId");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                schema: "Catalog",
                table: "Categories",
                newName: "ParentId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "Catalog",
                table: "Products",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "Catalog",
                table: "Products",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSlug",
                schema: "Catalog",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "Catalog",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "Catalog",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                schema: "Catalog",
                table: "Products",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "Catalog",
                table: "Categories",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "Catalog",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSlug",
                schema: "Catalog",
                table: "Categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "Catalog",
                table: "Categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "Catalog",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                schema: "Catalog",
                table: "Categories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Products_Id_ProductTypeId",
                schema: "Catalog",
                table: "Products",
                columns: new[] { "Id", "ProductTypeId" });

            migrationBuilder.CreateTable(
                name: "CommerceLocks",
                schema: "Catalog",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommerceLocks", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLocations",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                schema: "Media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "Messaging",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                schema: "Catalog",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => new { x.ProductId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ProductCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "Catalog",
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductCategories_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptions",
                schema: "Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptions", x => x.Id);
                    table.UniqueConstraint("AK_ProductOptions_ProductId_Id", x => new { x.ProductId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductOptions_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductTypes",
                schema: "Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductMedia",
                schema: "Catalog",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMedia", x => new { x.ProductId, x.MediaAssetId });
                    table.ForeignKey(
                        name: "FK_ProductMedia_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalSchema: "Media",
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductMedia_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptionValues",
                schema: "Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptionValues", x => x.Id);
                    table.UniqueConstraint("AK_ProductOptionValues_OptionId_Id", x => new { x.OptionId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductOptionValues_ProductOptions_OptionId",
                        column: x => x.OptionId,
                        principalSchema: "Catalog",
                        principalTable: "ProductOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductFieldDefinitions",
                schema: "Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HelpText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsFilterable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFieldDefinitions", x => x.Id);
                    table.UniqueConstraint("AK_ProductFieldDefinitions_Id_ProductTypeId_DataType", x => new { x.Id, x.ProductTypeId, x.DataType });
                    table.CheckConstraint("CK_Field_Type", "\"DataType\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_ProductFieldDefinitions_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalSchema: "Catalog",
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                schema: "Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CombinationKey = table.Column<string>(type: "character varying(700)", unicode: false, maxLength: 700, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CompareAtPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ImageMediaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.UniqueConstraint("AK_ProductVariants_ProductId_Id", x => new { x.ProductId, x.Id });
                    table.CheckConstraint("CK_Variant_Price", "(\"Price\" IS NULL OR (\"Price\" >= 0 AND \"Price\" = FLOOR(\"Price\"))) AND (\"CompareAtPrice\" IS NULL OR (\"Price\" IS NOT NULL AND \"CompareAtPrice\" > \"Price\" AND \"CompareAtPrice\" = FLOOR(\"CompareAtPrice\"))) AND \"Currency\" = 'VND'");
                    table.ForeignKey(
                        name: "FK_ProductVariants_ProductMedia_ProductId_ImageMediaId",
                        columns: x => new { x.ProductId, x.ImageMediaId },
                        principalSchema: "Catalog",
                        principalTable: "ProductMedia",
                        principalColumns: new[] { "ProductId", "MediaAssetId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "Catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductFieldChoices",
                schema: "Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFieldChoices", x => x.Id);
                    table.UniqueConstraint("AK_ProductFieldChoices_FieldDefinitionId_Id", x => new { x.FieldDefinitionId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProductFieldChoices_ProductFieldDefinitions_FieldDefinition~",
                        column: x => x.FieldDefinitionId,
                        principalSchema: "Catalog",
                        principalTable: "ProductFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OnHand = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.CheckConstraint("CK_Inventory_Nonnegative", "\"OnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryItems_InventoryLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "Inventory",
                        principalTable: "InventoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "Catalog",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VariantOptionValues",
                schema: "Catalog",
                columns: table => new
                {
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionValueId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantOptionValues", x => new { x.VariantId, x.OptionId });
                    table.ForeignKey(
                        name: "FK_VariantOptionValues_ProductOptionValues_OptionId_OptionValu~",
                        columns: x => new { x.OptionId, x.OptionValueId },
                        principalSchema: "Catalog",
                        principalTable: "ProductOptionValues",
                        principalColumns: new[] { "OptionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantOptionValues_ProductOptions_ProductId_OptionId",
                        columns: x => new { x.ProductId, x.OptionId },
                        principalSchema: "Catalog",
                        principalTable: "ProductOptions",
                        principalColumns: new[] { "ProductId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantOptionValues_ProductVariants_ProductId_VariantId",
                        columns: x => new { x.ProductId, x.VariantId },
                        principalSchema: "Catalog",
                        principalTable: "ProductVariants",
                        principalColumns: new[] { "ProductId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductFieldValues",
                schema: "Catalog",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldType = table.Column<int>(type: "integer", nullable: false),
                    TextValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    DateValue = table.Column<DateOnly>(type: "date", nullable: true),
                    ChoiceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductFieldValues", x => new { x.ProductId, x.FieldDefinitionId });
                    table.CheckConstraint("CK_FieldValue_Type", "(\"FieldType\" = 1 AND \"TextValue\" IS NOT NULL AND \"NumberValue\" IS NULL AND \"BooleanValue\" IS NULL AND \"DateValue\" IS NULL AND \"ChoiceId\" IS NULL) OR (\"FieldType\" = 2 AND \"TextValue\" IS NULL AND \"NumberValue\" IS NOT NULL AND \"BooleanValue\" IS NULL AND \"DateValue\" IS NULL AND \"ChoiceId\" IS NULL) OR (\"FieldType\" = 3 AND \"TextValue\" IS NULL AND \"NumberValue\" IS NULL AND \"BooleanValue\" IS NOT NULL AND \"DateValue\" IS NULL AND \"ChoiceId\" IS NULL) OR (\"FieldType\" = 4 AND \"TextValue\" IS NULL AND \"NumberValue\" IS NULL AND \"BooleanValue\" IS NULL AND \"DateValue\" IS NOT NULL AND \"ChoiceId\" IS NULL) OR (\"FieldType\" = 5 AND \"TextValue\" IS NULL AND \"NumberValue\" IS NULL AND \"BooleanValue\" IS NULL AND \"DateValue\" IS NULL AND \"ChoiceId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProductFieldValues_ProductFieldChoices_FieldDefinitionId_Ch~",
                        columns: x => new { x.FieldDefinitionId, x.ChoiceId },
                        principalSchema: "Catalog",
                        principalTable: "ProductFieldChoices",
                        principalColumns: new[] { "FieldDefinitionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductFieldValues_ProductFieldDefinitions_FieldDefinitionI~",
                        columns: x => new { x.FieldDefinitionId, x.ProductTypeId, x.FieldType },
                        principalSchema: "Catalog",
                        principalTable: "ProductFieldDefinitions",
                        principalColumns: new[] { "Id", "ProductTypeId", "DataType" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductFieldValues_Products_ProductId_ProductTypeId",
                        columns: x => new { x.ProductId, x.ProductTypeId },
                        principalSchema: "Catalog",
                        principalTable: "Products",
                        principalColumns: new[] { "Id", "ProductTypeId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Delta = table.Column<int>(type: "integer", nullable: false),
                    QuantityAfter = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_Movement_Amounts", "\"Delta\" <> 0 AND \"QuantityAfter\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "Inventory",
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Catalog",
                table: "CommerceLocks",
                columns: new[] { "Name", "Version" },
                values: new object[,]
                {
                    { "categories", "initial" },
                    { "initialization", "initial" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedSlug",
                schema: "Catalog",
                table: "Products",
                column: "NormalizedSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Status_ProductTypeId",
                schema: "Catalog",
                table: "Products",
                columns: new[] { "Status", "ProductTypeId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_Status",
                schema: "Catalog",
                table: "Products",
                sql: "\"Status\" IN (1,2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedSlug",
                schema: "Catalog",
                table: "Categories",
                column: "NormalizedSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                schema: "Catalog",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Category_Parent",
                schema: "Catalog",
                table: "Categories",
                sql: "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_LocationId",
                schema: "Inventory",
                table: "InventoryItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_VariantId_LocationId",
                schema: "Inventory",
                table: "InventoryItems",
                columns: new[] { "VariantId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLocations_Code",
                schema: "Inventory",
                table: "InventoryLocations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_State_ExpiresAt",
                schema: "Media",
                table: "MediaAssets",
                columns: new[] { "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_StorageKey",
                schema: "Media",
                table: "MediaAssets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_State_NextAttemptAt_LeaseUntil",
                schema: "Messaging",
                table: "OutboxMessages",
                columns: new[] { "State", "NextAttemptAt", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_CategoryId_ProductId",
                schema: "Catalog",
                table: "ProductCategories",
                columns: new[] { "CategoryId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldChoices_FieldDefinitionId_Code",
                schema: "Catalog",
                table: "ProductFieldChoices",
                columns: new[] { "FieldDefinitionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldDefinitions_ProductTypeId_Code",
                schema: "Catalog",
                table: "ProductFieldDefinitions",
                columns: new[] { "ProductTypeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldValues_FieldDefinitionId_BooleanValue_ProductId",
                schema: "Catalog",
                table: "ProductFieldValues",
                columns: new[] { "FieldDefinitionId", "BooleanValue", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldValues_FieldDefinitionId_ChoiceId_ProductId",
                schema: "Catalog",
                table: "ProductFieldValues",
                columns: new[] { "FieldDefinitionId", "ChoiceId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldValues_FieldDefinitionId_DateValue_ProductId",
                schema: "Catalog",
                table: "ProductFieldValues",
                columns: new[] { "FieldDefinitionId", "DateValue", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldValues_FieldDefinitionId_NumberValue_ProductId",
                schema: "Catalog",
                table: "ProductFieldValues",
                columns: new[] { "FieldDefinitionId", "NumberValue", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldValues_FieldDefinitionId_ProductTypeId_FieldType",
                schema: "Catalog",
                table: "ProductFieldValues",
                columns: new[] { "FieldDefinitionId", "ProductTypeId", "FieldType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldValues_FieldDefinitionId_TextValue_ProductId",
                schema: "Catalog",
                table: "ProductFieldValues",
                columns: new[] { "FieldDefinitionId", "TextValue", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductFieldValues_ProductId_ProductTypeId",
                schema: "Catalog",
                table: "ProductFieldValues",
                columns: new[] { "ProductId", "ProductTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductMedia_MediaAssetId",
                schema: "Catalog",
                table: "ProductMedia",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptions_ProductId_Code",
                schema: "Catalog",
                table: "ProductOptions",
                columns: new[] { "ProductId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptionValues_OptionId_Code",
                schema: "Catalog",
                table: "ProductOptionValues",
                columns: new[] { "OptionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_NormalizedCode",
                schema: "Catalog",
                table: "ProductTypes",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_NormalizedSku",
                schema: "Catalog",
                table: "ProductVariants",
                column: "NormalizedSku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                schema: "Catalog",
                table: "ProductVariants",
                column: "ProductId",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_CombinationKey",
                schema: "Catalog",
                table: "ProductVariants",
                columns: new[] { "ProductId", "CombinationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_ImageMediaId",
                schema: "Catalog",
                table: "ProductVariants",
                columns: new[] { "ProductId", "ImageMediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryItemId_CreatedAt_Id",
                schema: "Inventory",
                table: "StockMovements",
                columns: new[] { "InventoryItemId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OperationId",
                schema: "Inventory",
                table: "StockMovements",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantOptionValues_OptionId_OptionValueId",
                schema: "Catalog",
                table: "VariantOptionValues",
                columns: new[] { "OptionId", "OptionValueId" });

            migrationBuilder.CreateIndex(
                name: "IX_VariantOptionValues_ProductId_OptionId",
                schema: "Catalog",
                table: "VariantOptionValues",
                columns: new[] { "ProductId", "OptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_VariantOptionValues_ProductId_VariantId",
                schema: "Catalog",
                table: "VariantOptionValues",
                columns: new[] { "ProductId", "VariantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentId",
                schema: "Catalog",
                table: "Categories",
                column: "ParentId",
                principalSchema: "Catalog",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductTypes_ProductTypeId",
                schema: "Catalog",
                table: "Products",
                column: "ProductTypeId",
                principalSchema: "Catalog",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentId",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductTypes_ProductTypeId",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropTable(
                name: "CommerceLocks",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "Messaging");

            migrationBuilder.DropTable(
                name: "ProductCategories",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "ProductFieldValues",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "Inventory");

            migrationBuilder.DropTable(
                name: "VariantOptionValues",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "ProductFieldChoices",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "InventoryItems",
                schema: "Inventory");

            migrationBuilder.DropTable(
                name: "ProductOptionValues",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "ProductFieldDefinitions",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "InventoryLocations",
                schema: "Inventory");

            migrationBuilder.DropTable(
                name: "ProductVariants",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "ProductOptions",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "ProductTypes",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "ProductMedia",
                schema: "Catalog");

            migrationBuilder.DropTable(
                name: "MediaAssets",
                schema: "Media");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Products_Id_ProductTypeId",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedSlug",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Status_ProductTypeId",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_Status",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_NormalizedSlug",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentId",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Category_Parent",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "NormalizedSlug",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "NormalizedSlug",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Catalog",
                table: "Categories");

            migrationBuilder.RenameColumn(
                name: "ProductTypeId",
                schema: "Catalog",
                table: "Products",
                newName: "CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_Products_ProductTypeId",
                schema: "Catalog",
                table: "Products",
                newName: "IX_Products_CategoryId");

            migrationBuilder.RenameColumn(
                name: "ParentId",
                schema: "Catalog",
                table: "Categories",
                newName: "DeletedBy");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "Catalog",
                table: "Products",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "Catalog",
                table: "Products",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                schema: "Catalog",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                schema: "Catalog",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                schema: "Catalog",
                table: "Products",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                schema: "Catalog",
                table: "Products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "Catalog",
                table: "Categories",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                schema: "Catalog",
                table: "Categories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                schema: "Catalog",
                table: "Products",
                column: "CategoryId",
                principalSchema: "Catalog",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
