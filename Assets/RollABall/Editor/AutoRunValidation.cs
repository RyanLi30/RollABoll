using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Ryan.RollABall.Editor
{
    public static class AutoRunValidation
    {
        static int stage,level=1,readyFrames;
        static float deadline,keyUntil,releaseUntil,levelStarted;
        static double timeout;
        static bool oldEnabled,sawBoost,sawSlow,sawCruise,sawAirSteer;
        static float airStartX;static bool wasFlying;
        static EnterPlayModeOptions oldOptions;
        static InputSettings.BackgroundBehavior oldBackground;
        static Keyboard keyboard;
        static Key[] held=Array.Empty<Key>();
        static Key steering;
        static RollABallGame game,previous;
        static CourseSection[] sections;
        static readonly System.Text.StringBuilder report=new System.Text.StringBuilder();
        public static void Run()
        {
            if(!Application.isBatchMode)throw new Exception("Batch only");
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            oldEnabled=EditorSettings.enterPlayModeOptionsEnabled;oldOptions=EditorSettings.enterPlayModeOptions;oldBackground=InputSystem.settings.backgroundBehavior;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(0,0,1440,900);view.Show();view.Focus();
            EditorApplication.update+=Tick;InputSystem.onBeforeUpdate+=Queue;timeout=EditorApplication.timeSinceStartup+360;
            EditorApplication.EnterPlaymode();
        }
        static Rigidbody Body=>game.player.GetComponent<Rigidbody>();
        static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>timeout)throw new Exception("Timeout stage="+stage+" level="+level);
                if(!EditorApplication.isPlaying || Time.time<deadline)return;
                var found=UnityEngine.Object.FindFirstObjectByType<RollABallGame>();if(found==null)return;game=found;
                if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
                if(stage==0)
                {
                    Time.timeScale=2;
                    Check(game.player.AutoRun,"Auto run enabled");
                    sections=UnityEngine.Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s=>s.number).ToArray();
                    Check(sections.Length==6+level*2 && sections.All(s=>s.GetComponent<BoxCollider>().bounds.size.z>63.9f),"Extended 64m sections and progressive section count");
                    Check(game.cubes.Length==sections.Length*2,"Two route collectibles per section");
                    Check(UnityEngine.Object.FindObjectsByType<SpeedStrip>(FindObjectsSortMode.None).Any(s=>s.multiplier>1) && UnityEngine.Object.FindObjectsByType<SpeedStrip>(FindObjectsSortMode.None).Any(s=>s.multiplier<1),"Both real boost and slow triggers exist");
                    foreach(var s in sections.Where(s=>s.requiresSpring))
                        Check(s.gapAfter>game.player.MaximumSpeed*1.4f*(2*game.player.JumpSpeed/-Physics.gravity.y+.1f)+2,"Large gap remains spring-dependent at maximum boost");
                    for(int i=0;i<10;i++)game.player.ApplySpeedStrip(1.35f,4);
                    Check(Mathf.Approximately(game.player.SpeedMultiplier,1.35f),"Boost does not stack");
                    game.player.ApplySpeedStrip(.72f,4);
                    Check(Mathf.Approximately(game.player.SpeedMultiplier,.72f),"Slow replaces boost instead of multiplying it");
                    game.player.ReturnToCourse(new Vector3(0,.7f,-27),0);
                    Check(game.player.SpeedMultiplier==1 && game.player.RunStartsIn>0,"Recovery clears speed effects and provides ready time");
                    previous=game;held=new[]{Key.R};Next(1,.25f);
                }
                else if(stage==1)
                {
                    Check(!ReferenceEquals(game,previous) && game.levelNumber==level && game.Remaining==game.cubes.Length,"R restarts the current level with fresh progress");
                    sections=UnityEngine.Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s=>s.number).ToArray();
                    previous=game;sawBoost=sawSlow=sawCruise=sawAirSteer=wasFlying=false;steering=Key.None;keyUntil=releaseUntil=0;levelStarted=Time.time;
                    held=new[]{Key.S};Next(2,.01f);
                }
                else if(stage==2)
                {
                    if(!ReferenceEquals(previous,game))throw new Exception("Unexpected hazard restart during full route");
                    CheckSilent(game.player.Falls==0,"No falls on designed route");
                    if(Time.time-levelStarted>145)throw new Exception("Full route exceeded time budget, remaining="+game.Remaining);
                    var player=game.player;float z=Body.position.z;
                    if(player.SpeedMultiplier>1.1f && player.CurrentForwardSpeed>player.MaximumSpeed*1.2f)sawBoost=true;
                    if(player.SpeedMultiplier<.9f && player.CurrentForwardSpeed<player.MaximumSpeed*.85f && player.CurrentForwardSpeed>5)sawSlow=true;
                    if(player.SpeedEffectRemaining<=0 && player.CurrentForwardSpeed>=player.MaximumSpeed-.2f)sawCruise=true;
                    if(player.SpringInFlight && !wasFlying)airStartX=Body.position.x;
                    if(player.SpringInFlight && Mathf.Abs(Body.position.x-airStartX)>3)sawAirSteer=true;
                    wasFlying=player.SpringInFlight;
                    if(game.HasWon)
                    {
                        Check(sawBoost,"Real boost strip raises actual forward speed");Check(sawSlow,"Real slow strip reduces actual forward speed");Check(sawCruise,"Cruise speed available between strips");Check(sawAirSteer,"A/D steers across lanes during actual spring flights");
                        Check(player.Falls==0 && game.Remaining==0,"Full level completed with real controls, all cubes, no falls or hazard restarts while S held");
                        held=Array.Empty<Key>();ScreenCapture.CaptureScreenshot("output/rollaball/auto-run-level-"+level+"-complete.png");Next(3,.2f);return;
                    }
                    var section=sections.LastOrDefault(s=>z>=s.transform.position.z-32.4f)??sections[0];
                    int safe=(section.number+level)%3-1;
                    int phase=section.number%4;
                    float local=z-section.transform.position.z;
                    int desired=safe;
                    if(phase==3 && local>0)desired=safe==1?0:safe+1;
                    if(phase==1 && local>-1 && local<11)
                    {
                        var moving=UnityEngine.Object.FindObjectsByType<MovingObstacle>(FindObjectsSortMode.None).OrderBy(m=>Mathf.Abs(m.transform.position.z-z)).First();
                        float arrival=Mathf.Max(0,(moving.transform.position.z-z)/Mathf.Max(5,player.CurrentForwardSpeed));
                        float wave=(Mathf.Sin((Time.timeSinceLevelLoad+arrival)*2*Mathf.PI/moving.period+moving.phase)-Mathf.Sin(moving.phase))*.5f;
                        desired=wave*moving.travel.x<=0?1:-1;
                    }
                    bool jump=false;
                    if(player.SpringInFlight)
                    {
                        int next=Mathf.Min(sections.Length-1,local>0?section.number+1:section.number);
                        desired=(next+level)%3-1;
                        steering=Body.position.x<desired*4-.18f?Key.D:Body.position.x>desired*4+.18f?Key.A:Key.None;
                    }
                    else
                    {
                        if(Time.time>=keyUntil && steering!=Key.None){steering=Key.None;releaseUntil=Time.time+.09f;}
                        if(steering==Key.None && Time.time>=releaseUntil && desired!=player.CurrentLane)
                        {steering=desired>player.CurrentLane?Key.D:Key.A;keyUntil=Time.time+.085f;}
                        float edge=section.transform.position.z+32;
                        jump=!section.requiresSpring && section.gapAfter>0 && edge-z<3.1f && edge-z>-.4f && player.IsGrounded;
                    }
                    held=steering==Key.None?(jump?new[]{Key.S,Key.Space}:new[]{Key.S}):(jump?new[]{Key.S,steering,Key.Space}:new[]{Key.S,steering});
                }
                else if(stage==3){held=new[]{Key.Enter};Next(4,.35f);}
                else if(stage==4)
                {
                    held=Array.Empty<Key>();level++;
                    Check(game.levelNumber==(level<=3?level:1),"Enter advances correctly");
                    if(level<=3)Next(0,.1f);
                    else {Next(5,.5f);}
                }
                else if(stage==5){ScreenCapture.CaptureScreenshot("output/rollaball/auto-run-preview.png");Next(6,.4f);}
                else if(stage==6)Finish(0);
            }
            catch(Exception e)
            {
                report.AppendLine("FAIL: "+e+" position="+(game==null?"none":Body.position.ToString())+" lane="+(game==null?0:game.player.CurrentLane));
                if(game!=null)ScreenCapture.CaptureScreenshot("output/rollaball/auto-run-failure.png");Debug.LogException(e);Finish(1);
            }
        }
        static void Queue(){if(keyboard!=null && InputState.currentUpdateType==InputUpdateType.Dynamic)InputSystem.QueueStateEvent(keyboard,new KeyboardState(held));}
        static void Next(int next,float delay){stage=next;deadline=Time.time+delay;}
        static void CheckSilent(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Check(bool condition,string message){CheckSilent(condition,message);report.AppendLine("PASS level "+level+": "+message);}
        static void Finish(int code)
        {
            Time.timeScale=1;EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=Queue;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=oldBackground;EditorSettings.enterPlayModeOptionsEnabled=oldEnabled;EditorSettings.enterPlayModeOptions=oldOptions;
            Directory.CreateDirectory("output/rollaball");File.WriteAllText("output/rollaball/auto-run-validation.txt",report.ToString());Debug.Log("AUTO_RUN_VALIDATION_EXIT: "+code+"\n"+report);EditorApplication.Exit(code);
        }
    }
}
