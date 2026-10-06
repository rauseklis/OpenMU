using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionRemainingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuctionAccountRestriction",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    AdministratorName = table.Column<string>(type: "text", nullable: false),
                    ListingLimitOverride = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionAccountRestriction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionBid",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BidderAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BidderCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<byte>(type: "smallint", nullable: false),
                    ScalarAmount = table.Column<long>(type: "bigint", nullable: true),
                    StrengthAmount = table.Column<long>(type: "bigint", nullable: true),
                    AgilityAmount = table.Column<long>(type: "bigint", nullable: true),
                    VitalityAmount = table.Column<long>(type: "bigint", nullable: true),
                    EnergyAmount = table.Column<long>(type: "bigint", nullable: true),
                    CommandAmount = table.Column<long>(type: "bigint", nullable: true),
                    BidderIp = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionBid", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionCollection",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    Currency = table.Column<byte>(type: "smallint", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalScalar = table.Column<long>(type: "bigint", nullable: true),
                    RemainingScalar = table.Column<long>(type: "bigint", nullable: true),
                    OriginalStrength = table.Column<long>(type: "bigint", nullable: true),
                    OriginalAgility = table.Column<long>(type: "bigint", nullable: true),
                    OriginalVitality = table.Column<long>(type: "bigint", nullable: true),
                    OriginalEnergy = table.Column<long>(type: "bigint", nullable: true),
                    OriginalCommand = table.Column<long>(type: "bigint", nullable: true),
                    RemainingStrength = table.Column<long>(type: "bigint", nullable: true),
                    RemainingAgility = table.Column<long>(type: "bigint", nullable: true),
                    RemainingVitality = table.Column<long>(type: "bigint", nullable: true),
                    RemainingEnergy = table.Column<long>(type: "bigint", nullable: true),
                    RemainingCommand = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionCollection", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionCurrencyMovement",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    BidId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<byte>(type: "smallint", nullable: false),
                    FruitComponent = table.Column<byte>(type: "smallint", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackedUnitValue = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionCurrencyMovement", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionNotification",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    Currency = table.Column<byte>(type: "smallint", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionNotification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionOperation",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionOperation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionTransactionLog",
                schema: "data",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<byte>(type: "smallint", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    CounterpartAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<byte>(type: "smallint", nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: true),
                    Fee = table.Column<long>(type: "bigint", nullable: true),
                    BalanceBefore = table.Column<long>(type: "bigint", nullable: true),
                    BalanceAfter = table.Column<long>(type: "bigint", nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorIp = table.Column<string>(type: "text", nullable: true),
                    IsSuspicious = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionTransactionLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionAccountRestriction_AccountId",
                schema: "data",
                table: "AuctionAccountRestriction",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionBid_BidderAccountId_Status",
                schema: "data",
                table: "AuctionBid",
                columns: new[] { "BidderAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionBid_ListingId",
                schema: "data",
                table: "AuctionBid",
                column: "ListingId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollection_AccountId_Status",
                schema: "data",
                table: "AuctionCollection",
                columns: new[] { "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollection_CollectionNumber",
                schema: "data",
                table: "AuctionCollection",
                column: "CollectionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCollection_ItemId",
                schema: "data",
                table: "AuctionCollection",
                column: "ItemId",
                unique: true,
                filter: "\"ItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCurrencyMovement_AccountId_Currency",
                schema: "data",
                table: "AuctionCurrencyMovement",
                columns: new[] { "AccountId", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionCurrencyMovement_OperationId",
                schema: "data",
                table: "AuctionCurrencyMovement",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionNotification_AccountId_DeliveredAt",
                schema: "data",
                table: "AuctionNotification",
                columns: new[] { "AccountId", "DeliveredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionOperation_AccountId_OperationId",
                schema: "data",
                table: "AuctionOperation",
                columns: new[] { "AccountId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTransactionLog_ActorAccountId_CreatedAt",
                schema: "data",
                table: "AuctionTransactionLog",
                columns: new[] { "ActorAccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTransactionLog_ListingId",
                schema: "data",
                table: "AuctionTransactionLog",
                column: "ListingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionAccountRestriction",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionBid",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionCollection",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionCurrencyMovement",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionNotification",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionOperation",
                schema: "data");

            migrationBuilder.DropTable(
                name: "AuctionTransactionLog",
                schema: "data");
        }
    }
}
