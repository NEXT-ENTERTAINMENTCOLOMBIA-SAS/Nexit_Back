using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexit.Application.Services;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.BackgroundServices;
using Nexit.Infrastructure.Data;
using Nexit.Infrastructure.Repositories;
using Nexit.Infrastructure.Services;
using Nexit.Infrastructure.UnitOfWork;

namespace Nexit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection or DATABASE_URL.");
        services.AddDbContext<NexitDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(NexitDbContext).Assembly.FullName)).UseSnakeCaseNamingConvention());
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IClienteAdjuntoRepository, ClienteAdjuntoRepository>();
        services.AddScoped<IProveedorRepository, ProveedorRepository>();
        services.AddScoped<IProveedorAdjuntoRepository, ProveedorAdjuntoRepository>();
        services.AddScoped<IProyectoRepository, ProyectoRepository>();
        services.AddScoped<IProyectoAdjuntoRepository, ProyectoAdjuntoRepository>();
        services.AddScoped<IInformesRepository, InformesRepository>();
        services.AddScoped<ICatalogosRepository, CatalogosRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IUsuarioEliminadoRepository, UsuarioEliminadoRepository>();
        services.AddScoped<IDominioCorreoPermitidoRepository, DominioCorreoPermitidoRepository>();
        services.AddScoped<ISolicitudEliminacionRepository, SolicitudEliminacionRepository>();
        services.AddScoped<INotificacionRepository, NotificacionRepository>();
        services.AddScoped<IHistorialCambioRepository, HistorialCambioRepository>();
        services.AddScoped<IProveedorColaboradorRepository, ProveedorColaboradorRepository>();
        services.AddScoped<IInvitacionEquipoRepository, InvitacionEquipoRepository>();
        services.AddSingleton<IInformeExcelExporter, InformeExcelExporter>();
        services.AddScoped<IClientesImportExporter, ClientesImportExporter>();
        services.AddScoped<IProveedoresImportExporter, ProveedoresImportExporter>();
        services.AddScoped<IProyectosImportExporter, ProyectosImportExporter>();
        services.AddScoped<IUsuariosImportExporter, UsuariosImportExporter>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<ISupabaseAuthAdminService, SupabaseAuthAdminService>();
        services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
        services.AddHostedService<EliminacionAutomaticaUsuariosInactivosService>();
        return services;
    }
}
