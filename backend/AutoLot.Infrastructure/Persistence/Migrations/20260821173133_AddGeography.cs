using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "city_district_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "city_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "districts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    region_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_districts", x => x.id);
                    table.ForeignKey(
                        name: "fk_districts_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "region_translations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    region_id = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_region_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_region_translations_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    region_id = table.Column<long>(type: "bigint", nullable: false),
                    district_id = table.Column<long>(type: "bigint", nullable: true),
                    code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_region_centre = table.Column<bool>(type: "boolean", nullable: false),
                    population = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                    table.ForeignKey(
                        name: "fk_cities_districts_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cities_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "district_translations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    district_id = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_district_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_district_translations_districts_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_districts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    city_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_districts", x => x.id);
                    table.ForeignKey(
                        name: "fk_city_districts_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_translations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    city_id = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_city_translations_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_district_translations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    city_district_id = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_district_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_city_district_translations_city_districts_city_district_id",
                        column: x => x.city_district_id,
                        principalTable: "city_districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_city_district_id",
                table: "users",
                column: "city_district_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_city_id",
                table: "users",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_cities_code",
                table: "cities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cities_district_id",
                table: "cities",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "ix_cities_region_id_population",
                table: "cities",
                columns: new[] { "region_id", "population" });

            migrationBuilder.CreateIndex(
                name: "ix_city_district_translations_city_district_id_language",
                table: "city_district_translations",
                columns: new[] { "city_district_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_city_district_translations_name",
                table: "city_district_translations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_city_districts_city_id",
                table: "city_districts",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_city_districts_code",
                table: "city_districts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_city_translations_city_id_language",
                table: "city_translations",
                columns: new[] { "city_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_city_translations_name",
                table: "city_translations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_district_translations_district_id_language",
                table: "district_translations",
                columns: new[] { "district_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_district_translations_name",
                table: "district_translations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_districts_code",
                table: "districts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_districts_region_id",
                table: "districts",
                column: "region_id");

            migrationBuilder.CreateIndex(
                name: "ix_region_translations_name",
                table: "region_translations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_region_translations_region_id_language",
                table: "region_translations",
                columns: new[] { "region_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regions_code",
                table: "regions",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_cities_city_id",
                table: "users",
                column: "city_id",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_city_districts_city_district_id",
                table: "users",
                column: "city_district_id",
                principalTable: "city_districts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_cities_city_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_users_city_districts_city_district_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "city_district_translations");

            migrationBuilder.DropTable(
                name: "city_translations");

            migrationBuilder.DropTable(
                name: "district_translations");

            migrationBuilder.DropTable(
                name: "region_translations");

            migrationBuilder.DropTable(
                name: "city_districts");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "districts");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropIndex(
                name: "ix_users_city_district_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_city_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "city_district_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "city_id",
                table: "users");
        }
    }
}
