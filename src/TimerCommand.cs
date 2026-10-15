using System.Text.Json.Nodes;
namespace ThinQDesktop;
public record TimerCommand(string Resource,string Suffix,string Label)
{
    public static readonly TimerCommand[] All = { new("timer","Start","Ligar depois de"), new("timer","Stop","Desligar depois de"), new("sleepTimer","Stop","Sleep · desligar depois de") };
    public string StatusField => "relative"+Suffix+"Timer";
    string Hour => "relativeHourTo"+Suffix;
    string Minute => "relativeMinuteTo"+Suffix;
    static bool Writable(JsonNode? spec) => spec?["mode"] is JsonArray mode && mode.Any(v=>v?.ToString()=="w");
    public bool Supported(JsonNode? profile) => Writable(profile?["property"]?[Resource]?[Hour]) && Writable(profile?["property"]?[Resource]?[Minute]);
    public bool CanCancel(JsonNode? profile) => Writable(profile?["property"]?[Resource]?[StatusField]) && profile?["property"]?[Resource]?[StatusField]?["value"]?["w"] is JsonArray values && values.Any(v=>v?.ToString()=="UNSET");
    public JsonObject Payload(JsonNode? profile,int hours,int minutes,bool cancel)
    {
        if(cancel) { if(!CanCancel(profile)) throw new Exception("Cancelamento não disponível neste aparelho."); return new(){[Resource]=new JsonObject{[StatusField]="UNSET"}}; }
        if(!Supported(profile)) throw new Exception("Timer não disponível neste aparelho.");
        // UI accepts at most 23h59m. The service may impose a smaller model-specific limit.
        if(hours<0 || hours>23 || minutes<0 || minutes>59 || hours*60+minutes==0) throw new Exception("Escolha um período entre 1 minuto e 23h59.");
        return new(){[Resource]=new JsonObject{[Hour]=hours,[Minute]=minutes}};
    }
    public bool Matches(JsonNode? state,int hours,int minutes,bool cancel)
    {
        var block=state?[Resource];
        if(cancel) return block?[StatusField]?.ToString()=="UNSET";
        // A SET flag alone cannot prove the requested duration was applied.
        return block?[StatusField]?.ToString()=="SET" && block?[Hour]?.ToString()==hours.ToString() && block?[Minute]?.ToString()==minutes.ToString();
    }
}
