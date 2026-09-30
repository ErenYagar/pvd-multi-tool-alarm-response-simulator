using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using PvdSim.Core;

namespace PvdSim
{
    public sealed class SimulatorApp : MonoBehaviour
    {
        public Font cjkFont;
        public Material surfaceMaterial;
        public Simulation session;
        public EquipmentWorld world;
        public Camera sceneCamera;
        public int selected,tab;
        public bool guided=true,cutaway,muted;
        public float speed=1,volume=.18f;
        public string exportedPath="";
        public readonly Dictionary<string,Rect> controls=new Dictionary<string,Rect>();
        public Entry eventDetail;
        public bool TestMode;
        public float Yaw {get{return yaw;}}
        GuidedTour tour=new GuidedTour();
        double accumulator;
        float yaw=-14,pitch=51,distance=24,targetDistance=24;
        Vector3 target=new Vector3(0,.6f,.5f),smoothTarget;
        bool orbiting;
        GUIStyle label,title,small,button,field;
        readonly Color navy=new Color(.035f,.067f,.094f),panel=new Color(.05f,.095f,.125f),edge=new Color(.15f,.24f,.29f),ink=new Color(.91f,.96f,.97f),mutedInk=new Color(.55f,.68f,.74f),cyan=new Color(.28f,.9f,.77f),orange=new Color(1,.63f,.31f);
        readonly Color[] plotColors={new Color(.28f,.9f,.77f),new Color(1,.65f,.3f),new Color(.44f,.68f,1),new Color(.8f,.66f,1)};
        Vector2 jobScroll,eventScroll;
        int reason;
        int firstFailIndex;
        string note="";
        int lastAlarmCount;
        int guideStage=-1;
        AudioSource sound;
        AudioClip ping;
        bool showEvidence,signalPage,showLog;
        string[] tabs={"趨勢／證據","支援分工","共用管路","處置／驗證","交接回顧"};
        void Awake()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;
            cjkFont=cjkFont??Resources.Load<Font>("Fonts/NotoSansCJKtc-Regular");
            if(cjkFont==null)throw new InvalidOperationException("缺少授權 CJK 字型");
            if(surfaceMaterial==null)surfaceMaterial=Resources.Load<Material>("IndustrialSurface");
            CreateWorld();ResetSession(Scenario.A);smoothTarget=target;
            sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;
            int rate=22050;var samples=new float[rate/7];for(int i=0;i<samples.Length;i++)samples[i]=(float)(Math.Sin(2*Math.PI*540*i/rate)*Math.Pow(1-(double)i/samples.Length,2)*.2);
            ping=AudioClip.Create("Soft alarm cue",samples.Length,1,rate,false);ping.SetData(samples,0);
            TestMode=Environment.GetCommandLineArgs().Contains("-pvdVerify");
            if(TestMode)gameObject.AddComponent<PlayerVerification>();
        }
        void CreateWorld()
        {
            var root=new GameObject("Procedural PVD gallery").transform;root.SetParent(transform,false);world=new EquipmentWorld(root,surfaceMaterial);
            var cameraObject=new GameObject("PVD inspection camera");cameraObject.transform.SetParent(transform,false);sceneCamera=cameraObject.AddComponent<Camera>();
            sceneCamera.clearFlags=CameraClearFlags.SolidColor;sceneCamera.backgroundColor=new Color(.055f,.09f,.12f);sceneCamera.fieldOfView=47;sceneCamera.nearClipPlane=.1f;sceneCamera.farClipPlane=100;
            sceneCamera.rect=new Rect(230f/1920,245f/1080,1420f/1920,745f/1080);
            cameraObject.AddComponent<AudioListener>();
            Light key=new GameObject("Large overhead key").AddComponent<Light>();key.transform.SetParent(transform,false);key.type=LightType.Directional;key.transform.rotation=Quaternion.Euler(48,-30,0);key.intensity=1.4f;key.shadows=LightShadows.Soft;
            Light fill=new GameObject("Cool fill").AddComponent<Light>();fill.transform.SetParent(transform,false);fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(40,150,0);fill.intensity=.65f;fill.color=new Color(.65f,.8f,1);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.54f,.61f);
            QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=45;
        }
        public void ResetSession(Scenario mode)
        {
            Config c=JsonUtility.FromJson<Config>(Resources.Load<TextAsset>("scenario-config").text);c.Validate();session=new Simulation(c,mode);tour=new GuidedTour();accumulator=0;lastAlarmCount=0;guideStage=-1;eventDetail=null;tab=0;exportedPath="";showEvidence=false;
        }
        public void StartScenario(){if(session.ended)ResetSession(session.scenario);session.Start();}
        void Update()
        {
            if(!TestMode && session.running&&!session.paused)
            {
                accumulator+=Time.unscaledDeltaTime*speed;
                int guard=0;while(accumulator>=session.config.step&&guard++<500){session.Step();if(guided)tour.Update(session);accumulator-=session.config.step;}
                if(guided)GuidePresentation();
            }
            world.Refresh(session,selected,cutaway);
            int count=session.tools.Sum(t=>t.alarms.Count);
            if(count>lastAlarmCount&&!muted)sound.PlayOneShot(ping,volume);lastAlarmCount=count;
            HandleCamera();
            distance=Mathf.Lerp(distance,targetDistance,1-Mathf.Exp(-Time.unscaledDeltaTime*6));smoothTarget=Vector3.Lerp(smoothTarget,target,1-Mathf.Exp(-Time.unscaledDeltaTime*6));
            sceneCamera.transform.position=smoothTarget+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);sceneCamera.transform.LookAt(smoothTarget);
        }
        public bool IsWorldPoint(Vector2 screen)
        {
            Vector2 p=new Vector2(screen.x*1920/Screen.width,(Screen.height-screen.y)*1080/Screen.height);
            return new Rect(230,132,1420,703).Contains(p)&&!new Rect(242,143,660,58).Contains(p)&&!(cutaway&&new Rect(255,655,620,160).Contains(p));
        }
        void GuidePresentation()
        {
            int stage=session.ended?6:session.Time>=130?5:session.Time>=85?4:session.Time>=56?3:session.Time>=38?2:session.Time>=25?1:0;
            if(stage==guideStage)return;guideStage=stage;
            if(stage==1){tab=0;selected=session.Priorities()[0].tool;}
            if(stage==2)tab=1;
            if(stage==3)tab=session.grouped?2:3;
            if(stage==4){tab=3;int q=Array.FindIndex(session.tools,t=>t.phase==Phase.Qualifying);if(q>=0)selected=q;}
            if(stage>=5)tab=4;
        }
        void HandleCamera()
        {
            if(Application.isBatchMode)return;
            bool worldPoint=IsWorldPoint(Input.mousePosition);
            if(Input.GetMouseButtonDown(1))orbiting=worldPoint;
            if(!Input.GetMouseButton(1))orbiting=false;
            if(!worldPoint)return;
            if(orbiting){yaw+=Input.GetAxis("Mouse X")*4;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*3,20,78);}
            targetDistance=Mathf.Clamp(targetDistance-Input.mouseScrollDelta.y*1.1f,5,34);
            if(Input.GetMouseButtonDown(0)&&Physics.Raycast(sceneCamera.ScreenPointToRay(Input.mousePosition),out RaycastHit hit,100))
            {var mark=hit.collider.GetComponentInParent<ToolMarker>();if(mark!=null)Select(mark.index,true);}
        }
        public void Select(int i,bool focus){selected=i;if(focus){target=world.views[i].root.position+new Vector3(0,1,0);targetDistance=8.8f;}}
        public void Overview(){target=new Vector3(0,.6f,.5f);targetDistance=24;yaw=-14;pitch=51;}
        public void Export()
        {
            try
            {
                string root=TestMode?Path.Combine(Application.dataPath,"..","Verification","Exports"):Path.Combine(Application.persistentDataPath,"Reports");
                exportedPath=ReportExporter.Export(session,root,r=>JsonUtility.ToJson(r,true));session.lastMessage="匯出完成："+exportedPath;
            }
            catch(Exception e){session.lastMessage="匯出失敗："+e.Message;}
        }
        void Styles()
        {
            if(label!=null)return;
            label=new GUIStyle {font=cjkFont,fontSize=23,normal={textColor=ink},wordWrap=true,clipping=TextClipping.Clip};
            small=new GUIStyle(label){fontSize=20};title=new GUIStyle(label){fontSize=30,fontStyle=FontStyle.Bold};
            button=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,fontSize=22,padding=new RectOffset(7,7,2,2)};
            field=new GUIStyle(label){fontSize=21,normal={background=Texture2D.whiteTexture,textColor=navy},padding=new RectOffset(8,8,4,4)};
        }
        void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Text(Rect r,string s,Color? c=null,GUIStyle style=null){var st=style??label;var old=st.normal.textColor;st.normal.textColor=c??ink;GUI.Label(r,s,st);st.normal.textColor=old;}
        bool Btn(string key,Rect r,string text,bool enabled=true,bool active=false)
        {
            controls[key]=r;Fill(r,active?new Color(.1f,.37f,.35f):enabled?new Color(.10f,.19f,.24f):new Color(.07f,.12f,.15f));
            Fill(new Rect(r.x,r.y,r.width,2),active?cyan:edge);bool old=GUI.enabled;GUI.enabled=enabled;var c=button.normal.textColor;button.normal.textColor=enabled?ink:mutedInk;
            bool clicked=GUI.Button(r,text,button);
            button.normal.textColor=c;GUI.enabled=old;return clicked;
        }
        void Line(Vector2 a,Vector2 b,Color c,float width=2)
        {
            var m=GUI.matrix;float angle=Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg;GUIUtility.RotateAroundPivot(angle,a);Fill(new Rect(a.x,a.y-width/2,(b-a).magnitude,width),c);GUI.matrix=m;
        }
        public static string PhaseName(Tool t)
        {return new[]{"加工中","受控停止","模擬處置完成","設備驗證中","待模擬授權","可接新批次"}[(int)t.phase];}
        public static string WaferName(Wafer w){return new[]{"腔體內","載具內","搬片區（已確認）","未確認","空／無晶圓"}[(int)w];}
        void OnGUI()
        {
            Styles();GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1920f,Screen.height/1080f,1));
            Fill(new Rect(0,0,1920,90),navy);Fill(new Rect(0,90,230,745),panel);Fill(new Rect(1650,90,270,745),panel);Fill(new Rect(0,835,1920,245),navy);
            Header();LeftPanel();RightPanel();WorldOverlay();BottomPanel();
            GUI.matrix=Matrix4x4.identity;
        }
        void Header()
        {
            Text(new Rect(24,10,655,38),"PVD  /  多機台警報應變模擬器",null,title);
            Text(new Rect(25,51,610,29),"FIELD LAB     ·     MULTI-TOOL RESPONSE",mutedInk,small);
            if(Btn("scenarioA",new Rect(660,15,160,54),"情境 A",!session.running,session.scenario==Scenario.A))ResetSession(Scenario.A);
            if(Btn("scenarioB",new Rect(832,15,160,54),"情境 B",!session.running,session.scenario==Scenario.B))ResetSession(Scenario.B);
            if(Btn("mode",new Rect(1006,15,170,54),guided?"導覽模式":"練習模式",!session.running))guided=!guided;
            if(Btn("start",new Rect(1190,15,175,54),session.ended?"重新開始":"開始情境",!session.running,true))StartScenario();
            if(Btn("pause",new Rect(1379,15,173,54),session.paused?"繼續訓練":"訓練暫停",session.running))session.TogglePause();
            if(Btn("speed",new Rect(1566,15,120,54),speed.ToString("0.#")+"× 速度"))speed=speed==1?2:speed==2?4:speed==4?.5f:1;
            if(Btn("end",new Rect(1700,15,193,54),"結束／回顧",session.running)){session.End();tab=4;}
        }
        void LeftPanel()
        {
            Text(new Rect(17,107,200,34),"全區警報與待辦",null,new GUIStyle(title){fontSize=25});
            Text(new Rect(17,145,200,35),session.Time.ToString("000.0")+" s  共用時鐘",cyan);
            var priorities=session.Priorities();float y=200;
            foreach(var p in priorities)
            {
                var t=session.tools[p.tool];
                if(Btn("select"+p.tool,new Rect(12,y,205,47),t.Id+"   "+(t.ActiveAlarm?"!":"○"),true,selected==p.tool))Select(p.tool,true);
                Text(new Rect(20,y+54,196,30),PhaseName(t),t.ActiveAlarm?orange:cyan,small);
                Text(new Rect(20,y+85,196,30),p.basis,mutedInk,small);
                if(t.alarms.Count>0)Text(new Rect(20,y+116,196,27),"首報 "+t.alarms[0].time.ToString("F1")+" s",mutedInk,new GUIStyle(small){fontSize=18});
                y+=145;
            }
            Text(new Rect(17,785,200,42),"依未控制風險排序",mutedInk,small);
        }
        void RightPanel()
        {
            var t=session.tools[selected];float x=1667;
            Text(new Rect(x,106,240,43),t.Id,null,title);
            if(Btn("signals",new Rect(1829,108,75,38),signalPage?"摘要":"訊號"))signalPage=!signalPage;
            Text(new Rect(x,151,240,33),(t.ActiveAlarm?"[!] ":"[○] ")+PhaseName(t),t.ActiveAlarm?orange:cyan);
            if(signalPage)
            {
                Text(new Rect(x,200,236,59),"腔體壓力\n"+t.chamberTorr.ToString("F4")+" Torr",null,small);
                Text(new Rect(x,272,236,59),"Load Lock 壓力\n"+t.loadLockTorr.ToString("F3")+" Torr",null,small);
                Text(new Rect(x,344,236,59),"冷卻入口溫度\n"+t.coolantC.ToString("F1")+" °C",null,small);
                Text(new Rect(x,416,236,82),"泵："+(t.pump?"運轉":"停止")+"\n搬片："+(t.wafer==Wafer.Unconfirmed?"逾時／受控停止":t.armProgress>0?"轉移步驟":"原點／待命"),null,small);
                Text(new Rect(x,510,236,95),"原點訊號："+t.armHome+"\n終點訊號："+t.armDestination+"\n位置一致："+t.positionConsistent,t.positionConsistent?ink:orange,small);
                Text(new Rect(x,620,236,58),"資訊："+(session.Fresh(t)?"目前量測":"過期／未確認")+"\n負責："+t.owner,mutedInk,small);
                Text(new Rect(x,701,236,110),"下一步\n"+t.next,cyan,small);
                return;
            }
            Text(new Rect(x,195,235,57),"晶圓  "+WaferName(t.wafer),t.wafer==Wafer.Unconfirmed?orange:ink);
            Text(new Rect(x,255,237,60),t.productHold?"產品保留\n待 PE 評估／未放行":"產品：無異常保留紀錄",mutedInk,small);
            Value(x,329,"冷卻流量",t.flow.ToString("F2")+" L/min",t.flow<3);
            Value(x,408,"設備溫度",t.temperatureC.ToString("F1")+" °C",t.temperatureSlope>.08&&t.ActiveAlarm);
            Text(new Rect(x,489,235,52),"功率命令／回授\n"+t.commandKW.ToString("F2")+" / "+t.feedbackKW.ToString("F2")+" kW",null,small);
            Text(new Rect(x,556,238,50),"保護："+(t.protectionConfirmed?"[✓] 已確認":"[?] 尚未確認"),t.protectionConfirmed?cyan:orange,small);
            Text(new Rect(x,609,238,62),"批次："+(t.lotsBlocked?"保留":"允許")+"\n負責："+t.owner,null,small);
            if(Btn("ack",new Rect(x,680,234,47),"收悉 ACK",session.running&&!session.paused&&t.alarms.Count>0))session.Acknowledge(selected);
            if(Btn("firstFail",new Rect(x,739,234,47),"警報當下快照",t.alarms.Count>0)){tab=0;showEvidence=!showEvidence;}
            Text(new Rect(x,794,237,27),"ACK ≠ 修復 ≠ 復機",mutedInk,small);
        }
        void Value(float x,float y,string name,string value,bool alert){Text(new Rect(x,y,230,28),name,mutedInk,small);Text(new Rect(x,y+29,240,43),value,alert?orange:ink,title);}
        void WorldOverlay()
        {
            Fill(new Rect(230,90,1420,42),new Color(.07f,.12f,.16f));
            Text(new Rect(252,97,850,32),session.scenario==Scenario.A?"A  /  三台異常接近同時發生 · 依目前風險決定下一步":"B  /  多機台警報 · 建立共同原因假設並交叉驗證",mutedInk,small);
            Text(new Rect(1260,97,370,30),"左鍵選取 · 右鍵旋轉 · 滾輪縮放",mutedInk,small);
            if(Btn("overview",new Rect(248,150,144,45),"全區總覽"))Overview();
            if(Btn("cutaway",new Rect(404,150,188,45),cutaway?"關閉腔體剖視":"腔體剖視",true,cutaway))cutaway=!cutaway;
            if(Btn("mute",new Rect(605,150,144,45),muted?"靜音中":"提示音 開"))muted=!muted;
            controls["volume"]=new Rect(768,160,105,25);volume=GUI.HorizontalSlider(controls["volume"],volume,0,.7f);
            for(int i=0;i<4;i++)
            {
                Vector3 p=sceneCamera.WorldToScreenPoint(world.views[i].root.position+new Vector3(0,2.95f,0));
                if(p.z<=0)continue;var r=new Rect(p.x*1920/Screen.width-115,(Screen.height-p.y)*1080/Screen.height-21,230,43);
                if(r.x<234||r.xMax>1646||r.y<204||r.yMax>816)continue;
                Fill(r,new Color(.035f,.075f,.10f,.95f));Text(new Rect(r.x+8,r.y+5,220,33),session.tools[i].Id+"  "+(session.tools[i].ActiveAlarm?"[!] ALARM":"[○] "+(session.tools[i].phase==Phase.Processing?"RUN":"HOLD")),session.tools[i].ActiveAlarm?orange:cyan,small);
            }
            if(cutaway)
            {
                Fill(new Rect(255,655,620,160),panel);Text(new Rect(272,668,583,37),"腔體剖視  /  原理示意",cyan);
                Text(new Rect(272,710,583,95),"上方靶材 → 濺鍍粒子 → 下方晶圓\n只切換外殼顯示；沒有開門或改變設備狀態。\n沉積動畫不能預測膜厚或良率。",mutedInk,small);
            }
            if(!session.running&&!session.ended)
            {
                Fill(new Rect(910,721,705,94),panel);
                Text(new Rect(929,729,662,36),"四台獨立狀態 · 一個共同時鐘",cyan);
                Text(new Rect(929,770,662,34),"按上方「開始情境」；導覽約 3 分鐘，可切換練習。",mutedInk,small);
            }
        }
        void BottomPanel()
        {
            for(int i=0;i<tabs.Length;i++)if(Btn("tab"+i,new Rect(18+i*232,844,221,43),tabs[i],true,tab==i)){tab=i;eventDetail=null;}
            string guidance=guideStage<=1?"觀察跨台警報與保護回授":guideStage==2?"確認人員接手與分工":guideStage==3?"處置需持續符合安全與權限條件":guideStage==4?"重新驗證；產品仍待評估":"回顧決策、支援與待辦";
            Text(new Rect(1196,849,696,33),session.paused?"[Ⅱ] 全區訓練暫停":guided&&session.running?"導覽："+guidance:"練習：自行選擇下一步與理由",cyan,small);
            if(tab==0)Evidence();if(tab==1)Support();if(tab==2)Utilities();if(tab==3)Actions();if(tab==4)Review();
            Fill(new Rect(0,1021,1920,31),panel);Text(new Rect(20,1025,1880,27),session.lastMessage,cyan,new GUIStyle(small){fontSize=18});
            Text(new Rect(20,1055,1880,25),Simulation.Disclaimer,mutedInk,new GUIStyle(small){fontSize=17});
        }
        void Evidence()
        {
            var t=session.tools[selected];
            if(showEvidence&&t.alarms.Count>0)
            {
                firstFailIndex=Math.Min(firstFailIndex,t.alarms.Count-1);var a=t.alarms[firstFailIndex];var f=a.firstFail;
                Text(new Rect(25,902,450,40),"FIRST-FAIL  /  "+a.time.ToString("F1")+" s",orange);
                Text(new Rect(25,942,500,64),t.Id+"  "+a.code+"\n嚴重度 "+a.severity+" · "+(a.active?"仍有效":"已回常")+" · "+(a.acknowledged?"已收悉":"未收悉"),null,small);
                Text(new Rect(535,902,850,92),string.Format("流量 {0:F2} L/min  ·  溫度 {1:F1} °C\n功率命令／回授 {2:F2} / {3:F2} kW  ·  LL {4:F3} Torr\n搬片起點 {5}／終點 {6}；晶圓 {7}",f.flow,f.temperatureC,f.commandKW,f.feedbackKW,f.loadLockTorr,f.armHome,f.armDestination,f.wafer),null,small);
                Text(new Rect(1400,901,470,64),"快照固定在保護動作之前\n查看、ACK 不會覆寫。",mutedInk,small);
                if(t.alarms.Count>1 && Btn("nextAlarm",new Rect(1430,932,430,34),"切換原始警報 "+(firstFailIndex+1)+" / "+t.alarms.Count))firstFailIndex=(firstFailIndex+1)%t.alarms.Count;
                if(Btn("backTrend",new Rect(1430,969,430,38),"回到趨勢與跨台比較"))showEvidence=false;
                return;
            }
            Plot(new Rect(65,919,585,80),x=>x.flow,8,"冷卻流量 L/min");
            Plot(new Rect(750,919,585,80),x=>x.temperatureC,70,"設備溫度 °C");
            Text(new Rect(1390,901,475,29),"01 ━   02 ┄   03 ···   04 ─·",mutedInk,small);
            if(Btn("compare",new Rect(1400,938,460,43),"記錄跨台證據與正常對照",session.running&&!session.paused))session.ObserveComparison();
            Text(new Rect(1399,985,473,27),"趨勢由同一模型歷史產生；最近 90 秒",mutedInk,new GUIStyle(small){fontSize=17});
        }
        void Plot(Rect r,Func<Sample,double> value,double max,string name)
        {
            Text(new Rect(r.x,r.y-26,480,27),name,mutedInk,new GUIStyle(small){fontSize=18});
            for(int n=0;n<3;n++){float y=r.y+r.height*n/2;Line(new Vector2(r.x,y),new Vector2(r.xMax,y),edge,1);Text(new Rect(r.x-43,y-12,41,23),(max*(2-n)/2).ToString("0"),mutedInk,new GUIStyle(small){fontSize=16});}
            double start=Math.Max(0,session.Time-90),end=Math.Max(90,session.Time);
            for(int i=0;i<4;i++)
            {
                var history=session.tools[i].history;Sample prev=null;int segment=0;
                foreach(var h in history)
                {
                    if(h.time<start){prev=null;continue;}
                    if(prev!=null && (i==0||i==1&&segment%5<3||i==2&&segment%3==0||i==3&&segment%7<4))
                        Line(new Vector2(r.x+(float)((prev.time-start)/(end-start))*r.width,r.yMax-(float)(value(prev)/max)*r.height),new Vector2(r.x+(float)((h.time-start)/(end-start))*r.width,r.yMax-(float)(value(h)/max)*r.height),plotColors[i],2.5f);
                    prev=h;segment++;
                }
                foreach(var alarm in session.tools[i].alarms)
                {if(alarm.time<start||alarm.time>end)continue;float ax=r.x+(float)((alarm.time-start)/(end-start))*r.width;Line(new Vector2(ax,r.y),new Vector2(ax,r.yMax),new Color(.55f,.3f,.17f),1);}
            }
            Text(new Rect(r.x+480,r.y-27,105,25),end.ToString("F0")+" s",mutedInk,new GUIStyle(small){fontSize=17});
        }
        void Support()
        {
            bool on=session.running&&!session.paused;
            Text(new Rect(25,900,575,30),"角色為教學設定 · 現場 EE 1 位／廠務 1 位",mutedInk,small);
            if(Btn("safety",new Rect(25,942,240,52),"請求主管核准",on))session.Request(selected,JobKind.SupervisorSafety);
            if(Btn("position",new Rect(279,942,275,52),"請 EE 確認位置",on))session.Request(selected,JobKind.PositionCheck);
            if(Btn("product",new Rect(568,942,225,52),"通報 PE／製造",on))session.Request(selected,JobKind.ProductReview);
            Rect viewport=new Rect(820,902,1062,107);jobScroll=GUI.BeginScrollView(viewport,jobScroll,new Rect(0,0,1025,Math.Max(107,session.jobs.Count*39)));
            for(int n=0;n<session.jobs.Count;n++)
            {
                var j=session.jobs[n];string state=new[]{"提出請求／排隊","已接手","確認／處理中","已回報","條件改變／中止"}[(int)j.state];
                string row=session.tools[j.tool].Id+" · "+j.role+" · "+Simulation.JobText(j.kind)+" → "+state;
                Text(new Rect(0,n*39,1020,35),row,j.state==JobState.Blocked?orange:ink,small);
                if(GUI.Button(new Rect(0,n*39,1020,35),new GUIContent("",j.report),GUIStyle.none))
                {eventDetail=session.events.LastOrDefault(e=>e.equipment==session.tools[j.tool].Id&&e.category.StartsWith("支援"));tab=4;}
            }
            GUI.EndScrollView();
            if(session.jobs.Count==0)Text(new Rect(839,932,1010,60),"尚無支援請求。接手、處理及回報使用共同模擬時鐘；條件改變會中止任務。",mutedInk,small);
        }
        void Utilities()
        {
            Text(new Rect(26,899,563,35),"A → PVD-01 / 02     B → PVD-03 / 04",cyan);
            Text(new Rect(26,941,595,68),string.Format("上游 A  {0:F2} L/min · {1:F1} °C\n上游 B  {2:F2} L/min · {3:F1} °C",session.branches[0].measuredFlow,session.branches[0].temperatureC,session.branches[1].measuredFlow,session.branches[1].temperatureC),null,small);
            bool on=session.running&&!session.paused;
            if(Btn("topology",new Rect(625,904,220,44),"確認連接拓樸",on))session.ObserveTopology();
            if(Btn("upstream",new Rect(860,904,220,44),"記錄上游量測",on))session.ObserveUpstream();
            if(Btn("group",new Rect(1095,904,247,44),"歸組為待調查事件",on))session.GroupIncident();
            if(Btn("facilities",new Rect(1356,904,510,44),"請廠務確認疑似共同原因",on))session.Request(0,JobKind.Facilities);
            Text(new Rect(625,964,1253,44),"證據："+(session.compared?"[✓] 跨台 ":"[?] 跨台 ")+(session.topologyViewed?"[✓] 連接 ":"[?] 連接 ")+(session.upstreamViewed?"[✓] 上游 ":"[?] 上游 ")+" · 歸組不等於根因確認；局部處置不改變共用供應。",mutedInk,small);
        }
        void Actions()
        {
            var t=session.tools[selected];bool on=session.running&&!session.paused;
            if(Btn("confirm",new Rect(23,901,252,43),"確認保護與受控",on))session.ConfirmProtection(selected);
            if(Btn("block",new Rect(288,901,212,43),"阻止新批次",on))session.BlockLots(selected);
            if(Btn("reason",new Rect(514,901,309,43),Simulation.ReasonText((Reason)reason)))reason=(reason+1)%5;
            note=GUI.TextField(new Rect(838,903,252,40),note,70,field);
            if(string.IsNullOrEmpty(note))Text(new Rect(847,909,238,28),"可加註簡短理由",new Color(.32f,.4f,.43f),small);
            if(Btn("prioritize",new Rect(1104,901,198,43),"記錄優先理由",on))session.ChoosePriority(selected,(Reason)reason,note);
            if(Btn("repair",new Rect(23,957,252,44),"局部冷卻處置",on))session.Request(selected,JobKind.CoolingRepair);
            if(Btn("transferRepair",new Rect(288,957,212,44),"搬片模擬處置",on))session.Request(selected,JobKind.TransferRepair);
            if(Btn("vacuumRepair",new Rect(514,957,240,44),"Load Lock 處置",on))session.Request(selected,JobKind.VacuumRepair);
            if(Btn("clear",new Rect(768,957,164,44),"解除警報",on))session.ClearAlarms(selected);
            if(Btn("qualify",new Rect(946,957,177,44),"開始驗證",on,true))session.Qualify(selected);
            if(Btn("release",new Rect(1137,957,165,44),"申請復機",on))session.Request(selected,JobKind.Release);
            Fill(new Rect(1324,901,558,106),panel);
            Text(new Rect(1337,906,530,29),"QUALIFICATION · "+t.stableHeld.ToString("F1")+" / "+session.config.stableSeconds+" s",cyan,small);
            Fill(new Rect(1340,944,524,10),edge);Fill(new Rect(1340,944,524*Mathf.Clamp01((float)(t.stableHeld/session.config.stableSeconds)),10),cyan);
            Text(new Rect(1338,965,530,34),Flag(t.coolingPass)+"冷卻 "+Flag(t.temperaturePass)+"熱 "+Flag(t.powerPass)+"電源 "+Flag(t.positionPass)+"位置 "+Flag(t.vacuumPass)+"真空",mutedInk,small);
        }
        static string Flag(bool ok){return ok?"[✓]":"[·]";}
        void Review()
        {
            if(eventDetail!=null)
            {
                Text(new Rect(25,900,1310,35),eventDetail.time.ToString("F1")+"s  "+eventDetail.equipment+" / "+eventDetail.category+" / "+eventDetail.message,null,small);
                var f=eventDetail.visible;
                Text(new Rect(25,945,1350,67),"理由："+eventDetail.reason+" "+eventDetail.note+"\n"+(f==null?"區域事件；請對照各台原始歷史":string.Format("當時可見：{0:F2} L/min · {1:F1} °C · 功率 {2:F2}/{3:F2} kW · 晶圓 {4} · 保護確認 {5}",f.flow,f.temperatureC,f.commandKW,f.feedbackKW,f.wafer,f.protectionConfirmed)),mutedInk,small);
                if(Btn("timelineBack",new Rect(1410,907,457,46),"返回四台時間軸"))eventDetail=null;
            }
            else
            {
                if(showLog)
                {
                    eventScroll=GUI.BeginScrollView(new Rect(20,898,1008,112),eventScroll,new Rect(0,0,970,Math.Max(112,session.events.Count*38)));
                    for(int n=0;n<session.events.Count;n++)
                    {
                        var e=session.events[session.events.Count-1-n];string row=e.time.ToString("F1")+" s · "+e.equipment+" · "+e.category+" · "+e.message;
                        if(row.Length>64)row=row.Substring(0,64)+"…";
                        Text(new Rect(0,n*38,966,35),row,mutedInk,small);
                        if(GUI.Button(new Rect(0,n*38,966,35),GUIContent.none,GUIStyle.none))eventDetail=e;
                    }
                    GUI.EndScrollView();
                }
                else for(int i=0;i<4;i++)
                {
                    float y=899+i*28;Text(new Rect(22,y-2,113,27),session.tools[i].Id,plotColors[i],small);Line(new Vector2(137,y+12),new Vector2(1005,y+12),edge,2);
                    foreach(var e in session.events.Where(e=>e.equipment==session.tools[i].Id))
                    {
                        if(e.category=="操作未執行")continue;
                        float x=144+(float)(e.time/Math.Max(session.Time,1))*840;
                        var r=new Rect(x-5,y+1,11,21);Fill(r,e.category=="警報"?orange:plotColors[i]);
                        if(GUI.Button(new Rect(x-7,y,16,26),GUIContent.none,GUIStyle.none)){eventDetail=e;break;}
                    }
                }
                Text(new Rect(1050,903,363,55),"驗證完成  "+session.tools.Count(t=>t.qualified)+" / 4\n未收悉事件  "+session.tools.Sum(t=>t.alarms.Count(a=>!a.acknowledged)),cyan,small);
                if(Btn("export",new Rect(1445,903,423,46),"匯出 CSV／JSON／交接摘要"))Export();
                if(Btn("events",new Rect(1050,965,363,40),showLog?"四台並列時間軸":"所有事件與快照"))showLog=!showLog;
            }
            if(!string.IsNullOrEmpty(exportedPath))Text(new Rect(1424,956,460,60),exportedPath,mutedInk,new GUIStyle(small){fontSize=16});
            else if(session.ended)
            {
                if(Btn("debrief",new Rect(1445,963,423,40),"情境檢討／仍待確認事項"))eventDetail=new Entry {time=session.Time,equipment="AREA",category="結束後檢討",message=session.DebriefTruth(),note="產品沒有品質資料，保持待 PE 評估。所有未完成驗證、未回報支援均須繼續追蹤。"};
            }
            else Text(new Rect(1445,963,422,46),"點時間軸標記可看當時快照",mutedInk,small);
        }
    }
}
