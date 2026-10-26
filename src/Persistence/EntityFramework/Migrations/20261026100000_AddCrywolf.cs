// <copyright file="20261026100000_AddCrywolf.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds the persisted state of the crywolf event and the terrain variants of game maps.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261026100000_AddCrywolf")]
public partial class AddCrywolf : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CrywolfData",
            schema: "data",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GameServerId = table.Column<byte>(type: "smallint", nullable: false),
                IsOccupied = table.Column<bool>(type: "boolean", nullable: false),
                IsWarRunning = table.Column<bool>(type: "boolean", nullable: false),
                WarStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastBattleEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_CrywolfData", x => x.Id));

        migrationBuilder.CreateTable(
            name: "GameMapTerrainVariant",
            schema: "config",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                GameMapDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                Number = table.Column<short>(type: "smallint", nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                TerrainData = table.Column<byte[]>(type: "bytea", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameMapTerrainVariant", x => x.Id);
                table.ForeignKey(
                    name: "FK_GameMapTerrainVariant_GameMapDefinition_GameMapDefinitionId",
                    column: x => x.GameMapDefinitionId,
                    principalSchema: "config",
                    principalTable: "GameMapDefinition",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GameMapTerrainVariant_GameMapDefinitionId",
            schema: "config",
            table: "GameMapTerrainVariant",
            column: "GameMapDefinitionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GameMapTerrainVariant", schema: "config");
        migrationBuilder.DropTable(name: "CrywolfData", schema: "data");
    }
}
