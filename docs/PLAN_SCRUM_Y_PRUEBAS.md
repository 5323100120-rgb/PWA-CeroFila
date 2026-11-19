# Scrum, entregables y pruebas — FilaCero

Plan propuesto en Avance 1 (cinco sprints de una semana). La migración tecnológica a .NET MAUI 11 respeta el alcance funcional, no las tecnologías web de la PWA.

| Sprint | Entrega |
|---|---|
| 1 | Proyecto MAUI/.NET 11, navegación, pre-registro y validación CURP/NSS |
| 2 | Cuestionario de triaje y niveles demostrativos de prioridad |
| 3 | Turnos y QR firmado; pase guardado en SecureStorage para modo avión |
| 4 | WebSockets, cambios de estado, aviso visual y vibración en primer plano |
| 5 | Integración, pruebas funcionales, rendimiento, usabilidad y documentación |

Ramas sugeridas: `main`, `develop`, `feature/nombre` y `hotfix/nombre`. Pull requests revisados por otra persona; versiones `v0.1.0`, `v0.2.0` ... `v1.0.0`. CI básico en `.github/workflows/ci.yml` compila API/dominio y ejecuta comprobaciones de dominio.

| ID del documento | Qué probar | Situación en esta entrega |
|---|---|---|
| UT-TRIAGE-01 | Dolor torácico agudo + 140/90 → `NIVEL_1_URGENCIA`, rojo | Caso escrito en `FilaCero.SmokeTests` (ejecución requiere SDK) |
| IT-SOCKET-02 | Turno A-045 cambia a «Llamado a consultorio 3» <500ms, alerta y vibración | API WS y consumidor MAUI implementados; latencia real **no medida** |
| ST-E2E-03 | Registro NSS → triaje → QR → validación → atendido en PostgreSQL | Servicios y SQL incluidos; flujo hardware/DB **no probado** |
| PT-PERF-04 | FCP <= 1.5s 4G, API <200ms | FCP no es métrica equiparable en app instalada; tiempos API **no medidos** |
| RT-REG-05 | 100% validaciones CURP/NSS | 16 pruebas de dominio escritas; **no ejecutadas** en este entorno |
| ST-LOAD-06 | 2000 peticiones/seg x5min | **No ejecutado**; la API limita tráfico básico con HTTP 429 |
| FT-OFFLINE-07 | Modo avión + recarga, QR legible | SecureStorage implementado; **pendiente comprobar en Android/iOS** |
| UT-USAB-08 | Adultos mayores (10), >90% <2min, SUS >80 | **Pendiente estudio con usuarios reales** |

No se declara ninguna métrica validada en dispositivo o infraestructura. La validación de reglas de triaje debe aprobarla personal clínico autorizado.
