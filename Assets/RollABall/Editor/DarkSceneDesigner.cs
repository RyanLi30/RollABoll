using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Ryan.RollABall.Editor
{
    public static class DarkSceneDesigner
    {
        const string Folder = "Assets/RollABall/Materials/Night";
        static Material metal, trim, cyan, amber, violet, glass;
        static readonly List<string> checks = new List<string>();
        static double captureAt;
        static int captureStage;
        static bool oldPlayEnabled;
        static EnterPlayModeOptions oldPlayOptions;
        [MenuItem("Roll-a-Ball/Apply dark environment")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            checks.Clear();
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach (var path in PrototypeBuilder.LevelPaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                var old = GameObject.Find("Night environment - decorative only");
                if (old != null) Object.DestroyImmediate(old);
                var originals = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                var transforms = originals.ToDictionary(t => t, t => (t.position, t.rotation, t.localScale));
                var mechanics = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(m => m != null).ToDictionary(m => m, m => EditorJsonUtility.ToJson(m));
                var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
                int count = colliders.Count;
                var root = new GameObject("Night environment - decorative only").transform;
                metal = Mat("Obsidian alloy", new Color(.07f,.09f,.14f), .65f,.42f);
                trim = Mat("Architectural edges", new Color(.10f,.13f,.19f),.7f,.55f);
                cyan = Mat("Glacial cyan",new Color(.06f,.5f,.65f),.35f,.5f,new Color(.05f,.65f,.9f)*2.0f);
                amber = Mat("Lantern gold",new Color(.8f,.35f,.09f),.15f,.4f,new Color(1,.4f,.09f)*2.2f);
                violet = Mat("Rift violet",new Color(.32f,.09f,.55f),.25f,.45f,new Color(.5f,.09f,1)*2.2f);
                glass = Mat("Distant windows",new Color(.09f,.15f,.25f),.2f,.3f,new Color(.16f,.32f,.60f)*.8f);
                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    var source = renderer.sharedMaterial;
                    if (source == null) continue;
                    if (renderer.name == "Low poly canopy" || renderer.name == "Distant scenery tower") { renderer.enabled = false; continue; }
                    string key=source.name.Replace('_',' ');
                    Material replacement=null;
                    if (key=="Track foundation" || key=="Supports" || key=="Barrier black") replacement=metal;
                    else if(key=="Lane slate") replacement=Mat("Graphite outer lane",new Color(.08f,.105f,.15f),.45f,.4f);
                    else if(key=="Lane center") replacement=Mat("Graphite center lane",new Color(.11f,.14f,.20f),.4f,.4f);
                    else if(key=="Track cyan") replacement=cyan;
                    else if(key=="Spring magenta") replacement=violet;
                    else if(key=="Barrier white") replacement=Mat("Ivory warning panel",new Color(.65f,.7f,.76f),.1f,.3f,new Color(.25f,.28f,.32f)*.35f);
                    else if(key=="Barrier red") replacement=Mat("Crimson warning",new Color(.65f,.025f,.045f),.15f,.3f,new Color(1,.025f,.045f)*.7f);
                    else if(key=="Collectible blue") replacement=Mat("Azure collectible",new Color(.02f,.35f,.9f),.6f,.7f,new Color(.02f,.3f,1)*1.4f);
                    else if(key=="Checkpoint green") replacement=Mat("Jade checkpoint",new Color(.03f,.5f,.25f),.3f,.5f,new Color(.03f,1,.4f)*1.4f);
                    else if(key=="Player yellow") replacement=renderer.name.Contains("lamp") ? amber : Mat("Golden player",new Color(1,.65f,.08f),.55f,.7f,new Color(1,.35f,.03f)*.5f);
                    if(replacement!=null) renderer.sharedMaterial=replacement;
                }
                var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Night sky.mat");
                if(sky==null){sky=new Material(Shader.Find("RollABall/Night Sky"));AssetDatabase.CreateAsset(sky,Folder+"/Night sky.mat");}
                RenderSettings.skybox=sky;
                RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.16f,.20f,.32f);
                RenderSettings.ambientEquatorColor=new Color(.075f,.1f,.17f);
                RenderSettings.ambientGroundColor=new Color(.025f,.035f,.06f);
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
                RenderSettings.fogColor=new Color(.10f,.14f,.23f);
                RenderSettings.fogStartDistance=70;RenderSettings.fogEndDistance=235;
                foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if(light.type==LightType.Directional) { light.color=new Color(.55f,.67f,1);light.intensity=1.15f;light.shadows=LightShadows.Soft; }
                }
                var cam=Camera.main;cam.clearFlags=CameraClearFlags.Skybox;cam.backgroundColor=RenderSettings.fogColor;
                cam.allowHDR=true;cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/Night atmosphere.asset");
                if(profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"/Night atmosphere.asset"); }
                foreach(var component in profile.components.Where(c=>c!=null).ToArray()) { profile.Remove(component.GetType());Object.DestroyImmediate(component,true); }
                profile.components.Clear();
                var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.45f);bloom.threshold.Override(1);bloom.scatter.Override(.65f);
                var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
                var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.23f);vignette.smoothness.Override(.65f);
                foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
                EditorUtility.SetDirty(profile);
                var volume=new GameObject("Night atmosphere").AddComponent<Volume>();volume.transform.SetParent(root);volume.isGlobal=true;volume.sharedProfile=profile;
                var sections=Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s=>s.number).ToArray();
                foreach(var section in sections)
                {
                    float z=section.transform.position.z,top=section.transform.position.y+.75f;
                    float sectionLength=section.GetComponent<BoxCollider>().size.z * section.transform.lossyScale.z;
                    for(int side=-1;side<=1;side+=2)
                    {
                        // All added geometry is decoration: no colliders or triggers.
                        Box("Armored edge beam",new Vector3(side*6.6f,top-.35f,z),new Vector3(.45f,.7f,sectionLength),metal,root);
                        for(int n=-1;n<=1;n++)
                        {
                            float zz=z+n*(sectionLength*.38f);
                            Box("Outboard lantern pedestal",new Vector3(side*7.5f,top+1.2f,zz),new Vector3(.5f,2.4f,.5f),metal,root);
                            Box("Amber lantern glass",new Vector3(side*7.5f,top+2.55f,zz),new Vector3(.34f,.55f,.34f),amber,root);
                            Box("Lantern cap",new Vector3(side*7.5f,top+2.91f,zz),new Vector3(.7f,.15f,.7f),trim,root);
                        }
                        Box("Tall gateway pier",new Vector3(side*9.5f,top+7,z+9),new Vector3(1.1f,16,1.4f),metal,root);
                        Box("Pier luminous inset",new Vector3(side*9.5f,top+7,z+8.27f),new Vector3(.15f,12,.05f),section.number%2==0?cyan:violet,root);
                        Box("Gateway crown",new Vector3(side*7.8f,top+15,z+9),new Vector3(4.5f,.65f,1.5f),trim,root);
                        for(int j=0;j<3;j++)
                        {
                            float towerZ=z-7+j*13;
                            float height=18+((section.number*7+j*11+(side+1)*3)%5)*6;
                            float towerX=side*(23+j*10);
                            Box("Distant monolith",new Vector3(towerX,top+height*.5f-9,towerZ),new Vector3(5+j, height,6),metal,root);
                            Box("Monolith crown",new Vector3(towerX,top+height-9,towerZ),new Vector3(5.5f+j,.5f,6.5f),trim,root);
                            for(int row=0;row<5;row++)
                                Box("Monolith window slit",new Vector3(towerX,top+height-11-row*2.6f,towerZ-3.05f),new Vector3(.15f,.85f,.06f),glass,root);
                        }
                    }
                    for(int k=-(int)(sectionLength/8);k<=(int)(sectionLength/8);k++)
                    {
                        Box("Deck panel joint left",new Vector3(-4,top+.031f,z+k*4),new Vector3(3.7f,.012f,.055f),metal,root);
                        Box("Deck panel joint right",new Vector3(4,top+.031f,z+k*4),new Vector3(3.7f,.012f,.055f),metal,root);
                    }
                    var pool=new GameObject("Soft overhead moon fill").AddComponent<Light>();pool.transform.SetParent(root);
                    pool.type=LightType.Point;pool.transform.position=new Vector3(0,top+7,z);pool.range=sectionLength*.65f;pool.intensity=5;pool.color=new Color(.24f,.42f,.8f);pool.shadows=LightShadows.None;
                }
                // A distant cold moon and sparse stars give the void a defined horizon.
                var moon=GameObject.CreatePrimitive(PrimitiveType.Sphere);moon.name="Distant moon";moon.transform.SetParent(root);
                moon.transform.position=new Vector3(-100,100,220);moon.transform.localScale=Vector3.one*35;
                Object.DestroyImmediate(moon.GetComponent<Collider>());
                moon.GetComponent<Renderer>().sharedMaterial=Mat("Moon stone",new Color(.24f,.31f,.47f),.05f,.15f,new Color(.12f,.17f,.3f)*.6f);
                var random=new System.Random(75);
                for(int n=0;n<95;n++)
                {
                    var star=GameObject.CreatePrimitive(PrimitiveType.Quad);star.name="Distant star";star.transform.SetParent(root);
                    star.transform.position=new Vector3((float)random.NextDouble()*500-250,45+(float)random.NextDouble()*110,280+(float)random.NextDouble()*180);
                    star.transform.localScale=Vector3.one*(.15f+(float)random.NextDouble()*.5f);Object.DestroyImmediate(star.GetComponent<Collider>());star.GetComponent<Renderer>().sharedMaterial=glass;
                }
                foreach(var pair in transforms) if(pair.Key.position!=pair.Value.Item1 || pair.Key.rotation!=pair.Value.Item2 || pair.Key.localScale!=pair.Value.Item3) throw new Exception("Original transform changed: "+pair.Key.name);
                foreach(var pair in mechanics) if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value) throw new Exception("Gameplay component changed: "+pair.Key.name);
                foreach(var pair in colliders) if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value) throw new Exception("Collider changed: "+pair.Key.name);
                if(Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length!=count) throw new Exception("Added collider");
                EditorSceneManager.SaveScene(scene,path);
                checks.Add("PASS "+path+": original transforms, gameplay components and "+count+" colliders unchanged; decoration only.");
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            Directory.CreateDirectory("output/rollaball");File.WriteAllLines("output/rollaball/dark-scene-validation.txt",checks);
            Debug.Log("DARK_SCENE_APPLIED\n"+string.Join("\n",checks));
        }
        public static void ApplyAndPreview()
        {
            Apply();
            Preview();
        }
        public static void Preview()
        {
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(0,0,1440,900);view.Show();view.Focus();
            oldPlayEnabled=EditorSettings.enterPlayModeOptionsEnabled;oldPlayOptions=EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.update+=Capture;EditorApplication.EnterPlaymode();
        }
        static void Capture()
        {
            if(!EditorApplication.isPlaying)return;
            if(captureStage==0) { captureAt=EditorApplication.timeSinceStartup+4;captureStage=1; }
            if(EditorApplication.timeSinceStartup<captureAt)return;
            if(captureStage==1) { ScreenCapture.CaptureScreenshot("output/rollaball/dark-scene-preview.png");captureAt=EditorApplication.timeSinceStartup+1;captureStage=2; }
            else { EditorApplication.update-=Capture;EditorSettings.enterPlayModeOptionsEnabled=oldPlayEnabled;EditorSettings.enterPlayModeOptions=oldPlayOptions;EditorApplication.Exit(0); }
        }
        static GameObject Box(string name,Vector3 pos,Vector3 scale,Material mat,Transform parent)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent);o.transform.position=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(o.GetComponent<Collider>());return o;
        }
        static Material Mat(string name,Color color,float metallic,float smooth,Color emission=default)
        {
            string path=Folder+"/"+name.Replace(' ','_')+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
            if(emission.maxColorComponent>0){m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.SetColor("_EmissionColor",emission);MaterialEditor.FixupEmissiveFlag(m);m.EnableKeyword("_EMISSION");}
            EditorUtility.SetDirty(m);return m;
        }
    }
}
