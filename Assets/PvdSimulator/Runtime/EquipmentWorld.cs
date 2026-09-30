using UnityEngine;
using PvdSim.Core;
using System.Collections.Generic;

namespace PvdSim
{
    public sealed class EquipmentWorld
    {
        public sealed class View
        {
            public Transform root,arm,wafer,lamp,selection;
            public GameObject cover,plasma;
            public readonly List<Transform> particles=new List<Transform>();
        }
        public readonly View[] views=new View[4];
        public readonly List<Transform>[] arrows={new List<Transform>(),new List<Transform>()};
        readonly Material white,steel,dark,teal,blue,amber,waferMaterial,glass;
        public EquipmentWorld(Transform parent,Material template)
        {
            white=Mat(template,new Color(.79f,.85f,.88f),.32f,.35f);
            steel=Mat(template,new Color(.38f,.48f,.53f),.72f,.65f);
            dark=Mat(template,new Color(.035f,.067f,.091f),.4f,.2f);
            teal=Mat(template,new Color(.09f,.75f,.67f),.5f,.3f);
            blue=Mat(template,new Color(.20f,.51f,.88f),.5f,.3f);
            amber=Mat(template,new Color(.97f,.55f,.17f),.35f,.2f);
            waferMaterial=Mat(template,new Color(.18f,.28f,.48f),.86f,.95f);
            glass=Mat(template,new Color(.2f,.43f,.52f),.75f,.6f);
            Shape(parent,"Exhibition plinth",PrimitiveType.Cube,new Vector3(0,-.25f,0),new Vector3(20,.45f,14),dark);
            for(int x=-9;x<=9;x++)Shape(parent,"Floor seam",PrimitiveType.Cube,new Vector3(x,-.016f,0),new Vector3(.016f,.016f,13),steel);
            for(int z=-6;z<=6;z++)Shape(parent,"Floor seam",PrimitiveType.Cube,new Vector3(0,-.015f,z),new Vector3(19,.015f,.016f),steel);
            for(int i=0;i<4;i++)
            {
                var root=new GameObject("PVD-"+(i+1).ToString("00")).transform;root.SetParent(parent,false);
                root.localPosition=new Vector3(i%2==0?-4.6f:4.1f,0,i<2?-3.6f:3.5f);
                root.gameObject.AddComponent<ToolMarker>().index=i;
                views[i]=Build(root);
            }
            for(int branch=0;branch<2;branch++)
            {
                float z=branch==0?-1.25f:5.85f;
                Pipe(parent,"Shared supply "+(branch==0?"A":"B"),new Vector3(-8,.23f,z),new Vector3(7,.23f,z),.13f,branch==0?teal:blue);
                Pipe(parent,"Return "+branch,new Vector3(-8,.20f,z+.32f),new Vector3(7,.20f,z+.32f),.09f,steel);
                Shape(parent,"Supply sensor",PrimitiveType.Cube,new Vector3(-7.5f,.6f,z),new Vector3(.6f,.8f,.5f),white);
                Shape(parent,"Sensor display",PrimitiveType.Cube,new Vector3(-7.5f,.75f,z-.26f),new Vector3(.39f,.26f,.02f),dark);
                for(int i=0;i<8;i++)arrows[branch].Add(Shape(parent,"Flow direction",PrimitiveType.Sphere,new Vector3(-7+i*1.8f,.24f,z),new Vector3(.22f,.22f,.22f),branch==0?teal:blue));
                for(int i=branch*2;i<branch*2+2;i++)
                {
                    var p=views[i].root.localPosition;
                    Pipe(parent,"Cooling branch",new Vector3(p.x+1.25f,.25f,z),new Vector3(p.x+1.25f,.25f,p.z+.8f),.09f,branch==0?teal:blue);
                    Pipe(parent,"Cooling riser",new Vector3(p.x+1.25f,.25f,p.z+.8f),new Vector3(p.x+1.25f,1.8f,p.z+.8f),.09f,branch==0?teal:blue);
                }
            }
        }
        View Build(Transform root)
        {
            var v=new View {root=root};
            Shape(root,"Isolation base",PrimitiveType.Cube,new Vector3(0,.13f,0),new Vector3(4.4f,.26f,4.1f),steel);
            v.selection=Shape(root,"Selected perimeter",PrimitiveType.Cube,new Vector3(0,.03f,0),new Vector3(4.7f,.045f,4.35f),teal);
            Shape(root,"Equipment lower enclosure",PrimitiveType.Cube,new Vector3(0,.55f,.1f),new Vector3(2.9f,.9f,2.85f),white);
            Shape(root,"Dark service band",PrimitiveType.Cube,new Vector3(0,.42f,-1.335f),new Vector3(2.8f,.28f,.03f),dark);
            for(int i=0;i<6;i++)Shape(root,"Service vent",PrimitiveType.Cube,new Vector3(-.9f+i*.36f,.71f,-1.338f),new Vector3(.22f,.065f,.035f),steel);
            Shape(root,"Chamber lower flange",PrimitiveType.Cylinder,new Vector3(0,1.08f,.65f),new Vector3(2.14f,.11f,2.14f),steel);
            v.cover=Shape(root,"Chamber shell (visual only)",PrimitiveType.Cylinder,new Vector3(0,1.62f,.65f),new Vector3(2,.47f,2),white).gameObject;
            Shape(v.cover.transform,"Chamber lid",PrimitiveType.Cylinder,new Vector3(0,1.05f,0),new Vector3(1.07f,.14f,1.07f),steel);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                Shape(root,"Flange fastener",PrimitiveType.Cylinder,new Vector3(Mathf.Sin(a)*1.02f,1.21f,.65f+Mathf.Cos(a)*1.02f),new Vector3(.10f,.065f,.10f),dark);
            }
            Shape(root,"Sputter target",PrimitiveType.Cylinder,new Vector3(0,1.96f,.65f),new Vector3(1.46f,.035f,1.46f),amber);
            Shape(root,"Wafer chuck",PrimitiveType.Cylinder,new Vector3(0,1.24f,.65f),new Vector3(1.53f,.06f,1.53f),dark);
            Shape(root,"Deposition wafer (principle)",PrimitiveType.Cylinder,new Vector3(0,1.33f,.65f),new Vector3(1.27f,.013f,1.27f),waferMaterial);
            v.plasma=Shape(root,"Plasma principle",PrimitiveType.Cylinder,new Vector3(0,1.85f,.65f),new Vector3(1.1f,.025f,1.1f),blue).gameObject;
            for(int i=0;i<14;i++)
            {
                float a=i*2.4f;var bead=Shape(root,"Deposition principle particle",PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.45f,1.5f,.65f+Mathf.Cos(a)*.45f),Vector3.one*.045f,amber);v.particles.Add(bead);
            }
            Shape(root,"Load lock",PrimitiveType.Cube,new Vector3(1.22f,1.18f,-.65f),new Vector3(.9f,.5f,1.15f),steel);
            Shape(root,"Load lock inspection cap",PrimitiveType.Cylinder,new Vector3(1.22f,1.47f,-.65f),new Vector3(.58f,.035f,.68f),white);
            Shape(root,"Transfer deck",PrimitiveType.Cylinder,new Vector3(-.25f,1.13f,-.92f),new Vector3(1.35f,.12f,1.2f),dark);
            v.arm=new GameObject("Wafer robot assembly").transform;v.arm.SetParent(root,false);v.arm.localPosition=new Vector3(-.25f,1.3f,-.9f);
            Shape(v.arm,"Robot pivot",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.33f,.1f,.33f),teal);
            Shape(v.arm,"Robot link one",PrimitiveType.Cube,new Vector3(-.28f,.11f,0),new Vector3(.7f,.10f,.17f),steel);
            Shape(v.arm,"Robot link two",PrimitiveType.Cube,new Vector3(-.5f,.16f,-.32f),new Vector3(.13f,.08f,.7f),white);
            v.wafer=Shape(v.arm,"Observable transported wafer",PrimitiveType.Cylinder,new Vector3(-.5f,.23f,-.58f),new Vector3(.69f,.011f,.69f),waferMaterial);
            Shape(root,"Load port pedestal",PrimitiveType.Cube,new Vector3(-.9f,.75f,-1.83f),new Vector3(1.22f,1.35f,.63f),white);
            Shape(root,"Load port plate",PrimitiveType.Cube,new Vector3(-.9f,1.38f,-1.8f),new Vector3(1.35f,.08f,.95f),steel);
            Shape(root,"FOUP back",PrimitiveType.Cube,new Vector3(-.9f,1.77f,-1.48f),new Vector3(1.07f,.7f,.15f),dark);
            for(int i=0;i<5;i++)Shape(root,"FOUP wafer slot",PrimitiveType.Cube,new Vector3(-.9f,1.52f+i*.11f,-1.74f),new Vector3(.92f,.022f,.58f),glass);
            Shape(root,"Sputter power supply",PrimitiveType.Cube,new Vector3(-1.7f,1.15f,.55f),new Vector3(.57f,1.9f,1.9f),white);
            for(int i=0;i<6;i++)Shape(root,"Power rack panel",PrimitiveType.Cube,new Vector3(-1.7f,.55f+i*.21f,-.42f),new Vector3(.45f,.14f,.025f),dark);
            Shape(root,"Operator display",PrimitiveType.Cube,new Vector3(.75f,1.96f,-1.26f),new Vector3(.88f,.65f,.13f),dark);
            Shape(root,"Display glass",PrimitiveType.Cube,new Vector3(.75f,1.97f,-1.34f),new Vector3(.71f,.44f,.022f),teal);
            Pipe(root,"Display stand",new Vector3(.75f,.9f,-1.2f),new Vector3(.75f,1.85f,-1.2f),.1f,steel);
            var pump=Shape(root,"Vacuum dry pump",PrimitiveType.Cylinder,new Vector3(1.86f,.55f,.9f),new Vector3(.55f,.63f,.55f),steel);pump.localRotation=Quaternion.Euler(0,0,90);
            Shape(root,"Pump motor",PrimitiveType.Cube,new Vector3(1.6f,.52f,.9f),new Vector3(.5f,.6f,.69f),dark);
            Pipe(root,"Vacuum foreline",new Vector3(1,1.05f,.65f),new Vector3(1.8f,1.05f,.65f),.17f,steel);
            Pipe(root,"Vacuum drop",new Vector3(1.8f,1.05f,.65f),new Vector3(1.8f,.55f,.65f),.17f,steel);
            Pipe(root,"Cooling jacket feed",new Vector3(1.25f,1.8f,.8f),new Vector3(.85f,1.8f,.8f),.075f,teal);
            Pipe(root,"Power cable",new Vector3(-1.45f,2,.55f),new Vector3(-.85f,2,.55f),.07f,dark);
            Pipe(root,"Stacklight stalk",new Vector3(-1.3f,1,-.9f),new Vector3(-1.3f,2.5f,-.9f),.055f,steel);
            v.lamp=Shape(root,"Status lamp",PrimitiveType.Cylinder,new Vector3(-1.3f,2.55f,-.9f),new Vector3(.22f,.14f,.22f),teal);
            return v;
        }
        static Material Mat(Material template,Color c,float metallic,float smooth){var m=new Material(template);m.color=c;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);return m;}
        static Transform Shape(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material m)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=m;return go.transform;
        }
        static void Pipe(Transform p,string name,Vector3 a,Vector3 b,float radius,Material m)
        {var t=Shape(p,name,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius,(b-a).magnitude*.5f,radius),m);t.up=(b-a).normalized;}
        public void Refresh(Simulation s,int selected,bool cutaway)
        {
            for(int i=0;i<4;i++)
            {
                var t=s.tools[i];var v=views[i];bool cut=cutaway&&i==selected;
                v.cover.SetActive(!cut);v.plasma.SetActive(cut&&t.feedbackKW>.1);
                v.selection.gameObject.SetActive(i==selected);
                v.arm.localRotation=Quaternion.Euler(0,(float)t.armProgress*105,0);
                v.wafer.gameObject.SetActive(t.wafer==Wafer.Arm);
                v.lamp.GetComponent<Renderer>().sharedMaterial=t.ActiveAlarm?amber:t.phase==Phase.Ready||t.phase==Phase.Processing?teal:blue;
                foreach(var particle in v.particles)
                {particle.gameObject.SetActive(cut&&t.feedbackKW>.1);var p=particle.localPosition;p.y=1.86f-(float)((s.Time*.7+v.particles.IndexOf(particle)*.071)%1)*.48f;particle.localPosition=p;}
                // Unknown physical location is not drawn, including the illustrative chamber wafer.
                v.root.Find("Deposition wafer (principle)").gameObject.SetActive(t.wafer==Wafer.Chamber);
            }
            for(int b=0;b<2;b++)for(int k=0;k<arrows[b].Count;k++)
            {var p=arrows[b][k].localPosition;p.x=-7+(float)((k*1.8+s.Time*s.branches[b].measuredFlow*.065)%14);arrows[b][k].localPosition=p;}
        }
    }
}
