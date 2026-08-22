using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AutoLot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "enum_translations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    enum_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enum_translations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "makes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_popular = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_makes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "models",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    make_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    slug = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_models", x => x.id);
                    table.ForeignKey(
                        name: "fk_models_makes_make_id",
                        column: x => x.make_id,
                        principalTable: "makes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    model_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    year_from = table.Column<int>(type: "integer", nullable: false),
                    year_to = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generations", x => x.id);
                    table.ForeignKey(
                        name: "fk_generations_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_enum_translations_enum_name_value_language",
                table: "enum_translations",
                columns: new[] { "enum_name", "value", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enum_translations_name",
                table: "enum_translations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_generations_model_id_year_from",
                table: "generations",
                columns: new[] { "model_id", "year_from" });

            migrationBuilder.CreateIndex(
                name: "ix_generations_slug",
                table: "generations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_makes_is_popular_name",
                table: "makes",
                columns: new[] { "is_popular", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_makes_slug",
                table: "makes",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_models_make_id_name",
                table: "models",
                columns: new[] { "make_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_models_slug",
                table: "models",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "enum_translations");

            migrationBuilder.DropTable(
                name: "generations");

            migrationBuilder.DropTable(
                name: "models");

            migrationBuilder.DropTable(
                name: "makes");
        }
    }
}
