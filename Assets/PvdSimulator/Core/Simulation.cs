using System;
using System.Collections.Generic;
using System.Linq;

namespace PvdSim.Core
{
    public enum Scenario { A, B }
    public enum Phase { Processing, ControlledStop, Maintenance, Qualifying, AwaitAuthorization, Ready }
    public enum Wafer { Chamber, Carrier, Arm, Unconfirmed, Empty }
    public enum JobKind { SupervisorSafety, PositionCheck, CoolingRepair, TransferRepair, VacuumRepair, Facilities, ProductReview, Release }
    public enum JobState { Requested, Accepted, Working, Reported, Blocked }
    public enum Reason { ProtectionUnknown, TemperatureRising, WaferUnknown, ControlledEmpty, FollowUp }

    [Serializable] public sealed class Config
    {
        public double step = .1, faultCoolingAt = 18, faultTransferAt = 20, faultVacuumAt = 22;
        public double normalFlow = 6, lowFlowTrip = 3, qualifyFlow = 5.5, supplyTemperature = 24;
        public double powerKW = 3, powerResponseSeconds = .6, flowResponseSeconds = 2;
        public double thermalCapacity = 1, powerHeat = 1.7, coolingCoefficient = .05;
        public double overTemperature = 60, maintenanceTemperature = 55, qualifyTemperature = 46;
        public double normalPumpRate = .9, degradedPumpRate = .015, vacuumBaseTorr = .03, vacuumFaultBaseTorr = 45;
        public double loadLockTimeout = 10, transferTimeout = 4, qualifyVacuumTorr = .5;
        public double stableSeconds = 15, qualificationTimeout = 75, informationFreshSeconds = 2;
        public double supportAcceptSeconds = 2, safetySeconds = 5, positionSeconds = 10, repairSeconds = 18, facilitiesSeconds = 22, releaseSeconds = 5;
        public double historyInterval = .5, tourEndSeconds = 180;
        public double localFaultFlowFactor = .14, upstreamFaultFlowFactor = .15, upstreamWarmRiseC = 10;
        public double supplyResponseSeconds = 1.5, supplyThermalSeconds = 4, coolantResponseSeconds = 3;
        public double residualHeatFactor = .8, residualHeatSeconds = 7;
        public double stopCommandKW = .01, stopFeedbackKW = .05, maxStopRise = .08;
        public double qualificationPowerKW = .8, qualificationPowerTolerance = .03, maxQualificationSlope = .15, evidenceFreshSeconds = 45;
        public void Validate()
        {
            foreach (var f in typeof(Config).GetFields())
            {
                double v = (double)f.GetValue(this);
                if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) throw new ArgumentException("Invalid setting: " + f.Name);
            }
            if (step > .2 || lowFlowTrip >= qualifyFlow || qualifyFlow >= normalFlow || qualifyTemperature >= maintenanceTemperature || maintenanceTemperature >= overTemperature || stableSeconds >= qualificationTimeout)
                throw new ArgumentException("設定門檻順序不合理");
            if(localFaultFlowFactor>=1||upstreamFaultFlowFactor>=1||qualificationPowerKW>=powerKW||stopCommandKW>=stopFeedbackKW)
                throw new ArgumentException("故障係數或驗證功率設定不合理");
        }
    }

    [Serializable] public sealed class Sample
    {
        public double time, observedAt, chamberTorr, loadLockTorr, flow, coolantC, temperatureC, temperatureSlope, commandKW, feedbackKW, armProgress;
        public string equipment, phase, wafer;
        public bool pump, armHome, armDestination, blocked, protectionConfirmed, positionConsistent;
    }
    [Serializable] public sealed class Alarm
    {
        public string equipment, code, severity;
        public double time;
        public bool active = true, acknowledged, cleared;
        public Sample firstFail;
    }
    [Serializable] public sealed class Entry
    {
        public int sequence;
        public double time;
        public string equipment, category, message, reason, note;
        public Sample visible;
    }
    [Serializable] public sealed class Job
    {
        public int tool;
        public JobKind kind;
        public JobState state;
        public string role, report;
        public double requested, accepted = -1, started = -1, completed = -1;
        public bool OnSite { get { return role == "支援 EE" || role == "廠務"; } }
    }
    [Serializable] public sealed class Branch
    {
        public string id;
        public double supplyFactor = 1, measuredFlow = 12, temperatureC = 24;
    }
    public sealed class Tool
    {
        public readonly int index;
        public string Id { get { return "PVD-" + (index + 1).ToString("00"); } }
        public Phase phase = Phase.Processing;
        public Wafer wafer = Wafer.Chamber;
        public double chamberTorr = .004, loadLockTorr = .03, flow = 6, coolantC = 24, temperatureC = 41;
        public double temperatureSlope, commandKW = 3, feedbackKW = 3, armProgress, observedAt;
        public bool pump = true, armHome = true, armDestination, positionConsistent = true;
        public bool lotsBlocked, protectionConfirmed, safetyApproved, repaired, qualified, productHold, productReviewed;
        public double controlledAt = -1, qualificationStarted, stableHeld;
        public bool coolingPass, temperaturePass, powerPass, positionPass, vacuumPass;
        public string owner = "值班 EE", next = "持續監看", qualificationNote = "尚未啟動";
        public readonly List<Alarm> alarms = new List<Alarm>();
        public readonly List<Sample> history = new List<Sample>();
        // Scenario truth is private to the plant. UI, priority, hints and tour use observations only.
        internal double coolingFactor = 1, residualHeat, transferStarted = -1, vacuumStarted = -1;
        internal bool transferFault, vacuumFault, transferInjected, vacuumInjected, coolingInjected;
        public Tool(int i, Config c) { index = i; commandKW = feedbackKW = c.powerKW; flow = c.normalFlow; coolantC = c.supplyTemperature; if (i == 2) { wafer = Wafer.Empty; commandKW = feedbackKW = 0; temperatureC = 27; } }
        public Sample Capture(double time)
        {
            return new Sample { time=time, observedAt=observedAt, equipment=Id, phase=phase.ToString(), wafer=wafer.ToString(), chamberTorr=chamberTorr, loadLockTorr=loadLockTorr,
                flow=flow, coolantC=coolantC, temperatureC=temperatureC, temperatureSlope=temperatureSlope, commandKW=commandKW, feedbackKW=feedbackKW, pump=pump,
                armProgress=armProgress, armHome=armHome, armDestination=armDestination, blocked=lotsBlocked, protectionConfirmed=protectionConfirmed, positionConsistent=positionConsistent };
        }
        public bool HasAlarm(string code) { return alarms.Any(a=>a.code==code); }
        public bool ActiveAlarm { get { return alarms.Any(a=>a.active); } }
        public bool Latched { get { return alarms.Any(a=>!a.cleared); } }
    }
    public sealed class Priority
    {
        public int tool, score;
        public string next, basis;
    }

    public sealed class Simulation
    {
        public const string Disclaimer = "SIMULATED：通用設備概念與應變練習。非台積電或設備商正式系統、內部 SOP、真機配方或安全控制器。";
        public readonly Config config;
        public readonly Scenario scenario;
        public readonly Tool[] tools;
        public readonly Branch[] branches = { new Branch { id="A" }, new Branch { id="B" } };
        public readonly List<Entry> events = new List<Entry>();
        public readonly List<Job> jobs = new List<Job>();
        public bool running, paused, ended, grouped, compared, topologyViewed, upstreamViewed;
        public string lastMessage = "選擇情境與模式，再開始情境。";
        public double Time { get { return ticks * config.step; } }
        public double EvidenceAt { get; private set; }
        long ticks;
        double sampleAt;
        bool branchInjected, branchFault;
        public Simulation(Config c, Scenario mode)
        {
            c.Validate(); config = c; scenario = mode;
            tools = Enumerable.Range(0,4).Select(i=>new Tool(i,c)).ToArray();
            foreach (var b in branches) { b.measuredFlow=2*c.normalFlow; b.temperatureC=c.supplyTemperature; }
            foreach (var t in tools) t.history.Add(t.Capture(0));
        }
        public bool Start()
        {
            if (running || ended) return false;
            running=true; Log(-1,"開始","情境 " + scenario + "；全部數值與支援時間均為教學設定"); return true;
        }
        public void End() { if (!running) return; running=false; ended=true; Log(-1,"回顧","回合結束；尚未完成項目仍保留為待辦"); }
        public void TogglePause() { if (!running) return; paused=!paused; Log(-1,"訓練暫停",paused?"全部模型與支援計時凍結":"繼續共同時鐘"); }
        public void Step()
        {
            if (!running || paused) return;
            ticks++;
            Inject();
            foreach (var b in branches)
            {
                b.measuredFlow=Approach(b.measuredFlow,2*config.normalFlow*b.supplyFactor,config.step,config.supplyResponseSeconds);
                b.temperatureC=Approach(b.temperatureC,config.supplyTemperature+config.upstreamWarmRiseC*(1-b.supplyFactor),config.step,config.supplyThermalSeconds);
            }
            foreach (var t in tools) UpdatePlant(t);
            UpdateJobs();
            foreach (var t in tools) { UpdateQualification(t); if (t.protectionConfirmed && !SafeStopped(t)) t.protectionConfirmed=false; }
            if (Time + 1e-8 >= sampleAt) { foreach(var t in tools) t.history.Add(t.Capture(Time)); sampleAt=Time+config.historyInterval; }
        }
        void Inject()
        {
            if (scenario==Scenario.B)
            {
                if (!branchInjected && Time>=config.faultCoolingAt) { branchInjected=true; branchFault=true; branches[0].supplyFactor=config.upstreamFaultFlowFactor; }
                return;
            }
            var a=tools[0]; var b=tools[1]; var c=tools[2];
            if (!a.coolingInjected && Time>=config.faultCoolingAt) { a.coolingInjected=true; a.coolingFactor=config.localFaultFlowFactor; }
            if (!b.transferInjected && Time>=config.faultTransferAt)
            {
                b.transferInjected=true; b.transferFault=true; b.commandKW=0; b.transferStarted=Time; b.armProgress=.46;
                b.armHome=true; b.armDestination=true; b.positionConsistent=false; b.wafer=Wafer.Unconfirmed;
            }
            if (!c.vacuumInjected && Time>=config.faultVacuumAt)
            {
                c.vacuumInjected=true; c.vacuumFault=true; c.vacuumStarted=Time; c.loadLockTorr=760;
            }
        }
        static double Approach(double from,double to,double dt,double tau) { return to+(from-to)*Math.Exp(-dt/tau); }
        void UpdatePlant(Tool t)
        {
            double dt=config.step;
            var b=branches[t.index<2?0:1];
            if(!t.transferFault && t.phase==Phase.Processing && t.index!=2)
            {
                // Normal transfer reserves three seconds for power decay before movement.
                double cycle=(Time+t.index*7)%36;
                t.commandKW=cycle<=26?config.powerKW:0;
            }
            t.flow=Approach(t.flow,config.normalFlow*b.supplyFactor*t.coolingFactor,dt,config.flowResponseSeconds);
            t.coolantC=Approach(t.coolantC,b.temperatureC,dt,config.coolantResponseSeconds);
            t.feedbackKW=Approach(t.feedbackKW,t.commandKW,dt,config.powerResponseSeconds);
            t.residualHeat=Approach(t.residualHeat,0,dt,config.residualHeatSeconds);
            t.temperatureSlope=(config.powerHeat*t.feedbackKW+t.residualHeat-config.coolingCoefficient*t.flow*(t.temperatureC-t.coolantC))/config.thermalCapacity;
            t.temperatureC=Math.Max(t.coolantC,t.temperatureC+dt*t.temperatureSlope);
            t.chamberTorr=Approach(t.chamberTorr,t.pump?(t.commandKW>0?.004:.0004):760,dt,t.pump?1.5:30);
            if (t.vacuumStarted>=0 || t.phase==Phase.Qualifying)
            {
                double rate=t.vacuumFault?config.degradedPumpRate:config.normalPumpRate;
                t.loadLockTorr=Approach(t.loadLockTorr,t.vacuumFault?config.vacuumFaultBaseTorr:config.vacuumBaseTorr,dt,1/rate);
            }
            if (!t.transferFault && t.phase==Phase.Processing && t.index!=2)
            {
                double cycle=(Time+t.index*7)%36;
                t.armProgress=cycle>29?(cycle-29)/7:0;
                t.armHome=cycle<=29; t.armDestination=cycle>=35; t.positionConsistent=true;
                t.wafer=cycle>29?Wafer.Arm:Wafer.Chamber;
            }
            t.observedAt=Time;
            // All captures occur after measurement but BEFORE protection changes command or state.
            var observation=t.Capture(Time);
            CheckAlarm(t,"FLOW LOW","高",t.flow<config.lowFlowTrip,observation);
            CheckAlarm(t,"TEMP HIGH","高",t.temperatureC>config.overTemperature,observation);
            CheckAlarm(t,"TRANSFER TIMEOUT","高",t.transferStarted>=0 && Time-t.transferStarted>=config.transferTimeout && !t.positionConsistent,observation);
            CheckAlarm(t,"LL VACUUM TIMEOUT","中",t.vacuumStarted>=0 && Time-t.vacuumStarted>=config.loadLockTimeout && t.loadLockTorr>config.qualifyVacuumTorr,observation);
            foreach(var a in t.alarms)
            {
                if(a.code=="FLOW LOW") a.active=t.flow<config.lowFlowTrip;
                if(a.code=="TEMP HIGH") a.active=t.temperatureC>config.overTemperature-3;
                if(a.code=="TRANSFER TIMEOUT") a.active=!t.positionConsistent;
                if(a.code=="LL VACUUM TIMEOUT") a.active=t.loadLockTorr>config.qualifyVacuumTorr;
            }
        }
        void CheckAlarm(Tool t,string code,string severity,bool condition,Sample observation)
        {
            if (!condition || t.alarms.Any(a=>a.code==code && !a.cleared)) return;
            var a=new Alarm { equipment=t.Id,code=code,severity=severity,time=Time,firstFail=observation };
            t.alarms.Add(a); Log(t.index,"警報",code+"；First-Fail 已鎖存（保護前）",null,null,a.firstFail);
            t.residualHeat=t.feedbackKW*config.residualHeatFactor; t.commandKW=0; t.lotsBlocked=true; t.phase=Phase.ControlledStop;
            t.protectionConfirmed=false; t.qualified=false; t.safetyApproved=false; t.stableHeld=0;
            t.productHold=t.wafer!=Wafer.Empty; t.next="確認命令／回授、保護與晶圓";
            Log(t.index,"自動保護","停止濺鍍命令、停止後續動作、阻止新批次；仍須確認回授");
        }
        public bool Fresh(Tool t) { return Time-t.observedAt<=config.informationFreshSeconds; }
        public bool SafeStopped(Tool t) { return Fresh(t) && t.commandKW<config.stopCommandKW && t.feedbackKW<config.stopFeedbackKW && t.pump && t.lotsBlocked && t.temperatureC<config.maintenanceTemperature && t.temperatureSlope<=config.maxStopRise; }
        public bool MaintenanceAllowed(Tool t) { return SafeStopped(t) && t.protectionConfirmed && t.safetyApproved && t.phase!=Phase.Qualifying; }
        bool Operable(int i) { if (!running || paused || ended || i<0 || i>3) { lastMessage="請先啟動／繼續情境"; return false; } return true; }
        bool Reject(int i,string reason) { Log(i,"操作未執行",reason); return false; }
        public bool Acknowledge(int i)
        {
            if(!Operable(i))return false; foreach(var a in tools[i].alarms) a.acknowledged=true;
            Log(i,"收悉 ACK","只代表已收悉；不改變故障、鎖定或產品狀態"); return true;
        }
        public bool BlockLots(int i)
        {
            if(!Operable(i))return false; tools[i].lotsBlocked=true; Log(i,"批次保留","阻止新批次進入"); return true;
        }
        public bool ConfirmProtection(int i)
        {
            if(!Operable(i))return false; var t=tools[i];
            if(!SafeStopped(t))return Reject(i,"不能確認受控：需新鮮資料、零功率回授、泵運轉、溫度無持續上升與批次保留");
            t.protectionConfirmed=true; if(t.controlledAt<0)t.controlledAt=Time;
            t.next=t.wafer==Wafer.Unconfirmed?"請有權限支援確認晶圓位置":"申請模擬維修授權";
            Log(i,"確認受控","依目前命令、回授及熱趨勢確認；晶圓位置與產品品質另行確認"); return true;
        }
        public bool ChoosePriority(int i,Reason r,string note)
        {
            if(!Operable(i))return false; var t=tools[i]; bool valid=Fresh(t);
            if(r==Reason.ProtectionUnknown)valid &= !t.protectionConfirmed && t.alarms.Count>0;
            if(r==Reason.TemperatureRising)valid &= t.temperatureSlope>config.maxStopRise;
            if(r==Reason.WaferUnknown)valid &= t.wafer==Wafer.Unconfirmed;
            if(r==Reason.ControlledEmpty)valid &= t.protectionConfirmed && t.wafer==Wafer.Empty;
            if(!valid)return Reject(i,"所選理由與目前可見資料不符／資料過期；請重新觀察");
            Log(i,"優先決策","指定本台為下一個處理對象",ReasonText(r),note); return true;
        }
        public static string ReasonText(Reason r)
        {
            return new[]{"保護狀態尚未確認","溫度仍上升","晶圓位置未確認","已受控停止且無晶圓","安排後續追蹤"}[(int)r];
        }
        public List<Priority> Priorities()
        {
            // Pure observation-based policy: deliberately no access to fault truth.
            var list=new List<Priority>();
            foreach(var t in tools)
            {
                var p=new Priority {tool=t.index,score=0,next="監看正常對照",basis="目前無異常"};
                if(t.alarms.Count>0 && t.phase!=Phase.Ready) {p.score=15;p.next="完成驗證／交接";p.basis="仍有待辦";}
                if(t.alarms.Count>0 && t.phase!=Phase.Ready && t.phase!=Phase.Qualifying && !t.protectionConfirmed) {p.score=80;p.next="確認保護回授";p.basis="保護尚未確認";}
                if(t.wafer==Wafer.Unconfirmed) {p.score=85;p.next="安排授權位置確認";p.basis="晶圓位置未確認";}
                if(t.temperatureSlope>config.maxStopRise && t.ActiveAlarm) {p.score=95;p.next="確認停功率與熱趨勢";p.basis="溫度仍上升";}
                if(!Fresh(t)){p.score=100;p.next="重新取得資訊";p.basis="資料過期；不得當成安全";}
                if(t.protectionConfirmed && t.wafer==Wafer.Empty){p.score=10;p.next="指派負責人並追蹤";p.basis="已受控且無晶圓";}
                list.Add(p);
            }
            return list.OrderByDescending(p=>p.score).ToList();
        }
        public void ObserveComparison() { if(!running||paused)return; compared=true; EvidenceAt=Time; Log(-1,"證據","跨台流量、溫度與原始警報時間對照；時間接近不是根因證明"); }
        public void ObserveTopology() { if(!running||paused)return; topologyViewed=true; Log(-1,"證據","確認連接：A → 01/02；B → 03/04"); }
        public void ObserveUpstream() { if(!running||paused)return; upstreamViewed=true; EvidenceAt=Time; Log(-1,"證據",string.Format("上游 A {0:F2} L/min、{1:F1} °C；B {2:F2} L/min、{3:F1} °C",branches[0].measuredFlow,branches[0].temperatureC,branches[1].measuredFlow,branches[1].temperatureC)); }
        public bool GroupIncident()
        {
            if(!running||paused)return false;
            if(tools.Count(t=>t.HasAlarm("FLOW LOW"))<2)return Reject(-1,"至少需要兩台的原始流量警報作為待調查線索");
            grouped=true;Log(-1,"待調查事件","相關警報歸組：疑似共同原因，尚未證明；保留每台 First-Fail");return true;
        }
        public bool Request(int i,JobKind kind)
        {
            if(!Operable(i))return false;
            if(jobs.Any(j=>j.tool==i && j.kind==kind && !Finished(j)))return Reject(i,"已有同類請求，請查看支援進度");
            string role=kind==JobKind.Facilities?"廠務":kind==JobKind.ProductReview?"PE／製造":kind==JobKind.SupervisorSafety||kind==JobKind.Release?"值班主管":"支援 EE";
            var j=new Job {tool=i,kind=kind,role=role,state=JobState.Requested,requested=Time,report="等待接手"};
            string why;
            if(!JobAllowed(j,out why))return Reject(i,why);
            jobs.Add(j);
            if(kind!=JobKind.ProductReview){tools[i].owner=role;tools[i].next=JobText(kind)+"：等待接手";}
            Log(i,"支援請求",role+"："+JobText(kind));return true;
        }
        public static bool Finished(Job j) { return j.state==JobState.Reported||j.state==JobState.Blocked; }
        public static string JobText(JobKind k) { return new[]{"模擬安全／權限核准","確認晶圓位置","局部冷卻模擬處置","搬片模擬處置","Load Lock 模擬處置","疑似共同原因確認與處置","產品待評估回報","設備新批次模擬核准"}[(int)k]; }
        bool JobAllowed(Job j,out string why)
        {
            var t=tools[j.tool];why="";
            if(j.kind==JobKind.ProductReview)return true;
            if(j.kind==JobKind.Release)
            {
                if(t.qualified && t.phase==Phase.AwaitAuthorization && !t.Latched && !t.ActiveAlarm && SafeStopped(t))return true;
                why="需設備驗證完成、警報解除且目前仍受控，才可模擬核准新批次";return false;
            }
            if(j.kind==JobKind.SupervisorSafety)
            {
                if(t.protectionConfirmed && SafeStopped(t))return true;
                why="需先確認保護、受控停止與批次保留，才能請求主管核准";return false;
            }
            if(j.kind==JobKind.Facilities)
            {
                if(!grouped||!compared||!topologyViewed||!upstreamViewed||Time-EvidenceAt>config.evidenceFreshSeconds) { why="需歸組、近期跨台趨勢、管路連接與上游量測；時間接近不足以確認原因";return false; }
                if(!tools.Take(2).All(x=>MaintenanceAllowed(x))) {why="支路 A 兩台均需確認受控及模擬維修核准";return false;}
                return true;
            }
            if(!MaintenanceAllowed(t)){why="安全／權限條件不符；需保護確認、主管核准、低溫、停功率與新鮮資料";return false;}
            if(j.kind!=JobKind.PositionCheck && t.wafer==Wafer.Unconfirmed){why="晶圓位置未確認：先安排有權限的位置確認，不盲目重試";return false;}
            return true;
        }
        double Duration(JobKind k)
        {
            return k==JobKind.SupervisorSafety?config.safetySeconds:k==JobKind.PositionCheck?config.positionSeconds:k==JobKind.Facilities?config.facilitiesSeconds:k==JobKind.ProductReview||k==JobKind.Release?config.releaseSeconds:config.repairSeconds;
        }
        void UpdateJobs()
        {
            foreach(var j in jobs)
            {
                if(Finished(j))continue;
                string why;
                if(!JobAllowed(j,out why)) {j.state=JobState.Blocked;j.completed=Time;j.report="條件改變，未完成："+why;Log(j.tool,"支援中止",j.report);continue;}
                if(j.state==JobState.Requested)
                {
                    if(Time-j.requested<config.supportAcceptSeconds)continue;
                    if(j.OnSite && jobs.Any(o=>o!=j && o.role==j.role && (o.state==JobState.Accepted||o.state==JobState.Working)))continue;
                    j.accepted=Time;j.state=JobState.Accepted;j.report="已接手，確認條件";Log(j.tool,"支援接手",j.role+"："+JobText(j.kind));continue;
                }
                if(j.state==JobState.Accepted){j.state=JobState.Working;j.started=Time;j.report="確認／處理中";continue;}
                if(Time-j.started<Duration(j.kind))continue;
                CompleteJob(j);j.state=JobState.Reported;j.completed=Time;Log(j.tool,"支援回報",j.role+"："+j.report);
            }
        }
        void CompleteJob(Job j)
        {
            var t=tools[j.tool];
            switch(j.kind)
            {
                case JobKind.SupervisorSafety:t.safetyApproved=true;t.next="指派授權處置／位置確認";j.report="本教學情境抽象處置已授權；條件持續檢查";break;
                case JobKind.PositionCheck:t.wafer=Wafer.Arm;t.next="位置已確認；安排搬片處置";j.report="授權獨立確認：晶圓位於搬片區並保留；原位置訊號仍需處置";break;
                case JobKind.CoolingRepair:t.coolingFactor=1;t.repaired=true;t.phase=Phase.Maintenance;j.report="局部冷卻處置完成；未改變共用供應，需用趨勢確認結果";break;
                case JobKind.TransferRepair:t.transferFault=false;t.positionConsistent=true;t.armHome=true;t.armDestination=false;t.armProgress=0;t.wafer=Wafer.Carrier;t.repaired=true;t.phase=Phase.Maintenance;j.report="模擬搬片處置完成，位置通道已恢復；仍需重新搬片驗證";break;
                case JobKind.VacuumRepair:t.vacuumFault=false;t.repaired=true;t.phase=Phase.Maintenance;j.report="Load Lock 抽象處置完成；需重新抽真空驗證";break;
                case JobKind.Facilities:
                    if(branches[0].measuredFlow<2*config.lowFlowTrip && branches[1].measuredFlow>2*config.qualifyFlow && branchFault)
                    {branchFault=false;branches[0].supplyFactor=1;j.report="廠務回報：已確認 A 上游供應異常並完成模擬處置；01／02 各自驗證";tools[0].repaired=tools[1].repaired=true;}
                    else j.report="廠務回報：目前量測無法確認本項共同原因；未執行供應處置";
                    break;
                case JobKind.ProductReview:t.productReviewed=true;j.report=t.productHold?"PE 已接手；缺乏品質資料，產品仍待評估，沒有放行":"已確認空 Load Lock，無受影響晶圓";break;
                case JobKind.Release:t.phase=Phase.Ready;t.lotsBlocked=false;t.next="可接新批次；受影響產品另行保留";j.report="設備新批次已模擬核准；受影響晶圓保持原處置狀態";break;
            }
            if(j.kind==JobKind.CoolingRepair||j.kind==JobKind.TransferRepair||j.kind==JobKind.VacuumRepair)t.next="確認回常、解除警報並重新驗證";
        }
        public bool ClearAlarms(int i)
        {
            if(!Operable(i))return false;var t=tools[i];
            if(t.ActiveAlarm||t.alarms.Any(a=>!a.acknowledged)||!SafeStopped(t))return Reject(i,"清除需警報已回常、全部收悉與目前受控；清除不能修復故障");
            foreach(var a in t.alarms)a.cleared=true;
            Log(i,"解除鎖定","歷史與 First-Fail 保留；仍需設備驗證及新批次核准");return true;
        }
        public bool Qualify(int i)
        {
            if(!Operable(i))return false;var t=tools[i];
            if(!MaintenanceAllowed(t)||!t.repaired||t.wafer==Wafer.Unconfirmed||t.Latched||t.ActiveAlarm)return Reject(i,"驗證需處置完成、位置已確認、警報解除與安全／權限條件持續成立");
            t.phase=Phase.Qualifying;t.qualified=false;t.stableHeld=0;t.qualificationStarted=Time;t.commandKW=config.qualificationPowerKW;
            t.next="監看連續穩定時間與逐項條件";
            if(t.HasAlarm("LL VACUUM TIMEOUT")){t.loadLockTorr=760;t.vacuumStarted=Time;}
            t.qualificationNote="正在重新跑模型";Log(i,"驗證開始","低功率回授、冷卻熱穩定、位置／抽真空重新測試；需連續穩定時間");return true;
        }
        void UpdateQualification(Tool t)
        {
            if(t.phase!=Phase.Qualifying)return;
            double age=Time-t.qualificationStarted;
            if(t.HasAlarm("TRANSFER TIMEOUT")&&!t.transferFault)
            {
                t.armProgress=Math.Min(1,age/5);t.armHome=age<.2;t.armDestination=age>=5;t.positionConsistent=!(t.armHome&&t.armDestination);
                t.wafer=age<5?Wafer.Arm:Wafer.Carrier;
            }
            t.coolingPass=Fresh(t)&&t.flow>=config.qualifyFlow;
            t.temperaturePass=t.temperatureC<=config.qualifyTemperature&&Math.Abs(t.temperatureSlope)<config.maxQualificationSlope;
            t.powerPass=Math.Abs(t.feedbackKW-t.commandKW)<config.qualificationPowerTolerance;
            t.positionPass=t.wafer!=Wafer.Unconfirmed&&t.positionConsistent&&(!t.HasAlarm("TRANSFER TIMEOUT")||t.armDestination);
            t.vacuumPass=t.loadLockTorr<=config.qualifyVacuumTorr;
            bool all=t.coolingPass&&t.temperaturePass&&t.powerPass&&t.positionPass&&t.vacuumPass&&!t.ActiveAlarm;
            t.stableHeld=all?t.stableHeld+config.step:0;
            t.qualificationNote=all?"全部項目連續穩定中":"等待符合；任一失效即重算穩定時間";
            if(t.stableHeld+1e-8>=config.stableSeconds)
            {
                t.qualified=true;t.phase=Phase.AwaitAuthorization;t.commandKW=0;t.next="驗證完成、待模擬授權";
                t.qualificationNote="驗證完成、待模擬授權";Log(t.index,"驗證完成","設備通過本情境條件；產品仍依 PE 評估狀態保留");
            }
            else if(age>=config.qualificationTimeout)
            {t.phase=Phase.ControlledStop;t.commandKW=0;t.stableHeld=0;t.qualificationNote="驗證逾時；保持停止";Log(t.index,"驗證失敗","逾時，需重新診斷；未核准新批次");}
        }
        public Entry Log(int i,string category,string message,string reason=null,string note=null,Sample sample=null)
        {
            lastMessage=message;
            var e=new Entry {sequence=events.Count+1,time=Time,equipment=i<0?"AREA":tools[i].Id,category=category,message=message,reason=reason??"",note=note??"",visible=sample??(i<0?null:tools[i].Capture(Time))};events.Add(e);return e;
        }
        public string DebriefTruth()
        {
            if(!ended)return "回合結束後才開放情境真值";
            return scenario==Scenario.A?"情境真值：01 局部冷卻導通降低；02 搬片停止與位置通道不一致；03 Load Lock 有效抽速降低及背景負載增加。":"情境真值：共用 A 上游冷卻供應下降，經同一供應模型影響 01／02。";
        }
    }
}
