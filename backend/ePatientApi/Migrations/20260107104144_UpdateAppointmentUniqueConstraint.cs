using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAppointmentUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_appointments_reservation_day_time",
                table: "appointments");

            migrationBuilder.CreateIndex(
                name: "ux_appointments_doctor_day_time",
                table: "appointments",
                columns: new[] { "doctor_id", "reservation_day", "reservation_time" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_appointments_doctor_day_time",
                table: "appointments");

            migrationBuilder.CreateIndex(
                name: "ux_appointments_reservation_day_time",
                table: "appointments",
                columns: new[] { "reservation_day", "reservation_time" },
                unique: true);
        }
    }
}
