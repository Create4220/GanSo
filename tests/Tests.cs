using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace GanSo {
 public static class Tests {
  static int passed;
  static void Check(bool ok,string message) { if(!ok) throw new Exception(message); passed++; Console.WriteLine("PASS "+message); }
  public static int Run() {
   string temp=Path.Combine(Path.GetTempPath(),"GanSo-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp); Data.DataDir=temp;
   try {
    Check(Countdown.Format(1500)=="25:00" && Countdown.Format(0)=="00:00" && Countdown.Format(60.01)=="01:01", "digital countdown rounds up and never displays negatives");
    var t=new Countdown(.10); t.Start(); Thread.Sleep(40); t.Pause(); double remain=t.Remaining; Thread.Sleep(80); Check(!t.Tick() && t.Remaining==remain && remain>0,"pause freezes remaining time");
    t.Start(); Thread.Sleep(100); Check(t.Tick() && t.Remaining==0 && !t.Tick(),"completion fires exactly once");
    t.Restore(1500,800); Check(!t.Running && t.Remaining==800,"restored countdown stays paused");
    var list=Data.ParseTasks("{\"tasks\":[{\"name\":\"写作\",\"minutes\":25},\"阅读\",{\"name\":\"写作\",\"minutes\":5,\"seconds\":30}]}",true);
    Check(list.Count==2 && list[0].minutes==5 && list[0].seconds==30,"JSON imports Chinese names and deduplicates by name");
    Check(Data.ParseTasks("- 完成报告\r\n* 阅读资料\r\n",false).Count==2,"TXT task list import");
    bool invalid=false; try{Data.ParseTasks("[{\"name\":\"x\",\"minutes\":0,\"seconds\":0}]",true);}catch(FormatException){invalid=true;} Check(invalid,"zero duration rejected");
    invalid=false;try{Data.ParseTasks("[{\"name\":\"x\",\"minutes\":25,\"seconds\":60}]",true);}catch(FormatException){invalid=true;}Check(invalid,"invalid seconds rejected");
    Program.HandleArgs(new[]{"--task","测试中文 & 符号 $()", "--minutes","0","--seconds","2","--start"});
    string cmd=File.ReadAllText(Directory.GetFiles(Path.Combine(temp,"inbox"),"*.json").Single(),Encoding.UTF8); var command=Data.Json.Deserialize<Command>(cmd);
    Check(command.start && command.task.seconds==2 && command.task.name=="测试中文 & 符号 $()","Codex command interface preserves literal task names");
    var settings=new SettingsData{task="测试",remaining=123,tasks=list}; string file=Path.Combine(temp,"settings.json"); Data.AtomicWrite(file,Data.Json.Serialize(settings)); settings.remaining=99; Data.AtomicWrite(file,Data.Json.Serialize(settings)); Check(Data.Load().remaining==99 && Data.Load().tasks.Count==2,"atomic settings and task persistence");
    var up=new Countdown(1500){CountUp=true};up.Start();Thread.Sleep(60);up.Pause();double elapsed=up.UpElapsed;Thread.Sleep(50);up.Tick();Check(elapsed>=.04 && up.UpElapsed==elapsed,"stopwatch increases and pause excludes idle time");up.Start();Thread.Sleep(50);up.Pause();Check(up.UpElapsed>elapsed,"stopwatch resumes without resetting");
    Check(Countdown.FormatUp(3661.9)=="01:01:01" && Countdown.FormatUp(.9)=="00:00","stopwatch displays whole elapsed seconds and hours");
    var ledger=new SettingsData();FocusLedger.Credit(ledger,"项目",1800);FocusLedger.Credit(ledger,"项目",1980);Check(FocusLedger.Format(FocusLedger.Total(ledger,"项目"))=="1h03min","both focus sessions accumulate for same task");FocusLedger.Credit(ledger,"项目",-1);Check(FocusLedger.Total(ledger,"项目")==3780,"negative focus credit rejected");Check(FocusLedger.Format(3599)=="0h59min","cumulative minutes never round up");
    ledger.focusSeconds["ABC"]=65;Data.AtomicWrite(Path.Combine(temp,"settings.json"),Data.Json.Serialize(ledger));Check(FocusLedger.Total(Data.Load(),"abc")==65,"totals persist with case insensitive task names");
    string imageDir=Path.Combine(Data.Root,"assets","cutouts");var images=Directory.GetFiles(imageDir,"*.png");Check(images.Length==60,"all 60 original-pixel cutouts packaged");
    long removed=0;
    foreach(string image in images){var bitmap=Artwork.Load(image);var a=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);int stride=a.PixelWidth*4;byte[] bytes=new byte[stride*a.PixelHeight];a.CopyPixels(bytes,stride,0);long transparent=0;for(int i=3;i<bytes.Length;i+=4)if(bytes[i]==0)transparent++;if(transparent<a.PixelWidth*a.PixelHeight*.08)throw new Exception("Outer background not removed: "+image);if(bytes[((int)(a.PixelHeight*.55)*a.PixelWidth+(int)(a.PixelWidth*.51))*4+3]<250)throw new Exception("Central face not protected: "+image);removed+=transparent;}
    Check(removed>0,"all cutouts load with transparent backgrounds and protected faces");
    Check(CharacterNames.FromPath("11_全员_01.png")=="KASUMI"&&CharacterNames.FromPath("60_全员_50.png")=="YUNO","full cast naming covers first and last character");
    Check(CharacterNames.FromPath("53_全员_43.png")=="UMIRI"&&CharacterNames.FromPath("50_全员_40.png")=="CHU²","image names match character lettering");
    Check(Directory.GetFiles(imageDir,"*.png").Where(p=>int.Parse(Path.GetFileName(p).Substring(0,2))>=11).All(p=>CharacterNames.FromPath(p).Length>0),"all 50 full-cast images have a character name");
    Check(CharacterNames.FromPath("01_主唱组_01.png")==""&&CharacterNames.FromPath("10_主唱组_10.png")=="","vocalist names remain pending user supplied mapping");
    using(var stream=typeof(MainWindow).Assembly.GetManifestResourceStream("GanSo.Main.xaml")){
     var window=(System.Windows.Window)System.Windows.Markup.XamlReader.Load(stream);window.Measure(new System.Windows.Size(440,600));window.Arrange(new System.Windows.Rect(0,0,440,600));window.UpdateLayout();
     foreach(string name in new[]{"ModeDown","ModeUp"}){var button=(System.Windows.Controls.Button)window.FindName(name);Check(button.Height>=button.FontSize*1.5+button.Padding.Top+button.Padding.Bottom+2,"mode label fits inside "+name);}
     Check(window.FindName("ImageCaption")==null,"image filename and count removed from interface");window.Close();
    }
    var schedule=new RandomCueSchedule(()=>.5);Check(schedule.Remaining==240,"default random interval is 3 to 5 minutes");Check(!schedule.Advance(239)&&schedule.Advance(1)&&schedule.Remaining==240,"one cue at active-time deadline, then a new interval");Check(!schedule.Advance(0)&&schedule.Remaining==240,"pause adds no random cue time");Check(schedule.Advance(10000)&&schedule.Remaining==240,"late wakeup emits one cue without a burst");schedule.Reset(5,5);Check(schedule.Remaining==5,"custom interval reset applies immediately");
    bool badRange=false;try{schedule.Reset(300,180);}catch(ArgumentException){badRange=true;}Check(badRange,"inverted interval range rejected");
    Check(CuePolicy.CompletionCount()==3,"completion always plays three times independently of random checkbox");Check(CuePolicy.CompletionPath("codex.wav")=="codex.wav","completion always uses fixed Codex audio");
    var cues=new SettingsData{randomCues=true,randomMinSeconds=30,randomMaxSeconds=90};Data.AtomicWrite(Path.Combine(temp,"settings.json"),Data.Json.Serialize(cues));Check(Data.Load().randomCues&&Data.Load().randomMinSeconds==30&&Data.Load().randomMaxSeconds==90,"checkbox and interval preferences persist");
    int played=0,active=0,peak=0;using(var audio=new CueAudio((path,token)=>{int now=Interlocked.Increment(ref active);peak=Math.Max(peak,now);Thread.Sleep(8);Interlocked.Increment(ref played);Interlocked.Decrement(ref active);})){Check(audio.Play("codex.wav",3).Wait(2000)&&played==3&&peak==1,"three complete playbacks run sequentially on one channel");}
    int cancelledCalls=0;using(var begun=new ManualResetEvent(false))using(var audio=new CueAudio((path,token)=>{Interlocked.Increment(ref cancelledCalls);begun.Set();token.WaitHandle.WaitOne(2000);token.ThrowIfCancellationRequested();})){var pending=audio.Play("codex.wav",3);Check(begun.WaitOne(1000),"playback starts without blocking UI thread");audio.Stop();Check(pending.Wait(1000)&&cancelledCalls==1,"stop cancels the remaining repetitions");}
    int completedSounds=0;using(var begun=new ManualResetEvent(false))using(var audio=new CueAudio((path,token)=>{if(path=="random.wav"){begun.Set();token.WaitHandle.WaitOne(2000);token.ThrowIfCancellationRequested();}else Interlocked.Increment(ref completedSounds);})){var randomSound=audio.Play("random.wav",1);Check(begun.WaitOne(1000),"random sound can start");var ending=audio.Play("codex.wav",3);Check(ending.Wait(1000)&&randomSound.Wait(1000)&&completedSounds==3,"completion replaces random sound without overlap");}
    foreach(string sound in new[]{"codex-notification.wav","codex-classic.wav"}) { byte[] wav=File.ReadAllBytes(Path.Combine(Data.Root,"assets","sounds",sound)); Check(wav.Length>1000 && Encoding.ASCII.GetString(wav,0,4)=="RIFF","valid packaged "+sound); }
    CheckCompanionInteraction();
    File.WriteAllText(Path.Combine(Data.Root,"tests","results.txt"),"PASS "+passed+" checks\r\n"+DateTime.Now.ToString("s"),Encoding.UTF8);Console.WriteLine("All "+passed+" checks passed.");return 0;
   }catch(Exception ex){Console.Error.WriteLine(ex);File.WriteAllText(Path.Combine(Data.Root,"tests","results.txt"),ex.ToString(),Encoding.UTF8);return 1;}
   finally { if(Directory.Exists(temp)) Directory.Delete(temp,true); }
  }
  static void CheckCompanionInteraction(){
   using(var stream=typeof(MainWindow).Assembly.GetManifestResourceStream("GanSo.Main.xaml")){
    var window=(System.Windows.Window)System.Windows.Markup.XamlReader.Load(stream);
    var surface=(System.Windows.FrameworkElement)window.Content;Action layout=delegate{surface.Measure(new System.Windows.Size(440,600));surface.Arrange(new System.Windows.Rect(0,0,440,600));surface.UpdateLayout();};
    var interaction=new CompanionInteraction(window,null);layout();
    var controls=(System.Windows.FrameworkElement)window.FindName("ControlsPanel");var header=(System.Windows.Controls.Button)window.FindName("CompactToggle");var character=(System.Windows.Controls.Button)window.FindName("CharacterButton");
    var caption=(System.Windows.Controls.TextBlock)window.FindName("CharacterCaption");Check(object.ReferenceEquals(caption.Parent,controls),"character name appears only at bottom of expanded controls");
    Check(!interaction.Expanded&&controls.Visibility==System.Windows.Visibility.Collapsed,"controls collapsed on startup");
    Check(((System.Windows.FrameworkElement)window.FindName("TaskDisplay")).ActualHeight>0&&((System.Windows.FrameworkElement)window.FindName("Clock")).ActualHeight>0&&((System.Windows.FrameworkElement)window.FindName("Progress")).ActualHeight>0,"task, clock and progress remain visible");
    var panel=(System.Windows.FrameworkElement)window.FindName("GlassPanel");var brand=(System.Windows.Controls.TextBlock)window.FindName("BrandTitle");var task=(System.Windows.FrameworkElement)window.FindName("TaskDisplay");var personAnchor=character.TranslatePoint(new System.Windows.Point(),surface);
    Check(window.Width<500,"single column companion uses less desktop width");
    Check(brand.Text=="GanSo"&&brand.TranslatePoint(new System.Windows.Point(),surface).X<task.TranslatePoint(new System.Windows.Point(),surface).X,"formal GanSo title precedes task name");
    Check(panel.TranslatePoint(new System.Windows.Point(0,panel.ActualHeight),surface).Y<=personAnchor.Y,"compact time card stays above character");
    character.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Check(!interaction.Expanded,"character click does not open controls");
    var anchor=header.TranslatePoint(new System.Windows.Point(),surface);header.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));layout();Check(interaction.Expanded&&controls.Visibility==System.Windows.Visibility.Visible,"summary click opens controls");Check(Math.Abs(header.TranslatePoint(new System.Windows.Point(),surface).Y-anchor.Y)<.01,"summary stays in place when controls expand");
    Check(controls.TranslatePoint(new System.Windows.Point(0,controls.ActualHeight),surface).Y<=600,"expanded controls fit window without clipping");
    Check(character.TranslatePoint(new System.Windows.Point(),surface)==personAnchor,"expanding downward does not move character");
    var cover=new System.Windows.Rect(panel.TranslatePoint(new System.Windows.Point(),character),new System.Windows.Size(panel.ActualWidth,panel.ActualHeight));var overlap=System.Windows.Rect.Intersect(new System.Windows.Rect(0,0,character.ActualWidth,character.ActualHeight),cover);
    Check(!overlap.IsEmpty&&character.Clip!=null,"expanded card overlays character instead of pushing it down");
    Check(!character.Clip.FillContains(new System.Windows.Point(overlap.X+overlap.Width/2,overlap.Y+overlap.Height/2)),"overlapping character pixels hidden behind expanded glass");
    character.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Check(interaction.Expanded,"character click does not close controls");header.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));layout();Check(!interaction.Expanded,"second summary click closes controls");Check(character.Clip==null,"collapsing restores complete character silhouette");
    character.ApplyTemplate();var shade=(System.Windows.Shapes.Rectangle)character.Template.FindName("PressShade",character);var key=(System.Windows.DependencyPropertyKey)typeof(System.Windows.Controls.Primitives.ButtonBase).GetField("IsPressedPropertyKey",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
    for(int i=0;i<200;i++){character.SetValue(key,true);if(shade.Opacity!=.18)throw new Exception("Pressed shade missing");character.SetValue(key,false);if(shade.Opacity!=0)throw new Exception("Brightness drift after release");}Check(shade.Opacity==0,"200 press/release cycles restore exact original brightness");
    character.SetValue(key,true);character.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0){RoutedEvent=System.Windows.Input.Mouse.LostMouseCaptureEvent});Check(shade.Opacity==0,"lost mouse capture restores unpressed brightness");
    character.SetValue(key,true);character.IsEnabled=false;Check(shade.Opacity==0,"disabling pressed character clears dark overlay");character.SetValue(key,false);character.IsEnabled=true;Check(shade.Opacity==0,"re-enabling never retains pressed tint");window.Close();
   }
   var gesture=new DragGesture(4,4);gesture.Begin(new System.Windows.Point(10,10));Check(!gesture.Move(new System.Windows.Point(12,12)),"small pointer jitter remains a click");Check(gesture.Move(new System.Windows.Point(15,10))&&gesture.WasDragged,"drag threshold prevents accidental expand on movement");gesture.End();gesture.Begin(new System.Windows.Point());Check(!gesture.WasDragged&&gesture.Tracking,"new click resets previous drag state");gesture.End();
  }
  public static int RunAudioSmoke(){
   var clock=System.Diagnostics.Stopwatch.StartNew();int count=0;Exception failure=null;var lines=new List<string>();
   string path=Path.Combine(Data.Root,"assets","sounds","codex-notification.wav");
   using(var audio=new CueAudio(null,ex=>failure=ex)){
    audio.SoundCompleted+=n=>{Interlocked.Increment(ref count);string line="Completed Codex sound "+n+" at "+clock.ElapsedMilliseconds+" ms";lock(lines)lines.Add(line);Console.WriteLine(line);};
    if(!audio.Play(CuePolicy.CompletionPath(path),CuePolicy.CompletionCount()).Wait(15000)){Console.Error.WriteLine("Audio playback timed out");return 1;}
   }
   bool ok=failure==null&&count==3&&clock.Elapsed.TotalSeconds>=1.45;lines.Add("Result: "+(ok?"PASS":"FAIL")+"; full sounds="+count+"; duration="+clock.Elapsed.TotalSeconds.ToString("F3"));if(failure!=null)lines.Add(failure.ToString());File.WriteAllLines(Path.Combine(Data.Root,"tests","audio-smoke.txt"),lines,Encoding.UTF8);Console.WriteLine(lines.Last());return ok?0:1;
  }
 }
}
