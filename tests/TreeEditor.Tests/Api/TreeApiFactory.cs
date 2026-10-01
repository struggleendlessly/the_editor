using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace TreeEditor.Tests.Api;

public sealed class TreeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string? ExternalDatabase = Environment.GetEnvironmentVariable("TREE_TEST_DB");

    private readonly PostgreSqlContainer? _container =
        ExternalDatabase is null ? new PostgreSqlBuilder("postgres:18-alpine").Build() : null;

    public async ValueTask InitializeAsync()
    {
        if (_container is not null)
            await _container.StartAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Tree", ExternalDatabase ?? _container!.GetConnectionString());
}
