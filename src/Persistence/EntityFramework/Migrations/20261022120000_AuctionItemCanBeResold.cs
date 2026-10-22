using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <summary>
    /// An item which was bought and claimed from the Auction House can be listed and sold again. The unique index
    /// on the item of a collection only covers unclaimed collections (pending or partially claimed).
    /// </summary>
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261022120000_AuctionItemCanBeResold")]
    public partial class AuctionItemCanBeResold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX data."IX_AuctionCollection_ItemId";
                CREATE UNIQUE INDEX "IX_AuctionCollection_ItemId" ON data."AuctionCollection" ("ItemId")
                    WHERE "ItemId" IS NOT NULL AND "Status" IN (0, 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX data."IX_AuctionCollection_ItemId";
                CREATE UNIQUE INDEX "IX_AuctionCollection_ItemId" ON data."AuctionCollection" ("ItemId")
                    WHERE "ItemId" IS NOT NULL;
                """);
        }
    }
}
