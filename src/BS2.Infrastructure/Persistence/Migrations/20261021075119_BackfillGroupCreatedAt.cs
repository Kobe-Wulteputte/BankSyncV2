using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BS2.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Groups are listed newest first, but the Excel import stamped every group with the import time.
    /// Sets each group's created_at to its newest transaction date; groups without transactions keep theirs.
    /// </summary>
    public partial class BackfillGroupCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE banksync.groups g
                SET created_at = u.last_used::timestamp AT TIME ZONE 'UTC'
                FROM (
                    SELECT tg.group_id, max(t.date) AS last_used
                    FROM banksync.transaction_groups tg
                    JOIN banksync.transactions t ON t.id = tg.transaction_id
                    GROUP BY tg.group_id
                ) u
                WHERE g.id = u.group_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original timestamps are not kept; there is nothing to restore.
        }
    }
}
