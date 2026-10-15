using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ThinQDesktop;
public partial class MainWindow
{
    TextBlock Description(string text) => new(){Text=text,TextWrapping=TextWrapping.Wrap,Foreground=Color("#B8C8DC"),Margin=new Thickness(0,8,0,12)};
    void TimerClick(object sender,RoutedEventArgs e) => OpenTimer(false);
    void SleepClick(object sender,RoutedEventArgs e) => OpenTimer(true);
    void OpenTimer(bool sleep)
    {
        if(busy || !fresh || active is null) return;
        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text=sleep ? "Sleep Timer" : "Programação",FontSize=24,Foreground=Brushes.White});
        panel.Children.Add(Description("Escolha a ação e o intervalo. Um clique em Programar substitui a sequência de botões do controle físico. O timer é enviado ao aparelho, não depende de manter o PC ligado."));
        var options=TimerCommand.All.Where(t=>t.Resource==(sleep?"sleepTimer":"timer")).Where(t=>t.Supported(profile) || t.CanCancel(profile)).ToList();
        var kind=new ComboBox{ItemsSource=options,DisplayMemberPath="Label",SelectedIndex=0,Margin=new Thickness(0,0,0,12)}; panel.Children.Add(kind);
        var times=new WrapPanel(); panel.Children.Add(times);
        var hours=new ComboBox{ItemsSource=Enumerable.Range(0,24),SelectedIndex=1,Width=90};
        var minutes=new ComboBox{ItemsSource=Enumerable.Range(0,60),SelectedIndex=0,Width=90};
        times.Children.Add(hours); times.Children.Add(Description("  horas    ")); times.Children.Add(minutes); times.Children.Add(Description("  minutos"));
        var status=Description(""); panel.Children.Add(status);
        var row=new WrapPanel(); panel.Children.Add(row);
        var program=new Button{Content="Programar",Margin=new Thickness(0,0,12,0),Padding=new Thickness(14,9,14,9)};
        var cancel=new Button{Content="Cancelar este timer",Padding=new Thickness(14,9,14,9)}; row.Children.Add(program); row.Children.Add(cancel);
        void Update() { var t=kind.SelectedItem as TimerCommand; program.IsEnabled=t?.Supported(profile)==true; cancel.IsEnabled=t?.CanCancel(profile)==true; status.Text=t is null?"Nenhum timer disponível.":"Estado informado: "+(Value(t.Resource,t.StatusField)=="SET"?"programado":Value(t.Resource,t.StatusField)=="UNSET"?"sem programação":"não informado"); }
        kind.SelectionChanged+=(_,_)=>Update(); Update();
        var dialog=Dialog("Programação do aparelho",panel,570,430);
        async Task Apply(bool clear) {
            if(kind.SelectedItem is not TimerCommand t) return;
            int h=(int)hours.SelectedItem,m=(int)minutes.SelectedItem;
            // Validate before closing the editor; no network operation occurs here.
            System.Text.Json.Nodes.JsonObject payload;
            try { payload=t.Payload(profile,h,m,clear); } catch(Exception ex) { status.Text=ex.Message; return; }
            dialog.Close();
            await Run(async ()=>{
                StatusText.Text="Enviando programação…";
                var result=await ControlFlow.SendPayload(client!,active!.Id,payload,s=>t.Matches(s,h,m,clear));
                if(result.State is not null) { state=result.State; fresh=true; Render(); }
                else fresh=false;
                StatusText.Text=result.Confirmed?"✓  Programação confirmada pelo aparelho.":"Programação aceita pela LG; duração/estado ainda sem confirmação. Atualize os dados.";
            });
        }
        program.Click+=async(_,_)=>await Apply(false); cancel.Click+=async(_,_)=>await Apply(true);
        dialog.ShowDialog();
    }
}

