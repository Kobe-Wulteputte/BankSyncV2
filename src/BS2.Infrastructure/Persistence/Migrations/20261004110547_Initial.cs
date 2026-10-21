using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BS2.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "banksync");

            migrationBuilder.CreateTable(
                name: "bank_connections",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    psu_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    select_accounts_at_bank = table.Column<bool>(type: "boolean", nullable: false),
                    consent_validity_days = table.Column<int>(type: "integer", nullable: true),
                    configured_ibans_raw = table.Column<string>(type: "text", nullable: false),
                    external_session_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_authorized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sync_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_runs",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    connections_synced = table.Column<int>(type: "integer", nullable: false),
                    connections_skipped = table.Column<int>(type: "integer", nullable: false),
                    transactions_fetched = table.Column<int>(type: "integer", nullable: false),
                    transactions_new = table.Column<int>(type: "integer", nullable: false),
                    transactions_classified = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    details_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auth0subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_account_id = table.Column<string>(type: "text", nullable: true),
                    identifier = table.Column<string>(type: "text", nullable: false),
                    identifier_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_bank_connections_bank_connection_id",
                        column: x => x.bank_connection_id,
                        principalSchema: "banksync",
                        principalTable: "bank_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pending_authorizations",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pending_authorizations", x => x.id);
                    table.ForeignKey(
                        name: "fk_pending_authorizations_bank_connections_bank_connection_id",
                        column: x => x.bank_connection_id,
                        principalSchema: "banksync",
                        principalTable: "bank_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    external_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    booking_date = table.Column<DateOnly>(type: "date", nullable: true),
                    value_date = table.Column<DateOnly>(type: "date", nullable: true),
                    counterparty_name = table.Column<string>(type: "text", nullable: false),
                    counterparty_iban = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    raw_json = table.Column<string>(type: "text", nullable: true),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    classification_source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    classified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reimbursed = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_transactions_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "banksync",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "banksync",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "classification_runs",
                schema: "banksync",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    user_prompt = table.Column<string>(type: "text", nullable: false),
                    raw_response = table.Column<string>(type: "text", nullable: true),
                    predicted_category_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    predicted_category_id = table.Column<int>(type: "integer", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    alternatives_json = table.Column<string>(type: "jsonb", nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: true),
                    completion_tokens = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_classification_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_classification_runs_categories_predicted_category_id",
                        column: x => x.predicted_category_id,
                        principalSchema: "banksync",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_classification_runs_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalSchema: "banksync",
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_groups",
                schema: "banksync",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_groups", x => new { x.transaction_id, x.group_id });
                    table.ForeignKey(
                        name: "fk_transaction_groups_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "banksync",
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transaction_groups_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalSchema: "banksync",
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "banksync",
                table: "categories",
                columns: new[] { "id", "code", "color", "is_active", "kind", "name", "sort_order" },
                values: new object[,]
                {
                    { 1, "Clothes", null, true, "Expense", "Clothes", 0 },
                    { 2, "Communication", null, true, "Expense", "Communication", 1 },
                    { 3, "Education", null, true, "Expense", "Education", 2 },
                    { 4, "FoodAndDrink", null, true, "Expense", "Food and drink (other)", 3 },
                    { 5, "Groceries", null, true, "Expense", "Groceries", 4 },
                    { 6, "Health", null, true, "Expense", "Health", 5 },
                    { 7, "Home", null, true, "Expense", "Home", 6 },
                    { 8, "Activities", null, true, "Expense", "Activities", 7 },
                    { 9, "Chiro", null, true, "Expense", "Chiro", 8 },
                    { 10, "Cycling", null, true, "Expense", "Cycling", 9 },
                    { 11, "Sports", null, true, "Expense", "Sports", 10 },
                    { 12, "Subscriptions", null, true, "Expense", "Subscriptions", 11 },
                    { 13, "Takeaway", null, true, "Expense", "Takeaway", 12 },
                    { 14, "Transport", null, true, "Expense", "Transport", 13 },
                    { 15, "FastFood", null, true, "Expense", "Fast food", 14 },
                    { 16, "Gifts", null, true, "Expense", "Gifts", 15 },
                    { 17, "Donations", null, true, "Expense", "Donations", 16 },
                    { 18, "Drinks", null, true, "Expense", "Drinks", 17 },
                    { 19, "Gadgets", null, true, "Expense", "Gadgets", 18 },
                    { 20, "Games", null, true, "Expense", "Games", 19 },
                    { 21, "Restaurants", null, true, "Expense", "Restaurants", 20 },
                    { 22, "Shows", null, true, "Expense", "Shows", 21 },
                    { 23, "Travel", null, true, "Expense", "Travel", 22 },
                    { 24, "BankServices", null, true, "Expense", "Bank services", 23 },
                    { 25, "Fines", null, true, "Expense", "Fines", 24 },
                    { 26, "Cash", null, true, "Expense", "Cash", 25 },
                    { 27, "Investments", null, true, "Transfer", "Investments", 26 },
                    { 28, "Transfer", null, true, "Transfer", "Transfer", 27 },
                    { 29, "Vouchers", null, true, "Income", "Vouchers", 28 },
                    { 30, "Bonus", null, true, "Income", "Bonus", 29 },
                    { 31, "Wage", null, true, "Income", "Wage", 30 },
                    { 32, "Taxes", null, true, "Expense", "Taxes", 31 },
                    { 33, "Reimbursement", null, true, "Income", "Reimbursement", 32 },
                    { 34, "Utilities", null, true, "Expense", "Utilities", 33 },
                    { 35, "Mortgage", null, true, "Expense", "Mortgage", 34 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_bank_connection_id",
                schema: "banksync",
                table: "accounts",
                column: "bank_connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_user_id_identifier_hash",
                schema: "banksync",
                table: "accounts",
                columns: new[] { "user_id", "identifier_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_connections_user_id",
                schema: "banksync",
                table: "bank_connections",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_connections_user_id_provider_bank_name",
                schema: "banksync",
                table: "bank_connections",
                columns: new[] { "user_id", "provider", "bank_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_code",
                schema: "banksync",
                table: "categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_classification_runs_predicted_category_id",
                schema: "banksync",
                table: "classification_runs",
                column: "predicted_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_classification_runs_transaction_id_created_at",
                schema: "banksync",
                table: "classification_runs",
                columns: new[] { "transaction_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_groups_user_id_name",
                schema: "banksync",
                table: "groups",
                columns: new[] { "user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pending_authorizations_bank_connection_id",
                schema: "banksync",
                table: "pending_authorizations",
                column: "bank_connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_pending_authorizations_state",
                schema: "banksync",
                table: "pending_authorizations",
                column: "state",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sync_runs_user_id_started_at",
                schema: "banksync",
                table: "sync_runs",
                columns: new[] { "user_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_groups_group_id",
                schema: "banksync",
                table: "transaction_groups",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_id_external_id",
                schema: "banksync",
                table: "transactions",
                columns: new[] { "account_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id",
                schema: "banksync",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id_category_id",
                schema: "banksync",
                table: "transactions",
                columns: new[] { "user_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_id_date",
                schema: "banksync",
                table: "transactions",
                columns: new[] { "user_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_users_auth0subject",
                schema: "banksync",
                table: "users",
                column: "auth0subject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "classification_runs",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "pending_authorizations",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "sync_runs",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "transaction_groups",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "users",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "groups",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "banksync");

            migrationBuilder.DropTable(
                name: "bank_connections",
                schema: "banksync");
        }
    }
}
