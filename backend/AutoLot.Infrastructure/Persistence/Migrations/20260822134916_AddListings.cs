using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "features",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_features", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "listings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    seller_id = table.Column<long>(type: "bigint", nullable: false),
                    city_id = table.Column<long>(type: "bigint", nullable: false),
                    city_district_id = table.Column<long>(type: "bigint", nullable: true),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price_uah = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_negotiable = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_trade = table.Column<bool>(type: "boolean", nullable: false),
                    is_urgent = table.Column<bool>(type: "boolean", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listings", x => x.id);
                    table.ForeignKey(
                        name: "fk_listings_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_listings_city_districts_city_district_id",
                        column: x => x.city_district_id,
                        principalTable: "city_districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_listings_users_seller_id",
                        column: x => x.seller_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "country_translations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    country_id = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_country_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_country_translations_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_translations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    feature_id = table.Column<long>(type: "bigint", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_feature_translations_features_feature_id",
                        column: x => x.feature_id,
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cars",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    listing_id = table.Column<long>(type: "bigint", nullable: false),
                    vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: false),
                    condition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    make_id = table.Column<long>(type: "bigint", nullable: false),
                    model_id = table.Column<long>(type: "bigint", nullable: false),
                    generation_id = table.Column<long>(type: "bigint", nullable: true),
                    mileage = table.Column<int>(type: "integer", nullable: true),
                    owner_count = table.Column<int>(type: "integer", nullable: true),
                    fuel_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    engine_volume = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    engine_power = table.Column<int>(type: "integer", nullable: true),
                    fuel_consumption_city = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    fuel_consumption_highway = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    fuel_consumption_combined = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    battery_capacity = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    electric_range = table.Column<int>(type: "integer", nullable: true),
                    charging_port = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    transmission = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    drivetrain = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    body_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_metallic = table.Column<bool>(type: "boolean", nullable: false),
                    seat_count = table.Column<int>(type: "integer", nullable: true),
                    door_count = table.Column<int>(type: "integer", nullable: true),
                    ecology_standard = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    manufacturer_country_id = table.Column<long>(type: "bigint", nullable: true),
                    imported_from_country_id = table.Column<long>(type: "bigint", nullable: true),
                    is_customs_cleared = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_located_in_ukraine = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    was_in_accident = table.Column<bool>(type: "boolean", nullable: false),
                    damage_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    paint_condition = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    has_service_book = table.Column<bool>(type: "boolean", nullable: false),
                    is_garage_kept = table.Column<bool>(type: "boolean", nullable: false),
                    is_on_credit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cars", x => x.id);
                    table.ForeignKey(
                        name: "fk_cars_countries_imported_from_country_id",
                        column: x => x.imported_from_country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cars_countries_manufacturer_country_id",
                        column: x => x.manufacturer_country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cars_generations_generation_id",
                        column: x => x.generation_id,
                        principalTable: "generations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cars_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cars_makes_make_id",
                        column: x => x.make_id,
                        principalTable: "makes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cars_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "car_features",
                columns: table => new
                {
                    car_id = table.Column<long>(type: "bigint", nullable: false),
                    feature_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_car_features", x => new { x.car_id, x.feature_id });
                    table.ForeignKey(
                        name: "fk_car_features_cars_car_id",
                        column: x => x.car_id,
                        principalTable: "cars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_car_features_features_feature_id",
                        column: x => x.feature_id,
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "car_photos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    car_id = table.Column<long>(type: "bigint", nullable: false),
                    path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_car_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_car_photos_cars_car_id",
                        column: x => x.car_id,
                        principalTable: "cars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_car_features_feature_id",
                table: "car_features",
                column: "feature_id");

            migrationBuilder.CreateIndex(
                name: "ix_car_photos_car_id_sort_order",
                table: "car_photos",
                columns: new[] { "car_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_cars_body_type",
                table: "cars",
                column: "body_type");

            migrationBuilder.CreateIndex(
                name: "ix_cars_fuel_type",
                table: "cars",
                column: "fuel_type");

            migrationBuilder.CreateIndex(
                name: "ix_cars_generation_id",
                table: "cars",
                column: "generation_id");

            migrationBuilder.CreateIndex(
                name: "ix_cars_imported_from_country_id",
                table: "cars",
                column: "imported_from_country_id");

            migrationBuilder.CreateIndex(
                name: "ix_cars_listing_id",
                table: "cars",
                column: "listing_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cars_make_id_model_id",
                table: "cars",
                columns: new[] { "make_id", "model_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cars_manufacturer_country_id",
                table: "cars",
                column: "manufacturer_country_id");

            migrationBuilder.CreateIndex(
                name: "ix_cars_mileage",
                table: "cars",
                column: "mileage");

            migrationBuilder.CreateIndex(
                name: "ix_cars_model_id",
                table: "cars",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_cars_year",
                table: "cars",
                column: "year");

            migrationBuilder.CreateIndex(
                name: "ix_countries_code",
                table: "countries",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_country_translations_country_id_language",
                table: "country_translations",
                columns: new[] { "country_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_country_translations_name",
                table: "country_translations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_feature_translations_feature_id_language",
                table: "feature_translations",
                columns: new[] { "feature_id", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_feature_translations_name",
                table: "feature_translations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_features_category_sort_order",
                table: "features",
                columns: new[] { "category", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_features_code",
                table: "features",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listings_city_district_id",
                table: "listings",
                column: "city_district_id");

            migrationBuilder.CreateIndex(
                name: "ix_listings_city_id",
                table: "listings",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_listings_seller_id_status",
                table: "listings",
                columns: new[] { "seller_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_price_uah",
                table: "listings",
                columns: new[] { "status", "price_uah" });

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_published_at",
                table: "listings",
                columns: new[] { "status", "published_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "car_features");

            migrationBuilder.DropTable(
                name: "car_photos");

            migrationBuilder.DropTable(
                name: "country_translations");

            migrationBuilder.DropTable(
                name: "feature_translations");

            migrationBuilder.DropTable(
                name: "cars");

            migrationBuilder.DropTable(
                name: "features");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "listings");
        }
    }
}
