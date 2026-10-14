using GuessTheLyrics.API;
using GuessTheLyrics.API.Handlers;
using GuessTheLyrics.Application;
using GuessTheLyrics.Infrastructure;
using GuessTheLyrics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .RegisterApplication()
    .RegisterInfrastructure(builder.Configuration)
    .RegisterAPI(builder.Configuration);

var app = builder.Build();

// Single-instance app: apply migrations at startup.
using (var scope = app.Services.CreateScope())
{
    try
    {
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex,
            "Could not apply database migrations. Is SQL Server running and is ConnectionStrings:DefaultConnection correct? " +
            "(Development uses LocalDB: check with 'sqllocaldb info'.)");
        throw;
    }
}

// Error handling first, so it also covers failures in the middleware below.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseApiStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => options.RouteTemplate = "{documentName}/specifications.json");
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "api";
        options.SwaggerEndpoint("/v1/specifications.json", "GuessTheLyrics API");
    });
}

app.UseCors(GuessTheLyrics.API.Dependencies.FrontendCorsPolicy);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
