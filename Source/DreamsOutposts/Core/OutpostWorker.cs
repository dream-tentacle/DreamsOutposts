using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	public class OutpostWorker
	{
		public OutpostTypeDef def;

		public virtual AcceptanceReport CanCreate(IEnumerable<Pawn> pawns, PlanetTile tile)
		{
			return AcceptanceReport.WasAccepted;
		}

		public virtual void OnCreated(Outpost outpost)
		{
		}

		public virtual void OnRemoved(Outpost outpost)
		{
		}

		public virtual IEnumerable<Gizmo> GetGizmos(Outpost outpost)
		{
			yield break;
		}
	}
}
