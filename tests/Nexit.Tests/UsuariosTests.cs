using Microsoft.Extensions.Configuration;
using Moq;
using Nexit.Application.DTOs.Usuarios;
using Nexit.Application.UseCases.Usuarios;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Tests;

/// <summary>
/// Gestión de usuarios (exclusiva del super administrador, ver docs/06-modelo-permisos-roles.md).
/// El foco de estas pruebas está en las protecciones de auto-bloqueo: nadie debe poder desactivarse
/// a sí mismo, quitarse el rol de super_admin, ni eliminar su propia cuenta.
/// </summary>
public class UsuariosTests
{
    /// <summary>
    /// Configuración real pero sin valores -- así los `GetValue(clave, porDefecto)` del código
    /// devuelven su valor por defecto, que es justo lo que pasa en una instalación sin configurar.
    /// Un `Mock.Of&lt;IConfiguration&gt;()` no sirve: su GetSection devuelve null y GetValue revienta.
    /// </summary>
    private static readonly IConfiguration ConfiguracionVacia = new ConfigurationBuilder().Build();

    [Fact]
    public async Task CrearUsuario_persists_the_supabase_auth_id_as_the_profile_id()
    {
        var repository = new Mock<IUsuarioRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        Usuario? saved = null;
        repository.Setup(x => x.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>())).Callback<Usuario, CancellationToken>((u, _) => saved = u).Returns(Task.CompletedTask);
        var authId = Guid.NewGuid();

        var result = await new CrearUsuarioUseCase(repository.Object, unitOfWork.Object).ExecuteAsync(
            new CreateUsuarioDto { Id = authId, Nombre = "Ana", Apellido = "Ruiz", Email = "ana@next.com", Rol = Roles.Miembro });

        Assert.Equal(authId, result.Id);
        Assert.NotNull(saved);
        Assert.Equal(authId, saved!.Id);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarUsuario_throws_when_user_does_not_exist()
    {
        var repository = new Mock<IUsuarioRepository>();
        repository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => new ActualizarUsuarioUseCase(repository.Object, Mock.Of<IEmailService>(), ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(Guid.NewGuid(), new UpdateUsuarioDto { Rol = Roles.Admin }, Guid.NewGuid()));
    }

    [Fact]
    public async Task ActualizarUsuario_allows_a_super_admin_to_edit_someone_else()
    {
        var repository = new Mock<IUsuarioRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var id = Guid.NewGuid();
        var usuario = new Usuario { Id = id, Nombre = "Ana", Apellido = "Ruiz", Email = "ana@next.com", Rol = Roles.Miembro, Activo = true };
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(usuario);

        var result = await new ActualizarUsuarioUseCase(repository.Object, Mock.Of<IEmailService>(), ConfiguracionVacia, unitOfWork.Object)
            .ExecuteAsync(id, new UpdateUsuarioDto { Nombre = "Ana", Apellido = "Ruiz", Rol = Roles.Manager, Activo = true }, Guid.NewGuid());

        Assert.Equal(Roles.Manager, result.Rol);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarUsuario_rejects_deactivating_your_own_account()
    {
        var repository = new Mock<IUsuarioRepository>();
        var id = Guid.NewGuid();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Usuario { Id = id, Rol = Roles.SuperAdmin, Activo = true });

        await Assert.ThrowsAsync<ForbiddenOperationException>(() => new ActualizarUsuarioUseCase(repository.Object, Mock.Of<IEmailService>(), ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(id, new UpdateUsuarioDto { Rol = Roles.SuperAdmin, Activo = false }, id));
    }

    [Fact]
    public async Task ActualizarUsuario_rejects_removing_your_own_super_admin_role()
    {
        var repository = new Mock<IUsuarioRepository>();
        var id = Guid.NewGuid();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Usuario { Id = id, Rol = Roles.SuperAdmin, Activo = true });

        await Assert.ThrowsAsync<ForbiddenOperationException>(() => new ActualizarUsuarioUseCase(repository.Object, Mock.Of<IEmailService>(), ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(id, new UpdateUsuarioDto { Rol = Roles.Admin, Activo = true }, id));
    }

    [Fact]
    public async Task ActualizarUsuario_allows_a_super_admin_to_edit_their_own_non_sensitive_fields()
    {
        var repository = new Mock<IUsuarioRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var id = Guid.NewGuid();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Usuario { Id = id, Rol = Roles.SuperAdmin, Activo = true });

        var result = await new ActualizarUsuarioUseCase(repository.Object, Mock.Of<IEmailService>(), ConfiguracionVacia, unitOfWork.Object)
            .ExecuteAsync(id, new UpdateUsuarioDto { Nombre = "Alicia", Apellido = "Medina", Rol = Roles.SuperAdmin, Activo = true }, id);

        Assert.Equal("Alicia", result.Nombre);
    }

    // Los tres tests de EliminarUsuarioUseCase se movieron a SolicitudesEliminacionTests el
    // 2026-09-08 junto con la lógica: eliminar a una persona ya no es una acción directa, es aprobar
    // una solicitud de tipo "usuario" (docs/40).

    [Fact]
    public async Task ActualizarUsuario_stamps_FechaDesactivacion_when_deactivating()
    {
        var repository = new Mock<IUsuarioRepository>();
        var id = Guid.NewGuid();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Usuario { Id = id, Rol = Roles.Miembro, Activo = true, FechaDesactivacion = null });

        var result = await new ActualizarUsuarioUseCase(repository.Object, Mock.Of<IEmailService>(), ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(id, new UpdateUsuarioDto { Nombre = "Ana", Apellido = "Ruiz", Rol = Roles.Miembro, Activo = false }, Guid.NewGuid());

        Assert.False(result.Activo);
        Assert.NotNull(result.FechaDesactivacion);
    }

    [Fact]
    public async Task ActualizarUsuario_clears_FechaDesactivacion_when_reactivating()
    {
        var repository = new Mock<IUsuarioRepository>();
        var id = Guid.NewGuid();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new Usuario { Id = id, Rol = Roles.Miembro, Activo = false, FechaDesactivacion = DateTime.UtcNow.AddDays(-10) });

        var result = await new ActualizarUsuarioUseCase(repository.Object, Mock.Of<IEmailService>(), ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(id, new UpdateUsuarioDto { Nombre = "Ana", Apellido = "Ruiz", Rol = Roles.Miembro, Activo = true }, Guid.NewGuid());

        Assert.True(result.Activo);
        Assert.Null(result.FechaDesactivacion);
    }

    [Fact]
    public async Task EliminarUsuariosInactivos_archives_and_deletes_only_those_past_the_configured_threshold()
    {
        var repository = new Mock<IUsuarioRepository>();
        var archivoRepository = new Mock<IUsuarioEliminadoRepository>();
        var authAdmin = new Mock<ISupabaseAuthAdminService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["EliminacionAutomatica:DiasInactividad"] = "30" }).Build();
        var vencido1 = new Usuario { Id = Guid.NewGuid(), Nombre = "A", Apellido = "B", Email = "a@agencianextmkt.com", Rol = Roles.Miembro, Activo = false, FechaDesactivacion = DateTime.UtcNow.AddDays(-31) };
        var vencido2 = new Usuario { Id = Guid.NewGuid(), Nombre = "C", Apellido = "D", Email = "c@agencianextmkt.com", Rol = Roles.Miembro, Activo = false, FechaDesactivacion = DateTime.UtcNow.AddDays(-45) };
        // El repositorio real solo devuelve los que ya cumplieron el plazo -- este mock simula ese filtro.
        repository.Setup(x => x.GetInactivosDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([vencido1, vencido2]);

        var eliminados = await new EliminarUsuariosInactivosUseCase(repository.Object, archivoRepository.Object, authAdmin.Object, unitOfWork.Object, configuration).ExecuteAsync();

        Assert.Equal(2, eliminados);
        archivoRepository.Verify(x => x.AddAsync(It.IsAny<UsuarioEliminado>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(x => x.DeleteAsync(vencido1.Id, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.DeleteAsync(vencido2.Id, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        authAdmin.Verify(x => x.EliminarCuentaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EliminarUsuariosInactivos_does_nothing_when_no_one_is_past_the_threshold()
    {
        var repository = new Mock<IUsuarioRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository.Setup(x => x.GetInactivosDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var eliminados = await new EliminarUsuariosInactivosUseCase(repository.Object, Mock.Of<IUsuarioEliminadoRepository>(), Mock.Of<ISupabaseAuthAdminService>(), unitOfWork.Object, new ConfigurationBuilder().Build()).ExecuteAsync();

        Assert.Equal(0, eliminados);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsultarUsuarios_returns_mapped_users()
    {
        var repository = new Mock<IUsuarioRepository>();
        repository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new Usuario { Nombre = "Ana", Rol = Roles.Miembro }]);
        var result = await new ConsultarUsuariosUseCase(repository.Object).ListAsync();
        Assert.Single(result);
        Assert.Equal("Ana", result[0].Nombre);
    }

    // -- Endpoint /api/usuarios/equipo: a diferencia de ConsultarUsuarios (sólo AdminOrAbove), este
    // lo puede llamar cualquier usuario autenticado -- lo necesita el buscador de "miembros del
    // equipo" al armar un proyecto, y esa pantalla no está restringida a administradores. Por eso el
    // DTO es deliberadamente liviano (sin email ni Activo). Hasta el 2026-09-09 excluía admin/
    // super_admin, pero con un equipo real donde todos terminaron siendo admin/super_admin eso
    // dejaba el selector vacío, así que desde el 2026-09-18 cualquier rol cuenta -- solo se filtra
    // por inactivo.
    [Fact]
    public async Task ConsultarUsuariosEquipo_returns_only_active_users_of_any_role_ordered_by_name()
    {
        var repository = new Mock<IUsuarioRepository>();
        repository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new Usuario { Nombre = "Zoe", Apellido = "Ruiz", Rol = Roles.Miembro, Activo = true },
            new Usuario { Nombre = "Ana", Apellido = "Gómez", Rol = Roles.Manager, Activo = true },
            new Usuario { Nombre = "Beto", Apellido = "Admin", Rol = Roles.Admin, Activo = true },
            new Usuario { Nombre = "Cami", Apellido = "Root", Rol = Roles.SuperAdmin, Activo = true },
            new Usuario { Nombre = "Inés", Apellido = "Baja", Rol = Roles.Miembro, Activo = false },
        ]);

        var result = await new ConsultarUsuariosEquipoUseCase(repository.Object).ListAsync();

        Assert.Equal(4, result.Count);
        Assert.Equal("Ana", result[0].Nombre);
        Assert.Equal("Beto", result[1].Nombre);
        Assert.Equal("Cami", result[2].Nombre);
        Assert.Equal("Zoe", result[3].Nombre);
    }

    // --- Alta manual, sin correo de invitación de por medio (2026-09-08, docs/38).

    [Fact]
    public async Task RegistrarUsuario_usa_como_id_el_uuid_que_devuelve_Supabase()
    {
        // Es la razón de ser de este camino: el id del perfil TIENE que ser el de la cuenta de
        // Supabase Auth, porque es lo que trae el JWT con el que esa persona va a autenticarse.
        var idDeSupabase = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var authAdmin = new Mock<ISupabaseAuthAdminService>();
        authAdmin.Setup(x => x.CrearCuentaAsync("nueva@agencianextmkt.com", It.IsAny<CancellationToken>())).ReturnsAsync(idDeSupabase);
        var repository = new Mock<IUsuarioRepository>();
        Usuario? guardado = null;
        repository.Setup(x => x.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()))
            .Callback<Usuario, CancellationToken>((u, _) => guardado = u).Returns(Task.CompletedTask);
        var uow = new Mock<IUnitOfWork>();

        var result = await new RegistrarUsuarioUseCase(repository.Object, authAdmin.Object, uow.Object)
            .ExecuteAsync(new RegistrarUsuarioDto { Nombre = "Nueva", Apellido = "Persona", Email = "nueva@agencianextmkt.com", Rol = "admin" }, callerId);

        Assert.Equal(idDeSupabase, result.Id);
        Assert.NotNull(guardado);
        Assert.Equal(idDeSupabase, guardado!.Id);
        Assert.Equal("admin", guardado.Rol);
        Assert.True(guardado.Activo);
        Assert.Equal(callerId, guardado.CreatedBy);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarUsuario_no_guarda_perfil_si_Supabase_no_pudo_crear_la_cuenta()
    {
        // Sin cuenta de acceso, un perfil de negocio es una fila que nadie puede usar nunca.
        var authAdmin = new Mock<ISupabaseAuthAdminService>();
        authAdmin.Setup(x => x.CrearCuentaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Ese correo ya tiene una cuenta en Supabase Auth."));
        var repository = new Mock<IUsuarioRepository>();
        var uow = new Mock<IUnitOfWork>();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new RegistrarUsuarioUseCase(repository.Object, authAdmin.Object, uow.Object)
                .ExecuteAsync(new RegistrarUsuarioDto { Nombre = "Nueva", Apellido = "Persona", Email = "nueva@agencianextmkt.com" }, Guid.NewGuid()));

        repository.Verify(x => x.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- El aviso por correo a la persona afectada (2026-09-08, docs/39). Es el único correo que
    // este backend manda por su cuenta: una notificación dentro del sistema no serviría, porque
    // quien acaba de perder el acceso justamente ya no puede entrar a verla.

    [Fact]
    public async Task ActualizarUsuario_le_avisa_por_correo_a_quien_desactiva()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IUsuarioRepository>();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { Id = id, Nombre = "Ex", Apellido = "Compañero", Email = "ex@agencianextmkt.com", Rol = "miembro", Activo = true });
        var email = new Mock<IEmailService>();

        await new ActualizarUsuarioUseCase(repository.Object, email.Object, ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(id, new UpdateUsuarioDto { Nombre = "Ex", Apellido = "Compañero", Rol = "miembro", Activo = false }, Guid.NewGuid());

        email.Verify(x => x.EnviarAsync("ex@agencianextmkt.com", It.Is<string>(a => a.Contains("suspendido")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarUsuario_le_avisa_por_correo_a_quien_reactiva()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IUsuarioRepository>();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { Id = id, Nombre = "Vuelve", Apellido = "Persona", Email = "vuelve@agencianextmkt.com", Rol = "miembro", Activo = false, FechaDesactivacion = DateTime.UtcNow.AddDays(-3) });
        var email = new Mock<IEmailService>();

        await new ActualizarUsuarioUseCase(repository.Object, email.Object, ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(id, new UpdateUsuarioDto { Nombre = "Vuelve", Apellido = "Persona", Rol = "miembro", Activo = true }, Guid.NewGuid());

        email.Verify(x => x.EnviarAsync("vuelve@agencianextmkt.com", It.Is<string>(a => a.Contains("volvió")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarUsuario_no_manda_ningun_correo_si_solo_se_corrige_el_nombre()
    {
        // Nadie quiere recibir "tu acceso quedó suspendido" porque le arreglaron una tilde al apellido.
        var id = Guid.NewGuid();
        var repository = new Mock<IUsuarioRepository>();
        repository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuario { Id = id, Nombre = "Ana", Apellido = "Ruiz", Email = "ana@agencianextmkt.com", Rol = "miembro", Activo = true });
        var email = new Mock<IEmailService>();

        await new ActualizarUsuarioUseCase(repository.Object, email.Object, ConfiguracionVacia, Mock.Of<IUnitOfWork>())
            .ExecuteAsync(id, new UpdateUsuarioDto { Nombre = "Ana María", Apellido = "Ruiz", Rol = "miembro", Activo = true }, Guid.NewGuid());

        email.Verify(x => x.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

