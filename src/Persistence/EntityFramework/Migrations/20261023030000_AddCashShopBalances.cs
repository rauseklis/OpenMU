// <copyright file="20261023030000_AddCashShopBalances.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>SQL-managed wallets and retry-safe administrative credit records, outside the EF model.</summary>
[DbContext(typeof(EntityDataContext))]
[Migration("20261023030000_AddCashShopBalances")]
public sealed class AddCashShopBalances : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE data."CashShopBalance" (
                "AccountId" uuid NOT NULL REFERENCES data."Account" ("Id") ON DELETE CASCADE,
                "GameServerId" smallint NOT NULL CHECK ("GameServerId" BETWEEN 0 AND 255),
                "WCoinC" bigint NOT NULL CHECK ("WCoinC" BETWEEN 0 AND 2000000000),
                "WCoinP" bigint NOT NULL CHECK ("WCoinP" BETWEEN 0 AND 2000000000),
                "GoblinPoints" bigint NOT NULL CHECK ("GoblinPoints" BETWEEN 0 AND 2000000000),
                CONSTRAINT "PK_CashShopBalance" PRIMARY KEY ("AccountId", "GameServerId")
            );
            CREATE TABLE data."CashShopCredit" (
                "AccountId" uuid NOT NULL REFERENCES data."Account" ("Id") ON DELETE CASCADE,
                "GameServerId" smallint NOT NULL CHECK ("GameServerId" BETWEEN 0 AND 255),
                "OperationId" uuid NOT NULL,
                "WCoinC" bigint NOT NULL CHECK ("WCoinC" BETWEEN 0 AND 2000000000),
                "WCoinP" bigint NOT NULL CHECK ("WCoinP" BETWEEN 0 AND 2000000000),
                "GoblinPoints" bigint NOT NULL CHECK ("GoblinPoints" BETWEEN 0 AND 2000000000),
                "ResultWCoinC" bigint NOT NULL,
                "ResultWCoinP" bigint NOT NULL,
                "ResultGoblinPoints" bigint NOT NULL,
                "Reason" varchar(256) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_CashShopCredit" PRIMARY KEY ("AccountId", "GameServerId", "OperationId")
            );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE data."CashShopCredit";
            DROP TABLE data."CashShopBalance";
            """);
    }
}
