using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieManagerDesktop.Migrations
{
    /// <inheritdoc />
    public partial class AddMpvProgressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AirDay",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AirTime",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstAirDate",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAirDate",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetworkName",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextEpisodeDate",
                table: "VideoFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextEpisodeNumber",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NextEpisodeSeason",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalDurationSeconds",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "TotalEpisodesCount",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalSeasonsCount",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WatchProgressSeconds",
                table: "VideoFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "TvEpisodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbSeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Overview = table.Column<string>(type: "TEXT", nullable: true),
                    StillPath = table.Column<string>(type: "TEXT", nullable: true),
                    AirDate = table.Column<string>(type: "TEXT", nullable: true),
                    VoteAverage = table.Column<double>(type: "REAL", nullable: false),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvEpisodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvSeasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TmdbSeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Overview = table.Column<string>(type: "TEXT", nullable: true),
                    PosterPath = table.Column<string>(type: "TEXT", nullable: true),
                    AirDate = table.Column<string>(type: "TEXT", nullable: true),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsWatched = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvSeasons", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TvEpisodes");

            migrationBuilder.DropTable(
                name: "TvSeasons");

            migrationBuilder.DropColumn(
                name: "AirDay",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "AirTime",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "FirstAirDate",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "LastAirDate",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "NetworkName",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "NextEpisodeDate",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "NextEpisodeNumber",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "NextEpisodeSeason",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "TotalDurationSeconds",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "TotalEpisodesCount",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "TotalSeasonsCount",
                table: "VideoFiles");

            migrationBuilder.DropColumn(
                name: "WatchProgressSeconds",
                table: "VideoFiles");
        }
    }
}
