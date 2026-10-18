using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeDaemon.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BudgetModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "households",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsBudgetParticipant",
                table: "household_members",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "budget_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_categories_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "budget_incomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_incomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_incomes_household_members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "household_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_incomes_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "budget_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SettlementYear = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    FromMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_transfers_household_members_FromMemberId",
                        column: x => x.FromMemberId,
                        principalTable: "household_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_transfers_household_members_ToMemberId",
                        column: x => x.ToMemberId,
                        principalTable: "household_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_transfers_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "budget_subcategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_subcategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_subcategories_budget_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "budget_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_subcategories_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "budget_expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubcategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PaidByMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_budget_expenses_budget_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "budget_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_expenses_budget_subcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "budget_subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_expenses_household_members_PaidByMemberId",
                        column: x => x.PaidByMemberId,
                        principalTable: "household_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_budget_expenses_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budget_categories_HouseholdId_SortOrder",
                table: "budget_categories",
                columns: new[] { "HouseholdId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_expenses_CategoryId",
                table: "budget_expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_expenses_HouseholdId_CategoryId",
                table: "budget_expenses",
                columns: new[] { "HouseholdId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_expenses_HouseholdId_Date",
                table: "budget_expenses",
                columns: new[] { "HouseholdId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_expenses_HouseholdId_PaidByMemberId",
                table: "budget_expenses",
                columns: new[] { "HouseholdId", "PaidByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_expenses_PaidByMemberId",
                table: "budget_expenses",
                column: "PaidByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_expenses_SubcategoryId",
                table: "budget_expenses",
                column: "SubcategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_incomes_HouseholdId_MemberId_Year_Month",
                table: "budget_incomes",
                columns: new[] { "HouseholdId", "MemberId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_incomes_MemberId",
                table: "budget_incomes",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_subcategories_CategoryId",
                table: "budget_subcategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_subcategories_HouseholdId_CategoryId_SortOrder",
                table: "budget_subcategories",
                columns: new[] { "HouseholdId", "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_transfers_FromMemberId",
                table: "budget_transfers",
                column: "FromMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_budget_transfers_HouseholdId_SettlementYear_Date",
                table: "budget_transfers",
                columns: new[] { "HouseholdId", "SettlementYear", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_transfers_ToMemberId",
                table: "budget_transfers",
                column: "ToMemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_expenses");

            migrationBuilder.DropTable(
                name: "budget_incomes");

            migrationBuilder.DropTable(
                name: "budget_transfers");

            migrationBuilder.DropTable(
                name: "budget_subcategories");

            migrationBuilder.DropTable(
                name: "budget_categories");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "households");

            migrationBuilder.DropColumn(
                name: "IsBudgetParticipant",
                table: "household_members");
        }
    }
}
