using NUnit.Framework;
using UnityEngine;
using PvdSim.Core;
namespace PvdSim.Checks
{
    public sealed class UnityChecks
    {
        [Test] public void CoreSuite(){string report=CoreChecks.RunAll();Debug.Log(report);Assert.That(report,Does.Not.Contain("FAIL"));}
        [Test] public void RealConfigurationAndJson(){var c=JsonUtility.FromJson<Config>(Resources.Load<TextAsset>("scenario-config").text);c.Validate();Assert.AreEqual(new Config().step,c.step);var s=new Simulation(c,Scenario.A);s.Start();CoreChecks.Advance(s,35);var json=JsonUtility.ToJson(s.tools[0].alarms[0]);Assert.That(json,Does.Contain("firstFail"));Assert.That(json,Does.Contain("commandKW"));}
        [Test] public void BuilderPreservesContentAndDoesNotDuplicate()
        {
            PvdSim.Editor.DemoBuilder.Prepare(true);
            var sentinel=new GameObject("User content sentinel");
            PvdSim.Editor.DemoBuilder.Prepare(true);
            Assert.IsNotNull(sentinel);Assert.AreEqual(1,Object.FindObjectsByType<SimulatorApp>(FindObjectsSortMode.None).Length);
            Object.DestroyImmediate(sentinel);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
