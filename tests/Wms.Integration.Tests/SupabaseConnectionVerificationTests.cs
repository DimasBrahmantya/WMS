using FluentAssertions;
using Npgsql;
using Xunit;

namespace Wms.Integration.Tests;

public class SupabaseConnectionVerificationTests
{
    [Fact]
    public async Task VerifySupabaseConnection_DefaultPostgresDatabase()
    {
        var connStr = "Host=db.nurxlhkrixnqvnocqbop.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=hematpangkalkaya;SSL Mode=Require;Trust Server Certificate=true;";

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT current_database(), version();", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var dbName = reader.GetString(0);
        var version = reader.GetString(1);

        dbName.Should().Be("postgres");
        version.Should().Contain("PostgreSQL");
    }
}
