using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DreamsOutposts
{
	[StaticConstructorOnStartup]
	public static class OutpostAbandonUtility
	{
		private static readonly Texture2D AbandonCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/AbandonHome");

		private const float AbandonCommandOrder = 3000f;

		public static Command AbandonCommand(Outpost outpost)
		{
			return new Command_Action
			{
				defaultLabel = "DreamsOutposts.CommandAbandonOutpost".Translate(),
				defaultDesc = "DreamsOutposts.CommandAbandonOutpostDesc".Translate(),
				icon = AbandonCommandTex,
				Order = 3000f,
				action = delegate
				{
					TryAbandonViaInterface(outpost);
				}
			};
		}

		public static void TryAbandonViaInterface(Outpost outpost)
		{
			if (outpost == null || outpost.Destroyed)
			{
				Messages.Message("DreamsOutposts.OutpostGone".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(ConfirmationText(outpost), delegate
			{
				Abandon(outpost);
			}, destructive: true, "DreamsOutposts.ConfirmAbandonOutpostTitle".Translate(outpost.LabelCap)));
		}

		private static TaggedString ConfirmationText(Outpost outpost)
		{
			StringBuilder content = new StringBuilder();
			List<Pawn> colonists = outpost.Colonists.ToList();
			if (colonists.Count > 0)
			{
				content.Append("DreamsOutposts.ConfirmAbandonOutpostColonists".Translate(colonists.Count));
				for (int i = 0; i < colonists.Count; i++)
				{
					content.AppendLine();
					content.Append("    ").Append(colonists[i].LabelCap);
				}
				content.AppendLine();
			}
			content.AppendLine("DreamsOutposts.ConfirmAbandonOutpostOtherPawns".Translate(outpost.OtherPawns.Count()));
			content.AppendLine("DreamsOutposts.ConfirmAbandonOutpostItems".Translate(outpost.InventoryItems.Count));
			content.Append("DreamsOutposts.ConfirmAbandonOutpostPods".Translate(outpost.airdropPods));
			return "DreamsOutposts.ConfirmAbandonOutpost".Translate(content.ToString());
		}

		public static void Abandon(Outpost outpost)
		{
			if (outpost == null || outpost.Destroyed)
			{
				return;
			}
			string label = outpost.LabelCap;
			PlanetTile tile = outpost.Tile;
			List<Pawn> pawns = CollectPawns(outpost);
			for (int i = 0; i < pawns.Count; i++)
			{
				OutpostUtility.MovePawnInventoryIntoOutpost(outpost, pawns[i]);
			}
			pawns = CollectPawns(outpost);
			for (int i2 = pawns.Count - 1; i2 >= 0; i2--)
			{
				Pawn pawn = pawns[i2];
				if (pawn != null)
				{
					pawn.holdingOwner?.Remove(pawn);
					BanishPawn(pawn, tile);
				}
			}
			outpost.inventory?.ClearAndDestroyContents();
			outpost.Destroy();
			SoundDefOf.Tick_High.PlayOneShotOnCamera();
			Messages.Message("DreamsOutposts.OutpostAbandoned".Translate(label), MessageTypeDefOf.NeutralEvent, historical: false);
			Find.GameEnder.CheckOrUpdateGameOver();
		}

		private static void BanishPawn(Pawn pawn, PlanetTile tile)
		{
			if (pawn.Faction == Faction.OfPlayer || pawn.HostFaction == Faction.OfPlayer)
			{
				PawnBanishUtility.Banish(pawn, tile);
			}
			else
			{
				Log.Warning("Outpost abandon: " + pawn?.ToString() + " is neither a member nor a guest of the player faction, so it is not banished. It is only passed to the world.");
			}
			if (!pawn.IsWorldPawn())
			{
				Find.WorldPawns.PassToWorld(pawn);
			}
		}

		private static List<Pawn> CollectPawns(Outpost outpost)
		{
			List<Pawn> result = new List<Pawn>(outpost.PawnsListForReading);
			List<Pawn> pending = outpost.pendingAirdropPawns?.InnerListForReading;
			if (pending != null)
			{
				for (int i = 0; i < pending.Count; i++)
				{
					if (pending[i] != null && !result.Contains(pending[i]))
					{
						result.Add(pending[i]);
					}
				}
			}
			List<Thing> stock = outpost.InventoryItems;
			for (int j = 0; j < stock.Count; j++)
			{
				if (stock[j] is Pawn pawn && !result.Contains(pawn))
				{
					result.Add(pawn);
				}
			}
			return result;
		}
	}
}
