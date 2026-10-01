using Microsoft.EntityFrameworkCore;
using TreeEditor.Api.Data;
using TreeEditor.Api.Nodes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<TreeDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Tree"))
    .UseAsyncSeeding((db, _, ct) => SampleData.SeedAsync(db, ct)));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<TreeDbContext>().Database.MigrateAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapNodeEndpoints();
app.MapFallbackToFile("index.html");

app.Run();
