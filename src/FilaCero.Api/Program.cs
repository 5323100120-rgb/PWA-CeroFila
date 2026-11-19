using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FilaCero.Api.Data;
using FilaCero.Api.Realtime;
using FilaCero.Api.Security;
using FilaCero.Domain;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var demo = !string.Equals(builder.Configuration["DEMO_MODE"], "false", StringComparison.OrdinalIgnoreCase);
var tokenSecret = builder.Configuration["SESSION_SECRET"] ?? (demo ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) : "");
if (tokenSecret.Length < 32) throw new InvalidOperationException("Configura SESSION_SECRET con 32+ caracteres.");
builder.Services.AddSingleton(new TokenService(tokenSecret));
builder.Services.AddSingleton<QueueHub>();

if (demo)
    builder.Services.AddSingleton<IClinicStore, MemoryClinicStore>();
else
{
    var cs = builder.Configuration["DATABASE_URL"];
    if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("Configura DATABASE_URL para PostgreSQL.");
    if (string.IsNullOrWhiteSpace(builder.Configuration["FINGERPRINT_PEPPER"]) || builder.Configuration["FINGERPRINT_PEPPER"]!.Length < 24)
        throw new InvalidOperationException("Configura FINGERPRINT_PEPPER robusto.");
    builder.Services.AddSingleton(NpgsqlDataSource.Create(cs));
    builder.Services.AddSingleton<IClinicStore, PostgresClinicStore>();
}

builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "no-ip", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseRateLimiter();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });

var adminKey = builder.Configuration["ADMIN_DEMO_KEY"] ?? (demo ? "demo-fila-2026" : "");
if (adminKey.Length < 8) app.Logger.LogWarning("La API de administración está desactivada; establece ADMIN_DEMO_KEY.");
if (demo) app.Logger.LogWarning("DEMOSTRACIÓN: memoria volátil, sin validación clínica ni seguridad de producción.");

static IResult Error(int status, string text, Dictionary<string,string>? fields = null)
    => Results.Json(new ApiError(text, fields), statusCode: status);
static Guid? PatientId(HttpContext ctx, TokenService tokens)
{
    var bearer = ctx.Request.Headers.Authorization.ToString();
    return bearer.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase) ? tokens.Validate(bearer[7..].Trim()) : null;
}
static bool AdminValid(HttpContext ctx, string expected)
{
    if (string.IsNullOrEmpty(expected)) return false;
    var a = Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Admin-Key"].ToString());
    var b = Encoding.UTF8.GetBytes(expected);
    return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
}

app.MapGet("/api/health", () => Results.Ok(new HealthDto(true,demo?"demo":"postgresql","1.0.0")));
app.MapGet("/api/meta", () => Results.Ok(new { app="FilaCero", version="1.0.0", clinicalRulesValidated=false, demo }));

app.MapPost("/api/mobile/register", async (RegistrationRequest input, IClinicStore store, TokenService tokens) =>
{
    var data = RegistrationRules.Normalize(input);
    var issues = RegistrationRules.Validate(data);
    if (issues.Count > 0) return Error(422, "Revisa los campos del formulario.",issues);
    var patient = await store.RegisterAsync(data);
    return Results.Json(new RegisterResponse(patient,tokens.Issue(patient.Id),7200), statusCode: 201);
});

app.MapGet("/api/me", async (HttpContext ctx, IClinicStore store, TokenService tokens) =>
{
    var id = PatientId(ctx,tokens);
    if (id is null) return Results.Ok(new MeResponse(null,null,null));
    var patient = await store.GetPatientAsync(id.Value);
    if (patient is null) return Results.Ok(new MeResponse(null,null,null));
    return Results.Ok(new MeResponse(patient,await store.GetTriageAsync(id.Value),await store.GetTurnAsync(id.Value)));
});

app.MapPost("/api/triage", async (HttpContext ctx,TriageAnswers input,IClinicStore store,TokenService tokens) =>
{
    var patient = PatientId(ctx,tokens);
    if (patient is null || await store.GetPatientAsync(patient.Value) is null) return Error(401,"Primero registra al paciente.");
    if (await store.GetTurnAsync(patient.Value) is not null) return Error(409,"Ya hay un turno; el triaje está cerrado.");
    try
    {
        var result = DemoTriageRules.Evaluate(input);
        await store.SaveTriageAsync(patient.Value,result);
        return Results.Ok(new TriageResponse(result));
    }
    catch (ArgumentException ex) { return Error(422,ex.Message); }
});

app.MapPost("/api/turns", async (HttpContext ctx,IClinicStore store,TokenService tokens) =>
{
    var patient = PatientId(ctx,tokens);
    if (patient is null || await store.GetPatientAsync(patient.Value) is null) return Error(401,"Primero registra al paciente.");
    try
    {
        var turn = await store.IssueTurnAsync(patient.Value);
        return Results.Json(new TurnResponse(turn),statusCode:201);
    }
    catch (InvalidOperationException ex) { return Error(409,ex.Message); }
});

app.MapGet("/api/turns/mine", async (HttpContext ctx,IClinicStore store,TokenService tokens) =>
{
    var patient = PatientId(ctx,tokens);
    return patient is null ? Error(401,"Sesión caducada.") : Results.Ok(new TurnResponse(await store.GetTurnAsync(patient.Value)));
});

app.MapGet("/api/admin/turns", async (HttpContext ctx,IClinicStore store) =>
    !AdminValid(ctx,adminKey) ? Error(401,"Clave de administración incorrecta.") : Results.Ok(new { turns = await store.ListTurnsAsync() }));

app.MapPost("/api/admin/verify-qr", async (HttpContext ctx,QrCheck input,IClinicStore store,TokenService tokens) =>
{
    if(!AdminValid(ctx,adminKey)) return Error(401,"Clave de administración incorrecta.");
    if(!tokens.VerifyQr(input.QrPayload)) return Error(422,"Código QR inválido.");
    var parts = input.QrPayload.Split('|');
    var ticket = await store.GetTurnByCodeAsync(parts[1]);
    if (ticket is null || !ConstantEquals(ticket.QrPayload,input.QrPayload)) return Error(404,"Turno no encontrado.");
    return Results.Ok(new { valid = true, turn = ticket });
});

app.MapPatch("/api/admin/turns/{turnId:guid}", async (HttpContext ctx,Guid turnId,StatusUpdate input,IClinicStore store,QueueHub hub) =>
{
    if(!AdminValid(ctx,adminKey)) return Error(401,"Clave de administración incorrecta.");
    if(!QueueStatuses.All.Contains(input.Status)) return Error(422,"Estado no permitido.");
    var room = input.Room?.Trim();
    if(!string.IsNullOrEmpty(room) && !Regex.IsMatch(room,@"^Consultorio [1-9]\d?$")) return Error(422,"Usa Consultorio 3.");
    if(input.Status==QueueStatuses.Called && string.IsNullOrEmpty(room)) return Error(422,"Indica el consultorio.");
    var owner=await store.GetTurnOwnerAsync(turnId);
    if(owner is null) return Error(404,"Turno no encontrado.");
    var ticket=await store.UpdateTurnAsync(turnId,input.Status,room);
    if(ticket is null) return Error(404,"Turno no encontrado.");
    await hub.BroadcastAsync(owner.Value,ticket);
    return Results.Ok(new TurnResponse(ticket));
});

app.Map("/ws/queues", async (HttpContext ctx,TokenService tokens,IClinicStore store,QueueHub hub) =>
{
    if(!ctx.WebSockets.IsWebSocketRequest) {ctx.Response.StatusCode=400;return;}
    var protocols=ctx.Request.Headers["Sec-WebSocket-Protocol"].ToString().Split(',',StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    var token=protocols.FirstOrDefault(x=>x.StartsWith("fc-session.",StringComparison.Ordinal))?["fc-session.".Length..];
    var id=tokens.Validate(token);
    if(id is null || !protocols.Contains("filacero") || await store.GetPatientAsync(id.Value) is null)
    {ctx.Response.StatusCode=401;return;}
    using var socket=await ctx.WebSockets.AcceptWebSocketAsync("filacero");
    await hub.ListenAsync(id.Value,socket,ctx.RequestAborted);
});

app.MapGet("/", () => Results.Text("FilaCero API .NET 11 • /api/health • Solo demostración académica."));
app.Run();

static bool ConstantEquals(string x,string y)
{
    var a=Encoding.UTF8.GetBytes(x);var b=Encoding.UTF8.GetBytes(y);
    return a.Length==b.Length && CryptographicOperations.FixedTimeEquals(a,b);
}
public sealed record QrCheck(string QrPayload);
public sealed record StatusUpdate(string Status,string? Room);
