using GuessTheLyrics.API;
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
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => options.RouteTemplate = "{documentName}/specifications.json");
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "api";
        options.SwaggerEndpoint("/v1/specifications.json", "GuessTheLyrics API");
    });
}

app.UseForwardedHeaders();
app.UseCors(GuessTheLyrics.API.Dependencies.FrontendCorsPolicy);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseExceptionHandler();
app.MapControllers();

app.Run();
