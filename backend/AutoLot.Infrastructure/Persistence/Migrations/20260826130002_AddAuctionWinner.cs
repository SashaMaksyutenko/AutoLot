using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionWinner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "winner_id",
                table: "auctions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_auctions_winner_id",
                table: "auctions",
                column: "winner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_auctions_users_winner_id",
                table: "auctions",
                column: "winner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_auctions_users_winner_id",
                table: "auctions");

            migrationBuilder.DropIndex(
                name: "ix_auctions_winner_id",
                table: "auctions");

            migrationBuilder.DropColumn(
                name: "winner_id",
                table: "auctions");
        }
    }
}
