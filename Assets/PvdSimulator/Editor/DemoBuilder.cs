using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PvdSim.Editor
{
    public static class DemoBuilder
    {
        public const string ScenePath="Assets/PvdSimulator/Scenes/PvdDemo.unity";
        [MenuItem("Tools/PVD Simulator/Create or Open Demo Scene")]
        public static void OpenDemo(){Prepare(true);}
        public static void PrepareBatch(){try{Prepare(false);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Prepare(bool show)
        {
            var previous=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.isLoaded;
            if(opened)
            {
                if(File.Exists(ScenePath))scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
                else if(Application.isBatchMode && string.IsNullOrEmpty(previous.path))
                    scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else
                {
                    if(string.IsNullOrEmpty(previous.path) && !EditorSceneManager.SaveScene(previous))
                        throw new InvalidOperationException("Please save the current untitled scene before first creation; existing content is preserved.");
                    scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                }
            }
            SceneManager.SetActiveScene(scene);
            var apps=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SimulatorApp>(true)).ToArray();
            if(apps.Length>1)throw new InvalidOperationException("Multiple simulator roots; preserving user objects.");
            var app=apps.Length==1?apps[0]:new GameObject("PVD Multi-Tool Simulator").AddComponent<SimulatorApp>();
            app.cjkFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/PvdSimulator/Resources/Fonts/NotoSansCJKtc-Regular.otf");
            const string materialPath="Assets/PvdSimulator/Resources/IndustrialSurface.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,materialPath);}
            app.surfaceMaterial=mat;EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="FieldLabEducation";PlayerSettings.productName="PVD Multi-Tool Alarm Response Simulator";
            PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.allowUnsafeCode=false;AssetDatabase.SaveAssets();
            if(!show && previous.IsValid()&&previous.isLoaded){if(opened)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
            Debug.Log("PVD demo prepared; repeat calls preserve existing scene content.");
        }
        [MenuItem("Tools/PVD Simulator/Build Windows x64")]
        public static void BuildWindows()
        {
            try
            {
                Prepare(false);Directory.CreateDirectory("Builds/Windows");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName="Builds/Windows/PvdMultiToolSimulator.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
                Directory.CreateDirectory("Builds/Windows/ThirdPartyNotices");File.Copy("Assets/PvdSimulator/Resources/Fonts/OFL.txt","Builds/Windows/ThirdPartyNotices/Noto-OFL.txt",true);
                Debug.Log("Windows build completed: "+report.summary.totalSize+" bytes");if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
    }
}
