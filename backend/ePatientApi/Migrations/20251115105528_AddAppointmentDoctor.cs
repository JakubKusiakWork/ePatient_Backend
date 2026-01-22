using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentDoctor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "doctor_id",
                table: "appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doctor_name",
                table: "appointments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "doctor_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "doctor_name",
                table: "appointments");
        }
    }
}
