using CourseGuard.Backend.Data;
using Npgsql;
using System.Text.RegularExpressions;

internal static class DevDatabaseSetup
{
    public static async Task InitializeAsync(string projectRef)
    {
        if (string.IsNullOrWhiteSpace(projectRef)) throw new ArgumentException("Explicit --project-ref is required.");
        using var connection = new CourseGuardDbContext("").CreateConnection();
        var config = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (config.Username != "postgres." + projectRef || config.Database != "postgres")
            throw new InvalidOperationException("Configured database does not match the requested Supabase project.");
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema='public'", connection, transaction);
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Refusing initialization: public schema is not empty.");
        string? root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "CourseGuard", "CourseGuard", "CourseGuard.csproj")))
            root = Directory.GetParent(root)?.FullName;
        if (root == null) throw new InvalidOperationException("Repository root not found.");
        string scripts = Path.Combine(root, "CourseGuard", "CourseGuard", "Backend", "Database", "Scripts");
        string schema = await File.ReadAllTextAsync(Path.Combine(scripts, "SupabaseSchema.sql"));
        int createStart = schema.IndexOf("-- ROLES", StringComparison.Ordinal);
        if (createStart < 0) throw new InvalidOperationException("Schema creation marker not found.");
        command.CommandTimeout = 90;
        command.CommandText = schema[createStart..]; // Exclude the destructive reset preamble.
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Created base schema and repository demo data.");
        foreach (string migration in Directory.GetFiles(Path.Combine(scripts, "Migrations"), "*.sql").Order(StringComparer.Ordinal))
        {
            // All scripts share the outer transaction; PL/pgSQL BEGIN blocks are preserved.
            command.CommandText = Regex.Replace(await File.ReadAllTextAsync(migration), @"(?im)^\s*(BEGIN|COMMIT);\s*$", "");
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("Applied " + Path.GetFileName(migration));
        }
        command.CommandText = """
            DO $$ DECLARE t record; BEGIN
              FOR t IN SELECT tablename FROM pg_tables WHERE schemaname='public' LOOP
                EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t.tablename);
              END LOOP;
            END $$;
            """;
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        Console.WriteLine("COMMITTED: fresh development schema initialized with RLS enabled.");
    }
}
