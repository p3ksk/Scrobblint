using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scrobblint.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackInfoCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArtistKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TrackKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Found = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanonicalArtist = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CanonicalTrack = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CanonicalAlbum = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackInfos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackInfos_ArtistKey_TrackKey",
                table: "TrackInfos",
                columns: new[] { "ArtistKey", "TrackKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackInfos");
        }
    }
}
