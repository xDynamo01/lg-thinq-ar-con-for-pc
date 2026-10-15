using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace ThinQDesktop;
static class Tests
{
    public static void Run()
    {
        int count = 0;
        void Check(bool yes, string name) { if (!yes) throw new Exception(name); count++; }
        var p = JsonNode.Parse("""
        {"property":{
          "operation":{"airConOperationMode":{"type":"enum","mode":["r","w"],"value":{"w":["POWER_ON","POWER_OFF"]}}},
          "temperatureInUnits":[{"unit":"C","targetTemperature":{"type":"range","mode":["r","w"],"value":{"w":{"min":16,"max":30,"step":0.5,"except":[19]}}},"currentTemperature":{"type":"range","mode":["r"],"value":{"r":{"min":0,"max":50}}}}],
          "display":{"light":{"type":"enum","mode":["w"],"value":{"w":[true,false]}}},
          "timer":{"relativeHourToStop":{"type":"range","mode":["w"],"value":{"w":{"min":0,"max":24}}}}
        }}
        """);
        var caps = Capabilities.Read(p).ToList(); Check(caps.Count == 3, "Only supported writable controls");
        var cool = JsonNode.Parse("""{"property":{"temperatureInUnits":[{"unit":"C","coolTargetTemperature":{"type":"range","mode":["w"],"value":{"w":{"min":18,"max":30,"step":0.5}}}}]}}""");
        Check(Capabilities.Read(cool).Single().Payload("22.5")["temperatureInUnits"]?["coolTargetTemperature"]?.GetValue<decimal>() == 22.5m, "Model-specific cooling setpoint");
        var temp = caps.Single(c => c.Field == "targetTemperature");
        Check(temp.Payload("23,5")["temperatureInUnits"]?["targetTemperature"]?.GetValue<decimal>() == 23.5m, "Decimal temperature");
        Check(temp.Payload("23")["temperatureInUnits"]?["unit"]?.ToString() == "C", "Temperature unit");
        foreach (var bad in new[] { "15", "31", "19", "20.1", "invalid" }) { bool threw = false; try { temp.Payload(bad); } catch { threw = true; } Check(threw, "Reject " + bad); }
        Check(caps.Single(c => c.Field == "light").Payload("true")["display"]?["light"]?.GetValue<bool>() == true, "Boolean enum preserves type");
        Check(Device.Parse(JsonNode.Parse("""[{"deviceId":"ac","deviceInfo":{"deviceType":"DEVICE_AIR_CONDITIONER","alias":"Quarto"}},{"deviceId":"oven","deviceInfo":{"deviceType":"DEVICE_OVEN"}}]""")).Count == 1, "Filter to air conditioners");
        Check(Capabilities.Flatten(JsonNode.Parse("""{"temperature":[{"value":23}],"error":null}""")).Count() == 2, "Flatten nested/null data");
        var handler = new FakeHandler(); using var api = new ThinQClient("test-token", "BR", handler);
        var result = api.Get("devices").GetAwaiter().GetResult();
        Check(result is JsonArray, "Unwrap API response");
        Check(handler.Url == "https://api-aic.lgthinq.com/devices", "Brazil region routing");
        Check(handler.Auth == "Bearer test-token" && handler.Country == "BR", "Auth and country headers");
        api.Control("ac", temp.Payload("23")).GetAwaiter().GetResult();
        Check(handler.Method == "POST" && handler.Conditional == "true" && handler.Body!.Contains("targetTemperature"), "Control request contract");
        handler.Status = HttpStatusCode.Unauthorized;
        bool rejected = false; try { api.Get("devices").GetAwaiter().GetResult(); } catch (Exception ex) { rejected = ex.Message.Contains("Token inválido") && !ex.Message.Contains("test-token"); }
        Check(rejected, "Unauthorized response without token disclosure");
        handler.Status = HttpStatusCode.OK; handler.Response = "{}";
        rejected = false; try { api.Get("devices").GetAwaiter().GetResult(); } catch { rejected = true; }
        Check(rejected, "Unknown response fails explicitly");
        var swingProfile=JsonNode.Parse("""{"property":{"windDirection":{"rotateUpDown":{"type":"boolean","mode":["r","w"],"value":{"w":[true,false]}}}}}""");
        var swing=Capabilities.Read(swingProfile).Single();
        Check(swing.Payload("true")["windDirection"]?["rotateUpDown"]?.GetValue<bool>()==true,"Real boolean capability");
        Check(ControlFlow.Matches(JsonNode.Parse("""{"windDirection":{"rotateUpDown":false}}"""),swing,"false"),"Boolean readback confirmation");
        Check(!ControlFlow.Matches(JsonNode.Parse("""{"windDirection":{"rotateUpDown":true}}"""),swing,"false"),"Reject stale readback");
        var coolCap=Capabilities.Read(cool).Single();
        Check(ControlFlow.Matches(JsonNode.Parse("""{"temperatureInUnits":[{"unit":"C","targetTemperature":22.5}]}"""),coolCap,"22.5"),"Cooling command maps to reported setpoint");
        Check(!ControlFlow.Matches(JsonNode.Parse("""{"temperatureInUnits":[{"unit":"F","targetTemperature":22.5}]}"""),coolCap,"22.5"),"Reject mismatched temperature units");
        var timerProfile=JsonNode.Parse("""{"property":{"timer":{"relativeHourToStop":{"type":"number","mode":["r","w"]},"relativeMinuteToStop":{"type":"number","mode":["r","w"]},"relativeStopTimer":{"type":"enum","mode":["r","w"],"value":{"w":["UNSET"]}}}}}""");
        var timer=TimerCommand.All[1];
        Check(timer.Supported(timerProfile),"Timer requires both writable fields");
        Check(timer.Payload(timerProfile,1,30,false)["timer"]?["relativeMinuteToStop"]?.GetValue<int>()==30,"Timer compound payload");
        Check(timer.Payload(timerProfile,0,0,true)["timer"]?["relativeStopTimer"]?.ToString()=="UNSET","Timer cancellation");
        foreach(var duration in new[]{(0,0),(-1,30),(24,0),(1,60)}) { bool invalid=false;try{timer.Payload(timerProfile,duration.Item1,duration.Item2,false);}catch{invalid=true;}Check(invalid,"Invalid timer duration rejected"); }
        Check(!timer.Supported(null),"No assumed timer support");
        Check(!timer.Matches(JsonNode.Parse("""{"timer":{"relativeStopTimer":"SET"}}"""),1,30,false),"SET alone does not confirm duration");
        Check(timer.Matches(JsonNode.Parse("""{"timer":{"relativeStopTimer":"SET","relativeHourToStop":1,"relativeMinuteToStop":30}}"""),1,30,false),"Timer exact readback");
        Check(!MainWindow.CanRecover(new ThinQApiException(401,"test")),"No retry for invalid token");
        Check(!MainWindow.CanRecover(new ThinQApiException(429,"test")),"No automatic retry against rate limit");
        Check(MainWindow.CanRecover(new HttpRequestException()),"Recover network interruptions");
        var fastHandler=new FakeHandler(); using var fastApi=new ThinQClient("test-token","BR",fastHandler);
        var immediate=ControlFlow.SendPayload(fastApi,"ac",new JsonObject(),_=>true);
        Check(immediate.IsCompletedSuccessfully && immediate.Result.Confirmed,"Immediate readback has no artificial delay");
        Check(fastHandler.Method=="GET","Confirmation still reads device state");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "tests.txt"), $"PASS: {count} offline assertions. No LG account or real device was used.\n");
    }
    public static async Task LiveDisplay()
    {
        var log=new List<string>();
        try {
            using var api=new ThinQClient(AccountStore.Load() ?? throw new Exception("No saved account"),AccountStore.Country);
            var devices=Device.Parse(await api.Get("devices"));
            if(devices.Count!=1) throw new Exception("Expected one air conditioner");
            var id=devices[0].Id;
            var profile=await api.Get($"devices/{Uri.EscapeDataString(id)}/profile");
            var state=await api.Get($"devices/{Uri.EscapeDataString(id)}/state");
            var cap=Capabilities.Read(profile).Single(c=>c.Resource=="display" && c.Field=="light");
            var original=state?["display"]?["light"]?.ToString();
            if(original is not ("ON" or "OFF")) throw new Exception("Unknown initial display state");
            try {
                var elapsed=System.Diagnostics.Stopwatch.StartNew();
                var changed=await ControlFlow.SendAndConfirm(api,id,cap,original=="ON"?"OFF":"ON");
                log.Add("COMMAND_AND_READBACK_MS="+elapsed.ElapsedMilliseconds);
                log.Add(changed.Confirmed?"DISPLAY_TOGGLE_CONFIRMED":"DISPLAY_TOGGLE_NOT_CONFIRMED");
            } finally {
                var restored=await ControlFlow.SendAndConfirm(api,id,cap,original);
                log.Add(restored.Confirmed?"ORIGINAL_DISPLAY_RESTORED":"RESTORATION_NOT_CONFIRMED");
            }
        } catch(Exception e) { log.Add("FAILED: "+e.Message); }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"live-control-test.txt"),log);
    }
    sealed class FakeHandler : HttpMessageHandler
    {
        public string? Url, Auth, Country, Method, Conditional, Body;
        public string Response = "{\"response\":[]}";
        public HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c)
        {
            Url = r.RequestUri!.ToString(); Auth = r.Headers.Authorization?.ToString(); Country = r.Headers.GetValues("x-country").Single(); Method = r.Method.Method;
            Conditional = r.Headers.TryGetValues("x-conditional-control", out var v) ? v.Single() : null;
            Body = r.Content is null ? null : await r.Content.ReadAsStringAsync(c);
            return new HttpResponseMessage(Status) { Content = new StringContent(Response, Encoding.UTF8, "application/json") };
        }
    }
}



