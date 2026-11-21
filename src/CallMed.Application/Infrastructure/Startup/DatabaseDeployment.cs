using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using MKSANCrud.Data;
using MKSANCrud.Services.Database;
using MKSANCrud.Services.Usuarios;
namespace MKSANCrud.Services.Startup;

public static class DatabaseDeployment
{
    public const int CurrentVersion = 37;
    public static async Task ValidarAsync(MKSANContext db, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) return;
        await db.Database.OpenConnectionAsync(ct);
        try {
            await using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM \"CallMedSchemaVersions\" WHERE \"Version\"=37)";
            if (await cmd.ExecuteScalarAsync(ct) is not true) throw new InvalidOperationException("Banco desatualizado. Execute dotnet CallMedCrud.dll --migrate com a credencial de migração antes de iniciar.");
        } finally { await db.Database.CloseConnectionAsync(); }
    }
    public static async Task AplicarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<MKSANContext>();
        if (!db.Database.IsRelational()) return;
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(ct))
        {
            try { await creator.CreateAsync(ct); }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P04") { /* Another migration job created the same database. */ }
        }
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            // Session lock serializes even two independently started deploy jobs.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(20261111, 37)", ct);
            await using var probe = db.Database.GetDbConnection().CreateCommand();
            probe.CommandText = "SELECT to_regclass('public.\"Pacientes\"') IS NOT NULL";
            var legacy = await probe.ExecuteScalarAsync(ct) is true;
            if (!legacy) await db.Database.EnsureCreatedAsync(ct);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"CallMedSchemaVersions\" (\"Version\" integer PRIMARY KEY, \"AppliedAt\" timestamp with time zone NOT NULL DEFAULT now())", ct);
            var steps = new (int Version, Func<Task> Run)[]
            {
                (12, () => services.GetRequiredService<DatabaseSchemaV12Initializer>().AplicarAsync(ct)),
                (13, () => services.GetRequiredService<DatabaseSchemaV13Initializer>().AplicarAsync(ct)),
                (14, () => services.GetRequiredService<DatabaseSchemaV14Initializer>().AplicarAsync(ct)),
                (15, () => services.GetRequiredService<DatabaseSchemaV15Initializer>().AplicarAsync(ct)),
                (16, () => services.GetRequiredService<DatabaseSchemaV16Initializer>().AplicarAsync(ct)),
                (21, () => services.GetRequiredService<DatabaseSchemaV21Initializer>().AplicarAsync(ct)),
                (22, () => services.GetRequiredService<DatabaseSchemaV22Initializer>().AplicarAsync(ct)),
                (23, () => services.GetRequiredService<DatabaseSchemaV23Initializer>().AplicarAsync(ct)),
                (24, () => services.GetRequiredService<DatabaseSchemaV24Initializer>().AplicarAsync(ct)),
                (25, () => services.GetRequiredService<DatabaseSchemaV25Initializer>().AplicarAsync(ct)),
                (26, () => services.GetRequiredService<DatabaseSchemaV26Initializer>().AplicarAsync(ct)),
                (27, () => services.GetRequiredService<DatabaseSchemaV27Initializer>().AplicarAsync(ct)),
                (28, () => services.GetRequiredService<DatabaseSchemaV28Initializer>().AplicarAsync(ct)),
                (29, () => services.GetRequiredService<DatabaseSchemaV29Initializer>().AplicarAsync(ct)),
                (30, () => services.GetRequiredService<DatabaseSchemaV30Initializer>().AplicarAsync(ct)),
                (31, () => services.GetRequiredService<DatabaseSchemaV31Initializer>().AplicarAsync(ct)),
                (32, () => services.GetRequiredService<DatabaseSchemaV32Initializer>().AplicarAsync(ct)),
                (33, () => services.GetRequiredService<DatabaseSchemaV33Initializer>().AplicarAsync(ct)),
                (34, () => services.GetRequiredService<DatabaseSchemaV34Initializer>().AplicarAsync(ct)),
                (35, () => services.GetRequiredService<DatabaseSchemaV35Initializer>().AplicarAsync(ct)),
                (36, () => services.GetRequiredService<DatabaseSchemaV36Initializer>().AplicarAsync(ct)),
                (37, () => services.GetRequiredService<DatabaseSchemaV37Initializer>().AplicarAsync(ct)),
            };
            foreach (var step in steps)
            {
                await using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM \"CallMedSchemaVersions\" WHERE \"Version\"=@v";
                var parameter = cmd.CreateParameter(); parameter.ParameterName = "v"; parameter.Value = step.Version; cmd.Parameters.Add(parameter);
                if ((long)(await cmd.ExecuteScalarAsync(ct))! > 0) continue;
                // Compatibility patches remain immutable/idempotent. Each marker is
                // committed only after its step; a failed job can safely be repeated.
                if (legacy || step.Version == CurrentVersion) await step.Run();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CallMedSchemaVersions\" (\"Version\") VALUES ({step.Version}) ON CONFLICT DO NOTHING", ct);
            }
            var certificate = services.GetService<X509Certificate2>();
            if (certificate is not null) await KeyRingProtection.ProtegerLegadasAsync(db, certificate, services.GetRequiredService<ILoggerFactory>(), ct);
            services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseDeployment").LogInformation("Migrações CallMed concluídas. SchemaVersion={SchemaVersion}.", CurrentVersion);
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(20261111, 37)", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
