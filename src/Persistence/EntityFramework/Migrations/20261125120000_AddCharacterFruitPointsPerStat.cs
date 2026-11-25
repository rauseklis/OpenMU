// <copyright file="20261125120000_AddCharacterFruitPointsPerStat.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Additive: remembers per stat how many points came from fruits. Existing characters start with 0.</summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261125120000_AddCharacterFruitPointsPerStat")]
public sealed class AddCharacterFruitPointsPerStat : Migration
{
    private static readonly string[] Columns = ["FruitPointsStrength", "FruitPointsAgility", "FruitPointsVitality", "FruitPointsEnergy", "FruitPointsLeadership"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.AddColumn<int>(name: column, schema: "data", table: "Character", type: "integer", nullable: false, defaultValue: 0);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.DropColumn(name: column, schema: "data", table: "Character");
        }
    }
}
