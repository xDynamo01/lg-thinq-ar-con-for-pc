using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace ThinQDesktop;
sealed class AirflowAnimation : IDisposable
{
    readonly Path flow;
    readonly Canvas host;
    readonly TranslateTransform travel=new();
    readonly List<FrameworkElement> particles=new();
    bool on;
    string mode="FAN";
    int level=3;
    bool initialized;
    public AirflowAnimation(Path flow,FrameworkElement vane)
    {
        this.flow=flow;host=(Canvas)flow.Parent;
        flow.RenderTransform=travel;
        flow.StrokeDashArray=new DoubleCollection{9,5};
        // The appliance and its louver remain stationary; only emitted air moves.
        vane.RenderTransform=Transform.Identity;
        flow.IsVisibleChanged+=VisibilityChanged;
    }
    public void Update(bool powered,string? operatingMode,int speed)
    {
        speed=Math.Clamp(speed,1,5);operatingMode??="FAN";
        if(on==powered&&mode==operatingMode&&level==speed&&initialized)return;
        on=powered;mode=operatingMode;level=speed;initialized=true;Apply();
    }
    void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)=>Apply();
    static Brush Brush(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    static FrameworkElement Symbol(bool dry)
    {
        var icon=new Canvas{Width=20,Height=20,IsHitTestVisible=false};
        if(dry){
            icon.Children.Add(new Path{Data=Geometry.Parse("M 9,1 C 7,5 3,9 3,12 A 6,6 0 0 0 15,12 C 15,9 11,5 9,1 Z"),Fill=Brush("#6BD9EE"),Stroke=Brush("#BAF3FF"),StrokeThickness=0.6});
            var badge=new Ellipse{Width=10,Height=10,Fill=Brush("#341F27")};Canvas.SetLeft(badge,10);Canvas.SetTop(badge,10);icon.Children.Add(badge);
            icon.Children.Add(new Line{X1=12,Y1=15,X2=18,Y2=15,Stroke=Brush("#FF6473"),StrokeThickness=2.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        } else {
            for(int i=0;i<6;i++){
                double a=i*Math.PI/3;double x=10+8*Math.Cos(a),y=10+8*Math.Sin(a);
                icon.Children.Add(new Line{X1=10,Y1=10,X2=x,Y2=y,Stroke=Brush("#B6F2FF"),StrokeThickness=1.3});
                foreach(double d in new[]{-0.7,0.7})icon.Children.Add(new Line{X1=10+5*Math.Cos(a),Y1=10+5*Math.Sin(a),X2=10+5*Math.Cos(a)-3*Math.Cos(a+d),Y2=10+5*Math.Sin(a)-3*Math.Sin(a+d),Stroke=Brush("#B6F2FF"),StrokeThickness=1.1});
            }
        }
        return icon;
    }
    void ClearParticles(){foreach(var p in particles){p.BeginAnimation(UIElement.OpacityProperty,null);if(p.RenderTransform is TranslateTransform t){t.BeginAnimation(TranslateTransform.YProperty,null);t.BeginAnimation(TranslateTransform.XProperty,null);}host.Children.Remove(p);}particles.Clear();}
    void Apply()
    {
        flow.BeginAnimation(Shape.StrokeDashOffsetProperty,null);flow.BeginAnimation(UIElement.OpacityProperty,null);travel.BeginAnimation(TranslateTransform.YProperty,null);ClearParticles();
        flow.Opacity=on?0.5:0;travel.Y=0;
        flow.Stroke=Brush(mode=="COOL"?"#73D9F4":mode=="AIR_DRY"?"#66AEBE":"#7CB5C1");
        if(!on||!flow.IsVisible||!SystemParameters.ClientAreaAnimation)return;
        var duration=TimeSpan.FromSeconds(2.5-level*0.28);
        flow.BeginAnimation(Shape.StrokeDashOffsetProperty,new DoubleAnimation(0,-14,duration){RepeatBehavior=RepeatBehavior.Forever});
        flow.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0.2,0.6,duration){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever});
        travel.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,4,duration){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever});
        if(mode is not ("COOL" or "AIR_DRY"))return;
        Rect bounds=flow.Data.Bounds;
        for(int i=0;i<3;i++){
            var symbol=Symbol(mode=="AIR_DRY");symbol.Opacity=0;Canvas.SetLeft(symbol,bounds.Left+bounds.Width*(i+1)/4-10);Canvas.SetTop(symbol,bounds.Top-4);
            var move=new TranslateTransform();symbol.RenderTransform=move;host.Children.Add(symbol);particles.Add(symbol);
            var cycle=TimeSpan.FromSeconds(3.6);var delay=TimeSpan.FromSeconds(i*0.85);
            move.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,Math.Max(26,bounds.Height-12),cycle){BeginTime=delay,RepeatBehavior=RepeatBehavior.Forever});
            move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,(i-1)*9,cycle){BeginTime=delay,RepeatBehavior=RepeatBehavior.Forever});
            var fade=new DoubleAnimationUsingKeyFrames{Duration=cycle,BeginTime=delay,RepeatBehavior=RepeatBehavior.Forever};
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(0)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9,KeyTime.FromPercent(0.2)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.8,KeyTime.FromPercent(0.6)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(0.85)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(1)));symbol.BeginAnimation(UIElement.OpacityProperty,fade);
        }
    }
    public void Dispose(){on=false;Apply();flow.IsVisibleChanged-=VisibilityChanged;}
}
