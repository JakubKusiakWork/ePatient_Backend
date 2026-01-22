using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePriorityDefaultValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing records with empty priority to 'Routine'
            migrationBuilder.Sql(
                "UPDATE medicalreports SET priority = 'Routine' WHERE priority = '' OR priority IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No need to revert data changes
        }
    }
}
