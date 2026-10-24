// <copyright file="20261024050000_AddBoxLuckChance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds optional luck odds for individual box rewards.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261024050000_AddBoxLuckChance")]
public partial class AddBoxLuckChance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<double>(name: "LuckChance", schema: "config", table: "ItemDropItemGroup", type: "double precision", nullable: true);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "LuckChance", schema: "config", table: "ItemDropItemGroup");
}
