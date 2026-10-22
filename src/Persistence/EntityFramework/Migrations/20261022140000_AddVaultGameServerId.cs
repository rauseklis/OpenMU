using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <summary>
    /// Every game server has its own vaults. The vaults which exist already belong to the first game server (id 0).
    /// </summary>
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261022140000_AddVaultGameServerId")]
    public partial class AddVaultGameServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "GameServerId",
                schema: "data",
                table: "ItemStorage",
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
                table: "ItemStorage");
        }
    }
}
