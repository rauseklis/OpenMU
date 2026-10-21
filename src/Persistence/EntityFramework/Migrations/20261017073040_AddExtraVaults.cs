using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddExtraVaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                schema: "data",
                table: "ItemStorage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "VaultNumber",
                schema: "data",
                table: "ItemStorage",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateIndex(
                name: "IX_ItemStorage_AccountId",
                schema: "data",
                table: "ItemStorage",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemStorage_Account_AccountId",
                schema: "data",
                table: "ItemStorage",
                column: "AccountId",
                principalSchema: "data",
                principalTable: "Account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemStorage_Account_AccountId",
                schema: "data",
                table: "ItemStorage");

            migrationBuilder.DropIndex(
                name: "IX_ItemStorage_AccountId",
                schema: "data",
                table: "ItemStorage");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "data",
                table: "ItemStorage");

            migrationBuilder.DropColumn(
                name: "VaultNumber",
                schema: "data",
                table: "ItemStorage");
        }
    }
}
