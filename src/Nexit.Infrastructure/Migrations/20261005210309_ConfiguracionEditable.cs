using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracionEditable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_proyectos_prioridad",
                table: "proyectos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_proyectos_propuesta",
                table: "proyectos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_proyectos_tipo",
                table: "proyectos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_proyecto_seguimiento_area",
                table: "proyecto_seguimiento");

            migrationBuilder.CreateTable(
                name: "opciones_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    lista = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    valor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    orden = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opciones_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles_config",
                columns: table => new
                {
                    rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    etiqueta = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles_config", x => x.rol);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opciones_config_lista_valor",
                table: "opciones_config",
                columns: new[] { "lista", "valor" },
                unique: true);

            // Datos iniciales (idempotentes): los nombres de rol y las listas que estaban fijos en el código.
            migrationBuilder.Sql(@"
INSERT INTO roles_config (rol, etiqueta, descripcion) VALUES
 ('super_admin', 'Super admin', 'Manda en todo: es el único que crea, edita y elimina usuarios.'),
 ('admin', 'Admin', 'Administra el sistema y decide las solicitudes de eliminación. No toca usuarios.'),
 ('manager', 'Director', 'Director de sus proyectos: endosa la eliminación de los que tiene a cargo.'),
 ('miembro', 'Miembro', 'Trabaja en el sistema; para eliminar algo tiene que solicitarlo.')
ON CONFLICT (rol) DO NOTHING;

INSERT INTO opciones_config (lista, valor, orden) VALUES
 ('tipo-proyecto', 'Corporativo', 1), ('tipo-proyecto', 'Evento social', 2),
 ('prioridad', 'Alta', 1), ('prioridad', 'Media', 2), ('prioridad', 'Baja', 3),
 ('sede-next', 'Bogotá', 1), ('sede-next', 'Ciudad de México', 2),
 ('estado-propuesta', 'No enviada', 1), ('estado-propuesta', 'En proceso', 2), ('estado-propuesta', 'Enviada', 3),
 ('area-seguimiento', 'General', 1), ('area-seguimiento', 'Creativo', 2), ('area-seguimiento', 'Comercial', 3), ('area-seguimiento', 'Administrativo', 4)
ON CONFLICT (lista, valor) DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "opciones_config");

            migrationBuilder.DropTable(
                name: "roles_config");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proyectos_prioridad",
                table: "proyectos",
                sql: "prioridad IS NULL OR prioridad IN ('Alta', 'Media', 'Baja')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proyectos_propuesta",
                table: "proyectos",
                sql: "propuesta_estado IN ('No enviada', 'En proceso', 'Enviada')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proyectos_tipo",
                table: "proyectos",
                sql: "tipo_proyecto IS NULL OR tipo_proyecto IN ('Corporativo', 'Evento social')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_proyecto_seguimiento_area",
                table: "proyecto_seguimiento",
                sql: "area IN ('General', 'Creativo', 'Comercial', 'Administrativo')");
        }
    }
}
