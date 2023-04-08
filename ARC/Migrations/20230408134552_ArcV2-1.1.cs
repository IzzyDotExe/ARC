using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARC.Migrations
{
    /// <inheritdoc />
    public partial class ArcV211 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GuildSnowflake",
                table: "UserNotes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildSnowflake",
                table: "UserNotes");
        }
    }
}
