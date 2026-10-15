using System;
using System.Windows;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace ThinQDesktop;
sealed class TrayController : IDisposable
{
    readonly MainWindow main;
    readonly Forms.NotifyIcon icon;
    readonly Forms.ToolStripMenuItem startup;
    readonly System.Drawing.Icon trayImage=CreateIcon();
    bool exiting;
    const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public TrayController(MainWindow window)
    {
        main=window;
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("Controle rápido",null,(_,_)=>main.ShowCompact());
        menu.Items.Add("Abrir Clima completo",null,(_,_)=>OpenFull());
        startup=new Forms.ToolStripMenuItem("Iniciar com o Windows"){CheckOnClick=true,Checked=IsStartupEnabled()};
        startup.Click+=(_,_)=>{
            try { SetStartup(startup.Checked); }
            catch { startup.Checked=IsStartupEnabled();MessageBox.Show("Não foi possível alterar a inicialização automática.","Clima"); }
        };
        menu.Items.Add(startup);menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair do Clima",null,(_,_)=>{exiting=true;main.CloseCompact();main.Close();});
        icon=new Forms.NotifyIcon{Text="Clima · controle do ar",Icon=trayImage,ContextMenuStrip=menu,Visible=true};
        icon.MouseClick+=(_,e)=>{if(e.Button==Forms.MouseButtons.Left)main.ShowCompact();};
        main.Closing+=(_,e)=>{if(!exiting){e.Cancel=true;main.Hide();}};
    }
    public void OpenFull(){main.CloseCompact();main.ShowInTaskbar=true;main.Show();main.WindowState=WindowState.Normal;main.Activate();}
    public static bool IsStartupEnabled(){using var key=Registry.CurrentUser.OpenSubKey(RunKey);return key?.GetValue("ClimaThinQ") is string;}
    public static void SetStartup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(RunKey);
        if(enabled) key.SetValue("ClimaThinQ","\""+Environment.ProcessPath+"\" --tray"); else key.DeleteValue("ClimaThinQ",false);
    }
    static System.Drawing.Icon CreateIcon() {
        using var stream=typeof(TrayController).Assembly.GetManifestResourceStream("Clima.TrayIcon")!;
        using var original=new System.Drawing.Icon(stream,32,32);
        return (System.Drawing.Icon)original.Clone();
    }
    public void Dispose(){icon.Visible=false;icon.ContextMenuStrip?.Dispose();icon.Dispose();trayImage.Dispose();}
}


