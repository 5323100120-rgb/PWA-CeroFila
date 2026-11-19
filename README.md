# FilaCero — .NET MAUI 11 | Rediseño final de la app móvil

**Proyecto académico de aplicación móvil, basado en «Avance 1 - FilaCero».**

### Cambios de interfaz de esta versión

- **Navegación inferior real con MAUI Shell:** Inicio, Registro, Triaje, Mi turno y Ajustes, con iconos. Se elimina la barra de pestañas superior de `TabbedPage` y se oculta el encabezado nativo superior.
- **Sin IP en Ajustes:** el paciente nunca escribe direcciones. La conexión se resuelve dentro de `src/FilaCero.Mobile/Services/ApiConfiguration.cs` según el entorno.
- **Mismos colores de la PWA original:** tinta `#152943`, verde `#0A8479`, verde oscuro `#006C64`, menta `#DFF8EF`, fondo `#F6F8FB` y borde `#E8EDF1`. Hero degradado verde como la web.
- Se mantienen registro, triaje, pase QR sin conexión, WebSocket y backend ASP.NET Core.

**Atención:** no se ha publicado una API pública. En Android Emulator, una compilación Debug se conecta automáticamente a la API local `http://10.0.2.2:5071`; en un teléfono físico nuevo, un integrante del equipo debe configurar la dirección de desarrollo o un backend HTTPS antes de generar la app. Los pacientes no tienen que hacer ese paso.

La solución incluye app Android/iOS en C# y XAML, API ASP.NET Core 11 en C#, reglas compartidas, PostgreSQL opcional, WebSocket, QR firmado, almacenamiento local y comprobaciones de dominio.

> **PROTOTIPO, NO APTO PARA ATENCIÓN MÉDICA REAL.** No usar datos de pacientes reales. El triaje es una demostración y nunca reemplaza a un profesional. Los avisos y la vibración funcionan sólo con la app abierta.

## Requisitos

1. Windows 11 y **Visual Studio Community 2026 actualizado**, con la carga «Desarrollo de la interfaz de usuario de aplicaciones multiplataforma de .NET (.NET MAUI)» y Android SDK/emulador.
2. **SDK .NET 11** (11.0.100 o versión compatible posterior) y cargas MAUI/Android. Puedes comprobar con `dotnet --info` y `dotnet workload list`. .NET 11 y MAUI 11 se publicaron el 10 de noviembre de 2026.
3. Android físico con depuración USB o emulador Android. Para compilar/desplegar iOS necesitas un Mac con Xcode compatible y un entorno de firma Apple.
4. Para el modo opcional PostgreSQL: Docker Desktop o una instancia PostgreSQL externa.

## Arranque rápido sin Node.js ni PostgreSQL

**Paso A: Abrir el proyecto.** Descomprime el ZIP y abre `FilaCero.sln` en Visual Studio Community 2026. Espera que Visual Studio restaure paquetes NuGet y workloads.

**Paso B: Encender la API.** En la raíz ejecuta `INICIAR_API_WINDOWS.bat` o abre PowerShell:

```powershell
dotnet run --project src/FilaCero.Api/FilaCero.Api.csproj --launch-profile FilaCero.Api
```

Verifica en un navegador `http://localhost:5071/api/health`. Debe devolver `{ "ok": true, "mode": "demo", ... }`.

**Paso C: Ejecutar la app.** En Visual Studio elige `FilaCero.Mobile` como proyecto de inicio, selecciona tu Android Emulator o dispositivo y presiona **F5**. La app es **MAUI nativa**, no Expo.

**Paso D: Conexión automática.** No tienes que escribir una IP dentro de la app. En **Android Emulator (Debug)** la app encuentra la API de tu computadora en `http://10.0.2.2:5071` si ejecutaste primero `INICIAR_API_WINDOWS.bat`. Abre **Ajustes → Comprobar servicio** para verificar la disponibilidad.

**Para probar desde un Android físico nuevo:** como el teléfono no puede resolver automáticamente la IP del ordenador, el **desarrollador** debe abrir `src/FilaCero.Mobile/Services/ApiConfiguration.cs` y definir `DevelopmentPhoneApiUrl` (sólo compilación Debug). La computadora y el teléfono deben estar en la misma red y el firewall debe permitir el puerto 5071. El paciente no ve ni edita esta dirección.

**Para la distribución final:** publica la API con HTTPS y configura el dominio real en `ProductionApiUrl` en ese mismo archivo. Esta versión no incluye un servicio público y, sin una dirección configurada, el QR guardado seguirá siendo visible, pero el registro y el seguimiento no funcionarán. No uses HTTP ni claves de prueba en producción.

**Paso E: Flujo de uso.** Inicio → Registro (CURP o NSS, nombres ficticios) → Triaje → Mi turno → generar QR. La ficha se conserva en SecureStorage aunque cierres la app y pierdas conexión. Para una alerta de prueba usa el endpoint administrativo descrito abajo.

## Cambiar estado de un turno (API demo)

El panel web no forma parte de este entregable móvil; el personal clínico y su panel son un componente externo según el documento. Puedes simular el estado desde PowerShell. Busca primero el `id` del turno:

```powershell
$headers = @{ 'X-Admin-Key' = 'demo-fila-2026' }
$turns = Invoke-RestMethod 'http://localhost:5071/api/admin/turns' -Headers $headers
$turns.turns | Format-Table id,code,status
```

Para marcarlo como llamado:

```powershell
$id = $turns.turns[0].id
$body = @{ status = 'Llamado a consultorio'; room = 'Consultorio 3' } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5071/api/admin/turns/$id" -Method Patch -Headers $headers -Body $body -ContentType 'application/json'
```

La app abierta debería recibir `queue.updated` por WebSocket, mostrar el consultorio y vibrar; requiere prueba en dispositivo.

## PostgreSQL (opcional)

```powershell
docker compose up -d
$env:DEMO_MODE='false'
$env:DATABASE_URL='Host=localhost;Port=5433;Database=filacero;Username=filacero;Password=cambiar_esta_clave_demo'
$env:SESSION_SECRET='CAMBIA_ESTA_CADENA_LARGA_32_CARACTERES_MINIMO_2026'
$env:FINGERPRINT_PEPPER='CAMBIA_ESTE_PEPPER_LARGO_Y_DISTINTO_DE_LA_CLAVE_2026'
$env:ADMIN_DEMO_KEY='CAMBIA_TAMBIEN_LA_CLAVE_DE_ADMIN_DE_PRUEBAS'
dotnet run --project src/FilaCero.Api/FilaCero.Api.csproj --no-launch-profile --urls http://0.0.0.0:5071
```

`db/01_schema.sql` se ejecuta al inicializar por primera vez el contenedor PostgreSQL. Si había un volumen previo, ejecuta la migración manualmente. Estas credenciales son **sólo ejemplo académico**.

## Pruebas y compilación

```powershell
dotnet run --project tests/FilaCero.SmokeTests/FilaCero.SmokeTests.csproj
dotnet build src/FilaCero.Api/FilaCero.Api.csproj
dotnet build src/FilaCero.Mobile/FilaCero.Mobile.csproj -f net11.0-android
```

Para generar un **APK de prueba** en Windows, primero instala y compila con Visual Studio/Android SDK. Después puedes intentar:

```powershell
dotnet publish src/FilaCero.Mobile/FilaCero.Mobile.csproj -c Release -f net11.0-android -p:AndroidPackageFormat=apk
```

La firma para distribución/Play Store requiere configuración adicional.

**Importante sobre validación:** este paquete se creó en un entorno SIN SDK .NET ni workloads de MAUI, por lo que **no fue posible compilar el código ni ejecutar la app aquí**. Se revisó la estructura, archivos XML/JSON y los contratos estáticamente, pero debes restaurar NuGet, compilar y validar en Visual Studio Community 2026. No incluye `.apk` precompilado.

## Estructura

```
FilaCero.sln
src/
  FilaCero.Mobile/      # MAUI 11 C# XAML: Shell inferior, 5 secciones, QR y modo offline
  FilaCero.Api/         # ASP.NET Core 11: REST, WebSocket, demo, PostgreSQL
  FilaCero.Domain/      # Modelos, validaciones CURP/NSS, triaje demostrativo
 tests/FilaCero.SmokeTests/  # 16 comprobaciones de dominio
 db/01_schema.sql
 docs/ARQUITECTURA.md
 docs/PLAN_SCRUM_Y_PRUEBAS.md
 .github/workflows/ci.yml
 INICIAR_API_WINDOWS.bat
```

Las pantallas son nativas y pueden visualizarse/modificarse desde Visual Studio. La PWA original usaba Service Workers e IndexedDB; su equivalente solicitado en MAUI es interfaz instalada y SecureStorage para conservar el pase. La sincronización y los turnos nuevos **requieren internet**.

Para hacer una **prueba REST de extremo a extremo** después de arrancar la API:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\PRUEBA_FLUJO_API.ps1
```

El script crea un paciente ficticio, registra triaje, genera y verifica el QR y cambia el turno a `Llamado a consultorio 3`. **No sustituye** las pruebas nativas en el teléfono.
