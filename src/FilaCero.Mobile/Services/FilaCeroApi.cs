
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FilaCero.Domain;

namespace FilaCero.Mobile.Services;

public sealed class ApiException(int statusCode, string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class FilaCeroApi(AppState state)
{
    // Tiempo máximo de espera: 30 segundos
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    private async Task<T> RequestAsync<T>(
        HttpMethod method,
        string endpoint,
        object? body = null,
        bool withSession = true,
        CancellationToken ct = default)
    {
        // Validar la dirección del servidor
        if (string.IsNullOrWhiteSpace(state.ServerUrl))
        {
            throw new ApiException(
                0,
                "El servicio de la clínica no está configurado."
            );
        }

        // Evitar errores por barras duplicadas
        string url = state.ServerUrl.TrimEnd('/')
            + "/"
            + endpoint.TrimStart('/');

        using var request = new HttpRequestMessage(method, url);

        // Agregar datos JSON
        if (body is not null)
        {
            request.Content = JsonContent.Create(
                body,
                options: Json
            );
        }

        // Agregar token de sesión
        if (withSession &&
            !string.IsNullOrWhiteSpace(state.SessionToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    state.SessionToken
                );
        }

        try
        {
            // Enviar petición al backend
            using var response = await Http.SendAsync(
                request,
                ct
            );

            // Manejar errores HTTP
            if (!response.IsSuccessStatusCode)
            {
                string message =
                    $"El servidor devolvió el error {(int)response.StatusCode}.";

                try
                {
                    var error =
                        await response.Content
                            .ReadFromJsonAsync<ApiError>(
                                Json,
                                ct
                            );

                    if (!string.IsNullOrWhiteSpace(error?.Error))
                    {
                        message = error.Error;
                    }

                    if (error?.Fields is { Count: > 0 })
                    {
                        message += "\n" +
                            string.Join(
                                "\n",
                                error.Fields.Values
                            );
                    }
                }
                catch (JsonException)
                {
                    // Mantener el mensaje HTTP original
                }

                throw new ApiException(
                    (int)response.StatusCode,
                    message
                );
            }

            // Procesar respuesta exitosa
            try
            {
                var result = await response.Content
                    .ReadFromJsonAsync<T>(Json, ct);

                return result ?? throw new ApiException(
                    0,
                    "El servidor envió una respuesta vacía."
                );
            }
            catch (JsonException)
            {
                throw new ApiException(
                    0,
                    "El servidor devolvió datos que no se pudieron procesar."
                );
            }
        }
        catch (OperationCanceledException)
            when (!ct.IsCancellationRequested)
        {
            throw new ApiException(
                0,
                "El servidor tardó demasiado en responder. " +
                "Verifica tu conexión e inténtalo nuevamente."
            );
        }
        catch (HttpRequestException)
        {
            throw new ApiException(
                0,
                "No fue posible conectarse con el servidor de FilaCero. " +
                "Comprueba que el servicio esté disponible."
            );
        }
    }

    // Comprobar estado de la API
    public Task<HealthDto> HealthAsync(
        CancellationToken ct = default)
    {
        return RequestAsync<HealthDto>(
            HttpMethod.Get,
            "/api/health",
            withSession: false,
            ct: ct
        );
    }

    // Registrar paciente
    public Task<RegisterResponse> RegisterAsync(
        RegistrationRequest body)
    {
        return RequestAsync<RegisterResponse>(
            HttpMethod.Post,
            "/api/mobile/register",
            body,
            withSession: false
        );
    }

    // Consultar paciente
    public Task<MeResponse> GetMeAsync()
    {
        return RequestAsync<MeResponse>(
            HttpMethod.Get,
            "/api/me"
        );
    }

    // Evaluar triaje
    public Task<TriageResponse> EvaluateAsync(
        TriageAnswers answers)
    {
        return RequestAsync<TriageResponse>(
            HttpMethod.Post,
            "/api/triage",
            answers
        );
    }

    // Generar turno
    public Task<TurnResponse> IssueTurnAsync()
    {
        return RequestAsync<TurnResponse>(
            HttpMethod.Post,
            "/api/turns",
            new { }
        );
    }

    // Consultar turno actual
    public Task<TurnResponse> GetTurnAsync()
    {
        return RequestAsync<TurnResponse>(
            HttpMethod.Get,
            "/api/turns/mine"
        );
    }
}
