using HackerNewsBestStories.Api.Infrastructure;
using HackerNewsBestStories.Api.Options;
using HackerNewsBestStories.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOptions();
builder.Services.Configure<HackerNewsOptions>(builder.Configuration.GetSection(HackerNewsOptions.SectionName));

var hackerNewsOptions = builder.Configuration.GetSection(HackerNewsOptions.SectionName).Get<HackerNewsOptions>() ?? new HackerNewsOptions();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<KeyedLock>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Add the Hacker News client services.
builder.Services
.AddHttpClient<IHackerNewsClient, HackerNewsClient>(httpClient =>
{
    httpClient.BaseAddress = new Uri(hackerNewsOptions.BaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(hackerNewsOptions.HttpTimeoutSeconds);
})
// Add sensible retyr, timeout and circuit breaker behaviour out of the box. If Hacker News starts erroring or slows down, this stops us from making things worse by hammering it with retries, and stops one slow upstream call from tying up a thread pool thread.
.AddStandardResilienceHandler();

builder.Services.AddScoped<IHackerNewsService, HackerNewsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    // Anything that reaches here means Hacker News itself is unavailable (timeouts, circuit open, DNS trouble, etc). We turn that into a plain, hones 502 instead of a raw 500 with a stack trace, and let API callers decide whether to retry.
    context.Response.StatusCode = StatusCodes.Status502BadGateway;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { error = "Hacker News is unavailable. Please try again shortly." });
}));

app.MapControllers();
app.Run();

// Exposed so WebApplicationFactory<Program> can spin the app up in integration tests.
public partial class Program;

/*
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
*/
