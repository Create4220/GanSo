using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;

namespace GanSo {
 // Consumes active focus time, never wall time. A late tick emits at most one cue.
 public sealed class RandomCueSchedule {
  readonly Func<double> sample;
  double minimum,maximum;
  public double Remaining {get;private set;}
  public RandomCueSchedule(Func<double> randomSample=null) {var random=new Random();sample=randomSample??random.NextDouble;Reset(180,300);}
  public static void Validate(int min,int max) {if(min<1||max<min||max>86400)throw new ArgumentException("随机间隔须为 1–86400 秒，且最短不大于最长。");}
  public void Reset(int min,int max){Validate(min,max);minimum=min;maximum=max;Draw();}
  void Draw(){Remaining=minimum+Math.Max(0,Math.Min(1,sample()))*(maximum-minimum);}
  public bool Advance(double activeSeconds){if(activeSeconds<=0||double.IsNaN(activeSeconds)||double.IsInfinity(activeSeconds))return false;Remaining-=activeSeconds;if(Remaining>0)return false;Draw();return true;}
 }
 public static class CuePolicy {
  public static int CompletionCount(){return 3;}
  public static string CompletionPath(string codex){return codex;}
 }
 // One serialized audio channel. Completion replaces any random cue/preview;
 // PlaySync waits for a full WAV before the next repetition, off the UI thread.
 public sealed class CueAudio : IDisposable {
  readonly object gate=new object();
  readonly SemaphoreSlim serial=new SemaphoreSlim(1,1);
  readonly Action<string,CancellationToken> playback;
  readonly Action<Exception> failed;
  CancellationTokenSource cancellation;
  SoundPlayer current;
  bool disposed;
  public event Action<int> SoundCompleted;
  public CueAudio(Action<string,CancellationToken> testPlayback=null,Action<Exception> onFailure=null){playback=testPlayback??PlayOne;failed=onFailure;}
  public Task Play(string path,int count){
   if(count<1||count>3)throw new ArgumentOutOfRangeException("count");
   CancellationTokenSource request;
   lock(gate){if(disposed)throw new ObjectDisposedException("CueAudio");CancelLocked();request=cancellation=new CancellationTokenSource();}
   var token=request.Token;
   return Task.Factory.StartNew(delegate {
    serial.Wait();
    try{for(int i=0;i<count;i++){token.ThrowIfCancellationRequested();playback(path,token);token.ThrowIfCancellationRequested();var completed=SoundCompleted;if(completed!=null)completed(i+1);}}
    catch(OperationCanceledException){}
    catch(Exception ex){if(!token.IsCancellationRequested&&failed!=null)failed(ex);}
    finally{lock(gate){if(object.ReferenceEquals(cancellation,request))cancellation=null;}request.Dispose();serial.Release();}
   });
  }
  void PlayOne(string path,CancellationToken token){
   if(!File.Exists(path))throw new FileNotFoundException("提示音文件不存在。",path);
   using(var sound=new SoundPlayer(path)){
    sound.Load();
    lock(gate){token.ThrowIfCancellationRequested();current=sound;}
    using(token.Register(delegate {try{sound.Stop();}catch(ObjectDisposedException){}})){
     token.ThrowIfCancellationRequested();sound.PlaySync();
    }
    lock(gate){if(object.ReferenceEquals(current,sound))current=null;}
   }
  }
  void CancelLocked(){if(cancellation!=null)cancellation.Cancel();if(current!=null){try{current.Stop();}catch(ObjectDisposedException){}current=null;}}
  public void Stop(){lock(gate)CancelLocked();}
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;CancelLocked();}}
 }
}
