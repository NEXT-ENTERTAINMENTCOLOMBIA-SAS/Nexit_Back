using FluentValidation;
using Nexit.Application.DTOs.SolicitudesEliminacion;
using Nexit.Core.Constants;
using Nexit.Core.Interfaces;

namespace Nexit.Application.Validators.SolicitudesEliminacion;

public class CrearSolicitudEliminacionValidator : AbstractValidator<CrearSolicitudEliminacionDto>
{
    public CrearSolicitudEliminacionValidator(
        IClienteRepository clientes, IProveedorRepository proveedores, IProyectoRepository proyectos, IUsuarioRepository usuarios)
    {
        RuleFor(x => x.TipoEntidad).Must(tipo => TiposEntidadEliminable.Todos.Contains(tipo))
            .WithMessage("tipoEntidad debe ser 'cliente', 'proveedor', 'proyecto' o 'usuario'.");
        // El motivo es obligatorio para cualquiera que pase por acá (Alicia 2026-09-18): quien puede
        // eliminar directo -- solo super_admin, ver SuperAdminOnly en los controllers -- no pasa por
        // este validador en absoluto, así que todo el que sí llega aquí tiene que explicar por qué.
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Tienes que indicar el motivo de la eliminación.");
        RuleFor(x => x.EntidadId).MustAsync(async (dto, entidadId, token) => dto.TipoEntidad switch
        {
            TiposEntidadEliminable.Cliente => await clientes.GetByIdAsync(entidadId, token) is not null,
            TiposEntidadEliminable.Proveedor => await proveedores.GetByIdAsync(entidadId, token) is not null,
            TiposEntidadEliminable.Proyecto => await proyectos.GetByIdAsync(entidadId, token) is not null,
            TiposEntidadEliminable.Usuario => await usuarios.GetByIdAsync(entidadId, token) is not null,
            _ => true // el tipo inválido ya lo reporta la regla de arriba
        }).WithMessage("La entidad indicada no existe.").When(x => TiposEntidadEliminable.Todos.Contains(x.TipoEntidad));
    }
}
