using System;
using FlightWatch;
static class PlanTests {
 static int count;static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 public static void Run(){
  double feet;Check(Monitor.TryPlanAltitude("35000",out feet)&&feet==35000,"plan feet parsed");Check(Monitor.TryPlanAltitude("350",out feet)&&feet==35000,"Radar compact level convention parsed");Check(Monitor.TryPlanAltitude("FL350",out feet)&&feet==35000,"explicit flight level parsed");Check(Monitor.TryPlanAltitude("F350",out feet)&&feet==35000,"F level parsed");Check(!Monitor.TryPlanAltitude("VFR",out feet)&&!Monitor.TryPlanAltitude("",out feet)&&!Monitor.TryPlanAltitude("-350",out feet)&&!Monitor.TryPlanAltitude("99999",out feet),"invalid altitude has no fallback");
  var m=new Monitor{SelectedCid="7654321"};m.Connect("7654321","TEST123","A320",1000);Check(m.CruiseFL==0,"new session has no assumed cruise level");
  var p=new Plan{cid="7654321",callsign="TEST123",altitude="35000"};m.ApplyFlightPlan(p);Check(m.CruiseFL==350,"matching network plan applied automatically");m.Start();Check(m.CruiseFL==350,"starting monitor retains fetched plan");m.CruiseSeen=true;m.DescentTriggered=true;m.Deadline=1300;m.ApplyFlightPlan(p);Check(m.Deadline==1300&&m.CruiseSeen,"unchanged plan preserves descent tracking");p.altitude="37000";m.ApplyFlightPlan(p);Check(m.CruiseFL==370&&!m.CruiseSeen&&!m.DescentPending&&m.Deadline<0,"changed plan cancels descent based on old level");
  p.cid="111111";m.ApplyFlightPlan(p);Check(m.CruiseFL==0,"another CID cannot supply cruise level");p.cid="7654321";p.callsign="OTHER123";m.ApplyFlightPlan(p);Check(m.CruiseFL==0,"another flight cannot supply cruise level");p.callsign="TEST123";m.ApplyFlightPlan(p);m.ApplyFlightPlan(null);Check(m.CruiseFL==0,"missing plan clears previous cruise reference");m.ApplyFlightPlan(p);m.Connect("7654321","NEXT123","A320",1400);Check(m.CruiseFL==0,"new flight does not reuse previous plan");
  Console.WriteLine(count+" flight plan assertions passed");
 }
}
