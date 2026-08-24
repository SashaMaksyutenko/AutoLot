using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "thumbnail_path",
                table: "car_photos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "thumbnail_path",
                table: "car_photos");
        }
    }
}
