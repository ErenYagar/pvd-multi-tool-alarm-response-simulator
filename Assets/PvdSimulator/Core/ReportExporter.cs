using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;

namespace PvdSim.Core
{
    [Serializable] public sealed class Report
    {
        public string disclaimer,scenario,createdUtc,truth;
        public bool ended;
        public double simulatedSeconds;
        public Config config;
        public Sample[] finalState,history;
        public Alarm[] alarms;
        public Entry[] decisions;
        public Job[] support;
    }
    public static class ReportExporter
    {
        static string N(double v){return v.ToString("0.000",CultureInfo.InvariantCulture);}
        static string Q(string s){return "\""+(s??"").Replace("\"","\"\"")+"\"";}
        public static string Export(Simulation s,string root,Func<Report,string> json)
        {
            string path=Path.Combine(root,"PVD-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
            Directory.CreateDirectory(path);
            var report=new Report {disclaimer=Simulation.Disclaimer,scenario=s.scenario.ToString(),createdUtc=DateTime.UtcNow.ToString("O"),ended=s.ended,simulatedSeconds=s.Time,config=s.config,
                finalState=s.tools.Select(t=>t.Capture(s.Time)).ToArray(),history=s.tools.SelectMany(t=>t.history).ToArray(),alarms=s.tools.SelectMany(t=>t.alarms).ToArray(),decisions=s.events.ToArray(),support=s.jobs.ToArray(),truth=s.DebriefTruth()};
            File.WriteAllText(Path.Combine(path,"report.json"),json(report),new UTF8Encoding(false));
            var csv=new StringBuilder("time_s,equipment,phase,flow_L_min,coolant_C,temperature_C,chamber_Torr,loadlock_Torr,power_command_kW,power_feedback_kW,wafer,pump,arm_home,arm_destination,position_consistent,observed_at_s\r\n");
            foreach(var v in report.history)csv.AppendLine(string.Join(",",N(v.time),v.equipment,v.phase,N(v.flow),N(v.coolantC),N(v.temperatureC),N(v.chamberTorr),N(v.loadLockTorr),N(v.commandKW),N(v.feedbackKW),v.wafer,v.pump,v.armHome,v.armDestination,v.positionConsistent,N(v.observedAt)));
            File.WriteAllText(Path.Combine(path,"trends.csv"),csv.ToString(),new UTF8Encoding(true));
            csv=new StringBuilder("sequence,time_s,equipment,category,message,reason,note\r\n");
            foreach(var e in s.events)csv.AppendLine(string.Join(",",e.sequence.ToString(),N(e.time),e.equipment,Q(e.category),Q(e.message),Q(e.reason),Q(e.note)));
            File.WriteAllText(Path.Combine(path,"events.csv"),csv.ToString(),new UTF8Encoding(true));
            var text=new StringBuilder(Simulation.Disclaimer+"\r\n所有數值、門檻、支援時間均為教學設定。\r\n\r\n");
            text.AppendLine("PVD 多機台警報交接摘要／情境 "+s.scenario+"／模擬 "+N(s.Time)+" 秒／"+(s.ended?"回合結束":"尚在進行"));
            text.AppendLine("設備驗證完成："+s.tools.Count(t=>t.qualified)+" 台；未收悉警報事件："+s.tools.Sum(t=>t.alarms.Count(a=>!a.acknowledged)));
            foreach(var t in s.tools)
            {
                text.AppendLine("\r\n"+t.Id+"："+t.phase+"；負責人："+t.owner+"；下一步："+t.next);
                text.AppendLine("晶圓位置："+t.wafer+"；產品："+(t.productHold?"待 PE 評估（未放行）":"無受影響產品記錄")+"；保護確認："+t.protectionConfirmed);
                if(t.controlledAt>=0&&t.alarms.Count>0)text.AppendLine("首次警報至確認受控："+N(t.controlledAt-t.alarms[0].time)+" 模擬秒");
                foreach(var a in t.alarms)text.AppendLine("First-Fail "+N(a.time)+"s "+a.code+"；流量 "+N(a.firstFail.flow)+" L/min；溫度 "+N(a.firstFail.temperatureC)+" °C；功率命令／回授 "+N(a.firstFail.commandKW)+"／"+N(a.firstFail.feedbackKW)+" kW；有效 "+a.active+"；ACK "+a.acknowledged+"；已清除 "+a.cleared);
            }
            text.AppendLine("\r\n支援分工（等待／服務時間均為本次模擬）：");
            foreach(var j in s.jobs)text.AppendLine(s.tools[j.tool].Id+" "+j.role+" "+Simulation.JobText(j.kind)+"／"+j.state+"；等待 "+(j.accepted<0?"未接手":N(j.accepted-j.requested)+"s")+"；"+j.report);
            text.AppendLine("\r\n決策與未確認資訊：");
            foreach(var e in s.events)text.AppendLine(N(e.time)+"s "+e.equipment+" ["+e.category+"] "+e.message+" "+e.reason+" "+e.note);
            text.AppendLine("\r\n"+s.DebriefTruth());
            text.AppendLine("未建模：產品量測、膜厚預測、良率、實體安全控制器。設備驗證不能代替產品放行。");
            File.WriteAllText(Path.Combine(path,"交接摘要.txt"),text.ToString(),new UTF8Encoding(true));
            return path;
        }
    }
}
