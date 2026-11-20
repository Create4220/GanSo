using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GanSo {
 public sealed class DragGesture {
  Point origin;readonly double horizontal,vertical;
  public bool Tracking {get;private set;}
  public bool WasDragged {get;private set;}
  public DragGesture(double x,double y){horizontal=x;vertical=y;}
  public void Begin(Point p){origin=p;Tracking=true;WasDragged=false;}
  public bool Move(Point p){if(!Tracking||Math.Abs(p.X-origin.X)<horizontal&&Math.Abs(p.Y-origin.Y)<vertical)return false;WasDragged=true;Tracking=false;return true;}
  public void End(){Tracking=false;}
  public void Reset(){Tracking=false;WasDragged=false;}
 }
 public sealed class CompanionInteraction {
  readonly Window view;readonly Action changed;
  readonly Button summary,character;
  readonly DragGesture summaryDrag,characterDrag;
  public bool Expanded {get;private set;}
  public CompanionInteraction(Window window,Action onLayoutChanged){
   view=window;changed=onLayoutChanged;
   summary=(Button)view.FindName("CompactToggle");character=(Button)view.FindName("CharacterButton");
   summaryDrag=AttachDrag(summary);characterDrag=AttachDrag(character);
   summary.Click+=delegate(object sender,RoutedEventArgs e){if(!summaryDrag.WasDragged)SetExpanded(!Expanded);e.Handled=true;};
   character.Click+=delegate(object sender,RoutedEventArgs e){e.Handled=true;};
   view.Deactivated+=delegate{summaryDrag.End();characterDrag.End();Release(summary);Release(character);};
   view.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){summaryDrag.End();characterDrag.End();Release(summary);Release(character);}};
   SetExpanded(false);
  }
  public void SetExpanded(bool expanded){
   Expanded=expanded;((FrameworkElement)view.FindName("ControlsPanel")).Visibility=expanded?Visibility.Visible:Visibility.Collapsed;
   ((TextBlock)view.FindName("Disclosure")).Text=expanded?"⌃":"⌄";
   summary.ToolTip=expanded?"点击收起控件；按住拖动位置":"点击展开控件；按住拖动位置";
   view.UpdateLayout();if(changed!=null)changed();
  }
  static void Release(Button button){if(Mouse.Captured==button)button.ReleaseMouseCapture();}
  DragGesture AttachDrag(Button button){
   var gesture=new DragGesture(SystemParameters.MinimumHorizontalDragDistance,SystemParameters.MinimumVerticalDragDistance);
   button.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){gesture.Begin(button.PointToScreen(e.GetPosition(button)));};
   button.PreviewMouseMove+=delegate(object sender,MouseEventArgs e){
    if(e.LeftButton!=MouseButtonState.Pressed){gesture.End();return;}
    if(!gesture.Move(button.PointToScreen(e.GetPosition(button))))return;
    Release(button);
    try{view.DragMove();}catch(InvalidOperationException){}finally{gesture.End();Release(button);}
    e.Handled=true;
   };
   button.PreviewMouseLeftButtonUp+=delegate{gesture.End();};
   button.LostMouseCapture+=delegate{gesture.End();};
   button.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Space||e.Key==Key.Enter)gesture.Reset();};
   return gesture;
  }
 }
}
