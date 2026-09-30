using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using PvdSim.Core;

namespace PvdSim
{
    // Optional Windows player QA, activated only by -pvdVerify. Captures actual rendered frames.
    public sealed class PlayerVerification:MonoBehaviour
    {
        SimulatorApp app;
        readonly List<string> results=new List<string>();
        string root;
        bool error;
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd,ref Point p);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
        [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;}
        IEnumerator Start()
        {
            app=GetComponent<SimulatorApp>();root=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Verification",(Application.isBatchMode?"Batch-":"Interactive-")+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));Directory.CreateDirectory(root);
            results.Add(Application.isBatchMode?"MODE: batch; public simulation API and offscreen 3D camera rendering; no UI or mouse verification":"MODE: interactive Windows OS mouse clicks");
            Debug.Log("PVD verification output: "+root);
            Application.logMessageReceived+=Log;
            if(Application.isBatchMode){yield return Batch();yield break;}
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)})
            {
                Screen.SetResolution(size.x,size.y,size.x==1920?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(2);
                Check(Screen.width==size.x&&Screen.height==size.y,"Resolution "+size);
                app.guided=false;app.muted=false;app.selected=0;app.ResetSession(Scenario.A);app.Overview();app.cutaway=false;yield return new WaitForSecondsRealtime(.7f);
                yield return Capture(size,"01-overview");
                app.Select(0,true);app.cutaway=true;yield return new WaitForSecondsRealtime(.7f);yield return Capture(size,"09-cutaway");app.cutaway=false;app.Overview();
                yield return Click("start");Check(app.session.running,"Actual start button "+size);
                Step(38);yield return null;yield return Capture(size,"02-multiple-alarms");
                var before=app.session.Time;yield return Click("pause");Step(5);Check(app.session.Time==before&&app.session.paused,"Actual pause freezes all "+size);yield return Click("pause");
                int count=app.session.tools.Sum(t=>t.alarms.Count);yield return Click("mute");Check(app.muted&&app.session.tools.Sum(t=>t.alarms.Count)==count&&!app.session.tools[0].alarms[0].acknowledged,"Mute is not ACK "+size);
                yield return Click("ack");Check(app.session.tools[0].alarms[0].acknowledged&&app.session.tools[0].flow<1,"ACK is not repair "+size);
                app.tab=0;yield return null;yield return Click("compare");Check(app.session.compared,"Comparison evidence button "+size);
                app.Select(1,true);yield return new WaitForSecondsRealtime(.6f);Check(!app.world.views[1].wafer.gameObject.activeSelf,"Unknown wafer hidden "+size);
                app.cutaway=true;var time=app.session.Time;var cmd=app.session.tools[1].commandKW;yield return null;Check(app.session.Time==time&&app.session.tools[1].commandKW==cmd,"Cutaway no plant change "+size);app.cutaway=false;app.Overview();
                app.tab=3;yield return null;var selected=app.selected;yield return Click("block");Check(app.selected==selected,"UI click does not select equipment "+size);
                Check(!app.IsWorldPoint(new Vector2(50,50))&&!app.IsWorldPoint(new Vector2(Screen.width-50,Screen.height/2)),"UI camera exclusion "+size);
                app.selected=0;yield return Click("confirm");app.tab=1;yield return null;yield return Click("safety");Step(3);yield return Capture(size,"03-support");
                var tour=new GuidedTour();bool captured=false;
                for(int n=0;n<1500;n++)
                {
                    app.session.Step();tour.Update(app.session);
                    if(!captured&&app.session.tools.Any(t=>t.phase==Phase.Qualifying&&t.stableHeld>2))
                    {app.selected=Array.FindIndex(app.session.tools,t=>t.phase==Phase.Qualifying&&t.stableHeld>2);app.tab=3;yield return null;yield return Capture(size,"04-qualification");captured=true;}
                }
                Check(app.session.tools.Take(3).All(t=>t.qualified&&t.phase==Phase.Ready),"A full recovery "+size);
                app.session.End();app.tab=4;yield return null;yield return Click("export");Check(File.Exists(Path.Combine(app.exportedPath,"report.json")),"UTF-8 JSON CSV handover export "+size);yield return Capture(size,"05-review");
                app.ResetSession(Scenario.B);app.session.Start();Step(38);app.tab=0;app.Overview();yield return null;yield return Click("compare");yield return Capture(size,"06-cross-tool-trends");
                app.tab=2;yield return null;yield return Click("topology");yield return Click("upstream");yield return Click("group");yield return Capture(size,"07-shared-utilities");
                tour=new GuidedTour();for(int n=0;n<1450;n++){app.session.Step();tour.Update(app.session);}
                Check(app.session.tools.Take(2).All(t=>t.qualified&&t.phase==Phase.Ready)&&app.session.tools.Skip(2).All(t=>t.alarms.Count==0),"B full recovery and unaffected B branch "+size);
                app.session.End();app.tab=4;yield return null;yield return Capture(size,"08-B-review");
                foreach(var r in app.controls)Check(r.Value.x>=0&&r.Value.xMax<=1920&&r.Value.y>=0&&r.Value.yMax<=1080,"Visible control "+size+" "+r.Key);
            }
            Finish();
        }
        IEnumerator Batch()
        {
            results.Add("SKIP: IMGUI text/layout, real button clicks, sound and window resolution; unavailable in this batch run.");
            app.guided=false;app.muted=true;app.selected=0;app.Overview();
            yield return new WaitForSecondsRealtime(1);
            Check(app.world.views.Length==4&&app.GetComponentsInChildren<ToolMarker>().Length==4,"Four independent equipment views instantiated in Windows player");
            foreach(var scenario in new[]{Scenario.A,Scenario.B})
            {
                app.ResetSession(scenario);app.StartScenario();
                yield return null;
                Capture3D(scenario+"-overview");
                Step(38);yield return null;
                int expected=scenario==Scenario.A?3:2;
                var heldProducts=app.session.tools.Select(t=>t.productHold).ToArray();
                Check(app.session.tools.Count(t=>t.ActiveAlarm)==expected,scenario+" expected affected tools");
                Capture3D(scenario+"-alarms");
                var before=app.session.Time;app.session.TogglePause();Step(5);
                Check(app.session.Time==before,scenario+" public pause freezes simulation");
                app.session.TogglePause();
                if(scenario==Scenario.A)
                {
                    Check(!app.world.views[1].wafer.gameObject.activeSelf&&!app.world.views[1].root.Find("Deposition wafer (principle)").gameObject.activeSelf,"Unknown wafer hidden in both 3D locations");
                    app.session.Acknowledge(0);
                    Check(app.session.tools[0].alarms[0].acknowledged&&app.session.tools[0].flow<1,"ACK leaves cooling fault active");
                }
                else
                {
                    app.session.ObserveComparison();app.session.ObserveTopology();app.session.ObserveUpstream();app.session.GroupIncident();
                    Check(app.session.grouped,"B observed evidence permits grouping without release");
                }
                var tour=new GuidedTour();bool captured=false;
                for(int n=0;n<1450;n++)
                {
                    app.session.Step();tour.Update(app.session);
                    if(!captured&&app.session.tools.Any(t=>t.phase==Phase.Qualifying&&t.stableHeld>2))
                    {
                        yield return null;Capture3D(scenario+"-qualification");captured=true;
                    }
                }
                Check(captured,scenario+" ran real qualification interval");
                Check(app.session.tools.Take(expected).All(t=>t.qualified&&t.phase==Phase.Ready),scenario+" equipment recovered separately");
                Check(app.session.tools.Select(t=>t.productHold).SequenceEqual(heldProducts),scenario+" product holds unchanged by equipment recovery; empty load lock has no product hold");
                Check(app.session.tools.Skip(expected).All(t=>t.alarms.Count==0),scenario+" unaffected controls stay normal");
                app.session.End();app.Export();
                Check(new[]{"report.json","trends.csv","events.csv","交接摘要.txt"}.All(f=>File.Exists(Path.Combine(app.exportedPath,f))),scenario+" player JSON/CSV/Chinese handover export");
                results.Add("EXPORT "+app.exportedPath);
                yield return null;Capture3D(scenario+"-recovered");
            }
            app.ResetSession(Scenario.A);app.StartScenario();Step(10);app.Select(0,true);app.cutaway=true;
            yield return new WaitForSecondsRealtime(1);
            Check(!app.world.views[0].cover.activeSelf&&app.world.views[1].cover.activeSelf,"Cutaway hides only selected shell");
            Check(app.session.Time==10&&app.session.tools[0].commandKW>0,"Cutaway preserves simulation time and processing");
            Capture3D("A-cutaway");
            Finish();
        }
        void Capture3D(string name)
        {
            // Batch players do not run IMGUI. Render only the real scene camera, never synthesize UI.
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)})
            {
                var camera=app.sceneCamera;var previousTarget=camera.targetTexture;var previousRect=camera.rect;var previousActive=RenderTexture.active;
                var target=new RenderTexture(size.x,size.y,24);var pixels=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,size.x,size.y),0,0);pixels.Apply();
                    string path=Path.Combine(root,size.x+"x"+size.y+"-3D-only-"+name+".png");File.WriteAllBytes(path,pixels.EncodeToPNG());
                    var colors=pixels.GetPixels32();var first=colors[0];int different=colors.Count(c=>Math.Abs(c.r-first.r)+Math.Abs(c.g-first.g)+Math.Abs(c.b-first.b)>35);
                    Check(different>colors.Length/20,"Nonblank offscreen 3D render "+name+" "+size);
                }
                finally{camera.targetTexture=previousTarget;camera.rect=previousRect;RenderTexture.active=previousActive;target.Release();Destroy(target);Destroy(pixels);}
            }
        }
        void Finish()
        {
            Check(!error,"No Unity runtime errors");
            foreach(var renderer in app.GetComponentsInChildren<Renderer>(true))Check(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader.isSupported,"Material "+renderer.name);
            string chars="薄膜多機台警報應變復機腔體濺鍍晶圓搬送冷卻狀態當下快照負責驗證處置廠務支援權限歸組調查趨勢聯鎖閥確認暫停";
            foreach(char c in chars)Check(app.cjkFont.HasCharacter(c),"CJK glyph "+c);
            File.WriteAllLines(Path.Combine(root,"player-checks.txt"),results);
            Application.Quit(results.Any(x=>x.StartsWith("FAIL"))?1:0);
        }
        void Step(double seconds){for(int n=0;n<(int)Math.Round(seconds/app.session.config.step);n++)app.session.Step();}
        IEnumerator Click(string key)
        {
            yield return null;
            if(!app.controls.TryGetValue(key,out Rect r)){Check(false,"Missing control "+key);yield break;}
            Point p=new Point{x=(int)(r.center.x*Screen.width/1920),y=(int)(r.center.y*Screen.height/1080)};
            ClientToScreen(GetActiveWindow(),ref p);SetCursorPos(p.x,p.y);yield return new WaitForSecondsRealtime(.10f);
            mouse_event(0x0002,0,0,0,UIntPtr.Zero);yield return new WaitForSecondsRealtime(.08f);mouse_event(0x0004,0,0,0,UIntPtr.Zero);yield return new WaitForSecondsRealtime(.18f);
        }
        IEnumerator Capture(Vector2Int size,string name)
        {
            string path=Path.Combine(root,size.x+"x"+size.y+"-"+name+".png");
            yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
            Check(File.Exists(path),"Actual screenshot "+name+" "+size);
        }
        void Check(bool ok,string message){results.Add((ok?"PASS ":"FAIL ")+message);if(!ok)Debug.LogWarning("QA FAIL "+message);}
        void Log(string message,string stack,LogType type)
        {
            if(type!=LogType.Exception&&type!=LogType.Error)return;
            error=true;results.Add("FAIL Unity runtime: "+message);File.WriteAllLines(Path.Combine(root,"player-checks.txt"),results);Application.Quit(1);
        }
    }
}
