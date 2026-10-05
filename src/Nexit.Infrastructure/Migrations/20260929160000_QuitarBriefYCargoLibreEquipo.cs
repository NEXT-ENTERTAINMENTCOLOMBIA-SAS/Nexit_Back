using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nexit.Infrastructure.Data;

#nullable disable

namespace Nexit.Infrastructure.Migrations
{
    /// <summary>
    /// Quita el estado del brief (proyectos.estado_brief e informes_snapshot.por_brief) y abre el cargo
    /// de proyecto_equipo.rol a texto libre (se elimina el CHECK de lista fija).
    /// </summary>
    [DbContext(typeof(NexitDbContext))]
    [Migration("20260929160000_QuitarBriefYCargoLibreEquipo")]
    public partial class QuitarBriefYCargoLibreEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_proyectos_brief", table: "proyectos");
            migrationBuilder.DropIndex(name: "ix_proyectos_estado_brief", table: "proyectos");
            migrationBuilder.DropColumn(name: "estado_brief", table: "proyectos");
            migrationBuilder.DropColumn(name: "por_brief", table: "informes_snapshot");
            migrationBuilder.DropCheckConstraint(name: "ck_proyecto_equipo_rol", table: "proyecto_equipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "estado_brief", table: "proyectos", type: "text", nullable: false, defaultValue: "Pendiente por enviar");
            migrationBuilder.AddColumn<string>(
                name: "por_brief", table: "informes_snapshot", type: "jsonb", nullable: false, defaultValue: "{}");
            migrationBuilder.CreateIndex(name: "ix_proyectos_estado_brief", table: "proyectos", column: "estado_brief");
            migrationBuilder.AddCheckConstraint(
                name: "ck_proyectos_brief", table: "proyectos",
                sql: "estado_brief IN ('Pendiente por enviar', 'Entregado, a espera de respuesta', 'Requiere ajustes', 'Aprobado')");
            migrationBuilder.AddCheckConstraint(
                name: "ck_proyecto_equipo_rol", table: "proyecto_equipo",
                sql: "rol IN ('Ejecutivo', 'Comercial', 'Administrativo', 'Diseñador 3D', 'Diseñador gráfico')");
        }
    }
}
