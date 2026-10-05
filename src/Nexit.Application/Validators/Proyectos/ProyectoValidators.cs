using FluentValidation;
using Nexit.Application.DTOs.Proyectos;

namespace Nexit.Application.Validators.Proyectos;

public class CrearProyectoValidator : AbstractValidator<CrearProyectoDto>
{
    // Tipo, prioridad, sede, estado de la propuesta y área de seguimiento son listas editables desde
    // Configuración (tabla opciones_config, 2026-10-05): ya no hay un conjunto fijo en el código ni
    // CHECK en la base -- el formulario ofrece las opciones vigentes y acá solo se acota el largo.
    private const int MaxOpcion = 100;

    public CrearProyectoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(255);
        RuleFor(x => x.EstadoId).NotEmpty();
        RuleFor(x => x.PorcentajeAvance).InclusiveBetween(0, 100);
        RuleFor(x => x.TipoProyecto).MaximumLength(MaxOpcion).WithMessage("El tipo de proyecto no es válido.");
        RuleFor(x => x.Prioridad).MaximumLength(MaxOpcion).WithMessage("La prioridad no es válida.");
        RuleFor(x => x.SedeNext).MaximumLength(MaxOpcion).WithMessage("La sede no es válida.");
        RuleFor(x => x.PropuestaEstado).NotEmpty().MaximumLength(MaxOpcion).WithMessage("El estado de la propuesta no es válido.");
        RuleFor(x => x.FechaPago).NotNull().When(x => x.Pagado).WithMessage("La fecha de pago es requerida cuando el proyecto está pagado.");
        // Miembros del equipo (Alicia 2026-09-29): nombre escrito a mano y cargo libre (lo que hará en el
        // proyecto), ya no una lista fija de roles.
        RuleForEach(x => x.Equipo).ChildRules(equipo =>
        {
            equipo.RuleFor(x => x.Nombre).NotEmpty().MaximumLength(255);
            equipo.RuleFor(x => x.Rol).MaximumLength(100);
        });
    }
}

public class ActualizarProyectoValidator : AbstractValidator<ActualizarProyectoDto>
{
    public ActualizarProyectoValidator()
    {
        Include(new CrearProyectoValidator());
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class CrearSeguimientoProyectoValidator : AbstractValidator<CrearSeguimientoProyectoDto>
{
    public CrearSeguimientoProyectoValidator()
    {
        RuleFor(x => x.Nota).NotEmpty();
        RuleFor(x => x.Area).NotEmpty().MaximumLength(100).WithMessage("El área de seguimiento no es válida.");
    }
}
