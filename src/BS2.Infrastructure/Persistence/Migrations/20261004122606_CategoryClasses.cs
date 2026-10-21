using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BS2.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoryClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "category_class_id",
                schema: "banksync",
                table: "categories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "category_classes",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category_classes", x => x.id);
                });

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 1,
                column: "category_class_id",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 2,
                column: "category_class_id",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 3,
                column: "category_class_id",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 4,
                column: "category_class_id",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 5,
                column: "category_class_id",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 6,
                column: "category_class_id",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 7,
                column: "category_class_id",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 8,
                column: "category_class_id",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 9,
                column: "category_class_id",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 10,
                column: "category_class_id",
                value: 7);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 11,
                column: "category_class_id",
                value: 7);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 12,
                column: "category_class_id",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 13,
                column: "category_class_id",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 14,
                column: "category_class_id",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 15,
                column: "category_class_id",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 16,
                column: "category_class_id",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 17,
                column: "category_class_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 18,
                column: "category_class_id",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 19,
                column: "category_class_id",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 20,
                column: "category_class_id",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 21,
                column: "category_class_id",
                value: 4);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 22,
                column: "category_class_id",
                value: 6);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 23,
                column: "category_class_id",
                value: 5);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 24,
                column: "category_class_id",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 25,
                column: "category_class_id",
                value: 3);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 26,
                column: "category_class_id",
                value: 8);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 27,
                column: "category_class_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 28,
                column: "category_class_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 29,
                column: "category_class_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 30,
                column: "category_class_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 31,
                column: "category_class_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 32,
                column: "category_class_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 33,
                column: "category_class_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 34,
                column: "category_class_id",
                value: 2);

            migrationBuilder.UpdateData(
                schema: "banksync",
                table: "categories",
                keyColumn: "id",
                keyValue: 35,
                column: "category_class_id",
                value: 2);

            migrationBuilder.InsertData(
                schema: "banksync",
                table: "category_classes",
                columns: new[] { "id", "color", "name", "sort_order" },
                values: new object[,]
                {
                    { 1, "#59a14f", "Income", 0 },
                    { 2, "#4e79a7", "Housing", 1 },
                    { 3, "#f28e2b", "Cost of living", 2 },
                    { 4, "#e15759", "Food", 3 },
                    { 5, "#b07aa1", "Fun", 4 },
                    { 6, "#edc948", "Social", 5 },
                    { 7, "#76b7b2", "Sports", 6 },
                    { 8, "#9aa0a6", "Unknown", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_category_class_id",
                schema: "banksync",
                table: "categories",
                column: "category_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_category_classes_name",
                schema: "banksync",
                table: "category_classes",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_categories_category_classes_category_class_id",
                schema: "banksync",
                table: "categories",
                column: "category_class_id",
                principalSchema: "banksync",
                principalTable: "category_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('banksync.category_classes','id'), 999, true);");
            migrationBuilder.Sql("UPDATE banksync.categories SET category_class_id = 2 WHERE code = 'Rent' AND category_class_id IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categories_category_classes_category_class_id",
                schema: "banksync",
                table: "categories");

            migrationBuilder.DropTable(
                name: "category_classes",
                schema: "banksync");

            migrationBuilder.DropIndex(
                name: "ix_categories_category_class_id",
                schema: "banksync",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "category_class_id",
                schema: "banksync",
                table: "categories");
        }
    }
}
