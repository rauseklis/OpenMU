// <copyright file="20261024100000_AddAncientBoxOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Adds explicit Ancient set selection and stat bonus weighting for reward boxes.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261024100000_AddAncientBoxOptions")]
public partial class AddAncientBoxOptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "AncientSetId", schema: "config", table: "ItemDropItemGroup", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<double>(name: "AncientBonusLevelTwoChance", schema: "config", table: "ItemDropItemGroup", type: "double precision", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_ItemDropItemGroup_AncientSetId", schema: "config", table: "ItemDropItemGroup", column: "AncientSetId");
        migrationBuilder.AddForeignKey(name: "FK_ItemDropItemGroup_ItemSetGroup_AncientSetId", schema: "config", table: "ItemDropItemGroup",
            column: "AncientSetId", principalSchema: "config", principalTable: "ItemSetGroup", principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_ItemDropItemGroup_ItemSetGroup_AncientSetId", schema: "config", table: "ItemDropItemGroup");
        migrationBuilder.DropIndex(name: "IX_ItemDropItemGroup_AncientSetId", schema: "config", table: "ItemDropItemGroup");
        migrationBuilder.DropColumn(name: "AncientSetId", schema: "config", table: "ItemDropItemGroup");
        migrationBuilder.DropColumn(name: "AncientBonusLevelTwoChance", schema: "config", table: "ItemDropItemGroup");
    }
}
