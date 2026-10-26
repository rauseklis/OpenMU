// <copyright file="20261026010000_AddRewardAdditionalOptionSuppression.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Allows plain reward shields without changing shared equipment ADD settings.
/// </summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261026010000_AddRewardAdditionalOptionSuppression")]
public partial class AddRewardAdditionalOptionSuppression : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>(
        name: "SuppressAdditionalOption", schema: "config", table: "ItemDropItemGroup", type: "boolean", nullable: false, defaultValue: false);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "SuppressAdditionalOption", schema: "config", table: "ItemDropItemGroup");
}
