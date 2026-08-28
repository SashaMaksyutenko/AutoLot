using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "dealership_id",
                table: "listings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dealerships",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    logo_path = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    city_id = table.Column<long>(type: "bigint", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_by_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dealerships", x => x.id);
                    table.ForeignKey(
                        name: "fk_dealerships_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dealerships_users_verified_by_id",
                        column: x => x.verified_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dealership_members",
                columns: table => new
                {
                    dealership_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dealership_members", x => new { x.dealership_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_dealership_members_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dealership_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listings_dealership_id_status",
                table: "listings",
                columns: new[] { "dealership_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_dealership_members_user_id",
                table: "dealership_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dealerships_city_id_is_verified",
                table: "dealerships",
                columns: new[] { "city_id", "is_verified" });

            migrationBuilder.CreateIndex(
                name: "ix_dealerships_slug",
                table: "dealerships",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dealerships_verified_by_id",
                table: "dealerships",
                column: "verified_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_listings_dealerships_dealership_id",
                table: "listings",
                column: "dealership_id",
                principalTable: "dealerships",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_listings_dealerships_dealership_id",
                table: "listings");

            migrationBuilder.DropTable(
                name: "dealership_members");

            migrationBuilder.DropTable(
                name: "dealerships");

            migrationBuilder.DropIndex(
                name: "ix_listings_dealership_id_status",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "dealership_id",
                table: "listings");
        }
    }
}
