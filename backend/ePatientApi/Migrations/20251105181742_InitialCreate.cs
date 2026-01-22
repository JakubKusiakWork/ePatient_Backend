using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointmentsdetails",
                columns: table => new
                {
                    snapshot_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    appointment_id = table.Column<int>(type: "integer", nullable: false),
                    patient_id = table.Column<int>(type: "integer", nullable: false),
                    birth_number = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    insurance = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointmentsdetails", x => x.snapshot_id);
                });

            migrationBuilder.CreateTable(
                name: "doctoremail",
                columns: table => new
                {
                    doctor_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    doctor_email = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<bool>(type: "boolean", nullable: false),
                    registration_code = table.Column<string>(type: "text", nullable: true),
                    hashedpassword = table.Column<string>(type: "text", nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctoremail", x => x.doctor_id);
                });

            migrationBuilder.CreateTable(
                name: "healthcard_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    healthcard_id = table.Column<int>(type: "integer", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    data_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    modified_by = table.Column<string>(type: "text", nullable: true),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    change_summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_healthcard_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loggeddoctor",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    doctorfirstname = table.Column<string>(type: "text", nullable: false),
                    doctorlastname = table.Column<string>(type: "text", nullable: false),
                    doctorcode = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    hashedpassword = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loggeddoctor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loggeduser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loggeduser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "registereddoctor",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    firstname = table.Column<string>(type: "text", nullable: false),
                    lastname = table.Column<string>(type: "text", nullable: false),
                    doctorcode = table.Column<string>(type: "text", nullable: false),
                    doctor_email = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: false),
                    doctor_password = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verified_firstname = table.Column<string>(type: "text", nullable: true),
                    verified_lastname = table.Column<string>(type: "text", nullable: true),
                    verified_fullname = table.Column<string>(type: "text", nullable: true),
                    verified_specialization = table.Column<string>(type: "text", nullable: true),
                    verified_source_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registereddoctor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registeredpatient",
                columns: table => new
                {
                    birthnumber = table.Column<string>(type: "text", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    firstname = table.Column<string>(type: "text", nullable: false),
                    lastname = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    phonenumber = table.Column<string>(type: "text", nullable: false),
                    hashedpassword = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    insurance = table.Column<string>(type: "text", nullable: false),
                    resettoken = table.Column<string>(type: "text", nullable: true),
                    resettokenexpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    google_accesstoken = table.Column<string>(type: "text", nullable: true),
                    google_refreshtoken = table.Column<string>(type: "text", nullable: true),
                    google_tokenexpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registeredpatient", x => x.birthnumber);
                    table.UniqueConstraint("ux_registeredpatient_id", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webauthncredentials",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    user_type = table.Column<string>(type: "text", nullable: false),
                    credential_id = table.Column<string>(type: "text", nullable: false),
                    public_key = table.Column<string>(type: "text", nullable: false),
                    user_handle = table.Column<string>(type: "text", nullable: false),
                    signature_counter = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    authenticator_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webauthncredentials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    appointment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_time = table.Column<string>(type: "text", nullable: false),
                    reservation_day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    patient_birth_number = table.Column<string>(type: "text", nullable: false),
                    google_event_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.appointment_id);
                    table.ForeignKey(
                        name: "FK_appointments_registeredpatient_patient_birth_number",
                        column: x => x.patient_birth_number,
                        principalTable: "registeredpatient",
                        principalColumn: "birthnumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "healthcards",
                columns: table => new
                {
                    healthcard_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    patient_birth_number = table.Column<string>(type: "text", nullable: false),
                    patient_id = table.Column<int>(type: "integer", nullable: true),
                    blood_type = table.Column<string>(type: "text", nullable: true),
                    labs = table.Column<string>(type: "text", nullable: true),
                    advance_directives = table.Column<string>(type: "text", nullable: true),
                    consent_preferences = table.Column<string>(type: "text", nullable: true),
                    identity_date_of_birth = table.Column<string>(type: "text", nullable: true),
                    identity_city = table.Column<string>(type: "text", nullable: true),
                    identity_country = table.Column<string>(type: "text", nullable: true),
                    contact_address = table.Column<string>(type: "text", nullable: true),
                    emergency_name = table.Column<string>(type: "text", nullable: true),
                    emergency_phone = table.Column<string>(type: "text", nullable: true),
                    identity_first_name = table.Column<string>(type: "text", nullable: true),
                    identity_last_name = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    surgeries = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_healthcards", x => x.healthcard_id);
                    table.ForeignKey(
                        name: "FK_healthcards_registeredpatient_patient_id",
                        column: x => x.patient_id,
                        principalTable: "registeredpatient",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "medicalreports",
                columns: table => new
                {
                    report_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    patient_birth_number = table.Column<string>(type: "text", nullable: false),
                    appointment_id = table.Column<int>(type: "integer", nullable: false),
                    patient_id = table.Column<int>(type: "integer", nullable: true),
                    medication = table.Column<string>(type: "text", nullable: false),
                    externalexaminations = table.Column<string>(type: "text", nullable: false),
                    condition = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    attendingdoctor = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicalreports", x => x.report_id);
                    table.ForeignKey(
                        name: "FK_medicalreports_registeredpatient_patient_birth_number",
                        column: x => x.patient_birth_number,
                        principalTable: "registeredpatient",
                        principalColumn: "birthnumber",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patientexamination",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    patient_id = table.Column<int>(type: "integer", nullable: false),
                    examination = table.Column<string>(type: "text", nullable: true),
                    attendingdoctor = table.Column<string>(type: "text", nullable: true),
                    room = table.Column<string>(type: "text", nullable: true),
                    RegisteredPatientBirthNumber = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patientexamination", x => x.id);
                    table.ForeignKey(
                        name: "FK_patientexamination_registeredpatient_RegisteredPatientBirth~",
                        column: x => x.RegisteredPatientBirthNumber,
                        principalTable: "registeredpatient",
                        principalColumn: "birthnumber");
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_patient_birth_number",
                table: "appointments",
                column: "patient_birth_number");

            migrationBuilder.CreateIndex(
                name: "ux_appointments_reservation_day_time",
                table: "appointments",
                columns: new[] { "reservation_day", "reservation_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_healthcards_patient_id",
                table: "healthcards",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_medicalreports_patient_birth_number",
                table: "medicalreports",
                column: "patient_birth_number");

            migrationBuilder.CreateIndex(
                name: "IX_patientexamination_RegisteredPatientBirthNumber",
                table: "patientexamination",
                column: "RegisteredPatientBirthNumber");

            migrationBuilder.CreateIndex(
                name: "IX_registereddoctor_doctor_email",
                table: "registereddoctor",
                column: "doctor_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registereddoctor_doctorcode",
                table: "registereddoctor",
                column: "doctorcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webauthncredentials_user_id_user_type",
                table: "webauthncredentials",
                columns: new[] { "user_id", "user_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "appointmentsdetails");

            migrationBuilder.DropTable(
                name: "doctoremail");

            migrationBuilder.DropTable(
                name: "healthcard_versions");

            migrationBuilder.DropTable(
                name: "healthcards");

            migrationBuilder.DropTable(
                name: "loggeddoctor");

            migrationBuilder.DropTable(
                name: "loggeduser");

            migrationBuilder.DropTable(
                name: "medicalreports");

            migrationBuilder.DropTable(
                name: "patientexamination");

            migrationBuilder.DropTable(
                name: "registereddoctor");

            migrationBuilder.DropTable(
                name: "webauthncredentials");

            migrationBuilder.DropTable(
                name: "registeredpatient");
        }
    }
}
