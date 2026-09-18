using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadosProveedorCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_proveedores_estado",
                table: "proveedores");

            migrationBuilder.CreateTable(
                name: "estados_proveedor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estados_proveedor", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_estados_proveedor_nombre",
                table: "estados_proveedor",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "estados_proveedor");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proveedores_estado",
                table: "proveedores",
                sql: "estado IN ('Activo', 'En evaluación', 'Pausado', 'Bloqueado')");
        }
    }
}
