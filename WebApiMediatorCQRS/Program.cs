using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Reprise;
using WebApiMediatorCQRS.Behaviors;
using WebApiMediatorCQRS.Database;
using WebApiMediatorCQRS.Handlers;
using WebApiMediatorCQRS.Profiles;

var domainAssembly = typeof(Program).Assembly;
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Query caching
builder.Services.AddHybridCache();
builder.Services.Configure<HybridCacheOptions>(builder.Configuration.GetSection("HybridCache"));
builder.Services.AddSingleton<CacheInvalidationState>();
builder.Services.AddScoped<CacheExecutionContext>();

// Entity Framework Core
builder.AddSqlServerDbContext<NorthwindContext>(
    "NorthwindDB",
    static settings => settings.CommandTimeout = 15
);

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(domainAssembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    //cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(CacheInvalidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
});

// FluentValidation
builder.Services.AddValidatorsFromAssembly(domainAssembly);

// AutoMapper
builder.Services.AddAutoMapper(cfg => {
    cfg.AddMaps(domainAssembly);
});

// Reprise
builder.ConfigureServices();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseExceptionHandler();
//app.UseExceptionHandler(exceptionHandlerApp =>
//    exceptionHandlerApp.Run(async context => await Results.Problem().ExecuteAsync(context))
//);


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapEndpoints();
app.MapControllers();

app.Run();
