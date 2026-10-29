// <copyright file="20261029140000_AddPetRentalLifetime.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Additive metadata only. Existing items/storage rows remain null; no guessed origin or expiry.</summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261029140000_AddPetRentalLifetime")]
public sealed class AddPetRentalLifetime : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(name: "RentalExpiresAt", schema: "data", table: "Item", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<int>(name: "RentalDurationSeconds", schema: "data", table: "CashShopStorage", type: "integer", nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RentalExpiresAt", schema: "data", table: "Item");
        migrationBuilder.DropColumn(name: "RentalDurationSeconds", schema: "data", table: "CashShopStorage");
    }
}
