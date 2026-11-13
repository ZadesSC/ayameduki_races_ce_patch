// Trusted external RimWorldTestFramework adapter. No target assemblies are bundled.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Xml;
using Rwtf;
using Verse;
using RimWorld;

public sealed class SolarkRegression : Rwtf.Scenario {
 public override string Id => "solark.compatibility";
 public override string Intent => "Resolved Solark elite CE definitions and real self-explosion skill";
 public static object Field(object o,string n) => o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(o);
 public static void SequenceCases(Context c) {
  var root=Environment.GetEnvironmentVariable("SOLARK_TEST_SOURCE");
  var solark=Environment.GetEnvironmentVariable("SOLARK_TEST_SOLARK");
  var patch=new XmlDocument();patch.Load(Path.Combine(root,"Patches/Solark Race/ThingDefs_Races/AlienRace_Solark.xml"));
  var node=patch.SelectSingleNode("Patch/Operation/operations/li/match");
  foreach(var version in new[]{"1.4","1.5","1.6","1.7"}) {
   var doc=new XmlDocument();doc.Load(Path.Combine(solark,version,"Defs/RaceDefs/Race_NR _b.xml"));
   if(version=="1.7") {var comps=doc.SelectSingleNode("Defs/ThingDef/comps");var unrelated=doc.CreateElement("li");unrelated.SetAttribute("Class","Aya_Common.Aya_CompProperties_Skill");unrelated.InnerXml="<skillID>Unrelated_Explosion</skillID><explosiveDamageType>BombSuper</explosiveDamageType>";comps.AppendChild(unrelated);}
   var sequence=DirectXmlToObject.ObjectFromXml<PatchOperation>(node,false);
   bool applied=sequence.Apply(doc);
   var ops=(System.Collections.Generic.List<PatchOperation>)sequence.GetType().GetField("operations",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sequence);
   var failed=(PatchOperation)sequence.GetType().GetField("lastFailedOperation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sequence);
   c.Event("sequence-result",new{version,applied,failedOperation=failed==null?0:ops.IndexOf(failed)+1,failed=failed?.ToString()});
   File.WriteAllText(Path.Combine(c.Output,"patched-"+version+".xml"),doc.OuterXml);
   c.Check("full patch sequence applies to "+version,applied);
   c.Check("intended damage replaced "+version,doc.SelectSingleNode("Defs/ThingDef/comps/li[explosiveDamageType='Bomb_Solark']")!=null);
   if(version=="1.7") {
    c.Check("unrelated skill untouched",doc.SelectSingleNode("Defs/ThingDef/comps/li[skillID='Unrelated_Explosion']/explosiveDamageType").InnerText=="BombSuper");
    var missing=new XmlDocument();missing.Load(Path.Combine(solark,version,"Defs/RaceDefs/Race_NR _b.xml"));
    missing.SelectSingleNode("Defs/ThingDef/comps/li[skillID='Solark_b_Skill_a']/skillID").InnerText="Other_Skill";
    var negative=DirectXmlToObject.ObjectFromXml<PatchOperation>(node,false);
    bool negativeApplied=negative.Apply(missing);
    var negativeFailed=(PatchOperation)negative.GetType().GetField("lastFailedOperation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(negative);
    var negativeOps=(System.Collections.Generic.List<PatchOperation>)negative.GetType().GetField("operations",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(negative);
    c.Check("missing intended skill still fails operation 7",!negativeApplied && negativeOps.IndexOf(negativeFailed)==6);
   }
  }
 }
 public override void Assert(Context c) {
  SequenceCases(c);
  var d=c.Def<ThingDef>("HAR_Solark_b");
  var skill=d.comps.Single(x => (string)Field(x,"skillID")=="Solark_b_Skill_a" || x.GetType().FullName=="Solark_Dll.Solark_CompProperties_CommandExplosive");
  var bomb=(DamageDef)Field(skill,"explosiveDamageType");
  c.Event("resolved-defs",new{race=d.defName,skill=skill.GetType().FullName,damage=bomb?.defName,stats=d.statBases.Select(x=>new{name=x.stat.defName,value=x.value}).ToArray(),tools=d.tools.Select(x=>new{type=x.GetType().FullName,label=x.label,power=x.power,cooldown=x.cooldownTime,penetration=Field(x,"armorPenetrationBlunt")}).ToArray(),multipliers=d.damageMultipliers.Select(x=>new{damage=x.damageDef.defName,value=x.multiplier}).ToArray(),comps=d.comps.Select(x=>x.GetType().FullName).ToArray()});
  c.Check("intended skill resolves Bomb_Solark",bomb?.defName=="Bomb_Solark");
  foreach(var pair in new[]{Tuple.Create("ArmorRating_Sharp",18f),Tuple.Create("ArmorRating_Blunt",40f),Tuple.Create("ArmorRating_Heat",.5f)})
   c.Check(pair.Item1,d.statBases.Count(x=>x.stat.defName==pair.Item1)==1 && Math.Abs(d.statBases.Single(x=>x.stat.defName==pair.Item1).value-pair.Item2)<.001);
  c.Check("three unique CE melee tools",d.tools.Count==3 && d.tools.All(x=>x.GetType().FullName=="CombatExtended.ToolCE") && d.tools.Select(x=>x.label).Distinct().Count()==3);
  foreach(var tool in new[]{Tuple.Create("left fist",30f,1f,60f),Tuple.Create("right fist",30f,1f,60f),Tuple.Create("head",50f,2f,15f)}) {
   var t=d.tools.SingleOrDefault(x=>x.label==tool.Item1);
   c.Check("melee values: "+tool.Item1,t!=null && t.power==tool.Item2 && t.cooldownTime==tool.Item3 && Convert.ToSingle(Field(t,"armorPenetrationBlunt"))==tool.Item4);
  }
  var alien=Field(d,"alienRace");var restriction=Field(alien,"raceRestriction");var apparel=((IEnumerable)Field(restriction,"whiteApparelList")).Cast<ThingDef>().Select(x=>x.defName).ToArray();
  c.Event("apparel",apparel);
  foreach(var name in new[]{"CE_Apparel_TacVest","CE_Apparel_Backpack","CE_Apparel_TribalBackpack","CE_Apparel_BallisticShield","CE_Apparel_MeleeShield","CE_Apparel_GasMask","CE_Apparel_ImprovGasMask"})c.Check("apparel permission once: "+name,apparel.Count(x=>x==name)==1);
  foreach(var pair in new[]{Tuple.Create("Bomb_Solark",0f),Tuple.Create("Fragment",.3f),Tuple.Create("Bomb_Secondary",.2f),Tuple.Create("Thermobaric",.3f),Tuple.Create("Flame_Secondary",2f),Tuple.Create("PrometheumFlame",2f)})c.Check("multiplier once: "+pair.Item1,d.damageMultipliers.Count(x=>x.damageDef.defName==pair.Item1)==1 && Math.Abs(d.damageMultipliers.Single(x=>x.damageDef.defName==pair.Item1).multiplier-pair.Item2)<.001);
  foreach(var type in new[]{"CombatExtended.CompProperties_Inventory","CombatExtended.CompProperties_TacticalManager","CombatExtended.CompProperties_Suppressable"})c.Check("earlier comp retained: "+type,d.comps.Any(x=>x.GetType().FullName==type));
  var ceBomb=c.Def<DamageDef>("Bomb_Solark");c.Event("bomb-defaults",new{damage=ceBomb.defaultDamage,penetration=ceBomb.defaultArmorPenetration,stoppingPower=ceBomb.defaultStoppingPower});
  c.Check("CE bomb defaults unchanged",ceBomb.defaultDamage==550 && ceBomb.defaultArmorPenetration==55 && ceBomb.defaultStoppingPower==2);
 }
 public override void Setup(Context c) {
  // Isolated quicktest: clear a remote circle; no normal save or settings touched.
  var center=c.Map.AllCells.First(x=>x.x>25&&x.z>25&&x.x<c.Map.Size.x-25&&x.z<c.Map.Size.z-25&&x.DistanceTo(c.Map.Center)>30&&x.Standable(c.Map));
  foreach(var cell in GenRadial.RadialCellsAround(center,15,true))foreach(var thing in cell.GetThingList(c.Map).ToList())if(thing is Pawn || thing.def.category==ThingCategory.Building || thing.def.category==ThingCategory.Plant)thing.Destroy(DestroyMode.Vanish);
  var race=c.Def<ThingDef>("HAR_Solark_b");var kind=DefDatabase<PawnKindDef>.AllDefsListForReading.First(x=>x.race==race);
  var pawn=PawnGenerator.GeneratePawn(kind,Faction.OfPlayer);GenSpawn.Spawn(pawn,center,c.Map);
  var wall=ThingMaker.MakeThing(ThingDefOf.Wall,c.Def<ThingDef>("BlocksGranite"));GenSpawn.Spawn(wall,center+new IntVec3(2,0,0),c.Map);
  var comp=pawn.AllComps.Single(x=>(string)Field(x.props,"skillID")=="Solark_b_Skill_a" || x.props.GetType().FullName=="Solark_Dll.Solark_CompProperties_CommandExplosive");
  var command=comp.CompGetGizmosExtra().OfType<Command_Action>().First();
  c.Check("explosion command enabled",!command.Disabled);
  var hp=wall.HitPoints;var health=pawn.health.summaryHealth.SummaryHealthPercent;
  c.Event("ability-before",new{caster=pawn.GetUniqueLoadID(),center=center.ToString(),wallHP=hp,casterHealth=health,damage=((DamageDef)Field(comp.props,"explosiveDamageType")).defName,route="real self-target gizmo action, normal component and map ticks; wall is nonimmune control"});
  if(command.Disabled)throw new Unsupported("Explosion command unexpectedly disabled");
  command.action();
  c.Wait(()=>wall.Destroyed||wall.HitPoints<hp,600,20,"Self explosion did not damage nonimmune wall");
  for(int i=0;i<120;i++)c.Step();
  c.Check("explosion damaged nonimmune wall",wall.Destroyed||wall.HitPoints<hp,new{before=hp,after=wall.Destroyed?0:wall.HitPoints});
  c.Check("immune caster survives explosion",!pawn.Dead&&pawn.health.summaryHealth.SummaryHealthPercent>=health-.001,new{before=health,after=pawn.health.summaryHealth.SummaryHealthPercent});
  c.Check("ability entered cooldown",Convert.ToInt32(Field(comp,"coolDownTick"))>0);
  c.Event("ability-after",new{wallDestroyed=wall.Destroyed,wallHP=wall.Destroyed?0:wall.HitPoints,casterDead=pawn.Dead,cooldown=Field(comp,"coolDownTick")});
 }
}
// The public batch runner refuses unsupported CE metadata and aborts on expected
// startup patch errors. This diagnostic driver runs the same SDK Scenario/Context
// under the explicitly requested mixed-version setup and retains every error.
public sealed class SolarkDiagnosticDriver : GameComponent {
 static bool done;static int frames;public SolarkDiagnosticDriver(Game game){}
 public override void GameComponentUpdate(){
  var output=Environment.GetEnvironmentVariable("SOLARK_TEST_OUTPUT");
  if(output==null||done||Current.ProgramState!=ProgramState.Playing||Find.CurrentMap==null||LongEventHandler.ShouldWaitForEvent||++frames<30)return;
  done=true;Find.TickManager.Pause();int failed=0;
  Action<string,object> emit=(type,data)=>File.AppendAllText(Path.Combine(output,"semantic.jsonl"),Json.Encode(new{type,data})+"\n");
  var context=new Context((name,pass,detail)=>{if(!pass)failed++;emit(pass?"pass":"failure",new{name,detail});},emit,output);
  try {
   emit("loaded",new{game=VersionControl.CurrentVersionStringWithRev,mods=LoadedModManager.RunningModsListForReading.Select(m=>new{package=m.PackageId,root=m.RootDir,assemblies=m.assemblies.loadedAssemblies.Select(a=>new{name=a.GetName().Name,path=a.Location}).ToArray()}).ToArray()});
   var scenario=new SolarkRegression();scenario.Assert(context);
   if(Environment.GetEnvironmentVariable("SOLARK_TEST_ABILITY")=="1")scenario.Setup(context);
  }catch(Exception e){failed++;emit("exception",new{text=e.ToString()});}
  finally {try{context.Release();}catch(Exception e){failed++;emit("cleanup-exception",new{text=e.ToString()});}emit("complete",new{failed});
   var app=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Application",false)).First(t=>t!=null);app.GetMethod("Quit",Type.EmptyTypes).Invoke(null,null);}
 }
}
