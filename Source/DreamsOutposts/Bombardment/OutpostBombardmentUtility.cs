using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostBombardmentUtility
	{
		private static readonly List<ThingDefCountClass> NoCost = new List<ThingDefCountClass>();

		public static OutpostFacility GetBombardmentSource(Outpost outpost)
		{
			if (outpost == null)
			{
				return null;
			}
			OutpostFacility source = null;
			foreach (OutpostFacility facility in outpost.OperationalFacilities)
			{
				if (facility?.def?.bombardment != null)
				{
					if (source == null)
					{
						source = facility;
						continue;
					}
					Log.Warning("Outpost " + outpost.Label + " has more than one bombardment facility (" + source.def.defName + " and " + facility.def.defName + "); only the first one is used. Check maxPerOutpost on those defs.");
				}
			}
			return source;
		}

		public static OutpostBombardmentProperties GetBombardmentProperties(Outpost outpost)
		{
			return GetBombardmentSource(outpost)?.def?.bombardment;
		}

		public static int ShellsPerStrike(Outpost outpost)
		{
			OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
			if (props == null)
			{
				return 0;
			}
			int shells = props.shellsPerStrike;
			foreach (OutpostFacility facility2 in outpost.OperationalFacilities)
			{
				int bonus = (facility2?.def?.bombardmentShellBonus).GetValueOrDefault();
				if (bonus > 0)
				{
					shells += bonus;
				}
			}
			return Mathf.Max(shells, 0);
		}

		public static List<ThingDefCountClass> StrikeCost(Outpost outpost, out int shells)
		{
			shells = ShellsPerStrike(outpost);
			OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
			return (props == null) ? NoCost : props.CostForShells(shells);
		}

		private static bool HasQualifiedCrew(Outpost outpost, OutpostBombardmentProperties props)
		{
			if (props == null || !props.HasSkillRequirement)
			{
				return true;
			}
			foreach (Pawn pawn in outpost.Pawns)
			{
				if (props.PawnMeetsSkillRequirement(pawn))
				{
					return true;
				}
			}
			return false;
		}

		private static AcceptanceReport CanBombard(Outpost outpost)
		{
			if (outpost == null || outpost.Destroyed)
			{
				return "DreamsOutposts.OutpostGone".Translate().Resolve();
			}
			OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
			if (props == null)
			{
				return "DreamsOutposts.BombardNoMortar".Translate().Resolve();
			}
			if (props.ProjectileDef == null)
			{
				return "DreamsOutposts.BombardBrokenConfig".Translate().Resolve();
			}
			ResearchProjectDef research = props.researchPrerequisite;
			if (research != null && !research.IsFinished)
			{
				return "MissingRequiredResearch".Translate() + ": " + research.LabelCap;
			}
			if (!HasQualifiedCrew(outpost, props))
			{
				return "DreamsOutposts.BombardNoCrew".Translate(props.requiredSkill.LabelCap, props.requiredSkillLevel).Resolve();
			}
			int now = Find.TickManager.TicksGame;
			if (outpost.nextBombardTick > now)
			{
				return "DreamsOutposts.BombardOnCooldown".Translate((outpost.nextBombardTick - now).ToStringTicksToPeriod()).Resolve();
			}
			int shells = ShellsPerStrike(outpost);
			if (shells <= 0)
			{
				return "DreamsOutposts.BombardBrokenConfig".Translate().Resolve();
			}
			List<ThingDefCountClass> missing = new List<ThingDefCountClass>();
			if (!OutpostBuildUtility.CanAfford(outpost, props.CostForShells(shells), missing))
			{
				return "DreamsOutposts.BombardNotEnoughResources".Translate(OutpostBuildUtility.MissingLabel(missing)).Resolve();
			}
			return true;
		}

		public static Command BombardCommand(Outpost outpost)
		{
			OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
			if (props == null)
			{
				return null;
			}
			int shells = ShellsPerStrike(outpost);
			string crewRequirement = string.Empty;
			if (props.HasSkillRequirement)
			{
				crewRequirement = "\n\n" + "DreamsOutposts.CommandBombardCrew".Translate(props.requiredSkill.LabelCap, props.requiredSkillLevel);
			}
			Command_Action command = new Command_Action
			{
				defaultLabel = "DreamsOutposts.CommandBombard".Translate(),
				defaultDesc = "DreamsOutposts.CommandBombardDesc".Translate(shells, OutpostBuildUtility.CostLabel(props.CostForShells(shells)), props.maxRangeTiles, props.CooldownTicks.ToStringTicksToPeriod()) + crewRequirement,
				icon = props.shellDef?.uiIcon,
				action = delegate
				{
					BeginTargeting(outpost);
				}
			};
			AcceptanceReport report = CanBombard(outpost);
			if (!report.Accepted)
			{
				command.Disable(report.Reason);
			}
			return command;
		}

		public static void BeginTargeting(Outpost outpost)
		{
			AcceptanceReport report = CanBombard(outpost);
			if (!report.Accepted)
			{
				Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
			if (props == null)
			{
				return;
			}
			PlanetTile origin = outpost.Tile;
			CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(origin)));
			Find.WorldSelector.ClearSelection();
			Find.WorldTargeter.BeginTargeting((GlobalTargetInfo target) => ChoseWorldTarget(target, outpost, props), canTargetTiles: true, CompLaunchable.TargeterMouseAttachment, closeWorldTabWhenFinished: true, delegate
			{
				PlanetTile center = origin;
				if (origin.Layer != PlanetLayer.Selected)
				{
					center = PlanetLayer.Selected.GetClosestTile_NewTemp(origin);
				}
				GenDraw.DrawWorldRadiusRing(center, props.maxRangeTiles);
			}, (GlobalTargetInfo target) => TargetingLabelGetter(target, outpost, props), null, origin, showCancelButton: true);
		}

		private static bool ChoseWorldTarget(GlobalTargetInfo target, Outpost outpost, OutpostBombardmentProperties props)
		{
			if (outpost == null || outpost.Destroyed)
			{
				Messages.Message("DreamsOutposts.OutpostGone".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			if (props == null || !target.IsValid)
			{
				Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			int distance = Find.WorldGrid.TraversalDistanceBetween(outpost.Tile, target.Tile, passImpassable: true, int.MaxValue, canTraverseLayers: true);
			if (distance > props.maxRangeTiles)
			{
				Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			MapParent mapParent = Find.WorldObjects.MapParentAt(target.Tile);
			if (mapParent == null || !mapParent.HasMap)
			{
				Messages.Message("DreamsOutposts.BombardNoMapHere".Translate(), MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			AcceptanceReport report = CanBombard(outpost);
			if (!report.Accepted)
			{
				Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
				return false;
			}
			OpenMapTargeting(outpost, mapParent);
			return true;
		}

		private static TaggedString TargetingLabelGetter(GlobalTargetInfo target, Outpost outpost, OutpostBombardmentProperties props)
		{
			if (outpost == null || outpost.Destroyed || props == null || !target.IsValid)
			{
				return null;
			}
			if (target.Tile.Layer != outpost.Tile.Layer && !outpost.Tile.Layer.HasConnectionPathTo(target.Tile.Layer))
			{
				GUI.color = ColorLibrary.RedReadable;
				return "TransportPodDestinationNoPath".Translate(target.Tile.Layer.Def.Named("LAYER"));
			}
			int distance = Find.WorldGrid.TraversalDistanceBetween(outpost.Tile, target.Tile, passImpassable: true, props.maxRangeTiles, canTraverseLayers: true);
			if (distance > props.maxRangeTiles)
			{
				GUI.color = ColorLibrary.RedReadable;
				return "TransportPodDestinationBeyondMaximumRange".Translate();
			}
			MapParent mapParent = Find.WorldObjects.MapParentAt(target.Tile);
			if (mapParent == null || !mapParent.HasMap)
			{
				GUI.color = ColorLibrary.RedReadable;
				return "DreamsOutposts.BombardNoMapHere".Translate();
			}
			return "DreamsOutposts.BombardClickToChoose".Translate(mapParent.LabelCap);
		}

		private static void OpenMapTargeting(Outpost outpost, MapParent mapParent)
		{
			Map map = mapParent.Map;
			if (map != null)
			{
				Current.Game.CurrentMap = map;
				CameraJumper.TryHideWorld();
				TargetingParameters targetParams = TargetingParameters.ForCell();
				targetParams.validator = (TargetInfo t) => t.Map != null && t.Cell.InBounds(t.Map);
				Find.Targeter.BeginTargeting(targetParams, delegate(LocalTargetInfo x)
				{
					Fire(outpost, map, x.Cell);
				}, null, null, CompLaunchable.TargeterMouseAttachment);
			}
		}

		private static void Fire(Outpost outpost, Map map, IntVec3 cell)
		{
			if (outpost == null || outpost.Destroyed)
			{
				Messages.Message("DreamsOutposts.OutpostGone".Translate(), MessageTypeDefOf.RejectInput, historical: false);
			}
			else
			{
				if (map == null || !cell.InBounds(map))
				{
					return;
				}
				AcceptanceReport report = CanBombard(outpost);
				if (!report.Accepted)
				{
					Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
					return;
				}
				OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
				ThingDef projectileDef = props?.ProjectileDef;
				MapComponent_OutpostBombardment component = map.GetComponent<MapComponent_OutpostBombardment>();
				if (projectileDef == null || component == null)
				{
					Log.Error("Failed to bombard from outpost " + outpost.Label + ": " + ((projectileDef == null) ? "its bombardment has no usable projectile def." : "the target map has no MapComponent_OutpostBombardment."));
					return;
				}
				int shells;
				List<ThingDefCountClass> cost = StrikeCost(outpost, out shells);
				OutpostBuildUtility.TryPay(outpost, cost, "a bombardment from outpost " + outpost.Label);
				outpost.nextBombardTick = Find.TickManager.TicksGame + props.CooldownTicks;
				int now = Find.TickManager.TicksGame;
				for (int i = 0; i < shells; i++)
				{
					component.QueueShell(now + i * props.ticksBetweenShells, cell, props.EffectiveMissRadius, projectileDef);
				}
				Messages.Message("DreamsOutposts.BombardLaunched".Translate(outpost.LabelCap, map.Parent?.LabelCap ?? map.ToString(), shells), new GlobalTargetInfo(cell, map), MessageTypeDefOf.TaskCompletion, historical: false);
			}
		}
	}
}
