using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Reprise;
using System.Threading.RateLimiting;
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

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too Many Requests")
            .ExecuteAsync(context.HttpContext);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: string.Concat(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                ":",
                httpContext.GetEndpoint()?.DisplayName ?? httpContext.Request.Path.Value ?? "/"
            ),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }
        )
    );
});

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

app.UseRateLimiter();

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

public partial class Program;
