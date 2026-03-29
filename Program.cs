using GlobalConflictMonitor.API.BackgroundJobs;
using GlobalConflictMonitor.Application.Interfaces;
using GlobalConflictMonitor.Application.Services;
using GlobalConflictMonitor.Infrastructure.External;
using GlobalConflictMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddScoped<IEventRepository, EventRepository>();

builder.Services.AddScoped<NormalizationService>();
builder.Services.AddScoped<ThreatLevelService>();
builder.Services.AddScoped<OfwRiskService>();
builder.Services.AddScoped<SituationReportService>();
builder.Services.AddScoped<ConflictEscalationService>();

builder.Services.AddHostedService<EventIngestionWorker>();

builder.Services.AddHttpClient<NewsApiClient>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GlobalConflictMonitorApp/1.0");
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowReact");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
