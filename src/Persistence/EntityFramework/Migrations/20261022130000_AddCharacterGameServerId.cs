using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <summary>
    /// Every character belongs to one game server (for example the 50x or the 5x experience server). The characters
    /// which exist already stay on the first server (id 0).
    /// </summary>
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261022130000_AddCharacterGameServerId")]
    public partial class AddCharacterGameServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "GameServerId",
                schema: "data",
                table: "Character",
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
                table: "Character");
        }
    }
}
