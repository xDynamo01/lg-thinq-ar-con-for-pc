using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Globalization;

namespace ThinQDesktop;

public sealed class ThinQApiException(int statusCode,string message) : Exception(message)
{ public int StatusCode { get; } = statusCode; }

public sealed class ThinQClient : IDisposable
{
    readonly HttpClient http;
    readonly string country, clientId;
    public ThinQClient(string token, string country, HttpMessageHandler? handler = null)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsControl)) throw new Exception("Token em formato inválido. Cole somente o token gerado pela LG.");
        this.country = country;
        string region = country switch { "BR" or "US" => "aic", "PT" => "eic", "KR" => "kic", _ => throw new Exception("País não suportado nesta versão.") };
        http = handler is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        http.BaseAddress = new Uri($"https://api-{region}.lgthinq.com/"); http.Timeout = TimeSpan.FromSeconds(25);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Public application key from LG's Apache-2.0 Python SDK; not a user credential.
        http.DefaultRequestHeaders.Add("x-api-key", "v6GFvkweNo7DK7yD3ylIZ9w52aKBU0eJ7wLXkSR3");
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThinQDesktop");
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "client-id.txt");
        clientId = File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        if (!Guid.TryParse(clientId, out _)) { clientId = Guid.NewGuid().ToString(); File.WriteAllText(path, clientId); }
    }
    public Task<JsonNode?> Get(string endpoint) => Send(HttpMethod.Get, endpoint, null);
    public Task<JsonNode?> Control(string id, JsonObject payload) => Send(HttpMethod.Post, $"devices/{Uri.EscapeDataString(id)}/control", payload);
    async Task<JsonNode?> Send(HttpMethod method, string endpoint, JsonObject? payload)
    {
        using var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Add("x-country", country); request.Headers.Add("x-client-id", clientId);
        request.Headers.Add("x-service-phase", "OP");
        request.Headers.Add("x-message-id", Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+','-').Replace('/','_'));
        if (payload is not null) { request.Headers.Add("x-conditional-control", "true"); request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"); }
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) {
            var message = (int)response.StatusCode switch {
                401 => "Token inválido ou expirado. Gere outro token na LG.",
                403 => "Acesso recusado. Confira as permissões do token e o país da conta.",
                429 => "Limite de consultas da LG atingido. Aguarde antes de atualizar.",
                _ => $"A LG retornou HTTP {(int)response.StatusCode}. Verifique conexão, compatibilidade e permissões."
            };
            throw new ThinQApiException((int)response.StatusCode,message);
        }
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text) && method == HttpMethod.Post) return null;
        JsonNode? root;
        try { root = JsonNode.Parse(text); } catch { throw new Exception("A LG retornou uma resposta não reconhecida."); }
        if (root is not JsonObject obj || !obj.ContainsKey("response")) throw new Exception("Resposta da LG sem o campo esperado; nenhuma leitura foi presumida.");
        return obj["response"]?.DeepClone();
    }
    public void Dispose() => http.Dispose();
}

public record Device(string Id, string Name, JsonNode Info)
{
    public static List<Device> Parse(JsonNode? response)
    {
        if (response is not JsonArray list) throw new Exception("Formato da lista de aparelhos não reconhecido.");
        return list.OfType<JsonObject>().Where(x => x["deviceInfo"]?["deviceType"]?.ToString() == "DEVICE_AIR_CONDITIONER" && !string.IsNullOrWhiteSpace(x["deviceId"]?.ToString()))
            .Select(x => new Device(x["deviceId"]!.ToString(), x["deviceInfo"]?["alias"]?.ToString() ?? "Ar-condicionado", x.DeepClone())).ToList();
    }
}

public record Capability(string Resource, string Field, string? Unit, List<string> Options, decimal Min, decimal Max, decimal Step, List<decimal> Excluded, JsonArray? EnumValues)
{
    public string Label => Resource + "." + Field + (Unit is null ? "" : " (" + Unit + ")");
    public JsonObject Payload(string input)
    {
        JsonNode value;
        if (Options.Count > 0) {
            int index = Options.IndexOf(input); if (index < 0) throw new Exception("Opção não anunciada pelo aparelho.");
            value = EnumValues![index]!.DeepClone();
        } else {
            if (!decimal.TryParse(input.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) || number < Min || number > Max || (number - Min) % Step != 0 || Excluded.Contains(number))
                throw new Exception($"Use um valor de {Min} a {Max}, passo {Step}, permitido pelo aparelho.");
            value = JsonValue.Create(number)!;
        }
        var body = new JsonObject { [Field] = value };
        if (Unit is not null) body["unit"] = Unit;
        // ThinQ Connect control accepts temperatureInUnits as an object (SDK _get_attribute_payload).
        return new JsonObject { [Resource] = body };
    }
}

public static class Capabilities
{
    // Compound scheduling and coupled setpoints need dedicated validation; do not expose individual writes.
    static readonly HashSet<string> Simple = new() { "operation", "airConJobMode", "airFlow", "powerSave", "display", "windDirection", "temperatureInUnits" };
    public static IEnumerable<Capability> Read(JsonNode? profile)
    {
        if (profile?["property"] is not JsonObject props) yield break;
        foreach (var resource in props) {
            if (!Simple.Contains(resource.Key)) continue;
            var blocks = resource.Value is JsonArray arr ? arr.OfType<JsonObject>() : resource.Value is JsonObject block ? new[] { block } : Array.Empty<JsonObject>();
            foreach (var item in blocks) foreach (var field in item) {
                if (resource.Key == "temperatureInUnits" && field.Key is not ("targetTemperature" or "coolTargetTemperature" or "heatTargetTemperature" or "autoTargetTemperature")) continue;
                if (field.Value is not JsonObject spec || !Writable(spec["mode"])) continue;
                var options = spec["value"]?["w"] as JsonArray;
                string? unit = item["unit"]?.ToString();
                if (resource.Key == "temperatureInUnits" && unit is not ("C" or "F")) continue;
                if (spec["type"]?.ToString() is "enum" or "boolean" && options is { Count: > 0 } && options.All(v => v is JsonValue))
                    yield return new(resource.Key, field.Key, unit, options.Select(v => v!.ToString()).ToList(), 0, 0, 1, new(), options);
                else if (spec["type"]?.ToString() == "range" && spec["value"]?["w"] is JsonObject range && Number(range["min"], out var min) && Number(range["max"], out var max)) {
                    var step = range.ContainsKey("step") && Number(range["step"], out var parsed) ? parsed : 1;
                    if (step <= 0 || min > max) continue;
                    var excluded = (range["except"] as JsonArray)?.Select(v => Number(v, out var n) ? n : decimal.MinValue).ToList() ?? new();
                    yield return new(resource.Key, field.Key, unit, new(), min, max, step, excluded, null);
                }
            }
        }
    }
    static bool Writable(JsonNode? mode) => mode is JsonArray a ? a.Any(v => v?.ToString() == "w") : mode?.ToString().Contains('w') == true;
    static bool Number(JsonNode? node, out decimal value) => decimal.TryParse(node?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    public static IEnumerable<KeyValuePair<string,string>> Flatten(JsonNode? node, string path = "")
    {
        if (node is JsonObject obj) { foreach (var p in obj) foreach (var row in Flatten(p.Value, path.Length == 0 ? p.Key : path + "." + p.Key)) yield return row; }
        else if (node is JsonArray arr) { for (var i = 0; i < arr.Count; i++) foreach (var row in Flatten(arr[i], path + "[" + i + "]")) yield return row; }
        else yield return new(path, node?.ToString() ?? "null");
    }
}

