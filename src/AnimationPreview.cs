using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace ThinQDesktop;
static class AnimationPreview
{
    public static void Run()
    {
        var app=new Application();var row=new StackPanel{Orientation=Orientation.Horizontal};var animations=new List<AirflowAnimation>();
        var w=new Window{Title="Prévia visual · sem conexão LG",Width=840,Height=250,Content=row,Background=new SolidColorBrush(Color.FromRgb(16,28,43))};
        foreach(var mode in new[]{"FAN","COOL","AIR_DRY"}){
            var panel=new StackPanel{Width=270,Margin=new Thickness(4)};panel.Children.Add(new TextBlock{Text=mode,Foreground=Brushes.White,Margin=new Thickness(15)});var canvas=new Canvas{Width=260,Height=160};panel.Children.Add(canvas);row.Children.Add(panel);
            var flow=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 60,65 C 60,90 50,110 45,135 M 125,65 C 120,90 130,110 125,135 M 190,65 C 190,90 200,110 205,135"),StrokeThickness=1.5};canvas.Children.Add(flow);
            var body=new Border{Width=208,Height=55,CornerRadius=new CornerRadius(13),Background=Brushes.LightGray};Canvas.SetLeft(body,26);Canvas.SetTop(body,5);canvas.Children.Add(body);
            var vane=new Border{Width=180,Height=6,Background=Brushes.DarkSlateGray};Canvas.SetLeft(vane,40);Canvas.SetTop(vane,48);canvas.Children.Add(vane);
            var animation=new AirflowAnimation(flow,vane);animations.Add(animation);animation.Update(true,mode,3);
        }
        w.Loaded+=async(_,_)=>{await Task.Delay(2100);w.UpdateLayout();var bitmap=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(w);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"preview-animation-modes.png")))encoder.Save(file);foreach(var a in animations)a.Dispose();w.Close();};app.Run(w);
    }
}
