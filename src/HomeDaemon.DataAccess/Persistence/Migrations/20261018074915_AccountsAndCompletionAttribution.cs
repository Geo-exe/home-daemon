using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeDaemon.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountsAndCompletionAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_completion_history_HouseholdId",
                table: "completion_history");

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedByUserId",
                table: "completion_history",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdMemberId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_completion_history_HouseholdId_CompletedDate_RecordedAtUtc",
                table: "completion_history",
                columns: new[] { "HouseholdId", "CompletedDate", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_HouseholdMemberId",
                table: "AspNetUsers",
                column: "HouseholdMemberId",
                unique: true,
                filter: "\"HouseholdMemberId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_completion_history_HouseholdId_CompletedDate_RecordedAtUtc",
                table: "completion_history");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_HouseholdMemberId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "completion_history");

            migrationBuilder.DropColumn(
                name: "HouseholdMemberId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_completion_history_HouseholdId",
                table: "completion_history",
                column: "HouseholdId");
        }
    }
}
