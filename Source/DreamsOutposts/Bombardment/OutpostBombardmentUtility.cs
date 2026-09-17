using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostBombardmentUtility
	{
		private static readonly Dictionary<ThingCategoryDef, List<ThingDef>> shellCache = new Dictionary<ThingCategoryDef, List<ThingDef>>();

		/// <summary>分类 def 找不到时的兜底清单（Dictionary 不接受 null 键，所以单独放一份）。</summary>
		private static List<ThingDef> uncategorizedShells;

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

		/// <summary>
		/// 可选的迫击炮弹。原版判定「是炮弹」的依据就是 projectileWhenLoaded 非空，
		/// 再要求它属于炮弹分类（默认 MortarShells）并且有制造成本（CostList）；
		/// 没有制造材料的炮弹算不出「制造材料的 0.5 倍」，因此不入列。
		/// 结果按分类缓存，只在第一次使用时扫描一遍 DefDatabase。
		/// </summary>
		public static List<ThingDef> AvailableShells(OutpostBombardmentProperties props)
		{
			if (props == null)
			{
				return new List<ThingDef>();
			}
			ThingCategoryDef category = props.ShellCategory;
			if (category == null)
			{
				if (uncategorizedShells == null)
				{
					uncategorizedShells = BuildShellList(null);
				}
				return uncategorizedShells;
			}
			if (!shellCache.TryGetValue(category, out var shells))
			{
				shells = BuildShellList(category);
				shellCache[category] = shells;
			}
			return shells;
		}

		public static List<ThingDef> AvailableShells(Outpost outpost)
		{
			return AvailableShells(GetBombardmentProperties(outpost));
		}

		private static List<ThingDef> BuildShellList(ThingCategoryDef category)
		{
			if (category == null)
			{
				Log.Warning("Outpost bombardment could not resolve its shell category; every shell that has a manufacturing cost will be selectable. Check shellCategory on the bombardment node.");
			}
			List<ThingDef> result = new List<ThingDef>();
			List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefs.Count; i++)
			{
				ThingDef def = allDefs[i];
				if (def?.projectileWhenLoaded?.projectile == null)
				{
					continue;
				}
				if (category != null && (def.thingCategories == null || !def.thingCategories.Contains(category)))
				{
					continue;
				}
				if (def.CostList.NullOrEmpty())
				{
					continue;
				}
				result.Add(def);
			}
			result.Sort((ThingDef a, ThingDef b) => a.LabelCap.ToString().CompareTo(b.LabelCap.ToString()));
			return result;
		}

		/// <summary>
		/// 当前选中的炮弹：优先用据点自己记住的选择，其次用 XML 配置的默认炮弹（默认高爆弹），最后退回清单第一项。
		/// </summary>
		public static ThingDef SelectedShellDef(Outpost outpost)
		{
			OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
			if (props == null)
			{
				return null;
			}
			List<ThingDef> shells = AvailableShells(props);
			if (shells.Count == 0)
			{
				return null;
			}
			ThingDef selected = outpost?.selectedShellDef;
			if (selected != null && shells.Contains(selected))
			{
				return selected;
			}
			if (props.shellDef != null && shells.Contains(props.shellDef))
			{
				return props.shellDef;
			}
			return shells[0];
		}

		/// <summary>
		/// 整轮齐射的成本 = 对应迫击炮弹制造材料的 0.5 倍。
		/// 先把整轮要用的材料总量算出来再减半取整，所以 4 发高爆弹正好是 15×4×0.5 = 30 钢铁 + 30 化合燃料。
		/// </summary>
		public static List<ThingDefCountClass> ShellStrikeCost(ThingDef shellDef, int shells)
		{
			List<ThingDefCountClass> result = new List<ThingDefCountClass>();
			List<ThingDefCountClass> costList = shellDef?.CostList;
			if (shells <= 0 || costList.NullOrEmpty())
			{
				return result;
			}
			for (int i = 0; i < costList.Count; i++)
			{
				ThingDefCountClass entry = costList[i];
				if (entry?.thingDef == null || entry.count <= 0 || AlreadyListed(result, entry.thingDef))
				{
					continue;
				}
				int perShell = 0;
				for (int j = 0; j < costList.Count; j++)
				{
					ThingDefCountClass other = costList[j];
					if (other?.thingDef == entry.thingDef && other.count > 0)
					{
						perShell += other.count;
					}
				}
				int half = Mathf.FloorToInt((float)perShell * (float)shells * 0.5f + 0.5f);
				if (half < 1)
				{
					half = 1;
				}
				result.Add(new ThingDefCountClass(entry.thingDef, half));
			}
			return result;
		}

		private static bool AlreadyListed(List<ThingDefCountClass> list, ThingDef thingDef)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i]?.thingDef == thingDef)
				{
					return true;
				}
			}
			return false;
		}

		public static List<ThingDefCountClass> StrikeCost(Outpost outpost, out int shells)
		{
			shells = ShellsPerStrike(outpost);
			return ShellStrikeCost(SelectedShellDef(outpost), shells);
		}

		public static List<ThingDefCountClass> StrikeCost(Outpost outpost, ThingDef shellDef, out int shells)
		{
			shells = ShellsPerStrike(outpost);
			return ShellStrikeCost(shellDef, shells);
		}

		/// <summary>这个炮弹有没有解锁。成本取自炮弹自己的制造材料，所以研究门槛也沿用它的制造配方。</summary>
		public static AcceptanceReport ShellAvailability(ThingDef shellDef)
		{
			if (shellDef?.projectileWhenLoaded?.projectile == null)
			{
				return "DreamsOutposts.BombardBrokenConfig".Translate().Resolve();
			}
			RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("Make_" + shellDef.defName);
			if (recipe == null)
			{
				return true;
			}
			if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
			{
				return "DreamsOutposts.BombardShellLocked".Translate(recipe.researchPrerequisite.LabelCap).Resolve();
			}
			if (!recipe.researchPrerequisites.NullOrEmpty())
			{
				for (int i = 0; i < recipe.researchPrerequisites.Count; i++)
				{
					ResearchProjectDef research = recipe.researchPrerequisites[i];
					if (research != null && !research.IsFinished)
					{
						return "DreamsOutposts.BombardShellLocked".Translate(research.LabelCap).Resolve();
					}
				}
			}
			return true;
		}

		public static void SelectShell(Outpost outpost, ThingDef shellDef)
		{
			if (outpost == null || shellDef == null)
			{
				return;
			}
			outpost.selectedShellDef = shellDef;
			Messages.Message("DreamsOutposts.BombardShellSwitched".Translate(outpost.LabelCap, shellDef.LabelCap), outpost, MessageTypeDefOf.SilentInput, historical: false);
		}

		/// <summary>右键「炮击」命令时弹出的弹种菜单。</summary>
		public static IEnumerable<FloatMenuOption> ShellChoiceOptions(Outpost outpost)
		{
			OutpostBombardmentProperties props = GetBombardmentProperties(outpost);
			if (props == null)
			{
				yield return new FloatMenuOption("DreamsOutposts.BombardNoMortar".Translate(), null);
				yield break;
			}
			List<ThingDef> shells = AvailableShells(props);
			if (shells.Count == 0)
			{
				yield return new FloatMenuOption("DreamsOutposts.BombardNoShells".Translate(), null);
				yield break;
			}
			ThingDef current = SelectedShellDef(outpost);
			int shellsPerStrike = ShellsPerStrike(outpost);
			for (int i = 0; i < shells.Count; i++)
			{
				ThingDef captured = shells[i];
				string label = "DreamsOutposts.BombardShellOption".Translate(captured.LabelCap, OutpostBuildUtility.CostLabel(ShellStrikeCost(captured, shellsPerStrike))).ToString();
				if (captured == current)
				{
					label += "DreamsOutposts.BombardShellCurrentSuffix".Translate().ToString();
				}
				FloatMenuOption option = new FloatMenuOption(label, delegate
				{
					SelectShell(outpost, captured);
				}, captured);
				AcceptanceReport report = ShellAvailability(captured);
				if (!report.Accepted)
				{
					option.Disabled = true;
					option.tooltip = new TipSignal("DreamsOutposts.BombardShellLocked".Translate(report.Reason));
				}
				yield return option;
			}
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
			ThingDef shellDef = SelectedShellDef(outpost);
			if (shellDef == null)
			{
				return "DreamsOutposts.BombardNoShells".Translate().Resolve();
			}
			if (shellDef.projectileWhenLoaded == null)
			{
				return "DreamsOutposts.BombardBrokenConfig".Translate().Resolve();
			}
			AcceptanceReport shellReport = ShellAvailability(shellDef);
			if (!shellReport.Accepted)
			{
				return shellReport;
			}
			int shells = ShellsPerStrike(outpost);
			if (shells <= 0)
			{
				return "DreamsOutposts.BombardBrokenConfig".Translate().Resolve();
			}
			List<ThingDefCountClass> missing = new List<ThingDefCountClass>();
			if (!OutpostBuildUtility.CanAfford(outpost, ShellStrikeCost(shellDef, shells), missing))
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
			ThingDef shellDef = SelectedShellDef(outpost);
			string crewRequirement = string.Empty;
			if (props.HasSkillRequirement)
			{
				crewRequirement = "\n\n" + "DreamsOutposts.CommandBombardCrew".Translate(props.requiredSkill.LabelCap, props.requiredSkillLevel);
			}
			string desc = "DreamsOutposts.CommandBombardDesc".Translate(shells, shellDef?.LabelCap ?? "DreamsOutposts.Unknown".Translate(), OutpostBuildUtility.CostLabel(ShellStrikeCost(shellDef, shells)), props.maxRangeTiles, props.CooldownTicks.ToStringTicksToPeriod()) + crewRequirement + "\n\n" + "DreamsOutposts.BombardSwitchHint".Translate();
			AcceptanceReport report = CanBombard(outpost);
			if (!report.Accepted)
			{
				// 不在这里 Disable：灰色禁用的命令会被原版直接吞掉右键，玩家就没法在冷却或资源不足时换弹种了。
				desc = desc + "\n\n" + ("DisabledCommand".Translate() + ": " + report.Reason).Colorize(ColorLibrary.RedReadable);
			}
			return new Command_Bombard
			{
				outpost = outpost,
				defaultLabel = "DreamsOutposts.CommandBombard".Translate(),
				defaultDesc = desc,
				icon = shellDef?.uiIcon,
				action = delegate
				{
					BeginTargeting(outpost);
				}
			};
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
				ThingDef shellDef = SelectedShellDef(outpost);
				ThingDef projectileDef = shellDef?.projectileWhenLoaded;
				MapComponent_OutpostBombardment component = map.GetComponent<MapComponent_OutpostBombardment>();
				if (projectileDef == null || component == null)
				{
					Log.Error("[DreamsOutposts] Failed to bombard from outpost " + outpost.Label + ": " + ((projectileDef == null) ? "its bombardment has no usable projectile def." : "the target map has no MapComponent_OutpostBombardment."));
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
				Messages.Message("DreamsOutposts.BombardLaunched".Translate(outpost.LabelCap, map.Parent?.LabelCap ?? map.ToString(), shells, shellDef.LabelCap), new GlobalTargetInfo(cell, map), MessageTypeDefOf.TaskCompletion, historical: false);
			}
		}
	}
}
