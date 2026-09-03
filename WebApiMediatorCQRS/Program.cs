using AutoMapper;
using FluentValidation;
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

// OutputCache
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(10)));
    options.AddPolicy("Expire20", builder => builder.Expire(TimeSpan.FromSeconds(20)));
    options.AddPolicy("Expire30", builder => builder.Expire(TimeSpan.FromSeconds(30)));
});

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
    //cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
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
app.UseOutputCache();
app.UseAuthorization();
app.MapEndpoints();
app.MapControllers();

app.Run();
