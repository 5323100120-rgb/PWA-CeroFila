# Ejecútalo DESPUÉS de iniciar la API con INICIAR_API_WINDOWS.bat.
# Todos los registros son ficticios y desaparecen al reiniciar en modo demo.
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5071'
$registro = @{
  fullName = 'Paciente de Prueba'
  curp = ''
  nss = '12345678901'
  phone = ''
  clinic = 'Clínica Centro'
} | ConvertTo-Json
$r = Invoke-RestMethod "$base/api/mobile/register" -Method Post -ContentType 'application/json' -Body $registro
$auth = @{ Authorization = "Bearer $($r.sessionToken)" }
Write-Host "Registro creado: $($r.patient.fullName)" -ForegroundColor Green
$answers = @{
  chestPain = $false; breathingDifficulty = $false; unconscious = $false
  fever = $false; severePain = $false; dizziness = $false
  systolic = $null; diastolic = $null; description = 'Prueba automatizada'
} | ConvertTo-Json
$t = Invoke-RestMethod "$base/api/triage" -Headers $auth -Method Post -ContentType 'application/json' -Body $answers
Write-Host "Triaje: $($t.triage.level)" -ForegroundColor Green
$p = Invoke-RestMethod "$base/api/turns" -Headers $auth -Method Post -ContentType 'application/json' -Body '{}'
Write-Host "Turno: $($p.turn.code)" -ForegroundColor Green
$admin = @{ 'X-Admin-Key' = 'demo-fila-2026' }
$checked = Invoke-RestMethod "$base/api/admin/verify-qr" -Headers $admin -Method Post -ContentType 'application/json' -Body (@{qrPayload=$p.turn.qrPayload} | ConvertTo-Json)
if (-not $checked.valid) { throw 'Fallo en verificación QR' }
Write-Host 'Firma QR verificada.' -ForegroundColor Green
$change = @{ status = 'Llamado a consultorio'; room = 'Consultorio 3' } | ConvertTo-Json
Invoke-RestMethod "$base/api/admin/turns/$($p.turn.id)" -Method Patch -Headers $admin -ContentType 'application/json' -Body $change | Out-Null
$current = Invoke-RestMethod "$base/api/turns/mine" -Headers $auth
if ($current.turn.status -ne 'Llamado a consultorio' -or $current.turn.room -ne 'Consultorio 3') { throw 'No se actualizó el turno' }
Write-Host 'Flujo REST completo: registro -> triaje -> turno -> QR -> llamado. OK' -ForegroundColor Green
