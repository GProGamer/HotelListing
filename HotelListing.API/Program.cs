using Asp.Versioning;
using HotelListing.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using HotelListing.API.Core.Contracts;
using HotelListing.API.Core.Repository;
using HotelListing.API.Core.Configurations;
using HotelListing.API.Core.Middleware;
using HotelListing.API;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("HotelListingDbConnectionString");
builder.Services.AddDbContext<HotelListingDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddIdentityCore<ApiUser>()
    .AddRoles<IdentityRole>()
    .AddTokenProvider<DataProtectorTokenProvider<ApiUser>>("HotelListingApi") //para que sirve ?
    .AddEntityFrameworkStores<HotelListingDbContext>()
    .AddDefaultTokenProviders();//para que sirve ?

//builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
//builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(options => 
{
    //options.SwaggerDoc("v1", new OpenApiInfo 
    //{ 
    //    Title = "Hotel Listing Api", 
    //    Version = "v1-version" 
    //});

    options.OperationFilter<SwaggerDefaultValues>();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Vearer scheme.
                        Enter 'Bearer' [space] and then your token in the text input below.
                        Example: 'Bearer 123sdfdf55df5'",
        Name = "Autorizacion",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = JwtBearerDefaults.AuthenticationScheme //"Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "0auth2",
                Name = JwtBearerDefaults.AuthenticationScheme, //"Bearer"
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod());
});

builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("X-Version"),
        new MediaTypeApiVersionReader("ver")
        );
}).AddMvc().AddApiExplorer(
    options =>
    {
        
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    }
);

builder.Host.UseSerilog((ctx, lr) => lr.WriteTo.Console().ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddAutoMapper(typeof(MapperConfig));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<ICountriesRepository, CountriesRepository>();
builder.Services.AddScoped<IHotelsRepository, HotelsRepository>();
builder.Services.AddScoped<IAuthManager, AuthManager>();


builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; // "Bearer"
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]))
    };
});

builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 1024;
    options.UseCaseSensitivePaths = true;
});


//AspNetCore.HealthChecks.SqlServer
//EF Core - Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
builder.Services.AddHealthChecks()
    .AddCheck<CustomHealthCheck>("Custom health check",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "custom" }
    )
    .AddSqlServer(connectionString, tags: new[] { "database" })
    .AddDbContextCheck<HotelListingDbContext>(tags: new[] { "database" });


builder.Services.AddControllers().AddOData(options =>
{
    options.Select().OrderBy().Filter();
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var descriptions = app.DescribeApiVersions();
        // build a swagger endpoint for each discovered API version
        foreach (var description in descriptions)
        {
            var url = $"/swagger/{description.GroupName}/swagger.json";
            var name = description.GroupName.ToUpperInvariant();
            options.SwaggerEndpoint(url, name);
        }
    });
}

//ejecuta todos los health checks con tag "custom"
app.MapHealthChecks("/healthCheck", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("custom"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,

    },
    ResponseWriter = WriteRespose
});

//ejecuta todos los health checks, dado que le quitamos el "Predicate" que guarda los tags
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,

    },
    ResponseWriter = WriteRespose
});

//ejecuta todos los health checks con tag "database"
app.MapHealthChecks("/databaseHealthCheck", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("database"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,

    },
    ResponseWriter = WriteRespose
});

static Task WriteRespose(HttpContext context, HealthReport healthReport)
{
    context.Response.ContentType = "application/json; chartset = utf-8";

    var options = new JsonWriterOptions { Indented = true };

    using var memoryStream = new MemoryStream();

    using (var jsonWriter = new Utf8JsonWriter(memoryStream, options))
    {
        jsonWriter.WriteStartObject();
        jsonWriter.WriteString("status", healthReport.Status.ToString());
        jsonWriter.WriteStartObject("results");

        foreach (var healthReportEntry in healthReport.Entries)
        {
            jsonWriter.WriteStartObject(healthReportEntry.Key);
            jsonWriter.WriteString("status", healthReportEntry.Value.Status.ToString());
            jsonWriter.WriteString("description", healthReportEntry.Value.Description);
            jsonWriter.WriteStartObject("data");

            foreach (var item in healthReportEntry.Value.Data)
            {
                jsonWriter.WritePropertyName(item.Key);

                JsonSerializer.Serialize(jsonWriter, item.Value, 
                        item.Value.GetType() ?? typeof(object));

            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
        }

        jsonWriter.WriteEndObject();
        jsonWriter.WriteEndObject();
    }

    return context.Response.WriteAsync(
            Encoding.UTF8.GetString(memoryStream.ToArray())
        );
}

app.MapHealthChecks("/health");

app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseResponseCaching();

app.Use(async (context, next) =>
{
    context.Response.GetTypedHeaders().CacheControl =
        new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(10),
        };

    context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] = new string[] { "Accept-Encoding" };

    await next();
});

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();


internal class CustomHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
                                                CancellationToken cancellationToken = default)
    {
        var isHealthy = true;

        /*Custom check, logic etc */

        if (isHealthy)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Todos los sistemas seem to be ok"));
        }

        return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus));
    }
}