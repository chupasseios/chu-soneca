using System;
namespace FlightWatch {
 public interface ITelemetrySource:IDisposable {Action<Telemetry> Received {get;set;} Action<string> Error {get;set;} void Open();void Tick();}
}
