using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261014120000_AddAuctionMixedPrices")]
    public partial class AddAuctionMixedPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "PriceVector", schema: "data", table: "AuctionListing", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "BuyoutVector", schema: "data", table: "AuctionListing", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "CurrentBidVector", schema: "data", table: "AuctionListing", type: "text", nullable: true);
            migrationBuilder.AddColumn<int>(name: "KindMask", schema: "data", table: "AuctionListing", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "AmountVector", schema: "data", table: "AuctionBid", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "OriginalVector", schema: "data", table: "AuctionCollection", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "RemainingVector", schema: "data", table: "AuctionCollection", type: "text", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PriceVector", schema: "data", table: "AuctionListing");
            migrationBuilder.DropColumn(name: "BuyoutVector", schema: "data", table: "AuctionListing");
            migrationBuilder.DropColumn(name: "CurrentBidVector", schema: "data", table: "AuctionListing");
            migrationBuilder.DropColumn(name: "KindMask", schema: "data", table: "AuctionListing");
            migrationBuilder.DropColumn(name: "AmountVector", schema: "data", table: "AuctionBid");
            migrationBuilder.DropColumn(name: "OriginalVector", schema: "data", table: "AuctionCollection");
            migrationBuilder.DropColumn(name: "RemainingVector", schema: "data", table: "AuctionCollection");
        }
    }
}
