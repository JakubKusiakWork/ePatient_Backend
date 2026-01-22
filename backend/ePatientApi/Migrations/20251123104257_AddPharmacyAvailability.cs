using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ePatientApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use idempotent SQL so migration can be applied even if tables already exist
            migrationBuilder.Sql(@"BEGIN;

CREATE TABLE IF NOT EXISTS pharmacies (
  id serial PRIMARY KEY,
  external_id text NOT NULL,
  name text NOT NULL,
  base_url text
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_pharmacies_external_id ON pharmacies (external_id);

CREATE TABLE IF NOT EXISTS products (
  id serial PRIMARY KEY,
  external_code text NOT NULL,
  name text NOT NULL
);

CREATE TABLE IF NOT EXISTS availability_checks (
  id serial PRIMARY KEY,
  pharmacy_id integer NOT NULL REFERENCES pharmacies(id) ON DELETE CASCADE,
  product_id integer NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  timestamp timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
  status text NOT NULL,
  price numeric NULL,
  details jsonb NULL,
  scraper_version text NULL
);
CREATE INDEX IF NOT EXISTS ix_availability_checks_pharmacy_id ON availability_checks (pharmacy_id);
CREATE INDEX IF NOT EXISTS ix_availability_checks_product_id ON availability_checks (product_id);

COMMIT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"BEGIN;
DROP TABLE IF EXISTS availability_checks;
DROP TABLE IF EXISTS products;
DROP TABLE IF EXISTS pharmacies;
COMMIT;");
        }
    }
}
