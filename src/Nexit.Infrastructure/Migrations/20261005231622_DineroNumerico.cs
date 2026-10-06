using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DineroNumerico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "moneda",
                table: "proyectos",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "COP");

            migrationBuilder.AddColumn<decimal>(
                name: "valor",
                table: "proyectos",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "costo_referencia_valor",
                table: "proveedores",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "moneda",
                table: "proveedores",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "COP");

            migrationBuilder.AddColumn<string>(
                name: "moneda",
                table: "clientes",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "COP");

            migrationBuilder.AddColumn<decimal>(
                name: "valor_referencia_monto",
                table: "clientes",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "moneda",
                table: "proyectos");

            migrationBuilder.DropColumn(
                name: "valor",
                table: "proyectos");

            migrationBuilder.DropColumn(
                name: "costo_referencia_valor",
                table: "proveedores");

            migrationBuilder.DropColumn(
                name: "moneda",
                table: "proveedores");

            migrationBuilder.DropColumn(
                name: "moneda",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "valor_referencia_monto",
                table: "clientes");
        }
    }
}
