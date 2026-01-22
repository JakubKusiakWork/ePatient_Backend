using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAppointmentUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_appointments_doctor_day_time",
                table: "appointments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_appointments_doctor_day_time",
                table: "appointments",
                columns: new[] { "doctor_id", "reservation_day", "reservation_time" },
                unique: true);
        }
    }
}
