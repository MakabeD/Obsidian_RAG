using configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(o =>
    {
        o.IncludeScopes = true;
        o.SingleLine = true;
    });
}
else
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        o.UseUtcTimestamp = true;
    });
}

IConfigurationSection ragSection = builder.Configuration.GetSection(RagOptions.SectionName);

builder.Services
    .AddOptions<RagOptions>()
    .Bind(ragSection)
    .ValidateDataAnnotations()
    .ValidateOnStart();

RagOptions rag = new();
ragSection.Bind(rag);

builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<IEmbedder>(sp => sp.GetRequiredService<EmbeddingService>());
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddHostedService<SessionSweeper>();

builder.Services.AddHttpClient<ChromaService>(c =>
{
    c.BaseAddress = new Uri(rag.ChromaBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(rag.ChromaTimeoutSeconds);
});
builder.Services.AddSingleton<IChromaService>(sp => sp.GetRequiredService<ChromaService>());

builder.Services.AddHealthChecks()
    .AddCheck<ChromaHealthCheck>("chroma", tags: new[] { "ready" })
    .AddCheck<OnnxModelHealthCheck>("onnx_model", tags: new[] { "ready" });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = rag.MaxUploadBytes;
});
builder.Services.Configure<KestrelServerOptions>(o =>
{
    o.Limits.MaxRequestBodySize = rag.MaxUploadBytes;
});

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapGet("/siu", () => Results.Ok(new { status = "ok" }));

app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteJson
});

app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJson
});

app.MapSessionEndpoints();

app.Run();

public partial class Program;
