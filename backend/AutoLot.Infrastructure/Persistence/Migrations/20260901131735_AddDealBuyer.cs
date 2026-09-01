using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDealBuyer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "buyer_id",
                table: "listings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sold_at",
                table: "listings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_listings_buyer_id",
                table: "listings",
                column: "buyer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_listings_users_buyer_id",
                table: "listings",
                column: "buyer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_listings_users_buyer_id",
                table: "listings");

            migrationBuilder.DropIndex(
                name: "ix_listings_buyer_id",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "buyer_id",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "sold_at",
                table: "listings");
        }
    }
}
