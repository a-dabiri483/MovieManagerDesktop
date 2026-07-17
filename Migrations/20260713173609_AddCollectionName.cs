using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieManagerDesktop.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionName",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectionName",
                table: "VideoFiles");
        }
    }
}
