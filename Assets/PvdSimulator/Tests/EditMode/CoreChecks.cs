using System;
using System.Linq;
using PvdSim.Core;

namespace PvdSim.Checks
{
    public static class CoreChecks
    {
        static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
        public static void Advance(Simulation s,double seconds){for(int n=0;n<(int)Math.Round(seconds/s.config.step);n++)s.Step();}
        static Simulation Session(Scenario mode=Scenario.A){var s=new Simulation(new Config(),mode);s.Start();return s;}
        static Simulation Tour(Scenario mode){var s=Session(mode);var tour=new GuidedTour();for(int n=0;n<1900;n++){s.Step();tour.Update(s);}return s;}
        public static string RunAll()
        {
            string output="";
            Action<string,Action> run=(name,test)=>{try{test();output+="PASS "+name+"\n";}catch(Exception e){output+="FAIL "+name+": "+e.Message+"\n";}};
            run("A tour completes three independent recoveries",()=>{var s=Tour(Scenario.A);Assert(s.tools.Take(3).All(t=>t.phase==Phase.Ready),string.Join(";",s.tools.Select(t=>t.Id+" "+t.phase+" "+t.qualificationNote))+" / "+s.lastMessage);Assert(s.tools[3].alarms.Count==0,"Normal reference alarm");});
            run("B shared supply tour completes individual qualification",()=>{var s=Tour(Scenario.B);Assert(s.tools.Take(2).All(t=>t.phase==Phase.Ready),string.Join(";",s.tools.Select(t=>t.Id+" "+t.phase+" "+t.qualificationNote))+" / "+s.lastMessage);Assert(s.tools.Skip(2).All(t=>t.alarms.Count==0),"Wrong branch affected");});
            run("First-fail precedes automatic protection and remains immutable",()=>{var s=Session();Advance(s,35);var t=s.tools[0];var f=t.alarms[0].firstFail;double value=f.flow;Assert(f.commandKW==3&&t.commandKW==0,"Capture after protection");s.Acknowledge(0);Advance(s,20);Assert(f.flow==value&&f.commandKW==3,"Snapshot overwritten");var ev=s.events.Where(e=>e.equipment==t.Id).ToArray();Assert(ev[0].category=="警報"&&ev[1].category=="自動保護","Event order");});
            run("ACK and clear cannot repair or authorize",()=>{var s=Session();Advance(s,40);s.Acknowledge(0);Assert(!s.ClearAlarms(0)&&!s.Qualify(0)&&!s.Request(0,JobKind.Release),"Recovery gate bypass");Advance(s,20);Assert(s.tools[0].flow<1,"ACK repaired plant");});
            run("Simultaneous alarm snapshots all precede protection",()=>{var s=Session();s.tools[0].flow=.5;s.tools[0].temperatureC=70;s.Step();Assert(s.tools[0].alarms.Count==2,"Expected two observed conditions");Assert(s.tools[0].alarms.All(a=>a.firstFail.commandKW==3),"Second alarm captured after first protection");});
            run("Pause freezes all plants history and support",()=>{var s=Session();Advance(s,40);s.ConfirmProtection(0);s.Request(0,JobKind.SupervisorSafety);s.TogglePause();var time=s.Time;var flow=s.tools[0].flow;var count=s.tools[3].history.Count;Advance(s,30);Assert(s.Time==time&&s.tools[0].flow==flow&&s.tools[3].history.Count==count&&s.jobs[0].state==JobState.Requested,"Pause inconsistent");});
            run("All tools evolve independently on one clock",()=>{var s=Session();Advance(s,40);Assert(s.tools.All(t=>t.history.Last().time>39),"Unselected tool frozen");Assert(s.tools[0].flow!=s.tools[3].flow&&s.tools[1].wafer==Wafer.Unconfirmed&&s.tools[2].wafer==Wafer.Empty,"Mixed tool data");});
            run("Unknown and stale signals cannot prove safety",()=>{var s=Session();Advance(s,40);Assert(!s.Request(1,JobKind.TransferRepair),"Unknown wafer repair allowed");s.tools[0].observedAt=0;Assert(!s.ConfirmProtection(0),"Stale data accepted");Assert(s.Priorities()[0].tool==0&&s.Priorities()[0].basis.Contains("過期"),"Stale priority");});
            run("Priority changes after risk is controlled",()=>{var s=Session();Advance(s,42);Assert(s.ConfirmProtection(0),"Cannot confirm cooled tool");var p=s.Priorities();Assert(p.Find(x=>x.tool==1).score>p.Find(x=>x.tool==0).score,"Historical severe alarm stuck on top");Assert(s.ChoosePriority(1,Reason.WaferUnknown,"test"),"Evidence-based choice rejected");Assert(!s.ChoosePriority(0,Reason.TemperatureRising,"wrong reason"),"False rationale accepted");});
            run("Local repair does not remove upstream fault",()=>{var s=Session(Scenario.B);Advance(s,45);s.ConfirmProtection(0);s.Request(0,JobKind.SupervisorSafety);Advance(s,10);Assert(s.Request(0,JobKind.CoolingRepair),"Repair conditions");Advance(s,30);Assert(s.tools[0].flow<1&&s.tools[1].flow<1&&s.branches[0].measuredFlow<3,"Upstream repaired locally");Assert(s.tools[2].flow>5.5&&s.tools[3].flow>5.5,"Wrong branch");});
            run("Finite EE capacity and invalidated jobs cannot succeed",()=>{var s=Session();Advance(s,42);s.ConfirmProtection(0);s.ConfirmProtection(2);s.Request(0,JobKind.SupervisorSafety);s.Request(2,JobKind.SupervisorSafety);Advance(s,10);s.Request(0,JobKind.CoolingRepair);s.Request(2,JobKind.VacuumRepair);Advance(s,5);Assert(s.jobs.Count(j=>j.role=="支援 EE"&&(j.state==JobState.Working||j.state==JobState.Accepted))==1,"Double occupied");s.tools[0].lotsBlocked=false;Advance(s,1);Assert(s.jobs.Any(j=>j.kind==JobKind.CoolingRepair&&j.state==JobState.Blocked)&&!s.tools[0].repaired,"Invalid job completed");});
            run("Qualification stability resets on interrupted condition",()=>{var s=Session();var tour=new GuidedTour();bool tested=false;for(int n=0;n<1700;n++){s.Step();tour.Update(s);var t=s.tools[0];if(t.phase==Phase.Qualifying&&t.stableHeld>3){t.flow=3.5;s.Step();Assert(t.stableHeld==0&&!t.qualified,"Stable timer not reset");tested=true;break;}}Assert(tested,"Did not reach qualification");});
            run("Equipment recovery never releases affected product",()=>{var s=Tour(Scenario.A);Assert(s.tools[0].qualified&&s.tools[0].productHold&&s.tools[1].productHold,"Product auto-released");});
            run("Recovered historical alarm does not dominate priority",()=>{var s=Tour(Scenario.A);Assert(s.Priorities().All(p=>p.score<50),"Historical alarm permanently urgent");});
            run("Deterministic replay and finite physical values",()=>{var a=Tour(Scenario.A);var b=Tour(Scenario.A);Assert(a.events.Count==b.events.Count,"Event nondeterminism");for(int i=0;i<4;i++){Assert(a.tools[i].temperatureC==b.tools[i].temperatureC,"Numeric nondeterminism");foreach(var h in a.tools[i].history)foreach(double v in new[]{h.flow,h.temperatureC,h.chamberTorr,h.loadLockTorr,h.commandKW,h.feedbackKW})Assert(!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0,"Invalid physical quantity");}});
            run("Config rejects invalid values and order",()=>{var c=new Config {step=double.NaN};bool threw=false;try{c.Validate();}catch(ArgumentException){threw=true;}Assert(threw,"NaN configuration accepted");});
            run("Facilities request needs correlated evidence beyond timing",()=>{var s=Session(Scenario.B);Advance(s,40);Assert(s.GroupIncident(),"No correlation group");Assert(!s.Request(0,JobKind.Facilities),"Timing alone repaired supply");Assert(s.tools[0].alarms.Count>0&&s.tools[1].alarms.Count>0,"Grouping removed raw alarms");});
            return output;
        }
    }
}
