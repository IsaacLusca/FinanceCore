using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceCore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationInitiator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_operations_idempotency_key",
                table: "operations");

            migrationBuilder.AddColumn<Guid>(
                name: "initiated_by_user_id",
                table: "operations",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "ux_operations_user_idempotency_key",
                table: "operations",
                columns: new[] { "initiated_by_user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_operations_users",
                table: "operations",
                column: "initiated_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_operations_users",
                table: "operations");

            migrationBuilder.DropIndex(
                name: "ux_operations_user_idempotency_key",
                table: "operations");

            migrationBuilder.DropColumn(
                name: "initiated_by_user_id",
                table: "operations");

            migrationBuilder.CreateIndex(
                name: "ux_operations_idempotency_key",
                table: "operations",
                column: "idempotency_key",
                unique: true);
        }
    }
}
