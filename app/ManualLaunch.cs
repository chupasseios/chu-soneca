using System.Security.Principal;
namespace FlightWatch {
 public static class ManualLaunch {
  public static string EventName=>@"Local\ChuSoneca.Open."+WindowsIdentity.GetCurrent().User.Value;
 }
}
