# FilaCero MAUI 11 — Correcciones finales de diseño

## 1. Navegación de arriba hacia abajo

Antes: `MainTabsPage : TabbedPage`, pestañas superiores de Android.

Ahora: `MainTabsPage : Shell` con un `TabBar` de 5 iconos en la parte **inferior** y `Shell.SetNavBarIsVisible(..., false)` para quitar el encabezado nativo superior. Los botones en las pantallas siguen funcionando mediante `Shell.Current`.

## 2. Ajustes para usuarios finales

Se eliminaron la casilla URL/IP, Guardar dirección, guía de `ipconfig` y texto WebSocket de la interfaz. Ahora muestra **Estado del servicio**, **Mis datos en este dispositivo** y **Acerca de FilaCero**.

El paciente no debe escribir direcciones. La API se selecciona en `src/FilaCero.Mobile/Services/ApiConfiguration.cs`:

| Entorno | Resolución |
|---|---|
| Android Emulator (Debug) | `http://10.0.2.2:5071` automática |
| Simulador iOS (Debug, API en Mac) | `http://localhost:5071` automática |
| Android/iPhone físico de prueba | El equipo define `DevelopmentPhoneApiUrl` en Debug o usa backend HTTPS |
| Producción | El equipo publica la API y define `ProductionApiUrl` con HTTPS |

**No hay un servidor público preconfigurado**. La app no puede inventar la dirección de la computadora para un teléfono real. Un QR previamente guardado sigue estando disponible sin internet.

## 3. Colores iguales a PWA

| PWA CSS | MAUI App.xaml | Color |
|---|---|---|
| `--ink` | `Ink` | `#152943` |
| `--muted` | `Muted` | `#68798B` |
| `--teal` | `Teal` | `#0A8479` |
| `--deep` | `Deep` | `#006C64` |
| `--mint` | `Mint` | `#DFF8EF` |
| `--line` | `Line` | `#E8EDF1` |
| `--bg` | `Canvas` | `#F6F8FB` |
| `--red` | `Danger` | `#BF394B` |

El encabezado hero adopta el mismo degradado verde de la PWA.

## 4. Cómo actualizar en Visual Studio

1. Haz una copia de tu proyecto anterior y descomprime este ZIP en otra carpeta.
2. Abre `FilaCero.sln` en Visual Studio Community 2026.
3. Inicia `INICIAR_API_WINDOWS.bat` y comprueba `http://localhost:5071/api/health`.
4. Haz clic derecho en `FilaCero.Mobile` → **Establecer como proyecto de inicio**.
5. **Compilar → Limpiar solución** y luego **Recompilar solución**.
6. Elige un emulador Android y pulsa F5. Ve a Ajustes → **Comprobar servicio**. No necesitas registrar ninguna IP.

No necesitas migrar la base de datos: las APIs, modelos y contratos se mantienen intactos.

**Validación disponible aquí:** revisión estática de XAML/SVG y rutas de recursos. La compilación MAUI requiere Visual Studio/.NET 11 y un dispositivo o emulador Android; este entorno no los incluye.
