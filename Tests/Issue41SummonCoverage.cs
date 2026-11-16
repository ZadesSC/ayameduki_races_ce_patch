using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using RimWorld;
using RimWorld.Planet;
using Rwtf;
using Verse;
using Verse.AI.Group;

// External framework adapter: never compiled into the mod's runtime assembly.
public sealed class Issue41SummonCoverage : Rwtf.Scenario
{
    public override string Id => "aya.issue41";
    public override string Intent => "Summon coverage, owning-mod gates, idempotence and CE armor";
    public override string[] SupportedGameVersions => new[] { "1.6" };
    public override string[] RequiredPackages => new[] { "ceteam.combatextended" };
    public override FixtureProvider Fixture => new Issue41Fixture();

    private const string Parts = "//*[def='Body' or def='Tail' or def='Neck' or def='Head' or def='Leg' or def='Paw']";

    public override void Assert(Context c)
    {
        foreach (string owner in new[] { "Eveliet", "Idearn" })
        {
            string body = owner == "Eveliet" ? "HAR_EL_M_Body" : "HAR_IA_M_Body";
            bool active = LoadedModManager.RunningModsListForReading.Any(m =>
                m.PackageId.Equals("ayameduki.har" + owner.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
            string relative = owner == "Eveliet"
                ? "Patches/Eveliet Race/Bodies/Bodies_Summons.xml"
                : "Patches/Idearn Race/Bodies/Bodies_Summon.xml";
            var patch = new XmlDocument();
            patch.Load(Path.Combine(c.Parameter("candidate"), relative));
            XmlNode operation = patch.SelectSingleNode("Patch/Operation");
            foreach (string version in new[] { "1.4", "1.5", "1.6", "1.7" })
            {
                var source = new XmlDocument();
                source.Load(Path.Combine(c.Parameter(owner.ToLowerInvariant() + "Source"),
                    version, "Defs/NPCDefs/Summon_Bodies.xml"));
                string label = owner + " " + version;
                string before = source.OuterXml;
                var groups = Snapshot(source, body);
                c.Check(label + " full operation applies", Apply(operation, source));
                if (!active)
                {
                    c.Check(label + " absent owner leaves XML untouched", source.OuterXml == before);
                    continue;
                }
                XmlNodeList targets = source.SelectNodes("Defs/BodyDef[defName='" + body + "']" + Parts);
                c.Check(label + " twelve intended records", targets.Count == 12);
                c.Check(label + " one group list and coverage per record", targets.Cast<XmlNode>().All(n =>
                    n.SelectNodes("groups").Count == 1 && n.SelectNodes("groups/li[.='CoveredByNaturalArmor']").Count == 1));
                CheckPreserved(c, label, groups, Snapshot(source, body));
                string once = source.OuterXml;
                c.Check(label + " second application succeeds", Apply(operation, source));
                c.Check(label + " second application is unchanged", source.OuterXml == once);

                // Simulate partial coverage supplied before this patch by another addon.
                var mixed = new XmlDocument();
                mixed.LoadXml(before);
                XmlNode first = mixed.SelectNodes("Defs/BodyDef[defName='" + body + "']" + Parts)[0];
                XmlNode list = first.SelectSingleNode("groups");
                if (list == null) { list = mixed.CreateElement("groups"); first.AppendChild(list); }
                foreach (string name in new[] { "CoveredByNaturalArmor", "UnrelatedTestGroup" })
                {
                    if (list.SelectSingleNode("li[.='" + name + "']") != null) continue;
                    XmlElement li = mixed.CreateElement("li"); li.InnerText = name; list.AppendChild(li);
                }
                c.Check(label + " partial precoverage applies", Apply(operation, mixed));
                c.Check(label + " partial precoverage remains unique", first.SelectNodes("groups").Count == 1 &&
                    first.SelectNodes("groups/li[.='CoveredByNaturalArmor']").Count == 1 &&
                    first.SelectNodes("groups/li[.='UnrelatedTestGroup']").Count == 1);
            }
            var missing = new XmlDocument(); missing.LoadXml("<Defs/>");
            c.Check(owner + " missing-body control", active ? !Apply(operation, missing) : Apply(operation, missing));
            c.Check(owner + " missing-body control has no mutations", missing.OuterXml == "<Defs />");
            if (active) CheckResolved(c, body, owner == "Idearn");
            else c.Check(owner + " owning body is unavailable", DefDatabase<BodyDef>.GetNamedSilentFail(body) == null);
        }
    }

    public override void AfterReload(Context c)
    {
        foreach (string body in new[] { "HAR_EL_M_Body", "HAR_IA_M_Body" })
            if (DefDatabase<BodyDef>.GetNamedSilentFail(body) != null) CheckResolved(c, body, body == "HAR_IA_M_Body");
    }

    private static bool Apply(XmlNode operation, XmlDocument document) =>
        DirectXmlToObject.ObjectFromXml<PatchOperation>(operation, false).Apply(document);

    private static Dictionary<string, string[]> Snapshot(XmlDocument document, string body)
    {
        return document.SelectNodes("Defs/BodyDef[defName='" + body + "']//*[def]").Cast<XmlNode>()
            .ToDictionary(PathOf, n => n.SelectNodes("groups/li").Cast<XmlNode>().Select(x => x.InnerText).ToArray());
    }

    private static string PathOf(XmlNode node)
    {
        if (node.ParentNode == null) return "";
        int index = node.PreviousSibling == null ? 1 : node.ParentNode.ChildNodes.Cast<XmlNode>()
            .TakeWhile(x => !ReferenceEquals(x, node)).Count(x => x.Name == node.Name) + 1;
        return PathOf(node.ParentNode) + "/" + node.Name + "[" + index + "]";
    }

    private static void CheckPreserved(Context c, string label,
        Dictionary<string, string[]> before, Dictionary<string, string[]> after)
    {
        c.Check(label + " original body records preserved", before.Keys.OrderBy(x => x).SequenceEqual(after.Keys.OrderBy(x => x)));
        c.Check(label + " original attack and custom groups preserved", before.All(pair =>
            after.ContainsKey(pair.Key) && pair.Value.GroupBy(x => x).All(g => after[pair.Key].Count(x => x == g.Key) == g.Count())));
    }

    private static void CheckResolved(Context c, string bodyName, bool armored)
    {
        BodyDef body = c.Def<BodyDef>(bodyName);
        var targets = body.AllParts.Where(p => new[] { "Body", "Tail", "Neck", "Head", "Leg", "Paw" }.Contains(p.def.defName)).ToArray();
        c.Check(bodyName + " twelve loaded covered parts", targets.Length == 12);
        foreach (BodyPartRecord part in targets)
            c.Check(bodyName + " loaded coverage once " + part.Index,
                part.groups.Count(g => g.defName == "CoveredByNaturalArmor") == 1);
        c.Check(bodyName + " head attack group retained", body.AllParts.Any(p => p.groups.Any(g => g.defName == "HeadAttackTool")));
        foreach (string paw in new[] { "FrontLeftPaw", "FrontRightPaw" })
            c.Check(bodyName + " paw group retained " + paw, body.AllParts.Any(p => p.groups.Any(g => g.defName == paw)));

        PawnKindDef kind = DefDatabase<PawnKindDef>.AllDefsListForReading.First(k => k.race.race.body == body);
        Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
        Type utility = c.OptionalType("CombatExtended.ArmorUtilityCE");
        if (utility == null) throw new Unsupported("CE armor utility unavailable");
        MethodInfo partial = utility.GetMethod("PartialStat", new[] { typeof(Pawn), typeof(StatDef), typeof(BodyPartRecord) });
        BodyPartRecord core = body.corePart;
        var original = core.groups.ToList();
        try
        {
            foreach (StatDef stat in new[] { StatDefOf.ArmorRating_Sharp, StatDefOf.ArmorRating_Blunt })
            {
                float full = pawn.GetStatValue(stat);
                float covered = (float)partial.Invoke(null, new object[] { pawn, stat, core });
                c.Check(bodyName + " declared armor precondition " + stat.defName, armored ? full > 0 : full == 0);
                c.Check(bodyName + " CE core armor " + stat.defName, Math.Abs(covered - full) < .001,
                    new { full, covered });
            }
            // Direct CE hit calculation: no projectile-accuracy or survival claim.
            var packet = new DamageInfo(DamageDefOf.Blunt, 10f, 1f, -1f, null, core, c.Def<ThingDef>("Bullet_ChargeRifle"));
            MethodInfo afterArmor = utility.GetMethod("GetAfterArmorDamage");
            object[] args = { packet, pawn, core, false, false, false };
            float protectedDamage = ((DamageInfo)afterArmor.Invoke(null, args)).Amount;
            core.groups.RemoveAll(g => g.defName == "CoveredByNaturalArmor");
            args = new object[] { packet, pawn, core, false, false, false };
            float uncoveredDamage = ((DamageInfo)afterArmor.Invoke(null, args)).Amount;
            c.Check(bodyName + " CE blunt-hit coverage control", armored ? protectedDamage < uncoveredDamage : protectedDamage == uncoveredDamage,
                new { protectedDamage, uncoveredDamage, intervention = "coverage removed for negative control only; restored in finally" });
        }
        finally
        {
            core.groups.Clear(); core.groups.AddRange(original);
            c.Check(bodyName + " temporary coverage control restored", core.groups.SequenceEqual(original));
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        }
    }
}

public sealed class Issue41Fixture : FixtureProvider
{
    public override FixtureContract Contract => new FixtureContract
    {
        Id = "aya-issue41-empty", Intent = "focused summon armor regression", HorizonTicks = 10000,
        Capabilities = new[] { "generated-map", "environment", "save-reload" },
        Interventions = new[] { "fixed-weather-temperature", "storyteller-disabled", "random-diseases-disabled",
            "random-mental-states-disabled", "setup-pawn-world-retirement", "ambient-wildlife-disabled" },
        Invariants = new Dictionary<string, double> { { "population", 0 }, { "mapSize", 100 }, { "outdoorC", 21 } }
    };

    public override void Setup(Context c)
    {
        new MapRecipe { Seed = 43210, Size = 100, Biome = "Desert", Weather = "Clear", Temperature = 21 }.Generate(c);
        c.ConfigureEnvironment(new EnvironmentPolicy { Weather = "Clear", OutdoorC = 21, Storyteller = false,
            RandomDiseases = false, RandomMentalStates = false, SuppressWildlife = true });
        foreach (Pawn pawn in c.Map.mapPawns.AllPawnsSpawned.ToList())
        {
            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
            pawn.ownership?.UnclaimAll(); pawn.jobs?.StopAll(); pawn.DeSpawn();
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        }
    }

    public override void Validate(Context c, string stage)
    {
        bool passed = c.Map.Size.x == 100 && c.Map.Size.z == 100 && c.Map.mapPawns.AllPawnsSpawned.Count == 0 &&
            Math.Abs(c.Map.mapTemperature.OutdoorTemp - 21) < .01;
        c.Check("issue41 fixture " + stage, passed);
        c.Event("fixture-validation", new { stage, supported = true, passed, invariants = Contract.Invariants });
    }
}
