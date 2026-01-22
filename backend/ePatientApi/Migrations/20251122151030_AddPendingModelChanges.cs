using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gp_doctor_id",
                table: "registeredpatient",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_registeredpatient_gp_doctor_id",
                table: "registeredpatient",
                column: "gp_doctor_id");

            migrationBuilder.AddForeignKey(
                name: "FK_registeredpatient_registereddoctor_gp_doctor_id",
                table: "registeredpatient",
                column: "gp_doctor_id",
                principalTable: "registereddoctor",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_registeredpatient_registereddoctor_gp_doctor_id",
                table: "registeredpatient");

            migrationBuilder.DropIndex(
                name: "IX_registeredpatient_gp_doctor_id",
                table: "registeredpatient");

            migrationBuilder.DropColumn(
                name: "gp_doctor_id",
                table: "registeredpatient");
        }
    }
}
