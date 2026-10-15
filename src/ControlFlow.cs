using System.Text.Json.Nodes;
using System.Globalization;
namespace ThinQDesktop;
public static class ControlFlow
{
    public static async Task<(JsonNode? State,bool Confirmed)> SendAndConfirm(ThinQClient client,string id,Capability cap,string input,int delayMs=1200)
    {
        return await SendPayload(client,id,cap.Payload(input),state=>Matches(state,cap,input),delayMs);
    }
    public static async Task<(JsonNode? State,bool Confirmed)> SendPayload(ThinQClient client,string id,JsonObject payload,Func<JsonNode?,bool> matches,int delayMs=1200)
    {
        await client.Control(id,payload); // Never retry a write automatically.
        JsonNode? state=null;
        for(int attempt=0;attempt<4;attempt++) {
            // Read immediately after acceptance; back off only while the device is pending.
            if(attempt>0) await Task.Delay(delayMs * attempt);
            try { state=await client.Get($"devices/{Uri.EscapeDataString(id)}/state"); }
            catch { return (state,false); }
            if(matches(state)) return (state,true);
        }
        return(state,false);
    }
    public static bool Matches(JsonNode? state,Capability cap,string input)
    {
        JsonNode? actual;
        if(cap.Resource=="temperatureInUnits") {
            var block=(state?[cap.Resource] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x=>x["unit"]?.ToString()==cap.Unit);
            actual=block?[cap.Field] ?? block?["targetTemperature"];
        } else actual=state?[cap.Resource]?[cap.Field];
        if(actual is null) return false;
        var expected=cap.Payload(input)[cap.Resource]?[cap.Field];
        if(decimal.TryParse(actual.ToString(),NumberStyles.Number,CultureInfo.InvariantCulture,out var a) && decimal.TryParse(expected?.ToString(),NumberStyles.Number,CultureInfo.InvariantCulture,out var b)) return a==b;
        return actual.ToJsonString()==expected?.ToJsonString();
    }
}

