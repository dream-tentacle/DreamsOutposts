using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class MapComponent_OutpostBombardment : MapComponent
	{
		public class PendingShell : IExposable
		{
			public int fireTick;

			public IntVec3 centerCell;

			public float missRadius;

			public ThingDef projectileDef;

			public void ExposeData()
			{
				Scribe_Values.Look(ref fireTick, "fireTick", 0);
				Scribe_Values.Look(ref centerCell, "centerCell");
				Scribe_Values.Look(ref missRadius, "missRadius", 0f);
				Scribe_Defs.Look(ref projectileDef, "projectileDef");
			}
		}

		private List<PendingShell> pending;

		public MapComponent_OutpostBombardment(Map map)
			: base(map)
		{
			pending = new List<PendingShell>();
		}

		public void QueueShell(int fireTick, IntVec3 centerCell, float missRadius, ThingDef projectileDef)
		{
			if (pending == null)
			{
				pending = new List<PendingShell>();
			}
			pending.Add(new PendingShell
			{
				fireTick = fireTick,
				centerCell = centerCell,
				missRadius = missRadius,
				projectileDef = projectileDef
			});
		}

		public override void MapComponentTick()
		{
			if (pending == null || pending.Count == 0)
			{
				return;
			}
			int now = Find.TickManager.TicksGame;
			for (int i = pending.Count - 1; i >= 0; i--)
			{
				PendingShell shell = pending[i];
				if (shell == null)
				{
					pending.RemoveAt(i);
				}
				else if (shell.fireTick <= now)
				{
					pending.RemoveAt(i);
					LaunchShell(shell);
				}
			}
		}

		private void LaunchShell(PendingShell shell)
		{
			ThingDef projectileDef = shell.projectileDef;
			if (projectileDef?.projectile == null)
			{
				Log.Error("[DreamsOutposts] A queued outpost bombardment shell on map " + map?.ToString() + " has no usable projectile def; dropping it.");
				return;
			}
			IntVec3 intendedCell = shell.centerCell;
			if (!intendedCell.InBounds(map))
			{
				string[] obj = new string[5]
				{
					"A queued outpost bombardment shell on map ",
					map?.ToString(),
					" targets the out-of-bounds cell ",
					null,
					null
				};
				IntVec3 intVec = intendedCell;
				obj[3] = intVec.ToString();
				obj[4] = "; dropping it.";
				Log.Error(string.Concat(obj));
				return;
			}
			IntVec3 impactCell = ScatterCell(intendedCell, shell.missRadius);
			if (!impactCell.InBounds(map))
			{
				impactCell = intendedCell;
			}
			IntVec3 launchCell = EdgeCellInDirectionOf(impactCell);
			if (!(GenSpawn.Spawn(projectileDef, launchCell, map) is Projectile projectile))
			{
				Log.Error("[DreamsOutposts] Failed to spawn a bombardment shell of def " + projectileDef.defName + " on map " + map?.ToString() + ".");
			}
			else
			{
				projectile.Launch(null, launchCell.ToVector3Shifted(), impactCell, intendedCell, ProjectileHitFlags.None);
			}
		}

		private static IntVec3 ScatterCell(IntVec3 center, float missRadius)
		{
			if (missRadius <= 0.5f)
			{
				return center;
			}
			int maxExclusive = GenRadial.NumCellsInRadius(missRadius);
			if (maxExclusive <= 0)
			{
				return center;
			}
			return center + GenRadial.RadialPattern[Rand.Range(0, maxExclusive)];
		}

		private IntVec3 EdgeCellInDirectionOf(IntVec3 cell)
		{
			Vector3 direction = (cell.ToVector3Shifted() - map.Center.ToVector3Shifted()).Yto0();
			if (direction.sqrMagnitude < 0.0001f)
			{
				return cell;
			}
			direction = direction.normalized;
			IntVec3 farthest = cell;
			for (int i = 1; i <= 1000; i++)
			{
				IntVec3 candidate = (cell.ToVector3Shifted() + direction * i).ToIntVec3();
				if (!candidate.InBounds(map))
				{
					break;
				}
				farthest = candidate;
			}
			return farthest;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref pending, "pendingShells", LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && pending == null)
			{
				pending = new List<PendingShell>();
			}
		}
	}
}
