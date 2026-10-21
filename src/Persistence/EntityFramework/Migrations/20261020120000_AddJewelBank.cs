using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations
{
    /// <summary>
    /// Creates the tables of the Jewel Bank. The bank reads and writes them with plain SQL, so they are
    /// deliberately not part of the EF model (and the model snapshot).
    /// </summary>
    [DbContext(typeof(EntityDataContext))]
    [Migration("20261020120000_AddJewelBank")]
    public partial class AddJewelBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE data."JewelBankBalance" (
                    "AccountId" uuid NOT NULL,
                    "Kind" smallint NOT NULL,
                    "Count" bigint NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_JewelBankBalance" PRIMARY KEY ("AccountId", "Kind"),
                    CONSTRAINT "CK_JewelBankBalance_Count" CHECK ("Count" >= 0)
                );
                CREATE TABLE data."JewelBankSettings" (
                    "AccountId" uuid NOT NULL,
                    "AutoDeposit" boolean NOT NULL,
                    CONSTRAINT "PK_JewelBankSettings" PRIMARY KEY ("AccountId")
                );
                CREATE TABLE data."JewelBankMovement" (
                    "Id" uuid NOT NULL,
                    "AccountId" uuid NOT NULL,
                    "CharacterId" uuid NOT NULL,
                    "OperationId" uuid NOT NULL,
                    "Kind" smallint NOT NULL,
                    "Delta" bigint NOT NULL,
                    "Balance" bigint NOT NULL,
                    "Reason" smallint NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_JewelBankMovement" PRIMARY KEY ("Id")
                );
                CREATE INDEX "IX_JewelBankMovement_AccountId_Kind" ON data."JewelBankMovement" ("AccountId", "Kind");
                CREATE TABLE data."JewelBankOperation" (
                    "Id" uuid NOT NULL,
                    "AccountId" uuid NOT NULL,
                    "OperationId" uuid NOT NULL,
                    "Type" smallint NOT NULL,
                    "Outcome" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_JewelBankOperation" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX "IX_JewelBankOperation_AccountId_OperationId" ON data."JewelBankOperation" ("AccountId", "OperationId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE data."JewelBankOperation";
                DROP TABLE data."JewelBankMovement";
                DROP TABLE data."JewelBankSettings";
                DROP TABLE data."JewelBankBalance";
                """);
        }
    }
}
