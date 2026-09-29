// Tests the real component against controlled holders, death callbacks and serialization.
// Native RimWorld yield/death implementations still require an in-game smoke test.
using System;
using System.Collections.Generic;
using System.Linq;
using DreamsOutposts;
using Verse;

internal static class SlaughterhouseTests
{
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new Exception(message);
	}
	private static Pawn Animal(int yield = 151)
	{
		return new Pawn { RaceProps = new RaceProperties { Animal = true }, Yield = yield };
	}
	public static void Main()
	{
		Outpost outpost = new Outpost();
		Window_OutpostManage.Current = new Window_OutpostManage { Outpost = outpost };
		var comp = new OutpostFacilityComp_Slaughterhouse();
		Pawn first = Animal(), second = Animal(17), colonist = new Pawn { IsColonist = true }, prisoner = new Pawn();
		Pawn dead = Animal(); dead.Dead = true;
		foreach (Pawn pawn in new[] { first, second, colonist, prisoner, dead }) outpost.pawns.TryAdd(pawn);
		Thing cargo = new Thing { Marker = "cargo", stackCount = 31 };
		first.inventory.innerContainer.TryAdd(cargo);
		comp.Update(outpost, 1250);
		Check(comp.Paused && !first.Dead && outpost.inventory.Count == 0, "New facilities must remain paused.");
		Console.WriteLine("PASS default pause protects animals");

		var info = new UiFacilityInfoModel();
		comp.BuildUiInfo(outpost, info);
		info.Groups[0].Items.Single(i => i.Kind == UiFacilityInfoKind.Action).Action();
		Check(!comp.Paused && outpost.UpdateRequested && Window_OutpostManage.Current.Cache.Invalidated, "Start action must refresh state.");
		comp.Update(outpost, 60);
		Check(first.Dead && second.Dead && first.KillCalls == 1 && second.KillCalls == 1, "All live animals must be processed once despite holder changes.");
		Check(outpost.pawns.Count == 3 && !colonist.Dead && !prisoner.Dead && dead.KillCalls == 0, "Non-animals and already dead animals must remain untouched.");
		Check(first.LastEfficiency == 1f && second.LastEfficiency == 1f, "Native yields must use 100% efficiency without an extra difficulty multiplier.");
		Console.WriteLine("PASS action, animal filtering, snapshot iteration and native efficiency");
		Check(outpost.inventory.Contains(cargo) && !cargo.Destroyed, "Pack animal cargo must survive.");
		Check(outpost.inventory.InnerListForReading.Where(t => t.Marker == "product").Sum(t => t.stackCount) == 168, "Every product must enter stock.");
		Check(outpost.inventory.InnerListForReading.All(t => t.stackCount <= t.def.stackLimit), "Oversized products must be split without loss.");
		Check(first.Corpse.Destroyed && second.Corpse.Destroyed && colonist.records.Count == 2, "Corpses must be cleaned and slaughter counted.");
		comp.Update(outpost, 1250);
		Check(first.KillCalls == 1 && outpost.inventory.InnerListForReading.Sum(t => t.stackCount) == 199, "Later updates must not duplicate outputs.");
		Console.WriteLine("PASS cargo, split stacks, corpse cleanup and no duplicate output");

		comp.TogglePaused(outpost);
		Pawn third = Animal(); outpost.pawns.TryAdd(third);
		comp.Update(outpost, 1);
		Check(!third.Dead, "Pausing must protect newly stored animals immediately.");
		Scribe_Values.Loading = false; comp.ExposeData();
		var restored = new OutpostFacilityComp_Slaughterhouse();
		Scribe_Values.Loading = true; restored.ExposeData();
		Check(restored.Paused, "Paused state must survive reload.");
		Scribe_Values.Loading = false; comp.TogglePaused(outpost); comp.ExposeData();
		Scribe_Values.Loading = true; restored.ExposeData();
		Check(!restored.Paused, "Running state must survive reload.");
		Scribe_Values.Data.Clear(); restored.ExposeData();
		Check(restored.Paused, "Missing saved state must default to paused.");
		Console.WriteLine("PASS immediate pause and saved paused/running/default states");

		outpost = new Outpost(); comp = new OutpostFacilityComp_Slaughterhouse(); comp.TogglePaused(outpost);
		Pawn refused = Animal(), next = Animal(); refused.RefuseDeath = true;
		outpost.pawns.TryAdd(refused); outpost.pawns.TryAdd(next);
		comp.Update(outpost, 1250); comp.Update(outpost, 1250);
		Check(comp.Paused && outpost.pawns.Contains(refused) && refused.KillCalls == 1 && !next.Dead, "Failed death must restore the animal and pause without retries.");
		Console.WriteLine("PASS death failure preserves animal and stops retries");

		outpost = new Outpost(); comp = new OutpostFacilityComp_Slaughterhouse(); comp.TogglePaused(outpost);
		first = Animal(); second = Animal();
		first.OnKill = () => outpost.pawns.Remove(second);
		outpost.pawns.TryAdd(first); outpost.pawns.TryAdd(second);
		comp.Update(outpost, 1250);
		Check(first.Dead && !second.Dead && second.KillCalls == 0, "Animals removed by callbacks must not be processed from a stale snapshot.");
		Console.WriteLine("PASS callback-driven holder changes and no resident butcher");

		outpost = new Outpost(); comp = new OutpostFacilityComp_Slaughterhouse();
		comp.parent = new OutpostFacility(); comp.TogglePaused(outpost);
		first = Animal(); cargo = new Thing { Marker = "cargo", stackCount = 13 };
		first.inventory.innerContainer.TryAdd(cargo); outpost.pawns.TryAdd(first);
		OutpostAutomaticAirdropUtility.Enabled = true;
		comp.Update(outpost, 1250);
		Check(OutpostAutomaticAirdropUtility.Facility == comp.parent, "Airdrop must use the producing facility's settings.");
		Check(OutpostAutomaticAirdropUtility.Products.Sum(t => t.stackCount) == 151, "All butcher products must reach airdrop routing.");
		Check(OutpostAutomaticAirdropUtility.Products.All(t => t.stackCount <= t.def.stackLimit), "Airdrop output must be split into valid stacks.");
		Check(outpost.inventory.Count == 1 && outpost.inventory.Contains(cargo), "Only butcher products may be routed; cargo must stay in stock.");
		Check(first.Corpse.Destroyed, "Airdrop must not prevent corpse cleanup.");
		Console.WriteLine("PASS slaughter output routing, facility identity and cargo preservation");

		OutpostAutomaticAirdropUtility.Enabled = false;
		foreach (int yield in new[] { 75000, 75075, 75076 })
		{
			Log.Warnings.Clear();
			outpost = new Outpost(); comp = new OutpostFacilityComp_Slaughterhouse(); comp.TogglePaused(outpost);
			first = Animal(yield); outpost.pawns.TryAdd(first);
			comp.Update(outpost, 1250);
			Check(outpost.inventory.Count <= 1001, "Splitting must stop after at most 1000 iterations plus the remainder.");
			Check(outpost.inventory.InnerListForReading.Sum(t => t.stackCount) == yield, "Reaching the split cap must preserve all products.");
			Check(Log.Warnings.Count == (yield > 75075 ? 1 : 0), "Warn only when the split cap leaves unfinished work.");
			if (yield > 75075)
				Check(Log.Warnings[0].Contains("OutpostFacilityComp_Slaughterhouse.cs: Slaughter") && Log.Warnings[0].Contains("loop limit=1000"), "Warning must identify the source location and cap.");
			Check(first.Corpse.Destroyed, "Reaching the split cap must still finish corpse cleanup.");
		}
		Console.WriteLine("PASS below/at/above split cap, warning location and remainder preservation");
	}
}

namespace Verse
{
	public interface IExposable { void ExposeData(); }
	public static class Translation { public static string Translate(this string s) => s; }
	public static class Scribe_Values
	{
		public static bool Loading;
		public static readonly Dictionary<string, bool> Data = new Dictionary<string, bool>();
		public static void Look(ref bool value, string key, bool defaultValue)
		{
			if (Loading) value = Data.ContainsKey(key) ? Data[key] : defaultValue;
			else Data[key] = value;
		}
	}
	public class ThingDef { public int stackLimit = 75; }
	public class Thing
	{
		public bool Destroyed;
		public int stackCount = 1;
		public ThingDef def = new ThingDef();
		public string Marker;
		public object holdingOwner;
		public virtual void Destroy() { Destroyed = true; }
		public Thing SplitOff(int count) { stackCount -= count; return new Thing { stackCount = count, def = def, Marker = Marker }; }
	}
	public class ThingOwner<T> where T : Thing
	{
		public readonly List<T> InnerListForReading = new List<T>();
		public int Count => InnerListForReading.Count;
		public bool Contains(T item) => InnerListForReading.Contains(item);
		public bool TryAdd(T item) { InnerListForReading.Add(item); item.holdingOwner = this; return true; }
		public void Remove(T item) { InnerListForReading.Remove(item); item.holdingOwner = null; }
		public void TryTransferAllToContainer(ThingOwner<Thing> target)
		{
			foreach (T item in InnerListForReading.ToArray()) { Remove(item); target.TryAdd(item); }
		}
	}
	public class RaceProperties { public bool Animal; }
	public class PawnInventory { public ThingOwner<Thing> innerContainer = new ThingOwner<Thing>(); }
	public class Records { public int Count; public void Increment(object record) { Count++; } }
	public class Pawn : Thing
	{
		public bool Dead, Downed, IsColonist, RefuseDeath;
		public int Yield, KillCalls;
		public float LastEfficiency;
		public RaceProperties RaceProps = new RaceProperties();
		public PawnInventory inventory = new PawnInventory();
		public Records records = new Records();
		public Corpse Corpse;
		public Action OnKill;
		public void Kill(DamageInfo info)
		{
			KillCalls++;
			if (RefuseDeath) return;
			Dead = true; Corpse = new Corpse();
			foreach (Thing item in inventory.innerContainer.InnerListForReading) item.Destroy();
			OnKill?.Invoke();
		}
		public IEnumerable<Thing> ButcherProducts(Pawn butcher, float efficiency)
		{
			if (!Dead) throw new Exception("Butchering requires native death first.");
			LastEfficiency = efficiency;
			yield return new Thing { stackCount = Yield, Marker = "product" };
		}
	}
	public class Corpse : Thing { }
	public struct DamageInfo { public DamageInfo(object def, float amount, Pawn instigator) { } }
	public static class Log
	{
		public static readonly List<string> Warnings = new List<string>();
		public static void Error(string message) { }
		public static void Warning(string message) { Warnings.Add(message); }
	}
	public static class Find { public static WorldPawns WorldPawns = new WorldPawns(); }
	public class WorldPawns
	{
		private readonly HashSet<Pawn> pawns = new HashSet<Pawn>();
		public bool Contains(Pawn pawn) => pawns.Contains(pawn);
		public void PassToWorld(Pawn pawn) { pawns.Add(pawn); }
	}
}
namespace RimWorld
{
	public static class DamageDefOf { public static readonly object ExecutionCut = new object(); }
	public static class RecordDefOf { public static readonly object AnimalsSlaughtered = new object(); }
}
namespace DreamsOutposts
{
	public static class OutpostAutomaticAirdropUtility
	{
		public static bool Enabled;
		public static OutpostFacility Facility;
		public static List<Thing> Products;
		public static bool TryDeliver(Outpost outpost, OutpostFacility facility, List<Thing> products)
		{
			if (!Enabled) return false;
			Facility = facility; Products = products; return true;
		}
	}
	public enum UiChipKind { Neutral, Warn }
	public class OutpostFacility { }
	public class Outpost
	{
		public readonly ThingOwner<Pawn> pawns = new ThingOwner<Pawn>();
		public readonly ThingOwner<Thing> inventory = new ThingOwner<Thing>();
		public List<Pawn> PawnsListForReading => pawns.InnerListForReading;
		public bool UpdateRequested;
		public void RequestUpdate() { UpdateRequested = true; }
	}
	public class Cache { public bool Invalidated; public void Invalidate() { Invalidated = true; } }
	public class Window_OutpostManage
	{
		public static Window_OutpostManage Current;
		public Outpost Outpost;
		public Cache Cache = new Cache();
	}
}
