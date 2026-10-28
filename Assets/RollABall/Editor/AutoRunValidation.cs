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
        static int stage,level=1,jumpCase,gapCase,fallsBefore;
        static float jumpStarted;
        static int springCase,bouncesBefore;
        static float springFlightStarted,baseSpringFlight,springForwardSpeed;
        static CourseSection jumpFrom,jumpTo;
        static float oldMaxDelta;
        static float deadline,keyUntil,releaseUntil,levelStarted;
        static double timeout;
        static bool oldEnabled,sawBoost,sawSlow,sawCruise,sawAirSteer;
        static int avoidedSection=-1,avoidLane;
        static string lastRoutePosition="";
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
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(0,0,960,600);view.Show();view.Focus();
            oldMaxDelta=Time.maximumDeltaTime;Time.maximumDeltaTime=.05f;
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
                    Check(game.player.AutoRun && game.player.RunStartsIn==0 && game.player.CurrentForwardSpeed>game.player.MaximumSpeed-.2f,"Immediate auto run without any key or countdown");
                    Check(UnityEngine.Object.FindObjectsByType<SpeedStrip>(FindObjectsSortMode.None).Where(s=>s.multiplier<1).All(s=>s.GetComponent<BoxCollider>().bounds.size.x<4),"Every slow strip occupies only one lane and leaves two bypass lanes");
                    sections=UnityEngine.Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s=>s.number).ToArray();
                    Check(sections.Length==6+level*2 && sections.All(s=>s.GetComponent<BoxCollider>().bounds.size.z>63.9f),"Extended 64m sections and progressive section count");
                    Check(game.cubes.Length==sections.Length*2,"Two route collectibles per section");
                    Check(UnityEngine.Object.FindObjectsByType<SpeedStrip>(FindObjectsSortMode.None).Any(s=>s.multiplier>1) && UnityEngine.Object.FindObjectsByType<SpeedStrip>(FindObjectsSortMode.None).Any(s=>s.multiplier<1),"Both real boost and slow triggers exist");
                    foreach(var s in sections.Where(s=>s.requiresSpring))
                        Check(s.gapAfter>game.player.MaximumSpeed*2.5f*(2*game.player.JumpSpeed/(-Physics.gravity.y*BallController.NormalJumpGravityScale)+.1f)+2,"Large gap remains spring-dependent at maximum boost");
                    for(int i=0;i<10;i++)game.player.ApplySpeedStrip(2.5f,4);
                    Check(Mathf.Approximately(game.player.SpeedMultiplier,2.5f),"Boost does not stack");
                    game.player.ApplySpeedStrip(.72f,4);
                    Check(Mathf.Approximately(game.player.SpeedMultiplier,.72f),"Slow replaces boost instead of multiplying it");
                    game.player.ReturnToCourse(new Vector3(0,.7f,-27),0);
                    Check(game.player.SpeedMultiplier==1 && game.player.RunStartsIn==0,"Recovery clears effects and resumes forward motion immediately");
                    game.player.ApplySpeedStrip(2.5f,4);
                    game.player.CanMove=false;Body.isKinematic=true;Next(7,5.2f);
                }
                else if(stage==7)
                {
                    Check(Mathf.Approximately(game.player.SpeedMultiplier,2.5f) && float.IsPositiveInfinity(game.player.SpeedEffectRemaining),"Boost persists past original duration without a slow strip");
                    game.player.CanMove=true;
                    for(int i=0;i<10;i++)game.player.ApplySpeedStrip(2.5f,4);
                    Check(Mathf.Approximately(game.player.SpeedMultiplier,2.5f),"Repeated boost contacts remain at 2.50x without stacking");
                    game.player.ApplySpeedStrip(.72f,.2f);
                    Check(Mathf.Approximately(game.player.SpeedMultiplier,.72f),"Slow immediately replaces persistent boost");
                    game.player.CanMove=false;Next(8,.3f);
                }
                else if(stage==8)
                {
                    Check(game.player.SpeedMultiplier==1,"Slow expiry returns to base speed without restoring old boost");
                    Body.isKinematic=false;game.player.CanMove=true;game.player.ReturnToCourse(new Vector3(0,.7f,-27),0);
                    springCase=0;Next(30,.01f);
                }
                else if(stage==30)
                {
                    Time.timeScale=1;
                    jumpFrom=sections.First(s=>s.requiresSpring);jumpTo=sections[jumpFrom.number+1];
                    game.player.ReturnToCourse(new Vector3(0,jumpFrom.entry.y,jumpFrom.transform.position.z+25),0);
                    float factor=springCase==0?1:2.5f;game.player.ApplySpeedStrip(factor,4);
                    Body.linearVelocity=Vector3.forward*game.player.MaximumSpeed*factor;
                    bouncesBefore=game.player.Bounces;fallsBefore=game.player.Falls;
                    Physics.SyncTransforms();held=Array.Empty<Key>();jumpStarted=Time.time;Next(31,.01f);
                }
                else if(stage==31)
                {
                    if(game.player.Bounces==bouncesBefore){if(Time.time-jumpStarted>1)throw new Exception("Spring failed to launch");return;}
                    springFlightStarted=Time.time;springForwardSpeed=Body.linearVelocity.z;
                    Check(game.player.SpringInFlight && springForwardSpeed>game.player.MaximumSpeed*(springCase==0?.8f:2.3f),"Actual spring launch reflects "+(springCase==0?"normal":"2.5x boost")+" speed: "+springForwardSpeed.ToString("0.0"));
                    held=new[]{Key.D};Next(32,.01f);
                }
                else if(stage==32)
                {
                    CheckSilent(game.player.Falls==fallsBefore,"Spring lands safely");
                    if(game.player.SpringInFlight)
                    {
                        CheckSilent(Mathf.Abs(Body.linearVelocity.z-springForwardSpeed)<.2f,"Spring flight does not brake in midair");
                        held=Body.position.x<3.9f?new[]{Key.D}:Array.Empty<Key>();
                        if(Time.time-springFlightStarted>4)throw new Exception("Spring flight timeout");return;
                    }
                    float flight=Time.time-springFlightStarted;
                    Check(game.player.IsGrounded && Body.position.z>jumpTo.transform.position.z-32 && Body.position.z<jumpTo.transform.position.z-20 && Body.position.x>3.5f,"Spring lands on intended next platform and retains airborne A/D control; flight="+flight.ToString("0.00")+"s");
                    if(springCase==0){baseSpringFlight=flight;springCase++;Next(30,.01f);}
                    else
                    {
                        Check(flight<baseSpringFlight*.46f,"Boosted spring flight takes about 40% of normal time");
                        held=Array.Empty<Key>();gapCase=0;Next(20,.01f);
                    }
                }
                else if(stage==20)
                {
                    Time.timeScale=1;
                    var gaps=sections.Where(s=>!s.requiresSpring && s.gapAfter>0).ToArray();
                    jumpFrom=gaps[gapCase];jumpTo=sections[jumpFrom.number+1];
                    game.player.ReturnToCourse(new Vector3(0,jumpFrom.entry.y,jumpFrom.transform.position.z+26),0);
                    game.player.ApplySpeedStrip(2.5f,4);Body.linearVelocity=Vector3.forward*game.player.MaximumSpeed*2.5f;
                    Physics.SyncTransforms();fallsBefore=game.player.Falls;held=Array.Empty<Key>();jumpStarted=Time.time;Next(21,.02f);
                }
                else if(stage==21)
                {
                    CheckSilent(Body.linearVelocity.y<.6f,"No automatic upward kick at gap edge");
                    CheckSilent(!(game.player.IsGrounded && Body.position.z>jumpTo.transform.position.z-32),"Cannot cross an ordinary gap without jumping at 2.5x speed");
                    if(game.player.Falls==fallsBefore){if(Time.time-jumpStarted>2)throw new Exception("Did not fall at gap");return;}
                    Check(true,"Ordinary gap "+jumpFrom.number+" requires Space at 2.5x; no edge launch");
                    gapCase++;
                    if(gapCase<sections.Count(s=>!s.requiresSpring && s.gapAfter>0))Next(20,.01f);
                    else{jumpCase=0;Next(10,.01f);}
                }
                else if(stage==10)
                {
                    Time.timeScale=1;
                    jumpFrom=sections.First(s=>!s.requiresSpring && s.gapAfter>0);jumpTo=sections[jumpFrom.number+1];
                    game.player.ReturnToCourse(new Vector3(0,jumpFrom.entry.y,jumpFrom.transform.position.z+23.7f),0);
                    float factor=jumpCase==0?.72f:jumpCase==1?1:2.5f;
                    game.player.ApplySpeedStrip(factor,4);
                    Body.linearVelocity=new Vector3(0,0,game.player.MaximumSpeed*factor);
                    Physics.SyncTransforms();held=Array.Empty<Key>();Next(11,.02f);
                }
                else if(stage==11)
                {
                    if(jumpFrom.transform.position.z+32-Body.position.z>Mathf.Clamp(game.player.CurrentForwardSpeed*.055f,.7f,2.5f) || !game.player.IsGrounded)return;
                    held=new[]{Key.Space};jumpStarted=Time.time;Next(13,.12f);
                }
                else if(stage==13)
                {
                    Check(Body.linearVelocity.y>1 && !game.player.SpringInFlight,"Space jump responds at speed case "+jumpCase);
                    held=Array.Empty<Key>();Next(12,.02f);
                }
                else if(stage==12)
                {
                    if(!game.player.IsGrounded){if(Time.time-jumpStarted>1.2f)throw new Exception("Jump missed landing");return;}
                    float elapsed=Time.time-jumpStarted;
                    Check(elapsed<.68f && elapsed>.35f && Body.position.z>jumpTo.transform.position.z-31.7f && Mathf.Abs(Body.position.y-jumpTo.entry.y)<.3f,
                        "Fast space jump safely crosses actual small gap at "+(jumpCase==0?"slow":jumpCase==1?"normal":"boost")+" speed; air time="+elapsed.ToString("0.00")+"s");
                    jumpCase++;
                    if(jumpCase<3)Next(10,.03f);
                    else{previous=game;held=new[]{Key.R};Next(1,.25f);}
                }
                else if(stage==1)
                {
                    Check(!ReferenceEquals(game,previous) && game.levelNumber==level && game.Remaining==game.cubes.Length,"R restarts the current level with fresh progress");
                    Time.timeScale=2;
                    sections=UnityEngine.Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s=>s.number).ToArray();
                    avoidedSection=-1;previous=game;sawBoost=sawSlow=sawCruise=sawAirSteer=wasFlying=false;steering=Key.None;keyUntil=releaseUntil=0;levelStarted=Time.time;
                    held=new[]{Key.S};Next(2,.01f);
                }
                else if(stage==2)
                {
                    if(!ReferenceEquals(previous,game))throw new Exception("Unexpected hazard restart during full route; previous="+lastRoutePosition);
                    CheckSilent(game.player.Falls==0,"No falls on designed route");
                    if(Time.time-levelStarted>145)throw new Exception("Full route exceeded time budget, remaining="+game.Remaining);
                    var player=game.player;float z=Body.position.z;lastRoutePosition=Body.position+" lane="+player.CurrentLane+" speed="+player.CurrentForwardSpeed;
                    if(player.SpeedMultiplier>1.1f && player.CurrentForwardSpeed>player.MaximumSpeed*1.2f)sawBoost=true;
                    if(player.SpeedMultiplier<.9f && player.CurrentForwardSpeed<player.MaximumSpeed*.85f && player.CurrentForwardSpeed>5)sawSlow=true;
                    if(player.SpeedEffectRemaining<=0 && player.CurrentForwardSpeed>=player.MaximumSpeed-.2f)sawCruise=true;
                    if(player.SpringInFlight && !wasFlying)airStartX=Body.position.x;
                    if(player.SpringInFlight && Mathf.Abs(Body.position.x-airStartX)>3)sawAirSteer=true;
                    wasFlying=player.SpringInFlight;
                    if(game.HasWon)
                    {
                        Check(sawBoost,"Real boost strip raises actual forward speed");Check(sawSlow,"Real slow strip reduces actual forward speed");Check(sawAirSteer,"A/D steers across lanes during actual spring flights");
                        Check(player.Falls==0 && game.Remaining==0,"Full level completed with real controls, all cubes, no falls or hazard restarts while S held");
                        held=Array.Empty<Key>();ScreenCapture.CaptureScreenshot("output/rollaball/auto-run-level-"+level+"-complete.png");Next(3,.2f);return;
                    }
                    var section=sections.LastOrDefault(s=>z>=s.transform.position.z-32.4f)??sections[0];
                    int safe=(section.number+level)%3-1;
                    int phase=section.number%4;
                    float local=z-section.transform.position.z;
                    int desired=safe;
                    if(phase==3 && local>0)desired=safe==1?0:safe+1;
                    if(phase==1 && local<-13)
                    {
                        var strip=UnityEngine.Object.FindObjectsByType<SpeedStrip>(FindObjectsSortMode.None).Where(s=>s.multiplier<1).OrderBy(s=>Mathf.Abs(s.transform.position.z-(section.transform.position.z-20))).First();
                        desired=Mathf.RoundToInt(strip.transform.position.x/4);
                    }
                    if(phase==1 && local>-3.7f && local<11)
                    {
                        var moving=UnityEngine.Object.FindObjectsByType<MovingObstacle>(FindObjectsSortMode.None).OrderBy(m=>Mathf.Abs(m.transform.position.z-z)).First();
                        float arrival=Mathf.Max(0,(moving.transform.position.z-z)/Mathf.Max(5,player.CurrentForwardSpeed));
                        float wave=(Mathf.Sin((Time.timeSinceLevelLoad+arrival)*2*Mathf.PI/moving.period+moving.phase)-Mathf.Sin(moving.phase))*.5f;
                        if(avoidedSection!=section.number){avoidedSection=section.number;avoidLane=wave*moving.travel.x<=0?1:-1;}
                        desired=avoidLane;
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
                        jump=!section.requiresSpring && section.gapAfter>0 && edge-z<Mathf.Clamp(player.CurrentForwardSpeed*.055f,.7f,2.5f) && edge-z>-.4f && player.IsGrounded;
                    }
                    held=steering==Key.None?(jump?new[]{Key.S,Key.Space}:new[]{Key.S}):(jump?new[]{Key.S,steering,Key.Space}:new[]{Key.S,steering});
                }
                else if(stage==3){held=new[]{Key.Enter};Next(4,.35f);}
                else if(stage==4)
                {
                    held=Array.Empty<Key>();level++;
                    Check(game.levelNumber==(level<=3?level:1),"Enter advances correctly");
                    if(level<=3)Next(0,.005f);
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
        static void Check(bool condition,string message){CheckSilent(condition,message);report.AppendLine("PASS level "+level+": "+message);Debug.Log("PASS level "+level+": "+message);}
        static void Finish(int code)
        {
            Time.timeScale=1;Time.maximumDeltaTime=oldMaxDelta;EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=Queue;if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=oldBackground;EditorSettings.enterPlayModeOptionsEnabled=oldEnabled;EditorSettings.enterPlayModeOptions=oldOptions;
            Directory.CreateDirectory("output/rollaball");File.WriteAllText("output/rollaball/fast-spring-validation.txt",report.ToString());Debug.Log("AUTO_RUN_VALIDATION_EXIT: "+code+"\n"+report);EditorApplication.Exit(code);
        }
    }
}
