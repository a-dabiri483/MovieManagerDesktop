using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieManagerDesktop.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasNewEpisode",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTracked",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LastAiredSeason",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesStatus",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasNewEpisode",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "IsTracked",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "LastAiredSeason",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "SeriesStatus",
                table: "VideoFiles");
        }
    }
}
