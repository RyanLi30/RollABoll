using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Ryan.RollABall.Editor
{
    public static class PaceTuning
    {
        public static void Apply()
        {
            foreach(var path in PrototypeBuilder.LevelPaths)
            {
                var scene=EditorSceneManager.OpenScene(path);
                var player=Object.FindFirstObjectByType<BallController>();
                var settings=new SerializedObject(player);
                settings.FindProperty("autoRun").boolValue=true;
                settings.FindProperty("jumpSpeed").floatValue=9;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var slow=Object.FindObjectsByType<SpeedStrip>(FindObjectsSortMode.None).Where(s=>s.multiplier<1).OrderBy(s=>s.transform.position.z).ToArray();
                foreach(var strip in slow)
                {
                    var trigger=strip.GetComponent<BoxCollider>();
                    if(trigger.size.x<=4)continue;
                    var section=Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s=>Mathf.Abs(s.transform.position.z-strip.transform.position.z)).First();
                    var label=strip.transform.Find("Speed strip label");var labelPosition=label.position;
                    var p=strip.transform.position;p.x=(section.number/4)%2==0?-4:4;strip.transform.position=p;
                    trigger.size=new Vector3(3.7f,trigger.size.y,trigger.size.z);
                    var plate=strip.transform.Find("Speed strip surface");var scale=plate.localScale;scale.x=3.7f;plate.localScale=scale;
                    foreach(var arrow in strip.GetComponentsInChildren<Transform>().Where(t=>t.name=="Speed direction chevron" && Mathf.Abs(t.localPosition.x)>1).ToArray())Object.DestroyImmediate(arrow.gameObject);
                    label.position=labelPosition;
                }
                if(slow.Any(s=>s.GetComponent<BoxCollider>().bounds.size.x>4.01f))throw new Exception("Wide slow strip remains");
                EditorSceneManager.SaveScene(scene,path);
                Debug.Log("PACE_TUNED "+path+": "+slow.Length+" single-lane slow strips; immediate auto-run; 9m/s space jump at 2.2x gravity.");
            }
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
        }
    }
}
