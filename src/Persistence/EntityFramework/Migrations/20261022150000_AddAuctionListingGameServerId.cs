using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <summary>
    /// Every Auction House listing belongs to one game server. The listings which exist already belong to the first
    /// game server (id 0).
    /// </summary>
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261022150000_AddAuctionListingGameServerId")]
    public partial class AddAuctionListingGameServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "GameServerId",
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
                name: "GameServerId",
                schema: "data",
                table: "AuctionListing");
        }
    }
}
