using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BS2.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoryIdentityAndSyncLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "banksync",
                table: "categories",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // START WITH only applies to a future RESTART; move the live sequence past the seed rows now.
            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('banksync.categories', 'id'), " +
                "GREATEST(999, (SELECT COALESCE(MAX(id), 0) FROM banksync.categories)), true);");

            migrationBuilder.CreateIndex(
                name: "ux_sync_runs_user_running",
                schema: "banksync",
                table: "sync_runs",
                column: "user_id",
                unique: true,
                filter: "status = 'Running'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_sync_runs_user_running",
                schema: "banksync",
                table: "sync_runs");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "banksync",
                table: "categories",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:IdentitySequenceOptions", "'1000', '1', '', '', 'False', '1'")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
