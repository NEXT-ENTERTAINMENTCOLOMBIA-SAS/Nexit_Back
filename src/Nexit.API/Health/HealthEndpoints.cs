using System.Diagnostics;
using System.Reflection;
using Nexit.Infrastructure.Data;

namespace Nexit.API.Health;

/// <summary>
/// Chequeo de salud (2026-10-05). Dos endpoints anónimos y fuera del rate limiter, pensados para el
/// healthcheck de Railway y para monitoreo externo:
///   GET /health        -> "el proceso está vivo" (no toca la base; sirve de liveness).
///   GET /health/ready  -> además verifica que la base responda (503 si no) -- es el que se mira cuando
///                         el usuario reporta "me sale error con los datos".
/// No devuelve cadenas de conexión ni detalles internos: solo estado, tiempos y versión.
/// </summary>
public static class HealthEndpoints
{
    private static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev";

    public static void MapNexitHealth(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Json(new { status = "ok", version = Version, utc = DateTime.UtcNow }))
           .AllowAnonymous();

        app.MapGet("/health/ready", async (NexitDbContext db, CancellationToken ct) =>
        {
            var sw = Stopwatch.StartNew();
            bool dbOk;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                dbOk = await db.Database.CanConnectAsync(cts.Token);
            }
            catch { dbOk = false; }
            sw.Stop();
            var cuerpo = new { status = dbOk ? "ok" : "degraded", db = dbOk ? "ok" : "fail", dbMs = sw.ElapsedMilliseconds, version = Version, utc = DateTime.UtcNow };
            return Results.Json(cuerpo, statusCode: dbOk ? 200 : 503);
        }).AllowAnonymous();
    }
}
