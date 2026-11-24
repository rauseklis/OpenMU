// <copyright file="20261124120000_AddServerBonusLevelChances.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Additive and nullable: every existing server keeps the shared bonus group chances until a value is set.</summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261124120000_AddServerBonusLevelChances")]
public sealed class AddServerBonusLevelChances : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "SmallBonusLevelChances", schema: "config", table: "GameServerDefinition", type: "text", nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SmallBonusLevelChances", schema: "config", table: "GameServerDefinition");
    }
}
