using System.Windows;
using System.Threading;
using System.Windows.Threading;
namespace ThinQDesktop;
public class App : Application
{
    [STAThread] public static void Main(string[] args)
    {
        if (args.Contains("--preview-animations")) { AnimationPreview.Run(); return; }
        if (args.Contains("--self-test")) { Tests.Run(); return; }
        if (args.Contains("--test-display")) { Tests.LiveDisplay().GetAwaiter().GetResult(); return; }
        bool capture=args.Contains("--capture-live") || args.Contains("--preview");
        using var mutex=new Mutex(true,"Local\\ThinQDesktop.Clima",out bool first);
        using var signal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\ThinQDesktop.Show");
        if(!capture && !first) { signal.Set(); return; }
        var app = new App();
        var main=new MainWindow(!args.Contains("--preview"),capture);
        TrayController? tray=null;
        DispatcherTimer? timer=null;
        if(!capture) {
            tray=new TrayController(main);
            timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};
            timer.Tick+=(_,_)=>{if(signal.WaitOne(0))tray.OpenFull();};timer.Start();
            if(args.Contains("--tray")) { main.Opacity=0;main.ShowInTaskbar=false;main.Loaded+=(_,_)=>{main.Hide();main.Opacity=1;}; }
            app.Exit+=(_,_)=>{timer.Stop();tray.Dispose();};
        }
        app.Run(main);
        if(first)mutex.ReleaseMutex();
    }
}

