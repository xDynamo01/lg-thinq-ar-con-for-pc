using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace ThinQDesktop;
public partial class MainWindow
{
    void FanCardClick(object sender,MouseButtonEventArgs e)=>FanSettingsClick(sender,e);
    void FlowCardClick(object sender,MouseButtonEventArgs e)=>FlowSettingsClick(sender,e);
    void FanSettingsClick(object sender,RoutedEventArgs e) { if(!busy && fresh) FanDialog().ShowDialog(); }
    void FlowSettingsClick(object sender,RoutedEventArgs e) { if(!busy && fresh) FlowDialog().ShowDialog(); }
    static Point DialPoint(double index,double radius=134) { double a=(135+index*67.5)*Math.PI/180;return new(200+radius*Math.Cos(a),175+radius*Math.Sin(a)); }
    static PathGeometry DialArc()
    {
        var p=new PathFigure{StartPoint=DialPoint(0)};
        for(int i=1;i<=80;i++) p.Segments.Add(new LineSegment(DialPoint(i/20.0),true));
        return new PathGeometry(new[]{p});
    }
    Button DialogButton(string text) => new(){Content=text,Padding=new Thickness(14,10,14,10),Margin=new Thickness(5),Background=Color("#2B3A50"),Foreground=Brushes.White,BorderBrush=Color("#496075"),Cursor=Cursors.Hand};
    Window FanDialog()
    {
        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text="Velocidade da ventilação",FontSize=23,Foreground=Brushes.White});
        panel.Children.Add(Description("Clique no arco ou em um número para escolher o nível."));
        var canvas=new Canvas{Width=400,Height=350,HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(canvas);
        var arc=new Path{Data=DialArc(),Stroke=Color("#08B8EB"),StrokeThickness=48,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Cursor=Cursors.Hand};canvas.Children.Add(arc);
        var center=new TextBlock{Width=220,TextAlignment=TextAlignment.Center,FontSize=34,Foreground=Brushes.White};Canvas.SetLeft(center,90);Canvas.SetTop(center,145);canvas.Children.Add(center);
        var feedback=Description("");
        var row=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};panel.Children.Add(row);
        var minus=DialogButton("−");var plus=DialogButton("+");var auto=DialogButton("Automático");row.Children.Add(minus);row.Children.Add(plus);row.Children.Add(auto);
        panel.Children.Add(feedback);var close=DialogButton("Fechar");panel.Children.Add(close);
        var dialog=Dialog("Ventilação",panel,500,750);close.Click+=(_,_)=>dialog.Close();
        var step=Find("airFlow","windStep");var speed=Find("airFlow","windStrength");
        var nodes=new List<(Button Button,int Level)>();bool changing=false;
        void Update() {
            bool automatic=Value("airFlow","windStrength")=="AUTO";
            var current=Number("airFlow","windStep");center.Text=automatic?"Automático":"Nível "+Format(current);
            foreach(var (b,n) in nodes) { b.Background=Color(!automatic && current==n?"#535FF7":"#25374A"); b.IsEnabled=!changing && fresh && step is not null; }
            arc.IsHitTestVisible=!changing && fresh && step is not null;
            minus.IsEnabled=!changing && fresh && current.HasValue && step is not null && current>step.Min;
            plus.IsEnabled=!changing && fresh && current.HasValue && step is not null && current<step.Max;
            auto.IsEnabled=!changing && fresh && speed?.Options.Contains("AUTO")==true;
            auto.Content=automatic?"Automático ✓":"Automático";
        }
        async Task Apply(Capability? cap,string value) {
            if(changing || cap is null || !fresh) return;changing=true;Update();feedback.Text="Enviando ajuste…";
            await Run(()=>Send(cap,value));feedback.Text=StatusText.Text;changing=false;Update();
        }
        for(int n=1;n<=5;n++) {
            if(step is null || n<step.Min || n>step.Max || step.Excluded.Contains(n)) continue;
            int level=n;var b=DialogButton(n.ToString());b.Width=46;b.Height=40;b.Padding=new Thickness(0);b.Margin=new Thickness(0);
            var point=DialPoint(n-1,170);Canvas.SetLeft(b,point.X-23);Canvas.SetTop(b,point.Y-20);canvas.Children.Add(b);nodes.Add((b,n));b.Click+=async(_,_)=>await Apply(step,level.ToString());
        }
        arc.MouseLeftButtonUp+=async(_,e)=>{
            if(nodes.Count==0)return;var p=e.GetPosition(canvas);
            int nearest=nodes.OrderBy(x=>(DialPoint(x.Level-1)-p).LengthSquared).First().Level;
            await Apply(step,nearest.ToString());
        };
        minus.Click+=async(_,_)=>await Apply(step,((Number("airFlow","windStep")??1)-1).ToString(CultureInfo.InvariantCulture));
        plus.Click+=async(_,_)=>await Apply(step,((Number("airFlow","windStep")??1)+1).ToString(CultureInfo.InvariantCulture));
        auto.Click+=async(_,_)=>await Apply(speed,"AUTO");
        StateUpdated+=Update;dialog.Closed+=(_,_)=>StateUpdated-=Update;Update();return dialog;
    }
    Window FlowDialog()
    {
        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text="Fluxo de ar",FontSize=25,Foreground=Brushes.White});
        panel.Children.Add(Description("CIMA / BAIXO · clique no gráfico para ligar ou parar a oscilação."));
        var canvas=new Canvas{Width=390,Height=250,Background=Color("#223044"),Cursor=Cursors.Hand,ToolTip="Alternar oscilação vertical"};panel.Children.Add(canvas);
        canvas.Children.Add(new Path{Data=Geometry.Parse("M 60,48 L 242,48 L 242,76 L 273,76 L 273,219"),Stroke=Color("#9EAEBB"),StrokeThickness=8,StrokeLineJoin=PenLineJoin.Round});
        var rays=new List<Line>();
        for(int i=0;i<6;i++){double a=(165-i*14)*Math.PI/180;var ray=new Line{X1=239,Y1=83,X2=239+160*Math.Cos(a),Y2=83+160*Math.Sin(a),Stroke=Color("#6672FF"),StrokeThickness=8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};rays.Add(ray);canvas.Children.Add(ray);}
        var status=Description("");panel.Children.Add(status);
        var row=new WrapPanel();panel.Children.Add(row);var start=DialogButton("Circular ↕");var stop=DialogButton("Parar oscilação");row.Children.Add(start);row.Children.Add(stop);
        panel.Children.Add(Description("Posições fixas da aleta: indisponíveis na API conectada. O gráfico controla apenas a oscilação; não escolhe um ângulo."));
        var close=DialogButton("Fechar");panel.Children.Add(close);var dialog=Dialog("Fluxo de ar",panel,500,740);close.Click+=(_,_)=>dialog.Close();
        bool changing=false;var cap=Find("windDirection","rotateUpDown");
        void Update() {bool on=Value("windDirection","rotateUpDown")=="true";foreach(var ray in rays)ray.Stroke=Color(on?"#6672FF":"#788593");start.IsEnabled=!changing && fresh && cap is not null;stop.IsEnabled=start.IsEnabled;canvas.IsHitTestVisible=start.IsEnabled;}
        async Task Apply(bool on) { if(changing || cap is null || !fresh)return;changing=true;Update();status.Text="Enviando…";await Run(()=>Send(cap,on?"true":"false"));status.Text=StatusText.Text;changing=false;Update(); }
        start.Click+=async(_,_)=>await Apply(true);stop.Click+=async(_,_)=>await Apply(false);canvas.MouseLeftButtonUp+=async(_,_)=>await Apply(Value("windDirection","rotateUpDown")!="true");
        status.Text=Value("windDirection","rotateUpDown")=="true"?"Oscilação vertical ativa":"Oscilação parada";StateUpdated+=Update;dialog.Closed+=(_,_)=>StateUpdated-=Update;Update();return dialog;
    }
}


