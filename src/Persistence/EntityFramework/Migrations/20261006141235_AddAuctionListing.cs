using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuctionListing",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "text", nullable: false),
                    SellerIp = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Currency = table.Column<byte>(type: "smallint", nullable: false),
                    Category = table.Column<byte>(type: "smallint", nullable: false),
                    StartingPrice = table.Column<long>(type: "bigint", nullable: false),
                    BuyoutPrice = table.Column<long>(type: "bigint", nullable: true),
                    CurrentBid = table.Column<long>(type: "bigint", nullable: true),
                    StartingStrength = table.Column<long>(type: "bigint", nullable: false),
                    StartingAgility = table.Column<long>(type: "bigint", nullable: false),
                    StartingVitality = table.Column<long>(type: "bigint", nullable: false),
                    StartingEnergy = table.Column<long>(type: "bigint", nullable: false),
                    StartingCommand = table.Column<long>(type: "bigint", nullable: false),
                    BuyoutStrength = table.Column<long>(type: "bigint", nullable: true),
                    BuyoutAgility = table.Column<long>(type: "bigint", nullable: true),
                    BuyoutVitality = table.Column<long>(type: "bigint", nullable: true),
                    BuyoutEnergy = table.Column<long>(type: "bigint", nullable: true),
                    BuyoutCommand = table.Column<long>(type: "bigint", nullable: true),
                    CurrentBidStrength = table.Column<long>(type: "bigint", nullable: true),
                    CurrentBidAgility = table.Column<long>(type: "bigint", nullable: true),
                    CurrentBidVitality = table.Column<long>(type: "bigint", nullable: true),
                    CurrentBidEnergy = table.Column<long>(type: "bigint", nullable: true),
                    CurrentBidCommand = table.Column<long>(type: "bigint", nullable: true),
                    CurrentBidderAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentBidderCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginalEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastBidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BidCount = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ItemGroup = table.Column<int>(type: "integer", nullable: false),
                    ItemNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemLevel = table.Column<int>(type: "integer", nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionListing", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_CurrentBidderAccountId_Status",
                schema: "data",
                table: "AuctionListing",
                columns: new[] { "CurrentBidderAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_ItemId",
                schema: "data",
                table: "AuctionListing",
                column: "ItemId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_ListingNumber",
                schema: "data",
                table: "AuctionListing",
                column: "ListingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_SellerAccountId_Status",
                schema: "data",
                table: "AuctionListing",
                columns: new[] { "SellerAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionListing_Status_Currency_Category_EndsAt_ListingNumber",
                schema: "data",
                table: "AuctionListing",
                columns: new[] { "Status", "Currency", "Category", "EndsAt", "ListingNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionListing",
                schema: "data");
        }
    }
}
