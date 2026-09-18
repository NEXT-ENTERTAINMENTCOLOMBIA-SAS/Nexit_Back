using Microsoft.Extensions.Configuration;
using Nexit.Application.DTOs.Usuarios;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.Usuarios;

public class CrearUsuarioUseCase(IUsuarioRepository repository, IUnitOfWork unitOfWork) : ICrearUsuarioUseCase
{
    public async Task<UsuarioResponseDto> ExecuteAsync(CreateUsuarioDto input, CancellationToken cancellationToken = default)
    {
        // Desde que este endpoint pasó de SuperAdminOnly a AdminOrAbove (2026-09-09), una
        // administradora también puede llamarlo -- pero nadie más que la super administradora
        // sembrada directamente en la base puede llevar ese rol (ver Roles.Asignables).
        if (input.Rol == Roles.SuperAdmin) throw new ForbiddenOperationException("No puedes crear una cuenta con el rol de super administrador.");
        var usuario = new Usuario { Id = input.Id, Nombre = input.Nombre, Apellido = input.Apellido, Email = input.Email, Rol = input.Rol, Iniciales = input.Iniciales, Activo = input.Activo };
        await repository.AddAsync(usuario, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UsuarioMapper.ToResponse(usuario);
    }
}

/// <summary>Ver IRegistrarUsuarioUseCase.</summary>
public class RegistrarUsuarioUseCase(IUsuarioRepository repository, ISupabaseAuthAdminService authAdmin, IUnitOfWork unitOfWork) : IRegistrarUsuarioUseCase
{
    public async Task<UsuarioResponseDto> ExecuteAsync(RegistrarUsuarioDto input, Guid callerId, CancellationToken cancellationToken = default)
    {
        // Primero la cuenta de acceso, después el perfil -- mismo criterio que CrearInvitacionUseCase:
        // si Supabase falla, no queda un perfil de negocio sin ninguna forma de iniciar sesión.
        var usuarioId = await authAdmin.CrearCuentaAsync(input.Email, cancellationToken);

        var usuario = new Usuario
        {
            Id = usuarioId,
            Nombre = input.Nombre,
            Apellido = input.Apellido,
            Email = input.Email,
            Rol = input.Rol,
            Iniciales = input.Iniciales,
            Activo = true,
            CreatedBy = callerId,
        };
        await repository.AddAsync(usuario, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UsuarioMapper.ToResponse(usuario);
    }
}

public class ActualizarUsuarioUseCase(IUsuarioRepository repository, IEmailService email, IConfiguration configuration, IUnitOfWork unitOfWork) : IActualizarUsuarioUseCase
{
    public async Task<UsuarioResponseDto> ExecuteAsync(Guid id, UpdateUsuarioDto input, Guid callerId, CancellationToken cancellationToken = default)
    {
        var usuario = await repository.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Usuario", id);
        if (id == callerId)
        {
            // Protección contra que el super administrador se bloquee a sí mismo por accidente
            // (desactivarse o quitarse el rol super_admin, dejando el sistema sin nadie que pueda
            // administrar usuarios).
            if (!input.Activo) throw new ForbiddenOperationException("No puedes desactivar tu propia cuenta.");
            if (input.Rol != Roles.SuperAdmin) throw new ForbiddenOperationException("No puedes quitarte a ti mismo el rol de super administrador.");
        }
        // Nadie más puede quedar como super administrador (2026-09-08, ver Roles.Asignables): hay un
        // solo dueño del sistema, y ascender a alguien a ese rol desde un formulario sería la forma
        // más fácil de perder ese control sin darse cuenta.
        else if (input.Rol == Roles.SuperAdmin)
        {
            throw new ForbiddenOperationException("No puedes darle a nadie el rol de super administrador.");
        }
        // Arranca/limpia el conteo de 30 días para la eliminación automática (docs/17) justo cuando
        // Activo cambia de verdad -- no en cada edición, para no reiniciar el plazo al corregir, por
        // ejemplo, solo el nombre de alguien que ya estaba desactivado.
        var seDesactiva = usuario.Activo && !input.Activo;
        var seReactiva = !usuario.Activo && input.Activo;
        if (seDesactiva) usuario.FechaDesactivacion = DateTime.UtcNow;
        else if (seReactiva) usuario.FechaDesactivacion = null;
        usuario.Nombre = input.Nombre; usuario.Apellido = input.Apellido; usuario.Rol = input.Rol; usuario.Iniciales = input.Iniciales; usuario.Activo = input.Activo;
        usuario.UpdatedAt = DateTime.UtcNow;
        repository.Update(usuario);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Después de guardar, nunca antes: el aviso es una consecuencia del cambio, no un requisito
        // para hacerlo. IEmailService no lanza (ver su contrato), así que esto no puede deshacer lo
        // que ya quedó grabado. Una notificación dentro del sistema no serviría para el caso de
        // desactivar: esa persona justamente ya no puede entrar a verla.
        if (seDesactiva)
        {
            var dias = configuration.GetValue("EliminacionAutomatica:DiasInactividad", 30);
            await email.EnviarAsync(
                usuario.Email,
                "Tu acceso a Nexit quedó suspendido",
                PlantillasCorreoUsuario.CuentaDesactivada(usuario.Nombre, usuario.FechaDesactivacion!.Value.AddDays(dias), dias),
                cancellationToken);
        }
        else if (seReactiva)
        {
            await email.EnviarAsync(
                usuario.Email,
                "Tu acceso a Nexit volvió",
                PlantillasCorreoUsuario.CuentaReactivada(usuario.Nombre),
                cancellationToken);
        }

        return UsuarioMapper.ToResponse(usuario);
    }
}

public class ConsultarUsuariosUseCase(IUsuarioRepository repository) : IConsultarUsuariosUseCase
{
    public async Task<IReadOnlyList<UsuarioResponseDto>> ListAsync(CancellationToken cancellationToken = default) => (await repository.GetAllAsync(cancellationToken)).Select(UsuarioMapper.ToResponse).ToList();
    public async Task<UsuarioResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => UsuarioMapper.ToResponse(await repository.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Usuario", id));
}

/// <summary>
/// Cualquier usuario activo puede ser miembro (o líder) de un equipo de proyecto, sin importar su
/// rol (Alicia 2026-09-18, corrige la restricción del 2026-09-09 a solo miembro/manager): con un
/// equipo real donde todos terminaron siendo admin/super_admin, esa restricción dejaba el selector
/// vacío -- nadie con quien armar un proyecto. Sigue abierto a cualquier autenticado con perfil (ver
/// la política del endpoint) porque armar el equipo de un proyecto no es exclusivo de admin+.
/// </summary>
public class ConsultarUsuariosEquipoUseCase(IUsuarioRepository repository) : IConsultarUsuariosEquipoUseCase
{
    public async Task<IReadOnlyList<UsuarioEquipoDto>> ListAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(cancellationToken))
            .Where(u => u.Activo)
            .OrderBy(u => u.Nombre).ThenBy(u => u.Apellido)
            .Select(u => new UsuarioEquipoDto { Id = u.Id, Nombre = u.Nombre, Apellido = u.Apellido, Rol = u.Rol })
            .ToList();
}

// El antiguo EliminarUsuarioUseCase (borrado inmediato por el super_admin) desapareció el
// 2026-09-08 -- ver docs/40-eliminar-usuarios-por-solicitud.md. Quedan dos caminos, y ninguno es un
// botón que borre en el acto: aprobar una solicitud de eliminación (AprobarComoAdminUseCase) o la
// limpieza automática de abajo.

/// <summary>Ver IEliminarUsuariosInactivosUseCase. Lo dispara el background service, no un endpoint.</summary>
public class EliminarUsuariosInactivosUseCase(
    IUsuarioRepository repository,
    IUsuarioEliminadoRepository archivoRepository,
    ISupabaseAuthAdminService authAdmin,
    IUnitOfWork unitOfWork,
    IConfiguration configuration) : IEliminarUsuariosInactivosUseCase
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var diasInactividad = configuration.GetValue("EliminacionAutomatica:DiasInactividad", 30);
        var limite = DateTime.UtcNow.AddDays(-diasInactividad);
        var candidatos = await repository.GetInactivosDesdeAsync(limite, cancellationToken);

        foreach (var usuario in candidatos)
        {
            await archivoRepository.AddAsync(UsuarioMapper.ToArchivo(usuario, eliminadoPorId: null), cancellationToken);
            await repository.DeleteAsync(usuario.Id, cancellationToken);
        }
        if (candidatos.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var usuario in candidatos) await authAdmin.EliminarCuentaAsync(usuario.Id, cancellationToken);

        return candidatos.Count;
    }
}

internal static class UsuarioMapper
{
    public static UsuarioResponseDto ToResponse(Usuario usuario) => new()
    {
        Id = usuario.Id, Nombre = usuario.Nombre, Apellido = usuario.Apellido, Email = usuario.Email, Rol = usuario.Rol,
        Iniciales = usuario.Iniciales, Activo = usuario.Activo, FechaDesactivacion = usuario.FechaDesactivacion, CreatedAt = usuario.CreatedAt, UpdatedAt = usuario.UpdatedAt
    };

    public static UsuarioEliminado ToArchivo(Usuario usuario, Guid? eliminadoPorId) => new()
    {
        UsuarioIdOriginal = usuario.Id, Nombre = usuario.Nombre, Apellido = usuario.Apellido, Email = usuario.Email, Rol = usuario.Rol,
        Iniciales = usuario.Iniciales, FechaAltaOriginal = usuario.CreatedAt, FechaDesactivacion = usuario.FechaDesactivacion, EliminadoPorId = eliminadoPorId
    };
}
