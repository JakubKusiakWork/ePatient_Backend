using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpAndPriorityToMedicalReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "follow_up_required",
                table: "medicalreports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "priority",
                table: "medicalreports",
                type: "text",
                nullable: false,
                defaultValue: "Routine");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "follow_up_required",
                table: "medicalreports");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "medicalreports");
        }
    }
}
