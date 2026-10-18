using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeDaemon.DataAccess.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteChores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DeletedAtUtc",
                table: "chores",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "chores");
        }
    }
}
