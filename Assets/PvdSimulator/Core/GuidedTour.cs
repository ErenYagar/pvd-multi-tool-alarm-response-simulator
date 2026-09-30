using System.Linq;

namespace PvdSim.Core
{
    // Uses the same public commands as buttons; no hidden-fault reads or direct result assignments.
    public sealed class GuidedTour
    {
        double nextDecision;
        public void Update(Simulation s)
        {
            if(!s.running||s.paused||s.Time<nextDecision)return;
            nextDecision=s.Time+1;
            if(s.Time>=s.config.tourEndSeconds){s.End();return;}
            if(s.Time<28)return;
            foreach(var t in s.tools.Where(t=>t.alarms.Count>0))
            {
                if(t.alarms.Any(a=>!a.acknowledged))s.Acknowledge(t.index);
                if(!t.protectionConfirmed && s.SafeStopped(t))
                {
                    s.ChoosePriority(t.index,t.wafer==Wafer.Unconfirmed?Reason.WaferUnknown:Reason.ProtectionUnknown,"導覽：以當下可見資料選擇下一步");
                    s.ConfirmProtection(t.index);
                }
                if(t.protectionConfirmed && !t.safetyApproved && !HasJob(s,t.index,JobKind.SupervisorSafety))s.Request(t.index,JobKind.SupervisorSafety);
                if(t.productHold&&!HasJob(s,t.index,JobKind.ProductReview))s.Request(t.index,JobKind.ProductReview);
                if(!s.MaintenanceAllowed(t))continue;
                if(t.wafer==Wafer.Unconfirmed && !HasJob(s,t.index,JobKind.PositionCheck)){s.Request(t.index,JobKind.PositionCheck);continue;}
                bool shared=s.tools.Count(x=>x.HasAlarm("FLOW LOW"))>=2;
                if(shared && !s.grouped){s.ObserveComparison();s.ObserveTopology();s.ObserveUpstream();s.GroupIncident();}
                if(shared && !HasJob(s,0,JobKind.Facilities) && s.tools.Take(2).All(s.MaintenanceAllowed))
                {s.ObserveComparison();s.ObserveUpstream();s.Request(0,JobKind.Facilities);}
                if(!t.repaired && t.wafer!=Wafer.Unconfirmed)
                {
                    if(t.HasAlarm("FLOW LOW")&&!shared&&!HasJob(s,t.index,JobKind.CoolingRepair))s.Request(t.index,JobKind.CoolingRepair);
                    if(t.HasAlarm("TRANSFER TIMEOUT")&&!HasJob(s,t.index,JobKind.TransferRepair))s.Request(t.index,JobKind.TransferRepair);
                    if(t.HasAlarm("LL VACUUM TIMEOUT")&&!HasJob(s,t.index,JobKind.VacuumRepair))s.Request(t.index,JobKind.VacuumRepair);
                }
                if(t.repaired&&!t.ActiveAlarm&&t.Latched)s.ClearAlarms(t.index);
                if(t.repaired&&!t.Latched&&!t.ActiveAlarm&&!t.qualified&&t.phase!=Phase.Qualifying)s.Qualify(t.index);
            }
            foreach(var t in s.tools.Where(t=>t.qualified && t.phase==Phase.AwaitAuthorization))
                if(s.SafeStopped(t)&&!HasJob(s,t.index,JobKind.Release))s.Request(t.index,JobKind.Release);
        }
        static bool HasJob(Simulation s,int i,JobKind k){return s.jobs.Any(j=>j.tool==i&&j.kind==k&&j.state!=JobState.Blocked);}
    }
}
