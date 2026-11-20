using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace GanSo {
 public class TaskItem {
  public string name { get; set; }
  public int minutes { get; set; }
  public int seconds { get; set; }
  public override string ToString() { return name; }
 }
 public class SettingsData {
  public Dictionary<string, double> focusSeconds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
  public bool countUp;
  public double upElapsed;
  public bool randomCues;
  public int randomMinSeconds=180;
  public int randomMaxSeconds=300;
  public List<TaskItem> tasks = new List<TaskItem>();
  public string task = "";
  public double remaining = 1500;
  public double duration = 1500;
  public bool pinned = false;
  public string theme = "auto";
  public string sound = "codex-notification.wav";
  public bool soundEnabled = true;
  public string completedDate = "";
  public int completedToday = 0;
  public double left = -1, top = -1;
 }
 public class Command {
  public string action = "import";
  public List<TaskItem> tasks = new List<TaskItem>();
  public TaskItem task;
  public bool start;
 }
 public class Countdown {
  public double Duration { get; private set; }
  public double Remaining { get; private set; }
  public bool Running { get; private set; }
  public bool CountUp { get; set; }
  public double UpElapsed { get; private set; }
  public double Elapsed { get { return CountUp ? UpElapsed : Duration - Remaining; } }
  double before; long started;
  public Countdown(double duration) { Reset(duration); }
  public void Reset(double duration) { Duration = Remaining = Math.Max(0, duration); UpElapsed = 0; Running = false; }
  public void Restore(double duration, double remaining) { Duration = Math.Max(1, duration); Remaining = Math.Max(0, Math.Min(Duration, remaining)); Running = false; }
  public void RestoreUp(double elapsed) { UpElapsed = Math.Max(0,elapsed); Running = false; }
  public void Start() { if (!CountUp && Remaining <= 0) Remaining = Duration; before = CountUp ? UpElapsed : Remaining; started = Stopwatch.GetTimestamp(); Running = true; }
  public bool Tick() {
   if (!Running) return false;
   double delta = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
   if(CountUp) { UpElapsed = before + delta; return false; }
   Remaining = Math.Max(0, before - delta);
   if (Remaining > 0) return false;
   Running = false; return true;
  }
  public bool Pause() { bool done = Tick(); Running = false; return done; }
  public static string Format(double seconds) { int n = (int)Math.Ceiling(Math.Max(0, seconds)); return (n / 60).ToString("00") + ":" + (n % 60).ToString("00"); }
  public static string FormatUp(double seconds) { long n=(long)Math.Floor(Math.Max(0,seconds)); return n>=3600 ? (n/3600).ToString("00")+":"+((n/60)%60).ToString("00")+":"+(n%60).ToString("00") : (n/60).ToString("00")+":"+(n%60).ToString("00"); }
 }
 public static class FocusLedger {
  public static string Format(double seconds) { long m=(long)Math.Floor(Math.Max(0,seconds)/60);return (m/60)+"h"+(m%60).ToString("00")+"min"; }
  public static double Total(SettingsData s,string name) { double n;return s.focusSeconds.TryGetValue((name??"").Trim(),out n)?n:0; }
  public static void Credit(SettingsData s,string name,double seconds) { name=(name??"").Trim();if(name.Length==0||seconds<=0||double.IsNaN(seconds)||double.IsInfinity(seconds))return;s.focusSeconds[name]=Total(s,name)+seconds; }
 }
 public static class Data {
  public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
  public static string DataDir = Path.Combine(Root, "data");
  public static JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
  public static void AtomicWrite(string path, string content) {
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
   File.WriteAllText(temp, content, new UTF8Encoding(false));
   if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
  }
  public static string Text(object o) { return o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture); }
  public static TaskItem ParseTask(object o) {
   string name; int m = 25, s = 0;
   if (o is string) name = (string)o;
   else {
    var d = o as Dictionary<string, object>;
    if (d == null) throw new FormatException("任务应为名称文本，或含 name 的对象。");
    name = d.ContainsKey("name") ? Text(d["name"]) : d.ContainsKey("title") ? Text(d["title"]) : "";
    if (d.ContainsKey("minutes") && !int.TryParse(Text(d["minutes"]), out m)) throw new FormatException("minutes 必须是整数。");
    if (d.ContainsKey("seconds") && !int.TryParse(Text(d["seconds"]), out s)) throw new FormatException("seconds 必须是整数。");
   }
   name = (name ?? "").Trim();
   if (name.Length == 0 || name.Length > 200) throw new FormatException("任务名称须为 1–200 个字符。");
   if (m < 0 || m > 999 || s < 0 || s > 59 || m * 60 + s <= 0) throw new FormatException("时长须为 1 秒至 999 分 59 秒。");
   return new TaskItem { name = name, minutes = m, seconds = s };
  }
  public static List<TaskItem> ParseTasks(string text, bool json) {
   var result = new List<TaskItem>();
   if (json) {
    object value = Json.DeserializeObject(text);
    var dict = value as Dictionary<string, object>;
    if (dict != null && dict.ContainsKey("tasks")) value = dict["tasks"];
    var arr = value as object[];
    if (arr == null) result.Add(ParseTask(value)); else foreach (object o in arr) result.Add(ParseTask(o));
   } else {
    foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
     string name = line.Trim().TrimStart('-', '*', ' ', '\t');
     if (name.Length > 0) result.Add(ParseTask(name));
    }
   }
   if (result.Count > 2000) throw new FormatException("一次最多导入 2000 个任务。");
   return result.GroupBy(t => t.name, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
  }
  public static SettingsData Load() {
   string path = Path.Combine(DataDir, "settings.json");
   if (!File.Exists(path)) return new SettingsData();
   try { var s = Json.Deserialize<SettingsData>(File.ReadAllText(path, Encoding.UTF8)); if (s == null) throw new FormatException(); if (s.tasks == null) s.tasks = new List<TaskItem>(); s.focusSeconds = s.focusSeconds == null ? new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase) : new Dictionary<string,double>(s.focusSeconds,StringComparer.OrdinalIgnoreCase); return s; }
   catch { MessageBox.Show("保存的数据暂时无法读取，将使用默认设置。原文件已保留为 settings.unreadable.json。", "GanSo"); File.Copy(path, Path.Combine(DataDir, "settings.unreadable.json"), true); return new SettingsData(); }
  }
  public static void Queue(Command command) { AtomicWrite(Path.Combine(DataDir, "inbox", DateTime.UtcNow.Ticks + "-" + Guid.NewGuid().ToString("N") + ".json"), Json.Serialize(command)); }
 }
 public static class Artwork {
  public static BitmapSource Load(string path) {
   var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(path);image.DecodePixelWidth=900;image.EndInit();image.Freeze();return image;
  }
 }
 public class MainWindow {
  Window view; SettingsData settings; Countdown timer; DispatcherTimer tick;
  ComboBox taskName; TextBox minutes, seconds; TextBlock clock, status, today, totalFocus;
  Button start, pin; ProgressBar progress; Image character;
  string imagePath; string[] images; Random random = new Random(); CueAudio audio; RandomCueSchedule cueSchedule=new RandomCueSchedule(); double microRestUntil; bool previewPlaying;
  Forms.NotifyIcon tray; bool changing, closing, timerWasStarted; int heartbeat; double accounted; string activeTask; NativeGlass glass; CompanionInteraction interaction;
  public MainWindow() {
   settings = Data.Load(); timer = new Countdown(1500); timer.Restore(settings.duration, settings.remaining); timer.CountUp=settings.countUp; timer.RestoreUp(settings.upElapsed); accounted=timer.Elapsed; timerWasStarted = timer.CountUp ? timer.UpElapsed > 0 : timer.Remaining > 0 && timer.Remaining < timer.Duration;
   using (var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("GanSo.Main.xaml")) view = (Window)XamlReader.Load(stream);
   taskName = Find<ComboBox>("TaskName"); minutes = Find<TextBox>("Minutes"); seconds = Find<TextBox>("Seconds"); clock = Find<TextBlock>("Clock"); status = Find<TextBlock>("Status"); totalFocus = Find<TextBlock>("TotalFocus"); today = Find<TextBlock>("Today"); start = Find<Button>("StartPause"); pin = Find<Button>("Pin"); progress = Find<ProgressBar>("Progress"); character = Find<Image>("Character");
   audio=new CueAudio(null,delegate(Exception ex){if(closing)return;try{view.Dispatcher.BeginInvoke(new Action(delegate{if(!closing)status.Text="提示音播放失败，请在设置中重新选择音频。";}));}catch(InvalidOperationException){}});
   try{cueSchedule.Reset(settings.randomMinSeconds,settings.randomMaxSeconds);}catch(ArgumentException){settings.randomMinSeconds=180;settings.randomMaxSeconds=300;cueSchedule.Reset(180,300);}
   var randomToggle=Find<CheckBox>("RandomCueToggle");randomToggle.IsChecked=settings.randomCues;UpdateCueLabel();
   randomToggle.Click+=delegate{settings.randomCues=randomToggle.IsChecked==true;ResetCues();status.Text=settings.randomCues?"随机休息提示已开启":"随机休息提示已关闭";Save();};
   RefreshTasks(); taskName.Text = settings.task;
   interaction=new CompanionInteraction(view,delegate{if(glass!=null)glass.Sync();});
   SetInputs(timer.Duration); if(timerWasStarted) status.Text = "已恢复上次进度，点击继续专注"; view.Topmost = settings.pinned; UpdatePin();
   if (settings.left >= 0 && settings.top >= 0 && settings.left < SystemParameters.VirtualScreenWidth - 100 && settings.top < SystemParameters.VirtualScreenHeight - 100) { view.WindowStartupLocation = WindowStartupLocation.Manual; view.Left = settings.left; view.Top = settings.top; }
   Find<Grid>("DragArea").MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { if (!(e.OriginalSource is TextBlock) && e.OriginalSource != s) return; try { view.DragMove(); } catch (InvalidOperationException) {} };
   Find<Button>("Minimize").Click += delegate { view.WindowState = WindowState.Minimized; };
   Find<Button>("CloseButton").Click += delegate { view.Close(); };
   pin.Click += delegate { settings.pinned = view.Topmost = !view.Topmost; UpdatePin(); Save(); };
   Find<Button>("Shuffle").Click += delegate { Shuffle(); };
   Find<Button>("SaveTask").Click += delegate { Guard(delegate { SaveCurrentTask(); status.Text = "已保存到任务清单"; }); };
   start.Click += delegate { Guard(ToggleTimer); };
   Find<Button>("Reset").Click += delegate { Guard(delegate { if (timer.Running) { if (MessageBox.Show(view, "重置当前倒计时？", "GanSo", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; } StopSound(); Account(); timer.Reset(ReadDuration()); accounted=0;ResetCues(); timerWasStarted = false; status.Text = "准备好了，就开始吧"; Update(); Save(); }); };
   Find<Button>("Preset5").Click += delegate { Preset(300); }; Find<Button>("Preset25").Click += delegate { Preset(1500); }; Find<Button>("Preset45").Click += delegate { Preset(2700); };
   Find<Button>("Tasks").Click += delegate { Guard(ShowTasks); }; Find<Button>("Settings").Click += delegate { Guard(ShowSettings); };
   minutes.TextChanged += InputsChanged; seconds.TextChanged += InputsChanged;
   taskName.SelectionChanged += delegate { if (changing || timer.Running) return; var item = taskName.SelectedItem as TaskItem; if (item == null) return; SetInputs(item.minutes * 60 + item.seconds); Account(); timer.Reset(item.minutes * 60 + item.seconds); accounted=0;ResetCues(); timerWasStarted = false; Update(); };
   view.PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) { StopSound(); } if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control) { Shuffle(); e.Handled = true; } };
   view.Closing += delegate(object s, System.ComponentModel.CancelEventArgs e) {
    if (timer.Running && MessageBox.Show(view, "倒计时还在进行。退出后会暂停，重新打开可继续。\n要退出 GanSo 吗？", "GanSo", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { e.Cancel = true; return; }
    closing = true; if (timer.Pause()) Complete(); Account(); Save(); tick.Stop(); StopSound(); if (tray != null) tray.Dispose();
   };
   view.Closed+=delegate { audio.Dispose(); if(glass!=null)glass.Dispose(); };
   view.Loaded += delegate { if(glass==null)glass=new NativeGlass(view,Find<Border>("GlassPanel")); ApplyTheme(); if(images==null)LoadImages(); ProcessInbox(); Update(); };
   Find<Button>("ModeDown").Click+=delegate { SetMode(false); };
   Find<Button>("ModeUp").Click+=delegate { SetMode(true); };
   Find<Button>("Finish").Click+=delegate { if(!timer.CountUp||!timerWasStarted)return;timer.Pause();Account();Complete();Update(); };

   tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location), Text = "GanSo · 专注伙伴", Visible = true };
   tray.DoubleClick += delegate { view.Dispatcher.BeginInvoke(new Action(ShowMain)); };
   var menu = new Forms.ContextMenuStrip(); menu.Items.Add("显示 GanSo", null, delegate { view.Dispatcher.BeginInvoke(new Action(ShowMain)); }); menu.Items.Add("退出", null, delegate { view.Dispatcher.BeginInvoke(new Action(delegate { view.Close(); })); }); tray.ContextMenuStrip = menu;
   tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) }; tick.Tick += delegate { if (closing) return; if (Account()) Complete(); Update(); heartbeat++; if (heartbeat % 10 == 0) ProcessInbox(); if (heartbeat % 50 == 0) { ApplyTheme(); Save(); } }; tick.Start();
   Update();
  }
  T Find<T>(string name) where T : class { return view.FindName(name) as T; }
  void Guard(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(view, ex.Message, "GanSo", MessageBoxButton.OK, MessageBoxImage.Information); } }
  void SetInputs(double duration) { changing = true; int d = (int)Math.Ceiling(duration); minutes.Text = (d / 60).ToString(); seconds.Text = (d % 60).ToString("00"); changing = false; }
  int ReadDuration() { int m, s; if (!int.TryParse(minutes.Text, out m) || !int.TryParse(seconds.Text, out s) || m < 0 || m > 999 || s < 0 || s > 59 || m * 60 + s <= 0) throw new FormatException("请输入有效时长：0–999 分、0–59 秒，至少 1 秒。"); return m * 60 + s; }
  void InputsChanged(object sender, TextChangedEventArgs e) { if (changing || timer.Running || timer.CountUp) return; try { Account(); timer.Reset(ReadDuration()); accounted=0;ResetCues(); timerWasStarted = false; status.Text = "准备好了，就开始吧"; Update(); } catch (FormatException) {} }
  void Preset(int n) { if (timer.Running || timer.CountUp) return; StopSound(); Account(); SetInputs(n); timer.Reset(n); accounted=0;ResetCues(); timerWasStarted = false; Update(); Save(); }
  void ToggleTimer() {
   StopSound();
   if (timer.Running) { if (timer.Pause()) { Account(); Complete(); } else { Account(); } if(timer.Remaining>0||timer.CountUp) status.Text = "已暂停，随时可以继续"; }
   else {
    if (taskName.Text.Trim().Length == 0) taskName.Text = "自由专注";
    if (taskName.Text.Trim().Length > 200) throw new FormatException("任务名称最多 200 个字符。");
    if (!timerWasStarted || (!timer.CountUp && timer.Remaining <= 0)) { Account(); timer.Reset(ReadDuration()); accounted=0;ResetCues(); }
    activeTask=taskName.Text.Trim(); if(!settings.tasks.Any(t=>string.Equals(t.name,activeTask,StringComparison.OrdinalIgnoreCase))) SaveCurrentTask();
    timer.Start(); timerWasStarted = true; status.Text = "专注中 · 一次只做一件事";
   }
   Update(); Save();
  }
  void Complete() {
   StopSound(); timerWasStarted = false; ResetToday(); settings.completedToday++; status.Text = "完成了！休息一下吧";
   microRestUntil=0;
   previewPlaying=false;
   if (settings.soundEnabled) audio.Play(CuePolicy.CompletionPath(Path.Combine(Data.Root,"assets","sounds","codex-notification.wav")),CuePolicy.CompletionCount());
   tray.BalloonTipTitle = "GanSo · 专注完成"; tray.BalloonTipText = (taskName.Text.Length == 0 ? "本次专注" : taskName.Text) + " 的倒计时已结束。"; tray.ShowBalloonTip(5000);
   view.Title = "✓ 专注完成 · GanSo"; Save();
  }
  bool Account() {
   bool done=timer.Tick();double delta=Math.Max(0,timer.Elapsed-accounted);if(delta>0)FocusLedger.Credit(settings,activeTask,delta);accounted=timer.Elapsed;
   if(!closing&&timer.Running&&!done&&settings.randomCues&&cueSchedule.Advance(delta)){previewPlaying=false;audio.Play(SoundPath(),1);microRestUntil=timer.Elapsed+10;}
   return done;
  }
  void ResetCues(){cueSchedule.Reset(settings.randomMinSeconds,settings.randomMaxSeconds);microRestUntil=0;}
  void UpdateCueLabel(){Find<CheckBox>("RandomCueToggle").Content="随机休息提示（"+IntervalText(settings.randomMinSeconds)+"–"+IntervalText(settings.randomMaxSeconds)+"）";}
  static string IntervalText(int seconds){return seconds%60==0?(seconds/60)+"分":seconds+"秒";}
  void SetMode(bool up) {
   if(timer.Running||timer.CountUp==up)return;Account();timer.CountUp=up;timer.Reset(ReadDuration());accounted=0;ResetCues();timerWasStarted=false;StopSound();status.Text=up?"从 00:00 开始，记录每一分投入":"准备好了，就开始吧";ApplyTheme();Update();Save();
  }
  void ResetToday() { string date = DateTime.Now.ToString("yyyy-MM-dd"); if (settings.completedDate != date) { settings.completedDate = date; settings.completedToday = 0; } }
  void Update() {
   clock.Text = timer.CountUp ? Countdown.FormatUp(timer.UpElapsed) : Countdown.Format(timer.Remaining); clock.FontSize=clock.Text.Length>5?52:68; progress.Value = timer.CountUp ? 0 : timer.Duration <= 0 ? 0 : 1 - timer.Remaining / timer.Duration;
   progress.Visibility=Visibility.Visible;progress.IsIndeterminate=timer.CountUp&&timer.Running;progress.ToolTip=timer.CountUp?"正计时：无固定目标时长":"本次倒计时进度";
   Find<TextBlock>("TaskDisplay").Text=string.IsNullOrWhiteSpace(taskName.Text)?"自由专注":taskName.Text;
   Find<StackPanel>("DurationInputs").Visibility=timer.CountUp?Visibility.Collapsed:Visibility.Visible;
   Find<Button>("Finish").Visibility=timer.CountUp?Visibility.Visible:Visibility.Collapsed;
   Find<Button>("Finish").IsEnabled=timerWasStarted;
   Find<Button>("ModeDown").IsEnabled=Find<Button>("ModeUp").IsEnabled=!timer.Running;
   totalFocus.Text="累计 "+FocusLedger.Format(FocusLedger.Total(settings,taskName.Text));
   if(timer.Running&&microRestUntil>0){double rest=microRestUntil-timer.Elapsed;if(rest>0)status.Text="闭眼放松 "+((int)Math.Ceiling(rest))+" 秒 · 计时继续";else{microRestUntil=0;status.Text="专注中 · 一次只做一件事";}}
   start.Content = timer.Running ? "暂停" : (timerWasStarted && (timer.CountUp || timer.Remaining > 0) ? "继续专注" : "开始专注");
   minutes.IsEnabled = seconds.IsEnabled = taskName.IsEnabled = !timer.Running;
   Find<Button>("Preset5").IsEnabled = Find<Button>("Preset25").IsEnabled = Find<Button>("Preset45").IsEnabled = !timer.Running;
   ResetToday(); today.Text = settings.completedToday > 0 ? "今天已完成 " + settings.completedToday + " 次专注" : "今天，从一个小目标开始";
   if (timer.Running) { view.Title = clock.Text + " · " + taskName.Text + " · GanSo"; tray.Text = ("GanSo " + clock.Text + " " + taskName.Text).Substring(0, Math.Min(63, ("GanSo " + clock.Text + " " + taskName.Text).Length)); }
  }
  void Save() {
   if(timer.Running && Account()) Complete();
   settings.countUp=timer.CountUp; settings.upElapsed=timer.UpElapsed; settings.task = taskName.Text.Trim(); settings.remaining = timer.Remaining; settings.duration = timer.Duration; settings.left = view.Left; settings.top = view.Top;
   try { Data.AtomicWrite(Path.Combine(Data.DataDir, "settings.json"), Data.Json.Serialize(settings)); } catch (Exception ex) { status.Text = "保存失败：" + ex.Message; }
  }
  void RefreshTasks() { string text = taskName.Text; changing = true; taskName.ItemsSource = null; taskName.ItemsSource = settings.tasks; taskName.Text = text; changing = false; }
  int MergeTasks(IEnumerable<TaskItem> list) {
   int n = 0; foreach (var t in list) { var old = settings.tasks.FirstOrDefault(x => string.Equals(x.name, t.name, StringComparison.OrdinalIgnoreCase)); if (old != null) { old.minutes = t.minutes; old.seconds = t.seconds; } else { settings.tasks.Add(t); n++; } } RefreshTasks(); Save(); return n;
  }
  void SaveCurrentTask() { int d = ReadDuration(); var t = Data.ParseTask(new Dictionary<string, object> { { "name", taskName.Text }, { "minutes", d / 60 }, { "seconds", d % 60 } }); MergeTasks(new[] { t }); }
  void LoadImages() {
   string dir = Path.Combine(Data.Root, "assets", "cutouts");
   images = Directory.Exists(dir) ? Directory.GetFiles(dir).Where(p => new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(p).ToLowerInvariant())).OrderBy(p => p).ToArray() : new string[0]; Shuffle();
  }
  void Shuffle() {
   if (images == null || images.Length == 0) { status.Text = "未找到角色图片"; return; }
   var choices = images.Where(p => p != imagePath).ToArray(); if (choices.Length == 0) choices = images;
   imagePath = choices[random.Next(choices.Length)];
   try { character.Source = Artwork.Load(imagePath); Find<Button>("CharacterButton").Tag=character.Source; string name=CharacterNames.FromPath(imagePath);var label=Find<TextBlock>("CharacterCaption");label.Text=name.Length>0?"当前形象："+name:"";label.Visibility=name.Length>0?Visibility.Visible:Visibility.Collapsed; } catch (Exception ex) { status.Text = "这张图片无法读取："+ex.Message; }
  }
  string SoundPath() { return Path.IsPathRooted(settings.sound) ? settings.sound : Path.Combine(Data.Root, "assets", "sounds", settings.sound); }
  void PlaySound() {
   previewPlaying=true;
   audio.Play(SoundPath(),1);
  }
  void StopSound() { previewPlaying=false;if(audio!=null)audio.Stop(); }
  void UpdatePin() { pin.Content = view.Topmost ? "已置顶" : "置顶"; }
  void ShowMain() { view.Show(); view.WindowState = WindowState.Normal; view.Activate(); }
  bool Dark() {
   if (settings.theme != "auto") return settings.theme == "dark";
   try { object value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1); return Convert.ToInt32(value) == 0; } catch { return false; }
  }
  void ApplyTheme() {
   Brush fg = Dark() ? Brushes.White : Brushes.Black;
   view.Resources["ThemeForeground"] = fg;
   view.Resources["PopupBackground"] = Dark() ? new SolidColorBrush(Color.FromRgb(37,37,41)) : Brushes.White;
   view.Foreground = fg;
   ThemeChildren(view, fg, Dark());
   Find<Button>("ModeDown").Background=new SolidColorBrush(Color.FromArgb(!timer.CountUp?(byte)90:(byte)18,128,128,128));
   Find<Button>("ModeUp").Background=new SolidColorBrush(Color.FromArgb(timer.CountUp?(byte)90:(byte)18,128,128,128));
   if(glass!=null)glass.Theme(Dark());
  }
  static void ThemeChildren(DependencyObject parent, Brush fg, bool dark) {
   for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
    var child = VisualTreeHelper.GetChild(parent, i);
    var text = child as TextBlock; if (text != null) text.Foreground = fg;
    var control = child as Control; if (control != null) control.Foreground = fg;
    var button = child as Button; if (button != null) { button.Background = new SolidColorBrush(Color.FromArgb(38, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0)); button.BorderBrush = fg; }
    var input = child as TextBox; if (input != null) { input.Background = new SolidColorBrush(Color.FromArgb(25, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0)); input.BorderBrush = new SolidColorBrush(Color.FromArgb(80, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0)); input.CaretBrush = fg; }
    var bar = child as ProgressBar; if (bar != null) { bar.Foreground = fg; bar.Background = new SolidColorBrush(Color.FromArgb(40, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0)); }
    ThemeChildren(child, fg, dark);
   }
   var combo = parent as ComboBox;
   if (combo != null) {
    combo.Background = Brushes.Transparent;
    combo.ApplyTemplate();var editable = combo.Template.FindName("PART_EditableTextBox", combo) as TextBox; if (editable != null) { editable.Foreground = fg; editable.Background = Brushes.Transparent; editable.CaretBrush = fg; }
    foreach (var item in combo.Items) { var container = combo.ItemContainerGenerator.ContainerFromItem(item) as ComboBoxItem; if (container != null) { container.Background = dark ? new SolidColorBrush(Color.FromRgb(40,40,44)) : Brushes.White; container.Foreground = fg; } }
   }
  }
  Window Dialog(string title, int width, int height, UIElement content) {
   return new Window { Title = "GanSo · " + title, Width = width, Height = height, MinWidth = width, MinHeight = height, Owner = view, WindowStartupLocation = WindowStartupLocation.CenterOwner, FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = 13, Background = Dark() ? new SolidColorBrush(Color.FromRgb(35,35,40)) : new SolidColorBrush(Color.FromRgb(250,249,253)), Foreground = Dark() ? Brushes.White : Brushes.Black, Content = content };
  }
  static Button Btn(string text, Action action) { var b = new Button { Content = text, Padding = new Thickness(10,7,10,7), Margin = new Thickness(3) }; b.Click += delegate { action(); }; return b; }
  void ShowTasks() {
   var grid = new Grid { Margin = new Thickness(18) }; grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
   var help = new TextBlock { Text = "任务名称                                      累计专注\n正计时和倒计时均累计；暂停和退出不计入。", Margin = new Thickness(0,0,0,10) }; grid.Children.Add(help);
   var list = new ListBox { SelectionMode = SelectionMode.Extended };
   var template=new DataTemplate(); var rowFactory=new FrameworkElementFactory(typeof(Grid)); var nameFactory=new FrameworkElementFactory(typeof(TextBlock)); nameFactory.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("name")); nameFactory.SetValue(FrameworkElement.MarginProperty,new Thickness(10,10,130,10)); var timeFactory=new FrameworkElementFactory(typeof(TextBlock)); timeFactory.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("name"){Converter=new FocusValueConverter(settings)}); timeFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right);timeFactory.SetValue(FrameworkElement.MarginProperty,new Thickness(10));rowFactory.AppendChild(nameFactory);rowFactory.AppendChild(timeFactory);template.VisualTree=rowFactory;list.ItemTemplate=template;
   var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));list.ItemContainerStyle=itemStyle;
   Action refresh = delegate { list.ItemsSource = null; list.ItemsSource = settings.tasks; }; refresh(); Grid.SetRow(list,1); grid.Children.Add(list);
   Window dialog = null;
   Action use = delegate { var item = list.SelectedItem as TaskItem; if (item == null) return; if (timer.Running) { MessageBox.Show(dialog,"请先暂停当前倒计时，再切换任务。"); return; } taskName.Text = item.name; SetInputs(item.minutes*60+item.seconds); Account();timer.Reset(item.minutes*60+item.seconds);accounted=0;ResetCues(); timerWasStarted = false; status.Text="已选择任务"; Update(); Save(); dialog.Close(); };
   list.MouseDoubleClick += delegate { use(); };
   var row = new WrapPanel { Margin = new Thickness(0,10,0,0) }; Grid.SetRow(row,2); grid.Children.Add(row);
   row.Children.Add(Btn("使用所选", use));
   row.Children.Add(Btn("导入清单…", delegate { Guard(delegate { var file = new OpenFileDialog { Filter = "任务清单|*.json;*.txt|全部文件|*.*" }; if (file.ShowDialog(dialog) == true) { int n = MergeTasks(Data.ParseTasks(File.ReadAllText(file.FileName, Encoding.UTF8), Path.GetExtension(file.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase))); refresh(); MessageBox.Show(dialog, "已新增 " + n + " 个任务；同名任务已更新时长。"); } }); }));
   row.Children.Add(Btn("删除所选", delegate { var items = list.SelectedItems.Cast<TaskItem>().ToList(); if (items.Count == 0) return; if (MessageBox.Show(dialog,"从 GanSo 清单中删除所选的 " + items.Count + " 个任务？","GanSo",MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; foreach(var item in items) settings.tasks.Remove(item); RefreshTasks(); refresh(); Save(); }));
   var note = new TextBlock { Text = "双击任务即可选用。导入同名任务会保留累计时长。", Foreground = Brushes.Gray, Margin = new Thickness(0,12,0,0), TextWrapping = TextWrapping.Wrap }; Grid.SetRow(note,3); grid.Children.Add(note);
   dialog = Dialog("任务清单", 570, 440, grid);
   var live=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};live.Tick+=delegate {var selected=list.SelectedItems.Cast<TaskItem>().ToList();list.Items.Refresh();foreach(var item in selected)list.SelectedItems.Add(item);};dialog.Closed+=delegate {live.Stop();};live.Start();dialog.ShowDialog(); ApplyTheme();
  }
  void ShowSettings() {
   var panel = new StackPanel { Margin = new Thickness(22) };
   panel.Children.Add(new TextBlock { Text = "外观", FontSize = 17, Margin = new Thickness(0,0,0,9) });
   var themes = new ComboBox { ItemsSource = new[] { "跟随系统", "深色 · 白字", "浅色 · 黑字" }, SelectedIndex = settings.theme == "dark" ? 1 : settings.theme == "light" ? 2 : 0, Padding = new Thickness(5) }; panel.Children.Add(themes);
   var enabled = new CheckBox { Content = "完成时播放提示音", Foreground = Dark() ? Brushes.White : Brushes.Black, IsChecked = settings.soundEnabled, Margin = new Thickness(0,20,0,10) }; panel.Children.Add(enabled); enabled.Click += delegate { settings.soundEnabled = enabled.IsChecked == true; Save(); };
   var sounds = new ComboBox { ItemsSource = new[] { "Codex 任务完成提示音", "Codex 经典提示音", "自定义 WAV 文件…" }, SelectedIndex = Path.IsPathRooted(settings.sound) ? 2 : settings.sound == "codex-classic.wav" ? 1 : 0, Padding = new Thickness(5) }; panel.Children.Add(sounds);
   var soundInfo = new TextBlock { Text = Path.GetFileName(settings.sound), Margin = new Thickness(0,7,0,0), Foreground = Brushes.Gray }; panel.Children.Add(soundInfo);
   Window dialog = null;
   sounds.SelectionChanged += delegate { Guard(delegate { if(sounds.SelectedIndex == 2) { var file = new OpenFileDialog { Filter="WAV 提示音|*.wav" }; if (file.ShowDialog(dialog) != true) { sounds.SelectedIndex = settings.sound == "codex-classic.wav" ? 1 : 0; return; } string dest = Path.Combine(Data.DataDir,"custom-sound.wav"); if (!string.Equals(file.FileName,dest,StringComparison.OrdinalIgnoreCase)) File.Copy(file.FileName,dest,true); settings.sound = dest; } else settings.sound = sounds.SelectedIndex == 1 ? "codex-classic.wav" : "codex-notification.wav"; soundInfo.Text=Path.GetFileName(settings.sound); Save(); }); };
   var row = new WrapPanel { Margin = new Thickness(0,10,0,10) }; row.Children.Add(Btn("试听提示音", delegate { StopSound(); PlaySound(); })); row.Children.Add(Btn("停止试听", StopSound)); panel.Children.Add(row);
   panel.Children.Add(new TextBlock{Text="随机休息提示的间隔",FontSize=15,Margin=new Thickness(0,9,0,8)});
   var intervals=new WrapPanel();var minInput=new TextBox{Text=settings.randomMinSeconds.ToString(),Width=65,Padding=new Thickness(6),Margin=new Thickness(3)};var maxInput=new TextBox{Text=settings.randomMaxSeconds.ToString(),Width=65,Padding=new Thickness(6),Margin=new Thickness(3)};
   intervals.Children.Add(new TextBlock{Text="最短",VerticalAlignment=VerticalAlignment.Center});intervals.Children.Add(minInput);intervals.Children.Add(new TextBlock{Text="秒，最长",VerticalAlignment=VerticalAlignment.Center});intervals.Children.Add(maxInput);intervals.Children.Add(new TextBlock{Text="秒",VerticalAlignment=VerticalAlignment.Center});
   intervals.Children.Add(Btn("应用",delegate{Guard(delegate{int min,max;if(!int.TryParse(minInput.Text,out min)||!int.TryParse(maxInput.Text,out max))throw new ArgumentException("请输入整数秒数。");RandomCueSchedule.Validate(min,max);settings.randomMinSeconds=min;settings.randomMaxSeconds=max;ResetCues();UpdateCueLabel();Save();status.Text="随机提示间隔已更新";});}));panel.Children.Add(intervals);
   panel.Children.Add(new TextBlock{Text="主界面勾选后生效，默认 180–300 秒。\n随机提示沿用上方所选声音，每次播放一次；\n无论是否勾选，完成时均播放三次 Codex 提示音。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)});
   panel.Children.Add(new TextBlock { Text = "窗口背景透明，可拖动顶部标题移动。\n置顶按钮可让伙伴一直陪在桌面上。\n关闭窗口会暂停；最小化后继续计时。\n任务与设置保存在程序旁的 data 文件夹。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,9,0,0) });
   dialog = Dialog("设置", 470, 630, panel);
   themes.SelectionChanged += delegate { settings.theme = themes.SelectedIndex == 1 ? "dark" : themes.SelectedIndex == 2 ? "light" : "auto"; ApplyTheme(); dialog.Background = Dark() ? new SolidColorBrush(Color.FromRgb(35,35,40)) : new SolidColorBrush(Color.FromRgb(250,249,253)); dialog.Foreground = enabled.Foreground = Dark() ? Brushes.White : Brushes.Black; Save(); };
   dialog.Closed += delegate { if(previewPlaying)StopSound(); }; dialog.ShowDialog(); ApplyTheme();
  }
  void ProcessInbox() {
   string dir = Path.Combine(Data.DataDir,"inbox"); if(!Directory.Exists(dir)) return;
   foreach (string path in Directory.GetFiles(dir,"*.json").OrderBy(p=>p)) {
    try {
     if(new FileInfo(path).Length > 4*1024*1024) throw new FormatException("导入文件过大。");
     Command command = Data.Json.Deserialize<Command>(File.ReadAllText(path,Encoding.UTF8)); if(command == null) throw new FormatException("空命令。");
     if(command.action == "show") { ShowMain(); }
     else if(command.action == "import") {
      var list = command.tasks.Select(t=>Data.ParseTask(new Dictionary<string,object>{{"name",t.name},{"minutes",t.minutes},{"seconds",t.seconds}})).ToList();
      int count = MergeTasks(list); status.Text = "已接收任务清单 · 新增 " + count + " 项";
     } else if(command.action == "select") {
      if(timer.Running) continue; // Leave pending selection in inbox until the active countdown is paused/completed.
      var t = Data.ParseTask(new Dictionary<string,object>{{"name",command.task.name},{"minutes",command.task.minutes},{"seconds",command.task.seconds}});
      MergeTasks(new[]{t}); taskName.Text=t.name; SetInputs(t.minutes*60+t.seconds); Account();timer.Reset(t.minutes*60+t.seconds);accounted=0;ResetCues(); timerWasStarted=false; status.Text="已接收任务："+t.name; if(command.start) ToggleTimer(); Update(); Save(); ShowMain();
     } else throw new FormatException("未知指令。");
     File.Delete(path);
    } catch(Exception ex) {
     string failed = Path.Combine(Data.DataDir,"rejected",Path.GetFileName(path));
     try { Directory.CreateDirectory(Path.GetDirectoryName(failed)); File.Move(path,failed); File.WriteAllText(failed+".error.txt",ex.Message,Encoding.UTF8); } catch { }
     status.Text="任务导入失败，详情见 data/rejected";
    }
   }
  }
  public void Run() { Application.Current.Run(view); }
 }
 public class FocusValueConverter : System.Windows.Data.IValueConverter {
  SettingsData data; public FocusValueConverter(SettingsData s){data=s;}
  public object Convert(object v,Type t,object p,CultureInfo c){return FocusLedger.Format(FocusLedger.Total(data,Data.Text(v)));}
  public object ConvertBack(object v,Type t,object p,CultureInfo c){throw new NotSupportedException();}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args) {
#if TEST_RUNNER
   return args.Length>0&&args[0]=="--audio-smoke-test"?Tests.RunAudioSmoke():Tests.Run();
#else
   try {
    bool queued = HandleArgs(args);
    bool created; using(var mutex=new Mutex(true,"Local\\GanSo-"+StableId(Data.Root),out created)) {
     if(!created) { if(!queued) Data.Queue(new Command{action="show"}); return 0; }
     try { var app = new Application { ShutdownMode=ShutdownMode.OnMainWindowClose }; app.DispatcherUnhandledException += delegate(object s,DispatcherUnhandledExceptionEventArgs e){ MessageBox.Show(e.Exception.Message,"GanSo"); e.Handled=true; }; new MainWindow().Run(); }
     finally { mutex.ReleaseMutex(); }
    } return 0;
   } catch(Exception ex) { MessageBox.Show(ex.Message,"GanSo",MessageBoxButton.OK,MessageBoxImage.Error); return 1; }
#endif
  }
  static string StableId(string text) { using(var sha=System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()))).Replace("-","").Substring(0,16); }
  public static bool HandleArgs(string[] args) {
   if(args.Length==0) return false;
   if(args[0]=="--import-tasks" && args.Length==2) { string path=Path.GetFullPath(args[1]); if(new FileInfo(path).Length>4*1024*1024) throw new FormatException("任务清单最多 4 MB。"); Data.Queue(new Command { action="import",tasks=Data.ParseTasks(File.ReadAllText(path,Encoding.UTF8),Path.GetExtension(path).Equals(".json",StringComparison.OrdinalIgnoreCase)) }); return true; }
   if(args[0]=="--task") {
    if(args.Length<2) throw new FormatException("--task 后需要任务名称。");
    int m=25,s=0; bool start=false;
    for(int i=2;i<args.Length;i++) { if(args[i]=="--start") start=true; else if(args[i]=="--minutes" && i+1<args.Length) m=int.Parse(args[++i]); else if(args[i]=="--seconds" && i+1<args.Length) s=int.Parse(args[++i]); else throw new FormatException("未知参数："+args[i]); }
    var t=Data.ParseTask(new Dictionary<string,object>{{"name",args[1]},{"minutes",m},{"seconds",s}}); Data.Queue(new Command { action="select",task=t,start=start }); return true;
   }
   if(args[0]=="--show" && args.Length==1) { Data.Queue(new Command{action="show"}); return true; }
   throw new FormatException("用法：\nGanSo.exe\nGanSo.exe --task \"任务名称\" --minutes 25 [--seconds 0] [--start]\nGanSo.exe --import-tasks \"tasks.json\"\nGanSo.exe --show");
  }
 }
}
