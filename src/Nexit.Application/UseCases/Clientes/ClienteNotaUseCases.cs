using FluentValidation;
using Nexit.Application.DTOs.Clientes;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.Clientes;

public interface IClienteNotasUseCase
{
    Task<IReadOnlyList<ClienteNotaDto>> ListAsync(Guid clienteId, CancellationToken cancellationToken = default);
    Task<ClienteNotaDto> AgregarAsync(Guid clienteId, CrearClienteNotaDto input, Guid usuarioId, CancellationToken cancellationToken = default);
    /// <summary>Borra una nota: la puede borrar quien la escribió o un admin/super_admin.</summary>
    Task EliminarAsync(Guid clienteId, Guid notaId, Guid usuarioId, string? rol, CancellationToken cancellationToken = default);
}

public class CrearClienteNotaValidator : AbstractValidator<CrearClienteNotaDto>
{
    public CrearClienteNotaValidator()
    {
        RuleFor(x => x.Nota).NotEmpty().WithMessage("Escribe la nota.").MaximumLength(4000).WithMessage("La nota es demasiado larga (máximo 4000 caracteres).");
        RuleFor(x => x.Area).NotEmpty().MaximumLength(100).WithMessage("El área no es válida.");
    }
}

public class ClienteNotasUseCase(IClienteRepository clientes, IClienteNotaRepository notas, IUnitOfWork unitOfWork) : IClienteNotasUseCase
{
    public async Task<IReadOnlyList<ClienteNotaDto>> ListAsync(Guid clienteId, CancellationToken ct = default)
    {
        _ = await clientes.GetByIdAsync(clienteId, ct) ?? throw new EntityNotFoundException("Cliente", clienteId);
        return (await notas.GetByClienteIdAsync(clienteId, ct)).Select(ToDto).ToList();
    }

    public async Task<ClienteNotaDto> AgregarAsync(Guid clienteId, CrearClienteNotaDto input, Guid usuarioId, CancellationToken ct = default)
    {
        _ = await clientes.GetByIdAsync(clienteId, ct) ?? throw new EntityNotFoundException("Cliente", clienteId);
        var nota = new ClienteNota { ClienteId = clienteId, AutorId = usuarioId, Area = input.Area.Trim(), Nota = input.Nota.Trim(), Fecha = DateTime.UtcNow };
        await notas.AddAsync(nota, ct);
        await unitOfWork.SaveChangesAsync(ct);
        // Se vuelve a leer para devolver el nombre del autor ya resuelto.
        var guardada = (await notas.GetByClienteIdAsync(clienteId, ct)).First(n => n.Id == nota.Id);
        return ToDto(guardada);
    }

    public async Task EliminarAsync(Guid clienteId, Guid notaId, Guid usuarioId, string? rol, CancellationToken ct = default)
    {
        var nota = await notas.GetByIdAsync(notaId, ct);
        if (nota is null || nota.ClienteId != clienteId) throw new EntityNotFoundException("Nota", notaId);
        var esAdmin = rol is "admin" or "super_admin";
        if (!esAdmin && nota.AutorId != usuarioId) throw new BusinessRuleException("Solo quien escribió la nota o un administrador puede borrarla.");
        await notas.DeleteAsync(notaId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static ClienteNotaDto ToDto(ClienteNota n) => new()
    {
        Id = n.Id, AutorId = n.AutorId, Area = n.Area, Fecha = n.Fecha, Nota = n.Nota,
        AutorNombre = n.Autor is null ? null : $"{n.Autor.Nombre} {n.Autor.Apellido}".Trim(),
    };
}
