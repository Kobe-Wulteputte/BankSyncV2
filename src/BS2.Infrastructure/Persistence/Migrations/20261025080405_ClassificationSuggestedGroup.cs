using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BS2.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClassificationSuggestedGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "suggested_group_id",
                schema: "banksync",
                table: "classification_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_classification_runs_suggested_group_id",
                schema: "banksync",
                table: "classification_runs",
                column: "suggested_group_id");

            migrationBuilder.AddForeignKey(
                name: "fk_classification_runs_groups_suggested_group_id",
                schema: "banksync",
                table: "classification_runs",
                column: "suggested_group_id",
                principalSchema: "banksync",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_classification_runs_groups_suggested_group_id",
                schema: "banksync",
                table: "classification_runs");

            migrationBuilder.DropIndex(
                name: "ix_classification_runs_suggested_group_id",
                schema: "banksync",
                table: "classification_runs");

            migrationBuilder.DropColumn(
                name: "suggested_group_id",
                schema: "banksync",
                table: "classification_runs");
        }
    }
}
