using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionSearchSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClassMask",
                schema: "data",
                table: "AuctionListing",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemDefinitionId",
                schema: "data",
                table: "AuctionListing",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OptionFlags",
                schema: "data",
                table: "AuctionListing",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OptionLevel",
                schema: "data",
                table: "AuctionListing",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE data."AuctionListing" AS l
                SET "ItemDefinitionId" = i."DefinitionId",
                    "ItemGroup" = d."Group",
                    "ItemNumber" = d."Number",
                    "ItemLevel" = i."Level",
                    "NormalizedName" = upper(split_part(d."Name", '||', 1)),
                    "ClassMask" = COALESCE((
                        SELECT bit_or(1::bigint << (cc."Number" / 4))
                        FROM config."ItemDefinitionCharacterClass" AS idcc
                        JOIN config."CharacterClass" AS cc ON cc."Id" = idcc."CharacterClassId"
                        WHERE idcc."ItemDefinitionId" = i."DefinitionId"), 0),
                    "OptionFlags" =
                        CASE WHEN i."HasSkill" THEN 4 ELSE 0 END
                        + CASE WHEN i."SocketCount" > 0 THEN 128 ELSE 0 END
                        + CASE WHEN EXISTS (
                            SELECT 1 FROM data."ItemOptionLink" AS link
                            JOIN config."IncreasableItemOption" AS option ON option."Id" = link."ItemOptionId"
                            WHERE link."ItemId" = i."Id" AND option."OptionTypeId" = 'f193f91e-86d7-4456-add8-a3667e731303') THEN 1 ELSE 0 END
                        + CASE WHEN EXISTS (
                            SELECT 1 FROM data."ItemOptionLink" AS link
                            JOIN config."IncreasableItemOption" AS option ON option."Id" = link."ItemOptionId"
                            WHERE link."ItemId" = i."Id" AND option."OptionTypeId" = '3e3e9be8-4e16-4f27-a7cf-986d48454d76') THEN 2 ELSE 0 END
                        + CASE WHEN EXISTS (
                            SELECT 1 FROM data."ItemOptionLink" AS link
                            JOIN config."IncreasableItemOption" AS option ON option."Id" = link."ItemOptionId"
                            WHERE link."ItemId" = i."Id" AND option."OptionTypeId" IN (
                                '6487c498-58e0-48e5-b409-35d7598313fc', '55cb57a7-4fc6-47bb-9fee-84e6c4ebce95',
                                'ed978695-bd3e-46ea-86d8-f8c30ea99b50', 'c3ed45bc-5713-494d-a8c8-dc4afae56223',
                                '78e6db0b-ac53-454c-956f-cd2b5467856e')) THEN 8 ELSE 0 END
                        + CASE WHEN EXISTS (
                            SELECT 1 FROM data."ItemItemOfItemSet" AS link
                            JOIN config."ItemOfItemSet" AS item_set ON item_set."Id" = link."ItemOfItemSetId"
                            WHERE link."ItemId" = i."Id" AND item_set."AncientSetDiscriminator" <> 0) THEN 16 ELSE 0 END
                        + CASE WHEN EXISTS (
                            SELECT 1 FROM data."ItemOptionLink" AS link
                            JOIN config."IncreasableItemOption" AS option ON option."Id" = link."ItemOptionId"
                            WHERE link."ItemId" = i."Id" AND option."OptionTypeId" = '0ca234f0-4a0f-4fa1-8e07-cfb89c1ec94f') THEN 32 ELSE 0 END
                        + CASE WHEN EXISTS (
                            SELECT 1 FROM data."ItemOptionLink" AS link
                            JOIN config."IncreasableItemOption" AS option ON option."Id" = link."ItemOptionId"
                            WHERE link."ItemId" = i."Id" AND option."OptionTypeId" = '4aa95715-1ed3-453d-8d1d-093b281416ca') THEN 64 ELSE 0 END,
                    "OptionLevel" = COALESCE((
                        SELECT max(link."Level") FROM data."ItemOptionLink" AS link
                        JOIN config."IncreasableItemOption" AS option ON option."Id" = link."ItemOptionId"
                        WHERE link."ItemId" = i."Id" AND option."OptionTypeId" = 'f193f91e-86d7-4456-add8-a3667e731303'), 0)
                FROM data."Item" AS i
                JOIN config."ItemDefinition" AS d ON d."Id" = i."DefinitionId"
                WHERE l."ItemId" = i."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassMask",
                schema: "data",
                table: "AuctionListing");

            migrationBuilder.DropColumn(
                name: "ItemDefinitionId",
                schema: "data",
                table: "AuctionListing");

            migrationBuilder.DropColumn(
                name: "OptionFlags",
                schema: "data",
                table: "AuctionListing");

            migrationBuilder.DropColumn(
                name: "OptionLevel",
                schema: "data",
                table: "AuctionListing");
        }
    }
}
