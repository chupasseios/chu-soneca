using System;
namespace FlightWatch {
 public sealed class RecoverySchedule {
  int failures;
  public double Next {get;private set;}
  public double Fail(double now){double delay=Math.Min(60,5*Math.Pow(2,Math.Min(failures++,4)));Next=now+delay;return delay;}
  public void Reset(){failures=0;Next=0;}
 }
}
