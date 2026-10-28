using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ryan.RollABall.Editor
{
    public static class BackgroundDetailDesigner
    {
        const string Folder="Assets/RollABall/Materials/Night/Background";
        static Material facade,steel,windows,farFacade,beacon;
        [MenuItem("Roll-a-Ball/Refine background materials")]
        public static void Apply()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(Folder);
            CreateTextures();
            facade=Material("Weathered titanium",new Color(.27f,.34f,.42f),.5f,.48f);
            facade.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Metal panels.png"));
            facade.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Brushed normal.png"));
            facade.SetFloat("_BumpScale",.38f);facade.EnableKeyword("_NORMALMAP");facade.SetTextureScale("_BaseMap",new Vector2(2,6));facade.SetTextureScale("_BumpMap",new Vector2(2,6));
            steel=Material("Machined architectural ribs",new Color(.19f,.24f,.31f),.65f,.58f);
            windows=Material("Occupied city windows",new Color(.16f,.21f,.28f),.35f,.65f);
            windows.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Window grid.png"));
            windows.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Window grid.png"));
            windows.SetColor("_EmissionColor",new Color(.44f,.60f,.83f)*1.2f);windows.EnableKeyword("_EMISSION");
            farFacade=Material("Hazy distant skyline",new Color(.09f,.14f,.20f),.25f,.35f);
            beacon=Material("Distant amber signals",new Color(.6f,.22f,.06f),.2f,.4f);
            beacon.SetColor("_EmissionColor",new Color(1,.27f,.05f)*1.8f);beacon.EnableKeyword("_EMISSION");
            foreach(var m in new[]{facade,windows,beacon}){m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(m);}
            var report=new List<string>();
            foreach(string path in PrototypeBuilder.LevelPaths)
            {
                var scene=EditorSceneManager.OpenScene(path);
                var previous=GameObject.Find("Layered city background - decoration only");if(previous!=null)Object.DestroyImmediate(previous);
                var physics=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                var mechanics=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c=>c!=null).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                var transforms=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).ToDictionary(t=>t,t=>(t.position,t.rotation,t.localScale));
                var root=new GameObject("Layered city background - decoration only").transform;
                var sections=Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s=>s.number).ToArray();
                var towers=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Distant monolith").ToArray();
                int index=0;
                foreach(var tower in towers)
                {
                    tower.sharedMaterial=facade;
                    var p=tower.transform.position;var size=tower.transform.lossyScale;
                    // Detailed surfaces are outside the playable corridor and have no physics.
                    Box("Recessed illuminated facade",p+new Vector3(0,0,-size.z*.5f-.04f),new Vector3(size.x*.78f,size.y*.80f,.045f),windows,root);
                    for(int side=-1;side<=1;side+=2)
                        Box("Vertical machined rib",p+new Vector3(side*size.x*.47f,0,-size.z*.5f-.12f),new Vector3(.18f,size.y,.22f),steel,root);
                    Box("Tower service plinth",p+new Vector3(0,-size.y*.38f,-.16f),new Vector3(size.x*1.13f,1.1f,size.z*1.1f),steel,root);
                    if(index++%3==0)
                    {
                        Box("Rooftop antenna",p+new Vector3(0,size.y*.5f+2,0),new Vector3(.12f,4,.12f),steel,root);
                        Box("Rooftop navigation light",p+new Vector3(0,size.y*.5f+4,0),Vector3.one*.25f,beacon,root);
                    }
                }
                var random=new System.Random(241);
                foreach(var section in sections)
                {
                    float z=section.transform.position.z,top=section.transform.position.y+.75f;
                    for(int side=-1;side<=1;side+=2)
                    {
                        for(int row=0;row<2;row++)
                        {
                            float x=side*(68+row*38+(float)random.NextDouble()*14);
                            float height=28+(float)random.NextDouble()*42;
                            float width=9+(float)random.NextDouble()*9;
                            var p=new Vector3(x,top+height*.5f-18,z+row*26);
                            Box("Layered far skyline",p,new Vector3(width,height,12),farFacade,root);
                            Box("Far city window field",p+new Vector3(0,1,-6.05f),new Vector3(width*.68f,height*.7f,.05f),windows,root);
                            Box("Stepped city roof",p+new Vector3(0,height*.5f+1,0),new Vector3(width*.72f,2,8),steel,root);
                        }
                        // Short service bridges add depth behind the nearest towers.
                        if(section.number%2==0)
                        {
                            Box("Outlying service bridge",new Vector3(side*39,top+4,z+12),new Vector3(23,1,3),steel,root);
                            Box("Bridge illuminated underside",new Vector3(side*39,top+3.45f,z+10.5f),new Vector3(18,.08f,.1f),windows,root);
                        }
                    }
                }
                // The sky shader provides the textured moon; hide the old flat sphere.
                foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if(r.name=="Distant moon" || r.name=="Distant star" || r.name=="Monolith window slit")r.enabled=false;
                    if(r.name=="Tall gateway pier" || r.name=="Monolith crown" || r.name=="Gateway crown")r.sharedMaterial=steel;
                }
                RenderSettings.fogStartDistance=85;RenderSettings.fogEndDistance=325;
                RenderSettings.fogColor=new Color(.075f,.115f,.18f);
                foreach(var pair in transforms)
                    if(pair.Key.position!=pair.Value.Item1 || pair.Key.rotation!=pair.Value.Item2 || pair.Key.localScale!=pair.Value.Item3)throw new Exception("Transform changed: "+pair.Key.name);
                foreach(var pair in mechanics)if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new Exception("Gameplay changed: "+pair.Key.name);
                foreach(var pair in physics)if(EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new Exception("Collider changed: "+pair.Key.name);
                if(Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length!=physics.Count)throw new Exception("Added collider");
                EditorSceneManager.SaveScene(scene,path);
                report.Add("PASS "+path+": all original transforms, gameplay settings and "+physics.Count+" colliders unchanged; decorative city and material details only.");
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            Directory.CreateDirectory("output/rollaball");File.WriteAllLines("output/rollaball/background-detail-validation.txt",report);
            Debug.Log(string.Join("\n",report));
        }
        static void CreateTextures()
        {
            const int n=256;
            var metal=new Texture2D(n,n,TextureFormat.RGBA32,false);
            var normal=new Texture2D(n,n,TextureFormat.RGBA32,false,true);
            var window=new Texture2D(n,n,TextureFormat.RGBA32,false);
            var rng=new System.Random(58);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float scratch=(float)rng.NextDouble();float line=x%64<2 || y%64<2?.32f:1;
                float shade=(.61f+.12f*Mathf.PerlinNoise(x*.09f,y*.09f)+scratch*.09f)*line;
                if(y%32==0)shade*=.83f;
                metal.SetPixel(x,y,new Color(shade*.85f,shade*.93f,shade));
                normal.SetPixel(x,y,new Color(.5f+(scratch-.5f)*.10f,.5f+(y%64<2?-.15f:0),1));
                int cx=x/16,cy=y/16;
                bool frame=x%16<4 || x%16>12 || y%16<4 || y%16>11;
                float seed=Mathf.PerlinNoise(cx*4.38f+10,cy*6.93f+3);
                Color c=frame?new Color(.009f,.014f,.02f):seed>.46f?(seed>.67f?new Color(.70f,.44f,.22f):new Color(.28f,.46f,.65f)):new Color(.016f,.027f,.042f);
                window.SetPixel(x,y,c);
            }
            Write(metal,"Metal panels",false);Write(normal,"Brushed normal",true);Write(window,"Window grid",false);
        }
        static void Write(Texture2D t,string name,bool normal)
        {
            string path=Folder+"/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.maxTextureSize=256;importer.SaveAndReimport();
        }
        static Material Material(string name,Color color,float metal,float gloss)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",gloss);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static void Box(string name,Vector3 point,Vector3 scale,Material material,Transform parent)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent);o.transform.position=point;o.transform.localScale=scale;
            Object.DestroyImmediate(o.GetComponent<Collider>());var renderer=o.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
    }
}
