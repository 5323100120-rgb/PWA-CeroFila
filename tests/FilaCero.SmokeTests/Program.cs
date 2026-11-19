using FilaCero.Domain;

int total = 0;
void Check(bool condition,string name)
{
    total++;
    if(!condition) throw new Exception($"Prueba #{total} no superada: {name}");
    Console.WriteLine($"OK {total:00} - {name}");
}

var valid = RegistrationRules.Normalize(new RegistrationRequest("Paciente de Prueba", "", "12345678901", "", "Clínica Centro"));
Check(RegistrationRules.Validate(valid).Count==0,"NSS de 11 dígitos válido");
Check(RegistrationRules.Validate(valid with { Nss="123" }).ContainsKey("nss"),"NSS corto rechazado");
Check(RegistrationRules.Validate(valid with { Nss="" }).ContainsKey("curp"),"CURP o NSS requerido");
Check(RegistrationRules.Validate(valid with { FullName="Prueba" }).ContainsKey("fullName"),"Nombre y apellido requeridos");
Check(RegistrationRules.Validate(valid with { Phone="123" }).ContainsKey("phone"),"Teléfono inválido rechazado");
Check(RegistrationRules.Validate(valid with { Clinic="Inexistente" }).ContainsKey("clinic"),"Clínica inválida rechazada");
Check(RegistrationRules.Validate(valid with { Nss="", Curp="GODE561231HDFRRN09" }).Count==0,"CURP con estructura válida");
Check(RegistrationRules.Validate(valid with { Nss="", Curp="INVALIDA" }).ContainsKey("curp"),"CURP inválida rechazada");
var baseline = new TriageAnswers(false,false,false,false,false,false,null,null,"");
Check(DemoTriageRules.Evaluate(baseline).Level=="PENDIENTE_VALORACION","Triaje base demostrativo");
Check(DemoTriageRules.Evaluate(baseline with { Fever=true }).Color=="ambar","Fiebre requiere revisión demostrativa");
Check(DemoTriageRules.Evaluate(baseline with { ChestPain=true, Systolic=140, Diastolic=90 }).Level=="NIVEL_1_URGENCIA","UT-TRIAGE-01 dolor torácico -> rojo");
Check(DemoTriageRules.Evaluate(baseline with { BreathingDifficulty=true }).Color=="rojo","Dificultad respiratoria -> alerta");
Check(DemoTriageRules.Evaluate(baseline with { Unconscious=true }).Color=="rojo","Pérdida de conocimiento -> alerta");
try { DemoTriageRules.Evaluate(baseline with { Systolic=500 }); throw new Exception("Fallo: se admitió presión imposible"); }
catch (ArgumentException) { Check(true,"Presión fuera de rango rechazada"); }
Check(QueueStatuses.All.Contains("Llamado a consultorio"),"Estado de consultorio incluido");
Check(ClinicNames.All.Length==3,"Clínicas de demostración");
Console.WriteLine($"{total} comprobaciones completadas. Ninguna equivale a validación clínica.");
