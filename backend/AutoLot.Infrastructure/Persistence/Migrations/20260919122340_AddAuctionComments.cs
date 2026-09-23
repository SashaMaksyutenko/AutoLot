using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auction_comments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    listing_id = table.Column<long>(type: "bigint", nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_comments_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_auction_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auction_comments_author_id",
                table: "auction_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_comments_listing_id_author_id_created_at",
                table: "auction_comments",
                columns: new[] { "listing_id", "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_auction_comments_listing_id_created_at",
                table: "auction_comments",
                columns: new[] { "listing_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auction_comments");
        }
    }
}
