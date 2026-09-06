using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItemMaster.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialItemMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ItemMaster",
                schema: "dbo",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemCode = table.Column<string>(type: "varchar(30)", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    ItemCategoryId = table.Column<int>(type: "int", nullable: false),
                    ItemGroupId = table.Column<int>(type: "int", nullable: false),
                    ItemSubGroupId = table.Column<int>(type: "int", nullable: false),
                    ItemFamilyId = table.Column<int>(type: "int", nullable: false),
                    AttributeTemplateId = table.Column<int>(type: "int", nullable: true),
                    BaseUOMId = table.Column<int>(type: "int", nullable: false),
                    CanPurchase = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CanSell = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CanManufacture = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CanStock = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CanTransfer = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsSerialControlled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsLotControlled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ItemStatus = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Draft"),
                    VersionNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemMaster", x => x.ItemId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "varchar(255)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ItemMaster_Code",
                schema: "dbo",
                table: "ItemMaster",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "dbo",
                table: "OutboxMessages",
                column: "OccurredOnUtc",
                filter: "[ProcessedOnUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemMaster",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "dbo");
        }
    }
}
