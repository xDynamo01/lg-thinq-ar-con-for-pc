using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Markup;
namespace ThinQDesktop;
public partial class MainWindow
{
    Window? compact;
    Action? updateCompact;
    public void CloseCompact(){compact?.Hide();}
    public void ShowCompact()
    {
        compact ??= BuildCompact();
        if(compact.IsVisible){compact.Hide();return;}
        updateCompact?.Invoke();compact.Show();
        var area=SystemParameters.WorkArea;
        compact.Left=area.Right-compact.Width-12;compact.Top=area.Bottom-compact.Height-12;
        compact.Activate();_=Run(Refresh);
    }
    Window BuildCompact()
    {
        const string markup="""
<Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" CornerRadius="25" BorderBrush="#354348" BorderThickness="1" Padding="18,16">
 <Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="0.4,1"><GradientStop Color="#233A42"/><GradientStop Color="#171C20" Offset="0.38"/><GradientStop Color="#121518" Offset="1"/></LinearGradientBrush></Border.Background>
 <Grid><Grid.RowDefinitions><RowDefinition Height="40"/><RowDefinition Height="145"/><RowDefinition Height="67"/><RowDefinition Height="57"/><RowDefinition Height="79"/><RowDefinition Height="36"/></Grid.RowDefinitions>
  <Grid><StackPanel Orientation="Horizontal" VerticalAlignment="Center"><TextBlock Text="♧" Foreground="#8EA3AB" Margin="0,0,7,0"/><TextBlock x:Name="Room" FontSize="12" FontWeight="SemiBold" Foreground="#DDE9EC" MaxWidth="145" TextTrimming="CharacterEllipsis"/><TextBlock x:Name="Ambient" FontSize="12" Foreground="#95A6AD" Margin="7,0,0,0"/></StackPanel><Button x:Name="Power" Content="⏻" HorizontalAlignment="Right" Width="36" Height="36" FontSize="23" Padding="0" Background="#52D5F7" Foreground="#124455" BorderBrush="#7AE7FF"><Button.Effect><DropShadowEffect Color="#36C7F3" BlurRadius="20" ShadowDepth="0" Opacity="0.5"/></Button.Effect></Button></Grid>
  <Grid Grid.Row="1" Margin="0,14,0,0">
   <Canvas Width="260" Height="125" HorizontalAlignment="Center">
    <Path x:Name="CompactAirStream" Stroke="#79D8E8" StrokeThickness="1" Opacity="0.25" Data="M 58,58 C 60,82 47,97 40,122 M 77,58 C 85,84 67,103 66,127 M 100,58 C 96,83 101,103 98,130 M 124,58 C 120,83 130,108 128,130 M 145,58 C 146,85 149,100 158,127 M 168,58 C 163,87 180,100 186,124 M 191,58 C 190,85 203,104 216,123"/>
    <Border Canvas.Left="26" Canvas.Top="3" Width="208" Height="60" CornerRadius="15" BorderBrush="#BAC7CC" BorderThickness="1"><Border.Background><LinearGradientBrush StartPoint="0,0" EndPoint="0,1"><GradientStop Color="#FAFCFC"/><GradientStop Color="#D4DDDF" Offset="0.8"/><GradientStop Color="#A4B2B7" Offset="1"/></LinearGradientBrush></Border.Background><Border.Effect><DropShadowEffect BlurRadius="16" ShadowDepth="8" Opacity="0.4"/></Border.Effect><Grid><TextBlock x:Name="UnitTemp" Foreground="#5BC8E5" FontSize="10" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,12,24,0"/><Border x:Name="CompactAirVane" Height="6" VerticalAlignment="Bottom" Background="#17242B" CornerRadius="3" Margin="13,0,13,7"/></Grid></Border>
   </Canvas>
  </Grid>
  <StackPanel Grid.Row="2" Orientation="Horizontal" VerticalAlignment="Center"><TextBlock x:Name="Target" FontSize="49" FontWeight="SemiBold" Foreground="#F5F8F9"/><TextBlock Text="°C" FontSize="18" Foreground="#94A1A7" Margin="3,13,0,0"/></StackPanel>
  <Grid Grid.Row="3"><Grid.ColumnDefinitions><ColumnDefinition Width="34"/><ColumnDefinition/><ColumnDefinition Width="34"/></Grid.ColumnDefinitions><Button x:Name="Minus" Content="−" Height="32" FontSize="20" Padding="0"/><StackPanel Grid.Column="1" Margin="9,0"><Slider x:Name="TempSlider" Minimum="18" Maximum="30" TickFrequency="1" IsSnapToTickEnabled="True" Height="32" VerticalAlignment="Center"><Slider.Template><ControlTemplate TargetType="Slider"><Grid Background="Transparent"><Border Background="#2D3033" Height="30" CornerRadius="15"/><Track x:Name="PART_Track"><Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate><Border CornerRadius="15,0,0,15"><Border.Background><LinearGradientBrush><GradientStop Color="#287AFF"/><GradientStop Color="#40D4F8" Offset="1"/></LinearGradientBrush></Border.Background></Border></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width="23" Height="30"><Thumb.Template><ControlTemplate><Border Background="#EDF9FF" CornerRadius="12" Margin="0,2"><Border.Effect><DropShadowEffect Color="#42CFFF" BlurRadius="10" ShadowDepth="0" Opacity="0.6"/></Border.Effect></Border></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Slider.Template></Slider><Grid><TextBlock x:Name="MinLabel" Text="18°" Foreground="#687880" FontSize="9"/><TextBlock x:Name="MaxLabel" Text="30°" Foreground="#687880" FontSize="9" HorizontalAlignment="Right"/></Grid></StackPanel><Button x:Name="Plus" Grid.Column="2" Content="+" Height="32" FontSize="20" Padding="0"/></Grid>
  <UniformGrid Grid.Row="4" Columns="3" Margin="0,6,0,3"><Button x:Name="Mode" Padding="9" Margin="0,0,6,0" HorizontalContentAlignment="Left"><StackPanel><TextBlock Text="❄" Foreground="#8DADB9" FontSize="17"/><TextBlock Text="Modo" Foreground="#84949D" FontSize="10"/><TextBlock x:Name="ModeValue" Foreground="#E5ECEF" FontSize="11" FontWeight="SemiBold"/></StackPanel></Button><Button x:Name="Fan" Padding="9" Margin="0,0,6,0"><StackPanel><TextBlock Text="✣" Foreground="#8DADB9" FontSize="17"/><TextBlock Text="Ventilação" Foreground="#84949D" FontSize="10"/><TextBlock x:Name="FanValue" Foreground="#E5ECEF" FontSize="11" FontWeight="SemiBold"/></StackPanel></Button><Button x:Name="Swing" Padding="9" Background="#193039" BorderBrush="#315363"><StackPanel><TextBlock x:Name="SwingIcon" Text="↕" Foreground="#54D5F4" FontSize="17"/><TextBlock Text="Oscilação" Foreground="#84949D" FontSize="10"/><TextBlock x:Name="SwingValue" Foreground="#E5ECEF" FontSize="11" FontWeight="SemiBold"/></StackPanel></Button></UniformGrid>
  <Grid Grid.Row="5" Margin="0,6,0,0"><TextBlock x:Name="Status" Foreground="#788B95" FontSize="10" VerticalAlignment="Center" MaxWidth="210" HorizontalAlignment="Left" TextTrimming="CharacterEllipsis"/><StackPanel Orientation="Horizontal" HorizontalAlignment="Right"><Button x:Name="Full" Content="↗" ToolTip="Abrir aplicativo completo" Padding="6,0" Background="Transparent" BorderBrush="Transparent"/><Button x:Name="Refresh" Content="↻" ToolTip="Atualizar" Padding="6,0" Background="Transparent" BorderBrush="Transparent"/></StackPanel></Grid>
 </Grid>
</Border>
""";
        var root=(Border)XamlReader.Parse(markup);
        var w=new Window{Title="Clima · controle rápido",Width=338,Height=460,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,AllowsTransparency=true,ShowInTaskbar=false,Topmost=true,Background=Brushes.Transparent,Foreground=Brushes.White,FontFamily=FontFamily,Content=root};
        w.Resources[typeof(Button)]=Resources[typeof(Button)];
        T Get<T>(string name) where T:FrameworkElement => (T)root.FindName(name);
        void Bind(string name,TextBlock source)=>Get<TextBlock>(name).SetBinding(TextBlock.TextProperty,new Binding("Text"){Source=source});
        void Relay(string name,Button source){var b=Get<Button>(name);b.SetBinding(IsEnabledProperty,new Binding("IsEnabled"){Source=source});b.Click+=(_,_)=>{if(source.IsEnabled)source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};}
        Bind("Room",DeviceName);Relay("Power",PowerButton);Relay("Minus",MinusButton);Relay("Plus",PlusButton);Relay("Refresh",RefreshButton);
        Get<Button>("Full").Click+=(_,_)=>{w.Hide();ShowInTaskbar=true;Show();WindowState=WindowState.Normal;Activate();};
        Get<Button>("Swing").Click+=async(_,_)=>await Run(()=>Send(Find("windDirection","rotateUpDown")!,Value("windDirection","rotateUpDown")=="true"?"false":"true"));
        bool menuOpen=false;
        void Menu(string button,Capability? cap,string? current){if(cap is null)return;var menu=new ContextMenu();foreach(var option in cap.Options){var item=new MenuItem{Header=Translate(option),IsCheckable=true,IsChecked=option==current};item.Click+=async(_,_)=>await Run(()=>Send(cap,option));menu.Items.Add(item);}menu.PlacementTarget=Get<Button>(button);menu.Closed+=(_,_)=>{menuOpen=false;w.Activate();};menuOpen=true;menu.IsOpen=true;}
        Get<Button>("Mode").Click+=(_,_)=>Menu("Mode",Find("airConJobMode","currentJobMode"),Value("airConJobMode","currentJobMode"));
        Get<Button>("Fan").Click+=(_,_)=>{var cap=Find("airFlow","windStep");if(cap is null)return;var menu=new ContextMenu();for(int n=(int)cap.Min;n<=cap.Max;n++){string level=n.ToString();var item=new MenuItem{Header="Nível "+level};item.Click+=async(_,_)=>await Run(()=>Send(cap,level));menu.Items.Add(item);}var auto=Find("airFlow","windStrength");if(auto?.Options.Contains("AUTO")==true){var item=new MenuItem{Header="Automático"};item.Click+=async(_,_)=>await Run(()=>Send(auto,"AUTO"));menu.Items.Add(item);}menu.PlacementTarget=Get<Button>("Fan");menu.Closed+=(_,_)=>{menuOpen=false;w.Activate();};menuOpen=true;menu.IsOpen=true;};
        var slider=Get<Slider>("TempSlider");bool dragging=false;
        async Task ApplySlider(){if(!fresh||busy||Temperature is null)return;var value=decimal.Round((decimal)slider.Value);if(value==Target)return;await Run(()=>Send(Temperature,value.ToString(System.Globalization.CultureInfo.InvariantCulture)));}
        slider.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler((_,_)=>dragging=true));
        slider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler(async(_,_)=>{dragging=false;await ApplySlider();}));
        slider.PreviewMouseLeftButtonUp+=async(_,_)=>{if(!dragging)await ApplySlider();};
        slider.PreviewKeyUp+=async(_,e)=>{if(e.Key is System.Windows.Input.Key.Left or System.Windows.Input.Key.Right or System.Windows.Input.Key.Home or System.Windows.Input.Key.End)await ApplySlider();};
        void Sync(){bool ready=fresh&&!busy;Get<TextBlock>("Ambient").Text=AmbientValue.Text+"°C";Get<TextBlock>("Target").Text=Format(ControlTarget);Get<TextBlock>("UnitTemp").Text=Format(ControlTarget);Get<TextBlock>("ModeValue").Text=Translate(Value("airConJobMode","currentJobMode"));Get<TextBlock>("FanValue").Text=Value("airFlow","windStrength")=="AUTO"?"Auto":"Nível "+Format(Number("airFlow","windStep"));bool swing=Value("windDirection","rotateUpDown")=="true";Get<TextBlock>("SwingValue").Text=swing?"Ligada":"Desligada";Get<Button>("Swing").IsEnabled=SwingButton.IsEnabled;Get<Button>("Mode").IsEnabled=ready&&Find("airConJobMode","currentJobMode") is not null;Get<Button>("Fan").IsEnabled=ready&&Find("airFlow","windStep") is not null;Get<Button>("Power").Background=Color(Value("operation","airConOperationMode")=="POWER_ON"?"#52D5F7":"#65747B");Get<TextBlock>("Status").Text=busy?"◌ Ajustando…":fresh?"● Conectado · "+DateTime.Now.ToString("HH:mm"):"Verifique a conexão";Get<TextBlock>("Status").ToolTip=StatusText.Text;slider.IsEnabled=ready&&Temperature is not null;if(Temperature is { } t){slider.Minimum=(double)t.Min;slider.Maximum=(double)t.Max;Get<TextBlock>("MinLabel").Text=Format(t.Min)+"°";Get<TextBlock>("MaxLabel").Text=Format(t.Max)+"°";}if(!dragging)slider.Value=(double)(ControlTarget??18);}
        var airAnimation=new AirflowAnimation(Get<System.Windows.Shapes.Path>("CompactAirStream"),Get<Border>("CompactAirVane"));
        void SyncAll(){Sync();airAnimation.Update(fresh && Value("operation","airConOperationMode")=="POWER_ON",Value("airConJobMode","currentJobMode"),(int)(Number("airFlow","windStep")??3));}
        w.Closed+=(_,_)=>airAnimation.Dispose();
        updateCompact=SyncAll;SyncAll();
        w.Deactivated+=(_,_)=>{if(!menuOpen)w.Hide();};
        w.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)w.Hide();};
        return w;
    }
}



