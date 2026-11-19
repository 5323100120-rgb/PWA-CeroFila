
using Microsoft.Maui.Devices;

namespace FilaCero.Mobile.Services;

/// <summary>
/// Configuración de conexión de FilaCero.
/// La dirección del servidor se configura internamente.
/// El paciente no necesita introducir una IP.
/// </summary>
public static class ApiConfiguration
{
    // Servidor de producción.
    // Configurar cuando la API esté publicada con HTTPS.
    public const string ProductionApiUrl = "";

#if DEBUG

    // Teléfono Android físico conectado por USB.
    // Requiere ejecutar:
    // adb reverse tcp:5071 tcp:5071
    public const string DevelopmentPhoneApiUrl =
        "http://127.0.0.1:5071";

    // Emulador Android de Visual Studio.
    private const string EmulatorApiUrl =
        "http://10.0.2.2:5071";

    // Simulador iOS.
    private const string IosSimulatorApiUrl =
        "http://localhost:5071";

#endif

    public static string ResolveBaseUrl()
    {
#if DEBUG

        // ANDROID
        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        {
            // Emulador Android
            if (DeviceInfo.Current.DeviceType == DeviceType.Virtual)
            {
                return Validate(EmulatorApiUrl);
            }

            // Teléfono Android físico conectado por USB
            return Validate(DevelopmentPhoneApiUrl);
        }

        // IOS
        if (DeviceInfo.Current.Platform == DevicePlatform.iOS)
        {
            // Simulador iOS
            if (DeviceInfo.Current.DeviceType == DeviceType.Virtual)
            {
                return Validate(IosSimulatorApiUrl);
            }

            // iPhone físico en desarrollo:
            // utilizar una URL de servidor accesible.
            return string.IsNullOrWhiteSpace(ProductionApiUrl)
                ? string.Empty
                : Validate(ProductionApiUrl);
        }

#endif

        // PRODUCCIÓN
        return string.IsNullOrWhiteSpace(ProductionApiUrl)
            ? string.Empty
            : Validate(ProductionApiUrl);
    }

    private static string Validate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                "No se ha configurado el servidor de FilaCero."
            );
        }

        string cleanUrl = url.Trim().TrimEnd('/');

        if (!Uri.TryCreate(
                cleanUrl,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(
                uri.AbsolutePath.Trim('/')) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "La dirección de la API de FilaCero no es válida."
            );
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
