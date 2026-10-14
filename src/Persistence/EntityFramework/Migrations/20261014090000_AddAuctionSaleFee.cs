using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261014090000_AddAuctionSaleFee")]
    public partial class AddAuctionSaleFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SaleFeeHeld",
                schema: "data",
                table: "AuctionListing",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte>(
                name: "SaleFeeState",
                schema: "data",
                table: "AuctionListing",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaleFeeHeld",
                schema: "data",
                table: "AuctionListing");

            migrationBuilder.DropColumn(
                name: "SaleFeeState",
                schema: "data",
                table: "AuctionListing");
        }
    }
}
