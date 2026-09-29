using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_Slaughterhouse : OutpostFacilityCompProperties
	{
		public OutpostFacilityCompProperties_Slaughterhouse()
		{
			compClass = typeof(OutpostFacilityComp_Slaughterhouse);
		}
	}

	public class OutpostFacilityComp_Slaughterhouse : OutpostFacilityComp
	{
		// Installing or loading a component without saved state must never start killing animals.
		private bool paused = true;
		public bool Paused => paused;
		public string ToggleLabel => (paused ? "DreamsOutposts.Slaughterhouse.Start" : "DreamsOutposts.Slaughterhouse.Pause").Translate().ToString();

		public override void ExposeData()
		{
			Scribe_Values.Look(ref paused, "paused", defaultValue: true);
		}

		public void TogglePaused(Outpost outpost)
		{
			paused = !paused;
			outpost?.RequestUpdate();
			RefreshUi(outpost);
		}

		private static void RefreshUi(Outpost outpost)
		{
			Window_OutpostManage window = Window_OutpostManage.Current;
			if (window?.Outpost == outpost) window?.Cache.Invalidate();
		}

		public override void Update(Outpost outpost, int delta)
		{
			if (paused || outpost?.pawns == null || outpost.inventory == null) return;
			// Death callbacks modify holders; iterate a snapshot and recheck membership.
			List<Pawn> animals = outpost.PawnsListForReading
				.Where(p => p != null && !p.Dead && !p.Destroyed && p.RaceProps.Animal).ToList();
			if (animals.Count == 0) return;
			Pawn butcher = outpost.PawnsListForReading.FirstOrDefault(p => p != null && p.IsColonist && !p.Dead && !p.Downed);
			foreach (Pawn animal in animals)
			{
				if (paused) break;
				if (animal.Dead || animal.Destroyed || !outpost.pawns.Contains(animal)) continue;
				try
				{
					Slaughter(outpost, animal, butcher);
				}
				catch (Exception ex)
				{
					// Stop after a failure instead of retrying a destructive operation every update.
					paused = true;
					if (!animal.Dead && !animal.Destroyed && animal.holdingOwner == null)
						outpost.pawns.TryAdd(animal);
					Corpse corpse = animal.Corpse;
					if (corpse != null && !corpse.Destroyed && corpse.holdingOwner == null)
						outpost.inventory.TryAdd(corpse);
					Log.Error("[DreamsOutposts] Slaughterhouse paused after failing to process " + animal + ": " + ex);
				}
			}
			RefreshUi(outpost);
		}

		private void Slaughter(Outpost outpost, Pawn animal, Pawn butcher)
		{
			// Keep pack animals' cargo before native death/corpse cleanup destroys it.
			animal.inventory?.innerContainer.TryTransferAllToContainer(outpost.inventory);
			if ((animal.inventory?.innerContainer.Count ?? 0) != 0)
				throw new InvalidOperationException("Could not unload the animal's inventory.");
			// A Pawn-only holder cannot receive a corpse. Detach before native Kill creates it.
			outpost.pawns.Remove(animal);
			// Register off-map animals for native death/relationship cleanup and world-pawn notifications.
			if (!Find.WorldPawns.Contains(animal)) Find.WorldPawns.PassToWorld(animal);
			// ExecutionCut identifies slaughter without introducing wounds that reduce yields.
			animal.Kill(new DamageInfo(DamageDefOf.ExecutionCut, 0f, instigator: butcher));
			if (!animal.Dead) throw new InvalidOperationException("The animal did not die.");

			// MeatAmount, LeatherAmount and Thing.ButcherProducts already apply difficulty.
			// Pawn.ButcherProducts also handles race-specific products, without map-only blood effects.
			List<Thing> products = animal.ButcherProducts(butcher, 1f).ToList();
			List<Thing> stacks = new List<Thing>();
			foreach (Thing product in products)
			{
				int stackLimit = Math.Max(product.def.stackLimit, 1);
				const int maxSplits = 1000;
				for (int splits = 0; splits < maxSplits && product.stackCount > stackLimit; splits++)
					stacks.Add(product.SplitOff(stackLimit));
				if (product.stackCount > stackLimit)
					Log.Warning("[DreamsOutposts] Taming/OutpostFacilityComp_Slaughterhouse.cs: Slaughter: loop limit=" + maxSplits + ", animal=" + animal + ", product=" + product + ", remaining=" + product.stackCount + "; remainder kept as one oversized stack.");
				stacks.Add(product);
			}
			if (!OutpostAutomaticAirdropUtility.TryDeliver(outpost, parent, stacks))
				foreach (Thing product in stacks) StoreProduct(outpost, product);
			animal.Corpse?.Destroy();
			butcher?.records?.Increment(RecordDefOf.AnimalsSlaughtered);
		}

		private static void StoreProduct(Outpost outpost, Thing product)
		{
			if (!outpost.inventory.TryAdd(product))
				throw new InvalidOperationException("Could not store slaughter product " + product + ".");
		}

		public override void BuildUiInfo(Outpost outpost, UiFacilityInfoModel output)
		{
			UiFacilityInfoGroup group = NewUiInfoGroup("DreamsOutposts.Slaughterhouse.Title".Translate().ToString());
			group.Items.Add(new UiFacilityInfoItem
			{
				Id = "slaughter.status",
				Kind = UiFacilityInfoKind.Status,
				CardPlacement = UiFacilityCardPlacement.Header,
				CardPriority = UiFacilityCardPriority.Core,
				Importance = UiFacilityInfoImportance.Primary,
				Value = (paused ? "DreamsOutposts.Slaughterhouse.Paused" : "DreamsOutposts.Slaughterhouse.Running").Translate().ToString(),
				Tooltip = "DreamsOutposts.Slaughterhouse.Tooltip".Translate().ToString(),
				Tone = paused ? UiChipKind.Neutral : UiChipKind.Warn
			});
			group.Items.Add(new UiFacilityInfoItem
			{
				Id = "slaughter.toggle",
				Kind = UiFacilityInfoKind.Action,
				CardPlacement = UiFacilityCardPlacement.Action,
				CardPriority = UiFacilityCardPriority.Secondary,
				ActionLabel = ToggleLabel,
				ActionTooltip = "DreamsOutposts.Slaughterhouse.Tooltip".Translate().ToString(),
				Action = () => TogglePaused(outpost)
			});
			output.Groups.Add(group);
		}
	}
}
