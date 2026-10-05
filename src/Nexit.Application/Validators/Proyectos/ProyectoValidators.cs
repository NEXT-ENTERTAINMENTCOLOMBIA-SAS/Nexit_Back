using FluentValidation;
using Nexit.Application.DTOs.Proyectos;

namespace Nexit.Application.Validators.Proyectos;

public class CrearProyectoValidator : AbstractValidator<CrearProyectoDto>
{
    private static readonly string[] Tipos = ["Corporativo", "Evento social"];
    private static readonly string[] Prioridades = ["Alta", "Media", "Baja"];
    private static readonly string[] Propuestas = ["No enviada", "En proceso", "Enviada"];

    public CrearProyectoValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(255);
        RuleFor(x => x.EstadoId).NotEmpty();
        RuleFor(x => x.PorcentajeAvance).InclusiveBetween(0, 100);
        RuleFor(x => x.TipoProyecto).Must(x => string.IsNullOrWhiteSpace(x) || Tipos.Contains(x)).WithMessage("El tipo de proyecto no es válido.");
        RuleFor(x => x.Prioridad).Must(x => string.IsNullOrWhiteSpace(x) || Prioridades.Contains(x)).WithMessage("La prioridad no es válida.");
        RuleFor(x => x.PropuestaEstado).Must(x => Propuestas.Contains(x)).WithMessage("El estado de la propuesta no es válido.");
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
    private static readonly string[] Areas = ["General", "Creativo", "Comercial", "Administrativo"];
    public CrearSeguimientoProyectoValidator()
    {
        RuleFor(x => x.Nota).NotEmpty();
        RuleFor(x => x.Area).Must(x => Areas.Contains(x)).WithMessage("El área de seguimiento no es válida.");
    }
}
