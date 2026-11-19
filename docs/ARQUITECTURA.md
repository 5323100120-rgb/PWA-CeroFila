# FilaCero .NET 11 — Arquitectura y límites

Basado en **Avance 1 – FilaCero**, documento académico UTS Sonora (10 de noviembre de 2026).

## Estructura

```text
FilaCero.Mobile (.NET MAUI 11 / C# / XAML)
    ├─ Inicio, Registro, Triaje, Mi turno, Ajustes
    ├─ FilaCeroApi : HttpClient + JSON + Bearer
    ├─ QueueStream : ClientWebSocket /ws/queues (primer plano)
    ├─ MainTabsPage : Shell con TabBar inferior (5 secciones)
    └─ AppState : SecureStorage (sesión y QR); ApiConfiguration (URL interna)
             │ REST / HTTPS para producción, WS/WSS
             ▼
FilaCero.Api (ASP.NET Core 11 / Minimal API)
    ├─ Validation/triage desde FilaCero.Domain
    ├─ TokenService (HMAC-SHA256) y QR firmado FC1
    ├─ QueueHub (envíos WebSocket al paciente autenticado)
    └─ IClinicStore
         ├─ MemoryClinicStore (DEMO_MODE=true, volátil)
         └─ PostgresClinicStore (Npgsql; DEMO_MODE=false)
                  ▼
               PostgreSQL
```

## Correspondencia con el documento base

| Documento | Implementación MAUI |
|---|---|
| PWA en navegador | App móvil MAUI instalada (cambio de plataforma solicitado) |
| Pre-registro CURP/NSS + documentación | `RegisterPage` + `RegistrationRules` + API `/api/mobile/register` |
| Triaje y priorización inicial | `TriagePage` + reglas **demostrativas** `DemoTriageRules` |
| Turno virtual QR | `TicketPage` + QRCoder, HMAC servidor; QR local verificable |
| IndexedDB + Service Worker | SecureStorage del sistema + interfaz nativa; sólo último pase offline |
| WebSocket `/ws/queues` | `QueueStream` + `QueueHub`, estado y vibración en primer plano |
| Panel web externo | Endpoints de administración protegidos por clave **de prueba**, no panel clínico |
| PostgreSQL | Modo alternativo Npgsql + `db/01_schema.sql` |

## Protocolo REST

- `GET /api/health` — Diagnóstico de conexión
- `POST /api/mobile/register` — Crear paciente de prueba, devuelve Bearer de 2 h
- `GET /api/me` — Paciente/triage/turno actual
- `POST /api/triage` — Evaluación y guardado (no actualiza tras emitir turno)
- `POST /api/turns` — Turno virtual con QR firmado
- `GET /api/turns/mine` — Último estado del turno
- `GET /api/admin/turns` — Listar turnos (X-Admin-Key)
- `PATCH /api/admin/turns/{guid}` — Cambiar estado/consultorio (X-Admin-Key)
- `POST /api/admin/verify-qr` — Comprobar firma + existencia del pase (X-Admin-Key)
- `WS /ws/queues` — `Sec-WebSocket-Protocol: filacero, fc-session.<token>`

El servidor envía `{"type":"queue.updated","turn":{...},"ts":...}` al paciente correspondiente. La MAUI actualiza el pase almacenado y vibra cuando el estado es `Llamado a consultorio`. No hay avisos push ni operación WebSocket en segundo plano.

## Seguridad y operación

Esta versión **no es para pacientes reales**. El triaje no sustituye consulta médica y requiere reglas aprobadas por clínica. En nivel rojo se muestra aviso urgente y se impide generar un turno normal. La API por defecto tiene secretos temporales e in-memory, y la clave de administración es una contraseña académica. Para producción hace falta gestión de identidades, secretos persistentes, trazabilidad, cifrado y políticas de conservación de documentación, HTTPS/WSS y auditoría. El QR es un identificador firmado, **no contiene CURP, NSS ni nombre**. La interfaz sólo conserva el pase y el token en SecureStorage.

En PostgreSQL la BD conserva identificadores hasheados con pepper, metadatos y archivos; es una base para integración, no cumplimiento automático de normativa. El modo DEMO arranca sin PostgreSQL y pierde pacientes/turnos al reiniciar el servidor.

## Decisiones

- Frontend nativo: C# XAML/code-behind para reducir dependencias y facilitar inspección en Visual Studio Community.
- API C# .NET 11 con almacenamiento intercambiable `IClinicStore`, sin Node.js.
- `FilaCero.Domain` comparte contratos entre app y API.
- Autenticación firmada para prototipo (sin roles institucionales ni refresh de sesión).
- El QR se vuelve a renderizar desde su payload guardado al abrir la app sin internet.

## Rediseño de navegación y conexión (UI final)

- `MainTabsPage : Shell` define **un `Tab` por sección y un `ShellContent` por `Tab`**. No hay pestañas superiores, y `Shell.SetNavBarIsVisible` elimina el encabezado nativo. Se usan iconos SVG compilados como recursos MAUI.
- Las pantallas conservan sus estilos globales en `App.xaml`; no se instancian antes de inicializar el `ResourceDictionary` (corrección BigTitle).
- El color primario se toma de `FilaCero/web/style.css` de la PWA: verde `#0A8479` sobre blanco y tinta `#152943`.
- `SettingsPage` es exclusivamente para estado del servicio, información y privacidad; **sin campo de IP**.
- `ApiConfiguration.ResolveBaseUrl()` decide el endpoint sin intervención del paciente: `10.0.2.2:5071` para el emulador Android en Debug, configuración opcional del equipo para teléfono de prueba y `ProductionApiUrl` al liberar. Instalaciones previas en Debug pueden conservar la antigua URL guardada como compatibilidad sólo si no se resolvió otra.
- El entorno final debe tener una API HTTPS real configurada por el equipo. Sin backend publicado no hay datos online, aunque el QR offline sigue disponible.
