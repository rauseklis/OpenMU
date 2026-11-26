// <copyright file="20261126120000_AddServerRequestFilterDisabled.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>Additive: every existing server keeps the request filter (/re) available (default false).</summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261126120000_AddServerRequestFilterDisabled")]
public sealed class AddServerRequestFilterDisabled : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "RequestFilterDisabled", schema: "config", table: "GameServerDefinition", type: "boolean", nullable: false, defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RequestFilterDisabled", schema: "config", table: "GameServerDefinition");
    }
}
