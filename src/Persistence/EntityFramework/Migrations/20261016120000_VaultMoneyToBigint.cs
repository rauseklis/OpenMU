using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <summary>
    /// Widens the stored item storage money and the maximum vault money to 64 bit,
    /// so that the vault can hold more than 2.1 billion Zen.
    /// </summary>
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261016120000_VaultMoneyToBigint")]
    public partial class VaultMoneyToBigint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "Money",
                schema: "data",
                table: "ItemStorage",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "MaximumVaultMoney",
                schema: "config",
                table: "GameConfiguration",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql("UPDATE config.\"GameConfiguration\" SET \"MaximumVaultMoney\" = 1000000000000;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Money",
                schema: "data",
                table: "ItemStorage",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "MaximumVaultMoney",
                schema: "config",
                table: "GameConfiguration",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.Sql("UPDATE config.\"GameConfiguration\" SET \"MaximumVaultMoney\" = 2147483647;");
            migrationBuilder.Sql("UPDATE data.\"ItemStorage\" SET \"Money\" = 2147483647 WHERE \"Money\" > 2147483647;");
        }
    }
}
