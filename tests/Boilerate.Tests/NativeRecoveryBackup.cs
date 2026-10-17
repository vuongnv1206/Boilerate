using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Npgsql;
using Xunit;

namespace Boilerate.Tests;

/// <summary>Native backup helpers restricted to freshly generated fixture databases.</summary>
internal sealed class NativeRecoveryBackup(string provider, string sourceConnection, string backupPath)
{
    public static async Task<NativeRecoveryBackup> CreateAsync(string provider, FoundationDatabase source, string directory)
    {
        RequireFixtureName(source.Name);
        string sourceConnection = source.Configuration["DatabaseSettings:ConnectionString"]!;
        if (provider == "mssql")
        {
            var connection = new SqlConnectionStringBuilder(sourceConnection);
            string? shared = Environment.GetEnvironmentVariable("BOILERATE_RECOVERY_SQL_BACKUP_DIRECTORY");
            if (!connection.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(shared))
                throw new InvalidOperationException("Non-LocalDB recovery requires BOILERATE_RECOVERY_SQL_BACKUP_DIRECTORY accessible to both SQL Server and the test process.");
            string target = Path.GetFullPath(shared ?? directory);
            if (!Directory.Exists(target))
                throw new InvalidOperationException("The recovery backup directory must already exist.");
            string path = Path.Combine(target, source.Name + ".bak");
            if (File.Exists(path))
                throw new InvalidOperationException("Recovery never overwrites an existing backup.");
            var backup = new NativeRecoveryBackup(provider, sourceConnection, path);
            try
            {
                await using var db = new SqlConnection(sourceConnection);
                await db.OpenAsync();
                await using var command = db.CreateCommand();
                command.CommandTimeout = 60;
                command.CommandText = $"BACKUP DATABASE [{source.Name}] TO DISK = @path WITH COPY_ONLY, CHECKSUM";
                command.Parameters.AddWithValue("@path", path);
                await command.ExecuteNonQueryAsync();
                command.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM";
                await command.ExecuteNonQueryAsync();
                return backup;
            }
            catch (Exception failure)
            {
                // Also clean a partial backup in a configured shared directory outside the temp root.
                try { backup.DeleteBackup(); }
                catch (Exception cleanup) { throw new AggregateException("Recovery backup failed and its partial file could not be removed.", failure, cleanup); }
                throw;
            }
        }
        string dump = Path.Combine(directory, source.Name + ".dump");
        await PostgresAsync("pg_dump", sourceConnection, "--format=custom", "--file", dump);
        return new(provider, sourceConnection, dump);
    }

    public async Task RestoreAsync(FoundationDatabase destination)
    {
        RequireFixtureName(destination.Name);
        string destinationConnection = destination.Configuration["DatabaseSettings:ConnectionString"]!;
        if (provider != "mssql")
        {
            await PostgresAsync("pg_restore", destinationConnection, "--exit-on-error", "--no-owner", "--no-acl", backupPath);
            return;
        }
        var source = new SqlConnectionStringBuilder(sourceConnection);
        var target = new SqlConnectionStringBuilder(destinationConnection);
        Assert.Equal(source.DataSource, target.DataSource);
        RequireFixtureName(source.InitialCatalog);
        RequireFixtureName(target.InitialCatalog);
        Assert.NotEqual(source.InitialCatalog, target.InitialCatalog);

        // The fixture created this destination solely so failure cleanup owns it. Check it is empty,
        // then drop it before RESTORE: no existing database is overwritten and WITH REPLACE is never used.
        await using (var empty = new SqlConnection(destinationConnection))
        {
            await empty.OpenAsync();
            await using var check = empty.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0";
            Assert.Equal(0, Convert.ToInt32(await check.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
        }
        SqlConnection.ClearAllPools();
        target.InitialCatalog = "master";
        await using var admin = new SqlConnection(target.ConnectionString);
        await admin.OpenAsync();
        await using var command = admin.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = $"DROP DATABASE [{destination.Name}]";
        await command.ExecuteNonQueryAsync();
        command.CommandText = "RESTORE FILELISTONLY FROM DISK = @path";
        command.Parameters.AddWithValue("@path", backupPath);
        var files = new List<(string LogicalName, string PhysicalName, string Type, long Id)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                files.Add((reader.GetString(reader.GetOrdinal("LogicalName")), reader.GetString(reader.GetOrdinal("PhysicalName")),
                    reader.GetString(reader.GetOrdinal("Type")), reader.GetInt64(reader.GetOrdinal("FileId"))));
        }
        Assert.NotEmpty(files);
        var moves = new List<string>();
        foreach (var file in files)
        {
            Assert.True(file.Type is "D" or "L", "The test backup must contain ordinary data/log files only.");
            int separator = file.PhysicalName.LastIndexOfAny(['/', '\\']);
            Assert.True(separator >= 0, "SQL Server returned an invalid physical file path.");
            string physical = file.PhysicalName[..(separator + 1)] + destination.Name + "_" + file.Id.ToString(CultureInfo.InvariantCulture)
                + (file.Type == "L" ? ".ldf" : ".mdf");
            string logicalParameter = "@logical" + moves.Count.ToString(CultureInfo.InvariantCulture);
            string physicalParameter = "@physical" + moves.Count.ToString(CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue(logicalParameter, file.LogicalName);
            command.Parameters.AddWithValue(physicalParameter, physical);
            moves.Add($"MOVE {logicalParameter} TO {physicalParameter}");
        }
        command.CommandText = $"RESTORE DATABASE [{destination.Name}] FROM DISK = @path WITH CHECKSUM, RECOVERY, " + string.Join(", ", moves);
        await command.ExecuteNonQueryAsync();
    }

    public void DeleteBackup()
    {
        // backupPath was selected from an existing directory plus this fixture's validated unique name.
        string name = Path.GetFileNameWithoutExtension(backupPath);
        RequireFixtureName(name);
        File.Delete(backupPath);
    }

    private static void RequireFixtureName(string name)
    {
        const string prefix = "boilerate_test_";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length != prefix.Length + 32 ||
            name[prefix.Length..].Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidOperationException("Native recovery only accepts randomly named fixture databases.");
    }

    private static async Task PostgresAsync(string command, string connectionString, params string[] arguments)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        RequireFixtureName(connection.Database!);
        string? bin = Environment.GetEnvironmentVariable("BOILERATE_POSTGRES_BIN");
        if (string.IsNullOrWhiteSpace(bin) && OperatingSystem.IsWindows())
        {
            string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL", "16", "bin");
            if (Directory.Exists(installed))
                bin = installed;
        }
        string executable = string.IsNullOrWhiteSpace(bin) ? command : Path.Combine(bin, command + (OperatingSystem.IsWindows() ? ".exe" : ""));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        start.Environment["PGPASSWORD"] = connection.Password ?? "";
        start.Environment["PGCONNECT_TIMEOUT"] = "10";
        start.Environment["PGSSLMODE"] = connection.SslMode switch
        {
            SslMode.VerifyCA => "verify-ca",
            SslMode.VerifyFull => "verify-full",
            _ => connection.SslMode.ToString().ToLowerInvariant()
        };
        foreach (string argument in new[] { "--no-password", "--host", connection.Host!, "--port", connection.Port.ToString(CultureInfo.InvariantCulture),
            "--username", connection.Username!, "--dbname", connection.Database! }.Concat(arguments))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the PostgreSQL recovery tool.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(true);
            await process.WaitForExitAsync();
            throw new TimeoutException(command + " exceeded the recovery drill deadline.");
        }
        await Task.WhenAll(output, error); // Drain pipes, but never log connection arguments or native credential diagnostics.
        Assert.True(process.ExitCode == 0, command + " failed with exit code " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
    }
}
