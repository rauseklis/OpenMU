// <copyright file="20261022190000_AddSmallBonusServerOptIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Allows individual servers to opt in to shared small-bonus drop groups.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261022190000_AddSmallBonusServerOptIn")]
public partial class AddSmallBonusServerOptIn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "SmallBonusDropsEnabled",
            schema: "config",
            table: "GameServerDefinition",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SmallBonusDropsEnabled",
            schema: "config",
            table: "GameServerDefinition");
    }
}
