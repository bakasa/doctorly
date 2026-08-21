using Doctorly.Api.Endpoints;
using Doctorly.Api.Middleware;
using Doctorly.Application.Abstractions;
using Doctorly.Application.Events;
using Doctorly.Application.Notifications;
using Doctorly.Infrastructure.Notifications;
using Doctorly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connectionString = builder.Configuration.GetConnectionString("Doctorly")
    ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Doctorly' configuration.");

builder.Services.AddDbContext<DoctorlyDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<INotificationService, ConsoleNotificationService>();
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<EventsAppService>();

builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DoctorlyDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapEventsEndpoints();
app.MapAttendeesEndpoints();

app.Run();

public partial class Program;
