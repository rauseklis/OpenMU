// <copyright file="20261125100000_AddServerFruitNpcSaleDisabled.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Additive: every existing server keeps selling Fruits to NPC merchants (default false).</summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261125100000_AddServerFruitNpcSaleDisabled")]
public sealed class AddServerFruitNpcSaleDisabled : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "FruitNpcSaleDisabled", schema: "config", table: "GameServerDefinition", type: "boolean", nullable: false, defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FruitNpcSaleDisabled", schema: "config", table: "GameServerDefinition");
    }
}
