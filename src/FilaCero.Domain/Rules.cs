using System.Text.RegularExpressions;

namespace FilaCero.Domain;

// Mismas validaciones CURP/NSS del prototipo móvil. La regex valida estructura, no identidad real.
public static partial class RegistrationRules
{
    [GeneratedRegex(@"^[A-Z][AEIOUX][A-Z]{2}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[HM](AS|BC|BS|CC|CL|CM|CS|CH|DF|DG|GT|GR|HG|JC|MC|MN|MS|NT|NL|OC|PL|QT|QR|SP|SL|SR|TC|TS|TL|VZ|YN|ZS|NE)[B-DF-HJ-NP-TV-Z]{3}[A-Z\d]\d$", RegexOptions.CultureInvariant)]
    private static partial Regex CurpPattern();

    [GeneratedRegex(@"^[\p{L} '.\-]{5,110}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    public static RegistrationRequest Normalize(RegistrationRequest request) => request with
    {
        FullName = Regex.Replace(request.FullName?.Trim() ?? "", @"\s+", " "),
        Curp = (request.Curp ?? "").Trim().ToUpperInvariant(),
        Nss = (request.Nss ?? "").Trim(),
        Phone = (request.Phone ?? "").Trim(),
        Clinic = (request.Clinic ?? "").Trim()
    };

    public static Dictionary<string,string> Validate(RegistrationRequest r)
    {
        var problems = new Dictionary<string,string>();
        if (!NamePattern().IsMatch(r.FullName) || r.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2)
            problems["fullName"] = "Escribe nombre y apellido válidos.";
        if (string.IsNullOrEmpty(r.Curp) && string.IsNullOrEmpty(r.Nss))
            problems["curp"] = "Indica CURP o NSS.";
        if (!string.IsNullOrEmpty(r.Curp) && !CurpPattern().IsMatch(r.Curp))
            problems["curp"] = "La CURP debe tener 18 caracteres válidos.";
        if (!string.IsNullOrEmpty(r.Nss) && !Regex.IsMatch(r.Nss, @"^\d{11}$"))
            problems["nss"] = "El NSS debe contener exactamente 11 dígitos.";
        if (!string.IsNullOrEmpty(r.Phone) && !Regex.IsMatch(r.Phone, @"^\d{10}$"))
            problems["phone"] = "El teléfono debe contener exactamente 10 dígitos.";
        if (!ClinicNames.All.Contains(r.Clinic))
            problems["clinic"] = "Selecciona una clínica de la lista.";
        if (r.Document is not null)
        {
            var d = r.Document;
            if (d.Name.Length is < 1 or > 100 || !Regex.IsMatch(d.Name, @"^[a-zA-Z0-9_\- .()]+$") ||
                !new[] { "application/pdf", "image/jpeg", "image/png" }.Contains(d.Mime))
            { problems["document"] = "Nombre o tipo de archivo inválido."; }
            else
            {
                try
                {
                    var bytes = Convert.FromBase64String(d.DataBase64);
                    bool isPdf = d.Mime == "application/pdf" && bytes.AsSpan().StartsWith("%PDF-"u8);
                    bool isPng = d.Mime == "image/png" && bytes.AsSpan().StartsWith(new byte[] { 137,80,78,71,13,10,26,10 });
                    bool isJpeg = d.Mime == "image/jpeg" && bytes.AsSpan().StartsWith(new byte[] {255,216,255});
                    if (bytes.Length is < 1 or > 1_048_576 || !(isPdf || isPng || isJpeg))
                        problems["document"] = "Documento inválido o mayor de 1 MB.";
                }
                catch (FormatException) { problems["document"] = "Archivo codificado incorrectamente."; }
            }
        }
        return problems;
    }
}

/// <summary>SOLO DEMOSTRACIÓN: reglas no validadas por personal clínico.</summary>
public static class DemoTriageRules
{
    public static TriageDto Evaluate(TriageAnswers a)
    {
        if (a.Systolic is < 40 or > 260 || a.Diastolic is < 40 or > 260)
            throw new ArgumentException("Presión arterial fuera del intervalo de captura.");
        var text = (a.Description ?? "").Trim();
        var clean = a with { Description = text.Length > 400 ? text[..400] : text };
        if (clean.ChestPain || clean.BreathingDifficulty || clean.Unconscious)
            return new("NIVEL_1_URGENCIA", "rojo", "Aviso de atención urgente",
                "No esperes un turno digital. Acude a urgencias o llama a emergencias.", clean);
        if (clean.SeverePain || clean.Fever || clean.Dizziness)
            return new("REVISION_PRIORITARIA", "ambar", "Revisión inicial prioritaria",
                "Requiere revisión por personal autorizado. Esta clasificación es orientativa.", clean);
        return new("PENDIENTE_VALORACION", "verde", "Registro recibido",
            "El personal médico establecerá la prioridad definitiva.", clean);
    }
}
