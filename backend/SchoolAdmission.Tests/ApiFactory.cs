using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

public class ApiFactory : WebApplicationFactory<Program>
{
    public int HoldSeconds { get; init; } = 600;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var cs = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
                 ?? "Host=localhost;Port=5432;Database=school_admission_test;Username=postgres;Password=postgres";
        builder.UseSetting("ConnectionStrings:Default", cs);
        builder.UseSetting("HOLD_DURATION_SECONDS", HoldSeconds.ToString());
    }
}
