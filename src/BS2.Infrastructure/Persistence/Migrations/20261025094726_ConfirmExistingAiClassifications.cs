using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BS2.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Source "Ai" now means "AI classified, not yet confirmed". Everything AI classified before the confirm
    /// feature counts as already reviewed, so it starts out confirmed.
    /// </summary>
    public partial class ConfirmExistingAiClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE banksync.transactions
                SET classification_source = 'Manual', updated_at = now()
                WHERE classification_source = 'Ai';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which rows were AI classified is not kept here (the classification runs still show it); nothing to restore.
        }
    }
}
