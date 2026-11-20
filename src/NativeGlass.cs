using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
namespace GanSo {
 // A non-layered compositor surface behind the layered character/control window.
 // Windows supplies live Desktop Acrylic; foreground artwork is never blurred.
 public sealed class NativeGlass : IDisposable {
  Window main,plate; FrameworkElement panel; IntPtr handle; bool disposed;
  [StructLayout(LayoutKind.Sequential)] struct Margins { public int left,right,top,bottom; }
  [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref Margins m);
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h,int n);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h,int n,int value);
  [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
  [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
  int lastWidth,lastHeight;
  public bool Available {get;private set;}
  public NativeGlass(Window owner,FrameworkElement target) {
   main=owner;panel=target;
   plate=new Window { Title="GanSo · Glass",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Focusable=false,Background=Brushes.Transparent,Width=target.ActualWidth,Height=target.ActualHeight };
   plate.SourceInitialized+=delegate {
    handle=new WindowInteropHelper(plate).Handle;
    SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x08000080);
    HwndSource.FromHwnd(handle).CompositionTarget.BackgroundColor=Colors.Transparent;
    var margins=new Margins {left=-1,right=-1,top=-1,bottom=-1};DwmExtendFrameIntoClientArea(handle,ref margins);
    int backdrop=3;Available=DwmSetWindowAttribute(handle,38,ref backdrop,4)==0;
    int corners=1;DwmSetWindowAttribute(handle,33,ref corners,4);
   };
   plate.Show();main.Owner=plate;
   main.LocationChanged+=SyncEvent;main.SizeChanged+=SyncSize;main.StateChanged+=SyncEvent;main.Activated+=SyncEvent;
   Sync();
  }
  void SyncEvent(object sender,EventArgs e){Sync();}
  void SyncSize(object sender,SizeChangedEventArgs e){Sync();}
  public void Theme(bool dark){if(disposed)return;int v=dark?1:0;DwmSetWindowAttribute(handle,20,ref v,4);if(!Available)plate.Background=dark?new SolidColorBrush(Color.FromRgb(40,43,48)):new SolidColorBrush(Color.FromRgb(232,237,240));Sync();}
  public void Sync(){
   if(disposed||handle==IntPtr.Zero||!main.IsLoaded)return;
   if(main.WindowState==WindowState.Minimized||!main.IsVisible){plate.Hide();return;}
   Point p=panel.PointToScreen(new Point());var source=PresentationSource.FromVisual(main);if(source==null)return;var scale=source.CompositionTarget.TransformToDevice;
   plate.Topmost=main.Topmost;
   if(!plate.IsVisible)plate.Show();
   SetWindowPos(handle,new WindowInteropHelper(main).Handle,(int)p.X,(int)p.Y,(int)Math.Ceiling(panel.ActualWidth*scale.M11),(int)Math.Ceiling(panel.ActualHeight*scale.M22),0x0010|0x0200);
   int w=(int)Math.Ceiling(panel.ActualWidth*scale.M11),h=(int)Math.Ceiling(panel.ActualHeight*scale.M22);
   if(w!=lastWidth||h!=lastHeight){IntPtr region=CreateRoundRectRgn(0,0,w+1,h+1,(int)Math.Round(60*scale.M11),(int)Math.Round(60*scale.M22));if(SetWindowRgn(handle,region,true)==0)DeleteObject(region);lastWidth=w;lastHeight=h;}
  }
  public void Dispose(){if(disposed)return;disposed=true;main.LocationChanged-=SyncEvent;main.SizeChanged-=SyncSize;main.StateChanged-=SyncEvent;main.Activated-=SyncEvent;plate.Close();}
 }
}
