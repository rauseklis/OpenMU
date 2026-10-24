// <copyright file="20261024010000_AddBoxRewardOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds optional box reward rules while preserving shared Luck and ADD configuration.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261024010000_AddBoxRewardOptions")]
public partial class AddBoxRewardOptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "SmallBonusDropChance",
            schema: "config",
            table: "GameServerDefinition",
            type: "double precision",
            nullable: true);
        migrationBuilder.AddColumn<double>(
            name: "ExcellentSecondOptionChance",
            schema: "config",
            table: "ItemDropItemGroup",
            type: "double precision",
            nullable: true);
        migrationBuilder.AddColumn<double>(
            name: "SkillChance",
            schema: "config",
            table: "ItemDropItemGroup",
            type: "double precision",
            nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "GuaranteeJewelryOption",
            schema: "config",
            table: "ItemDropItemGroup",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SmallBonusDropChance", schema: "config", table: "GameServerDefinition");
        migrationBuilder.DropColumn(name: "ExcellentSecondOptionChance", schema: "config", table: "ItemDropItemGroup");
        migrationBuilder.DropColumn(name: "SkillChance", schema: "config", table: "ItemDropItemGroup");
        migrationBuilder.DropColumn(name: "GuaranteeJewelryOption", schema: "config", table: "ItemDropItemGroup");
    }
}
