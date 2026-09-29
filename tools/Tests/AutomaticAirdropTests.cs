// Exercises the real routing code with controlled holders and a recording drop-pod API.
using System;
using System.Collections.Generic;
using System.Linq;
using DreamsOutposts;
using Verse;
using RimWorld;

internal static class AutomaticAirdropTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static OutpostFacility Producer(bool taming = false, bool slaughter = false)
    {
        var facility = new OutpostFacility { autoAirdropEnabled = true };
        if (taming) facility.def.Processes.Add(new OutpostProcessProperties { Worker = new OutpostProcessWorker_Taming() });
        else if (slaughter) facility.def.Slaughter = new OutpostFacilityCompProperties_Slaughterhouse();
        else facility.def.Productions.Add(new object());
        return facility;
    }
    static Outpost Setup(bool intelligent = true)
    {
        DropPodUtility.Dropped.Clear(); Messages.Sent.Clear(); Find.AnyPlayerHomeMap = new Map();
        var outpost = new Outpost();
        outpost.OperationalFacilities.Add(new OutpostFacility { def = new OutpostFacilityDef {
            automaticAirdropController = true, intelligentAirdropController = intelligent } });
        return outpost;
    }
    static Thing Item(ThingDef def, int count) => new Thing { def = def, stackCount = count };
    static int Dropped(ThingDef def) => DropPodUtility.Dropped.Where(t => t.def == def).Sum(t => t.stackCount);
    public static void Main()
    {
        var taming = Producer(taming: true); var slaughter = Producer(slaughter: true); var normal = Producer();
        Check(OutpostAutomaticAirdropUtility.IsSelectableProducer(taming), "Taming core must be selectable.");
        Check(OutpostAutomaticAirdropUtility.IsSelectableProducer(slaughter), "Slaughterhouse must be selectable.");
        Check(OutpostAutomaticAirdropUtility.IsSelectableProducer(normal), "Normal production must remain selectable.");
        Check(!OutpostAutomaticAirdropUtility.IsSelectableProducer(new OutpostFacility()), "Non-producing facilities must be excluded.");
        normal.def.automaticAirdropController = true;
        Check(!OutpostAutomaticAirdropUtility.IsSelectableProducer(normal), "Controllers must be excluded.");
        normal.def.automaticAirdropController = false;
        Check(!OutpostAutomaticAirdropUtility.IsSelectableProducer(null), "Null facility must be excluded.");
        Console.WriteLine("PASS ordinary, taming and slaughter facility detection");

        var meat = new ThingDef { LabelCap = "meat" }; var leather = new ThingDef { LabelCap = "leather" };
        var outpost = Setup(); slaughter.intelligentAirdropStockTarget = 10;
        outpost.inventory.TryAdd(Item(meat, 3)); outpost.inventory.TryAdd(Item(leather, 8));
        var batch = new List<Thing> { Item(meat, 4), Item(leather, 8), Item(meat, 20) };
        Check(OutpostAutomaticAirdropUtility.TryDeliver(outpost, slaughter, batch), "Mixed output must be handled.");
        Check(OutpostStockUtility.CountInStock(outpost, meat) == 10 && OutpostStockUtility.CountInStock(outpost, leather) == 10, "Each product needs its own reserve across stacks.");
        Check(Dropped(meat) == 17 && Dropped(leather) == 6, "Only surplus may be delivered, without loss.");
        Check(Messages.Sent.Count == 2, "Mixed outputs must have accurate per-product messages.");
        Console.WriteLine("PASS mixed butcher products, split stacks and per-product reserves");

        var cow = new ThingDef { LabelCap = "cow" }; var sheep = new ThingDef { LabelCap = "sheep" };
        outpost = Setup(); taming.intelligentAirdropStockTarget = 2;
        outpost.pawns.TryAdd(new Pawn { def = cow });
        outpost.pawns.TryAdd(new Pawn { def = sheep, Dead = true });
        var keptCow = new Pawn { def = cow }; var sentCow = new Pawn { def = cow }; var keptSheep = new Pawn { def = sheep };
        Check(OutpostAutomaticAirdropUtility.TryDeliver(outpost, taming, new List<Thing> { keptCow, sentCow, keptSheep }), "Animal batch must be handled.");
        Check(outpost.pawns.Contains(keptCow) && outpost.pawns.Contains(keptSheep), "Reserved animals must enter the pawn holder.");
        Check(!outpost.pawns.Contains(sentCow) && DropPodUtility.Dropped.Contains(sentCow), "Surplus animal must enter pod only.");
        Check(outpost.inventory.InnerListForReading.Count == 0 && Dropped(sheep) == 0, "Species reserves must be independent; animals are not items.");
        Check(OutpostAutomaticAirdropUtility.TryDeliver(outpost, taming, new List<Thing> { new Pawn { def = cow } }), "Later captured animals must see the updated stock.");
        Check(Dropped(cow) == 2, "Sequential captures must not repeatedly reserve the same shortage.");
        Console.WriteLine("PASS animal holders, per-species reserves and sequential captures");

        outpost = Setup(false); slaughter.intelligentAirdropStockTarget = 999;
        Check(OutpostAutomaticAirdropUtility.TryDeliver(outpost, slaughter, new List<Thing> { Item(meat, 6) }) && Dropped(meat) == 6, "Basic controller must send all enabled output.");
        taming.intelligentAirdropStockTarget = 999;
        Check(OutpostAutomaticAirdropUtility.TryDeliver(outpost, taming, new List<Thing> { new Pawn { def = cow } }) && Dropped(cow) == 1, "Basic controller must also send live animals.");
        outpost = Setup(); normal.intelligentAirdropStockTarget = 0;
        Check(OutpostAutomaticAirdropUtility.TryDeliver(new OutpostProductionContext { Outpost = outpost, Facility = normal, Products = new List<Thing> { Item(meat, 12) } }) && Dropped(meat) == 12, "Existing production entry point must still work.");
        Console.WriteLine("PASS basic controller and existing production entry point");

        foreach (string mode in new[] { "disabled", "no map", "no controller" })
        {
            outpost = Setup(); slaughter.autoAirdropEnabled = mode != "disabled";
            if (mode == "no map") Find.AnyPlayerHomeMap = null;
            if (mode == "no controller") outpost.OperationalFacilities.Clear();
            var output = Item(meat, 20);
            Check(!OutpostAutomaticAirdropUtility.TryDeliver(outpost, slaughter, new List<Thing> { output }), "Must let caller store output: " + mode);
            Check(output.stackCount == 20 && output.holdingOwner == null && DropPodUtility.Dropped.Count == 0, "Fallback must leave output untouched: " + mode);
        }
        outpost = Setup(); slaughter.autoAirdropEnabled = true; slaughter.intelligentAirdropStockTarget = 100;
        Check(OutpostAutomaticAirdropUtility.TryDeliver(outpost, slaughter, new List<Thing> { Item(meat, 8) }) && Dropped(meat) == 0 && OutpostStockUtility.CountInStock(outpost, meat) == 8, "Below-reserve output must all be stored.");
        Console.WriteLine("PASS disabled/no-map/no-controller fallbacks and fully reserved output");
    }
}
namespace UnityEngine { public static class Mathf { public static int Max(int a, int b) => Math.Max(a,b); public static int Min(int a, int b) => Math.Min(a,b); } }
namespace Verse
{
    public class ThingDef { public string LabelCap; }
    public class Thing
    {
        public ThingDef def; public int stackCount = 1; public bool Destroyed; public object holdingOwner;
        public Thing SplitOff(int count) { stackCount -= count; return new Thing { def = def, stackCount = count }; }
    }
    public class Pawn : Thing { public bool Dead; }
    public class ThingOwner<T> where T : Thing
    {
        public List<T> InnerListForReading = new List<T>();
        public bool Contains(T thing) => InnerListForReading.Contains(thing);
        public bool TryAdd(T thing) { if (thing.holdingOwner != null) throw new Exception("Already owned"); InnerListForReading.Add(thing); thing.holdingOwner = this; return true; }
    }
    public class Map { }
    public struct IntVec3 { }
    public struct TargetInfo { public TargetInfo(IntVec3 spot, Map map) { } }
    public static class Find { public static Map AnyPlayerHomeMap; }
    public static class Extensions
    {
        public static bool NullOrEmpty<T>(this List<T> list) => list == null || list.Count == 0;
        public static string Translate(this string key, params object[] args) => key + ":" + string.Join(",", args);
    }
    public static class Messages { public static List<string> Sent = new List<string>(); public static void Message(string text, TargetInfo target, object type, bool historical) { Sent.Add(text); } }
}
namespace RimWorld
{
    public class Faction { public static Faction OfPlayer = new Faction(); }
    public static class MessageTypeDefOf { public static object TaskCompletion; }
    public static class DropCellFinder { public static IntVec3 TradeDropSpot(Map map) => new IntVec3(); }
    public static class DropPodUtility
    {
        public static List<Thing> Dropped = new List<Thing>();
        public static void DropThingsNear(IntVec3 spot, Map map, IEnumerable<Thing> things, int delay, bool canInstaDropDuringInit, bool leaveSlag, bool canRoofPunch, bool forbid, bool allowFogged, Faction faction)
        {
            foreach (Thing thing in things) { if (thing.holdingOwner != null) throw new Exception("Pod received owned output"); thing.holdingOwner = Dropped; Dropped.Add(thing); }
        }
    }
}
namespace DreamsOutposts
{
    public class OutpostFacilityCompProperties_Slaughterhouse { }
    public class OutpostProcessWorker_Taming { }
    public class OutpostProcessProperties { public object Worker; }
    public class OutpostFacilityDef
    {
        public bool automaticAirdropController, intelligentAirdropController;
        public List<object> Productions = new List<object>();
        public List<OutpostProcessProperties> Processes = new List<OutpostProcessProperties>();
        public OutpostFacilityCompProperties_Slaughterhouse Slaughter;
        public T GetCompProperties<T>() where T : class => Slaughter as T;
    }
    public class OutpostFacility { public OutpostFacilityDef def = new OutpostFacilityDef(); public bool autoAirdropEnabled; public int intelligentAirdropStockTarget; }
    public class Outpost
    {
        public List<OutpostFacility> OperationalFacilities = new List<OutpostFacility>();
        public ThingOwner<Pawn> pawns = new ThingOwner<Pawn>(); public ThingOwner<Thing> inventory = new ThingOwner<Thing>();
        public List<Pawn> PawnsListForReading => pawns.InnerListForReading;
    }
    public class OutpostProductionContext { public Outpost Outpost; public OutpostFacility Facility; public List<Thing> Products; }
    public static class OutpostStockUtility { public static int CountInStock(Outpost outpost, ThingDef def) => outpost.inventory.InnerListForReading.Where(t => t.def == def && !t.Destroyed).Sum(t => t.stackCount); }
}
