using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public enum UiPipState
	{
		Future,
		Done,
		Current
	}

	public struct UiLevelPip
	{
		public int Level;

		public int SlotCount;

		public float DaysRequired;

		public UiPipState State;

		public string Tooltip;
	}

	public struct UiUpgradeCheck
	{
		public ThingDef Thing;

		public string Name;

		public string ValueText;

		public bool Ok;

		public bool Live;
	}

	public sealed class UiUpgradeView
	{
		public bool IsMaxLevel;

		public string Title;

		public string MaxLevelText;

		public string ButtonLabel;

		public bool CanUpgrade;

		public string Reason;

		public readonly List<UiUpgradeCheck> Checks = new List<UiUpgradeCheck>();
	}

	public sealed class UiProductionView
	{
		public OutpostProductionProperties Props;

		public ThingDef Product;

		public string ProductLabel;

		public string IntervalText;

		/// <summary>人员能力合计：对满足显示条件与技能要求的成员求和 capacityStat（未乘 outputPerCapacity）。</summary>
		public float PersonnelCapacity;

		public float Output;

		public bool HasProgress;

		public float Progress;

		public int TicksRemaining;

		public string AmountText;

		/// <summary>「{能力属性} 合计 {百分比}」；该生产规则没有能力属性时为空，元信息行就不显示这一段。</summary>
		public string CapacityText;

		public string MetaText;

		public bool HasConfiguration;

		public string ConfigurationSummary;

		public bool UsesDynamicProduct;

		public readonly List<UiChipView> ModifierChips = new List<UiChipView>();
	}

	public sealed class UiFacilityView
	{
		public OutpostFacility Facility;

		public OutpostSlot Slot;

		public int SlotIndex = -1;

		public bool IsCore;

		public string Label;

		public string Description;

		public string SubLabel;

		public UiIcon Icon = UiIcon.Crate;

		public float Defense;

		public bool CanRemove;

		public string RemoveReason;

		public string RefundLabel;

		public string CostLabel;

		public int TooltipId;

		public Func<string> TooltipGetter;

		public int RemoveTooltipId;

		public Func<string> RemoveTooltipGetter;

		public readonly List<UiChipView> Chips = new List<UiChipView>();

		public int PowerChipIndex = -1;

		public readonly List<UiProductionView> Productions = new List<UiProductionView>();

		public readonly List<UiFacilitySectionView> Sections = new List<UiFacilitySectionView>();

		public int ChainId;

		public UiFacilityView ChainPrev;

		public UiFacilityView ChainNext;
	}

	public struct UiCostLine
	{
		public ThingDef Thing;

		public int Need;

		public int Have;

		public bool Ok;
	}

	public sealed class UiRuleView
	{
		public ThingDef Product;

		public string ProductLabel;

		public string ProgressText;

		public bool HasProgress;

		public float Progress;

		public readonly List<string> Facts = new List<string>();

		public readonly List<string> FactKinds = new List<string>();

		public readonly List<UiCostLine> Inputs = new List<UiCostLine>();

		public string MaxCraftableText;
	}

	public sealed class UiDetailsView
	{
		public string Title;

		public string Subtitle;

		public string Description;

		public UiFacilityView Source;

		public readonly List<UiRuleView> Rules = new List<UiRuleView>();

		public readonly List<KeyValuePair<string, string>> Bombardment = new List<KeyValuePair<string, string>>();

		public bool CanRemove;

		public string RefundLabel;

		public string DemolishTooltip;
	}

	public sealed class UiPawnView
	{
		public Pawn Pawn;

		public string Name;

		public bool IsColonist;

		public int Shooting = -1;

		public int Melee = -1;

		public int Defense;
	}

	/// <summary>仓库里按 ThingDef 聚合后的一行。</summary>
	public sealed class UiItemStackView
	{
		public ThingDef Def;

		public int Count;

		public int Stacks;
	}

	/// <summary>事件选项视图（弹窗用）。</summary>
	public sealed class UiEventOptionView
	{
		public OutpostEventOption Option;

		public string Id;

		public string Label;

		public string Description;

		public bool PlayerSelectable;

		public bool RequirementsMet;

		public string FailureReason;

		/// <summary>可选 = 玩家可选 且 条件满足。</summary>
		public bool Selectable;

		/// <summary>效果预览文本与颜色类别（add / remove / schedule / skill / neutral）。</summary>
		public readonly List<string> EffectLines = new List<string>();

		public readonly List<string> EffectKinds = new List<string>();
	}

	/// <summary>一个进行中事件的视图。</summary>
	public sealed class UiEventView
	{
		public OutpostEventInstance Instance;

		public string Label;

		public string Description;

		public OutpostEventCategoryDef Category;

		public string CategoryLabel;

		public string DurationText;

		public bool HasProgress;

		public float Progress;

		public int TicksRemaining;

		public string RemainingText;

		public string RemainingChipText;

		public bool HasDefault;

		public string DefaultOptionLabel;

		public readonly List<UiEventOptionView> Options = new List<UiEventOptionView>();
	}

	public sealed class UiInstallCardView
	{
		public OutpostFacilityDef Def;

		public string Label;

		public string Description;

		public UiIcon Icon = UiIcon.Crate;

		public bool Allowed;

		public string Reason;

		public string Tooltip;

		public int TooltipId;

		public readonly List<UiCostLine> Cost = new List<UiCostLine>();

		public readonly List<UiChipView> Chips = new List<UiChipView>();

		public readonly List<string> ModLines = new List<string>();
	}

	/// <summary>
	/// UI 数据缓存：结构相关的东西（文案、chip、tooltip、候选设施）只在据点结构变化时重建，
	/// 随时间变化的东西（进度、剩余时间、预期产量、天数）每 tick 刷新一次。
	/// 页面只读缓存，不直接算数值。
	/// </summary>
	public sealed class OutpostUiCache
	{
		private readonly Outpost outpost;

		private int builtStamp = -1;

		private int builtTick = -1;

		private List<UiInstallCardView> installAll;

		private List<UiInstallCardView> installAvailable;

		private OutpostSlot installSlot;

		private readonly List<ThingDefCountClass> missingBuffer = new List<ThingDefCountClass>();

		public OutpostUiCache(Outpost outpost)
		{
			this.outpost = outpost;
		}

		public Outpost Outpost => outpost;

		public int SlotCount { get; private set; }

		public int UsedSlots { get; private set; }

		public int MaxLevel { get; private set; }

		public bool IsMaxLevel { get; private set; }

		public float DaysSinceEstablished { get; private set; }

		public string DaysText { get; private set; }

		public float Defense { get; private set; }

		public float DefenseFromPawns { get; private set; }

		public float DefenseFromFacilities { get; private set; }

		public readonly List<UiPawnView> DefensePawns = new List<UiPawnView>();

		/// <summary>殖民者（按名字排序），仓库页用。</summary>
		public readonly List<UiPawnView> Colonists = new List<UiPawnView>();

		/// <summary>其他人员（按名字排序），仓库页用。</summary>
		public readonly List<UiPawnView> OtherPawns = new List<UiPawnView>();

		/// <summary>仓库物品（按 ThingDef 聚合、按名字排序）。</summary>
		public readonly List<UiItemStackView> Inventory = new List<UiItemStackView>();

		/// <summary>进行中的事件（顺序同 outpost.events）。</summary>
		public readonly List<UiEventView> Events = new List<UiEventView>();

		/// <summary>已排期的后续事件数量。只向玩家暴露条数，不暴露具体内容。</summary>
		public int ScheduledCount { get; private set; }

		private readonly Dictionary<ThingDef, UiItemStackView> inventoryLookup = new Dictionary<ThingDef, UiItemStackView>();

		public string DefenseText { get; private set; }

		public string TileText { get; private set; }

		public UiFacilityView Core { get; private set; }

		public readonly List<UiFacilityView> Slots = new List<UiFacilityView>();

		public readonly List<UiLevelPip> Pips = new List<UiLevelPip>();

		public readonly List<UiChipView> LevelChips = new List<UiChipView>();

		public readonly UiUpgradeView Upgrade = new UiUpgradeView();

		public void Invalidate()
		{
			builtStamp = -1;
			builtTick = -1;
			installAll = null;
			installAvailable = null;
		}

		public void Refresh()
		{
			if (outpost == null || outpost.Destroyed)
			{
				return;
			}
			int stamp = StructureStamp();
			if (stamp != builtStamp)
			{
				builtStamp = stamp;
				builtTick = -1;
				installAll = null;
				installAvailable = null;
				RebuildStructure();
			}
			int tick = Find.TickManager.TicksGame;
			if (tick != builtTick)
			{
				builtTick = tick;
				RefreshDynamic();
			}
		}

		private int StructureStamp()
		{
			int stamp = 17;
			stamp = stamp * 31 + outpost.level;
			stamp = stamp * 31 + ((outpost.coreFacility?.def != null) ? outpost.coreFacility.def.shortHash : 0);
			List<OutpostSlot> slots = outpost.extensionSlots;
			stamp = stamp * 31 + ((slots != null) ? slots.Count : 0);
			if (slots != null)
			{
				for (int i = 0; i < slots.Count; i++)
				{
					OutpostFacilityDef def = slots[i]?.facility?.def;
					stamp = stamp * 31 + ((def != null) ? def.shortHash : 0);
				}
			}
			stamp = stamp * 31 + ((outpost.pawns != null) ? outpost.pawns.Count : 0);
			int itemCount = 0;
			int stackTotal = 0;
			List<Thing> items = outpost.InventoryItems;
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i] != null && !items[i].Destroyed)
				{
					itemCount++;
					stackTotal += items[i].stackCount;
				}
			}
			stamp = stamp * 31 + itemCount;
			stamp = stamp * 31 + stackTotal;
			stamp = stamp * 31 + EventSignature();
			return stamp;
		}

		// ---------------------------------------------------------------
		// 结构相关
		// ---------------------------------------------------------------

		private void RebuildStructure()
		{
			MaxLevel = outpost.MaxLevel;
			IsMaxLevel = outpost.IsMaxLevel;
			SlotCount = outpost.extensionSlots?.Count ?? 0;
			UsedSlots = 0;
			OutpostTypeDef typeDef = outpost.outpostTypeDef;
			TileText = "DreamsOutposts.Ui.Tile".Translate(outpost.Tile.tileId).ToString();
			Core = BuildFacilityView(outpost.coreFacility, null, -1);
			Slots.Clear();
			for (int i = 0; i < SlotCount; i++)
			{
				OutpostSlot slot = outpost.extensionSlots[i];
				if (slot != null && !slot.IsEmpty)
				{
					UsedSlots++;
					Slots.Add(BuildFacilityView(slot.facility, slot, i));
				}
				else
				{
					Slots.Add(null);
				}
			}
			// 等级 pip
			Pips.Clear();
			for (int level = 1; level <= MaxLevel; level++)
			{
				OutpostLevelProperties properties = typeDef?.GetLevel(level);
				UiLevelPip pip = default(UiLevelPip);
				pip.Level = level;
				pip.SlotCount = properties?.slotCount ?? 0;
				pip.DaysRequired = properties?.daysRequired ?? 0f;
				pip.State = (level < outpost.level) ? UiPipState.Done : ((level == outpost.level) ? UiPipState.Current : UiPipState.Future);
				string suffix = (pip.DaysRequired > 0f)
					? "，" + "DreamsOutposts.Ui.PipDaysRequired".Translate(pip.DaysRequired.ToString("0.#"))
					: "，" + "DreamsOutposts.StartingLevel".Translate();
				pip.Tooltip = "DreamsOutposts.Ui.PipTooltip".Translate(level, pip.SlotCount) + suffix;
				Pips.Add(pip);
			}
			// 升级区
			Upgrade.Checks.Clear();
			Upgrade.IsMaxLevel = IsMaxLevel;
			if (IsMaxLevel)
			{
				Upgrade.Title = null;
				Upgrade.MaxLevelText = "DreamsOutposts.MaxLevelReached".Translate(outpost.SlotCountForLevel).ToString();
				Upgrade.ButtonLabel = null;
				Upgrade.CanUpgrade = false;
				Upgrade.Reason = null;
			}
			else
			{
				OutpostLevelProperties next = outpost.NextLevelProperties;
				Upgrade.ButtonLabel = "DreamsOutposts.UpgradeToLevel".Translate(outpost.level + 1).ToString();
				Upgrade.Title = (next != null)
					? "DreamsOutposts.Ui.UpgradeTitle".Translate(outpost.level + 1, outpost.SlotCountForLevel, next.slotCount).ToString()
					: "DreamsOutposts.NoNextLevel".Translate().ToString();
			}
			RebuildInventory();
			RebuildEvents();
			// 装配链（详情弹窗用 prev/next 跳转）
			List<UiFacilityView> chain = new List<UiFacilityView>();
			if (Core != null)
			{
				chain.Add(Core);
			}
			for (int i = 0; i < Slots.Count; i++)
			{
				if (Slots[i] != null)
				{
					chain.Add(Slots[i]);
				}
			}
			for (int i = 0; i < chain.Count; i++)
			{
				chain[i].ChainId = i;
				chain[i].ChainPrev = (i > 0) ? chain[i - 1] : null;
				chain[i].ChainNext = (i + 1 < chain.Count) ? chain[i + 1] : null;
			}
		}

		private UiFacilityView BuildFacilityView(OutpostFacility facility, OutpostSlot slot, int slotIndex)
		{
			if (facility == null)
			{
				return null;
			}
			OutpostFacilityDef def = facility.def;
			UiFacilityView view = new UiFacilityView();
			view.Facility = facility;
			view.Slot = slot;
			view.SlotIndex = slotIndex;
			view.IsCore = slotIndex < 0;
			view.Label = def?.LabelCap.ToString() ?? "DreamsOutposts.UnknownFacility".Translate().ToString();
			view.Description = def?.description ?? "DreamsOutposts.FacilityNoDef".Translate().ToString();
			view.Icon = UiIconMap.ForFacility(def);
			view.Defense = def?.defense ?? 0f;
			view.RefundLabel = OutpostBuildUtility.RefundLabel(def);
			view.CostLabel = OutpostBuildUtility.CostLabel(def);
			view.SubLabel = view.IsCore
				? "DreamsOutposts.Ui.CoreSubLabel".Translate().ToString()
				: "DreamsOutposts.Ui.SlotLabel".Translate(slotIndex + 1).ToString();
			view.TooltipId = GenText.StableStringHash("facility-tip-" + (def?.defName ?? "null") + "-" + slotIndex);
			view.TooltipGetter = () => FacilityTooltip(view);
			if (!view.IsCore && slot != null)
			{
				AcceptanceReport report = slot.CanRemove(outpost);
				view.CanRemove = report.Accepted;
				view.RemoveReason = report.Reason;
				view.RemoveTooltipId = GenText.StableStringHash("facility-remove-" + slotIndex);
				view.RemoveTooltipGetter = () => "DreamsOutposts.RemoveFacility".Translate(view.Label, OutpostBuildUtility.RefundLabel(def)).ToString();
			}
			// footer chips（结构相关）
			if (def != null)
			{
				if (def.defense > 0f)
				{
					view.Chips.Add(new UiChipView("DreamsOutposts.Ui.Chip.Defense".Translate(def.defense.ToString("0.#")).ToString(), UiChipKind.Info));
				}
				if (def.bombardment != null)
				{
					view.Chips.Add(new UiChipView("DreamsOutposts.Ui.Chip.Bombardable".Translate().ToString(), UiChipKind.Warn));
				}
				if (def.bombardmentShellBonus > 0)
				{
					view.Chips.Add(new UiChipView("DreamsOutposts.Ui.Chip.ShellBonus".Translate(def.bombardmentShellBonus).ToString(), UiChipKind.Warn));
				}
				if (OutpostTrainingUtility.Trains(def))
				{
					OutpostTrainingProperties training = OutpostTrainingUtility.GetTraining(def);
					view.Chips.Add(new UiChipView(
						"DreamsOutposts.Ui.Chip.Training".Translate(training.skill.LabelCap, training.xpPerHour.ToString("0.#")).ToString(),
						UiChipKind.Info,
						"DreamsOutposts.Ui.Chip.TrainingTip".Translate(training.skill.LabelCap, OutpostTrainingUtility.CountTrainees(outpost, def)).ToString()));
				}
				OutpostFacilityComp_PowerGenerator power = facility?.GetComp<OutpostFacilityComp_PowerGenerator>();
				if (power != null)
				{
					string status;
					if (power.linkedReceiver == null) status = "DreamsOutposts.RemotePower.StatusUnbound".Translate();
					else if (!power.IsPoweredNow) status = "DreamsOutposts.RemotePower.StatusWaitingFuel".Translate(power.Props.fuel.LabelCap, power.Props.fuelPerCycle);
					else
					{
						RemotePowerSource source = new RemotePowerSource { Outpost = outpost, Facility = facility, Comp = power };
						float watts = RemotePowerUtility.PowerOutput(source, power.linkedReceiver);
						status = "DreamsOutposts.RemotePower.StatusActive".Translate(watts.ToString("0"), (power.poweredUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
					}
					view.PowerChipIndex = view.Chips.Count;
					view.Chips.Add(new UiChipView(status.ToString(), power.IsPoweredNow ? UiChipKind.Good : UiChipKind.Warn));
				}
				if (!def.productionModifiers.NullOrEmpty())
				{
					for (int i = 0; i < def.productionModifiers.Count; i++)
					{
						OutpostProductionModifier modifier = def.productionModifiers[i];
						if (modifier == null)
						{
							continue;
						}
						view.Chips.Add(new UiChipView(
							"DreamsOutposts.Ui.Chip.ProductionFactor".Translate(modifier.factor.ToString("0.##")).ToString(),
							UiChipKind.Good));
					}
				}
			}
			// 生产
			if (def != null && !def.Productions.NullOrEmpty())
			{
				for (int i = 0; i < def.Productions.Count; i++)
				{
					OutpostProductionProperties production = def.Productions[i];
					if (production == null)
					{
						continue;
					}
					UiProductionView productionView = new UiProductionView();
					productionView.Props = production;
					productionView.HasConfiguration = production.Worker.HasConfiguration(production);
					productionView.UsesDynamicProduct = production.Worker.UsesDynamicProduct;
					BuildModifierChips(productionView, facility, production);
					view.Productions.Add(productionView);
				}
			}
			return view;
		}

		private void BuildModifierChips(UiProductionView productionView, OutpostFacility producingFacility, OutpostProductionProperties production)
		{
			foreach (OutpostProductionModifierSource source in OutpostProductionUtility.MatchingModifiers(outpost, producingFacility, production))
			{
				OutpostProductionModifier modifier = source.Modifier;
				string sourceLabel = source.IsLevelModifier
					? "DreamsOutposts.Ui.LevelSource".Translate(outpost.level).ToString()
					: source.SourceFacility.LabelCap.ToString();
				if (!Mathf.Approximately(modifier.factor, 1f))
				{
					string multiplier = "×" + modifier.factor.ToString("0.##");
					productionView.ModifierChips.Add(new UiChipView(
						"DreamsOutposts.Ui.Chip.ProductionDelta".Translate(multiplier, sourceLabel).ToString(),
						modifier.factor >= 1f ? UiChipKind.Good : UiChipKind.Bad,
						"DreamsOutposts.Ui.Chip.ProductionModifierTip".Translate(sourceLabel).ToString()));
				}
				if (!Mathf.Approximately(modifier.offset, 0f))
				{
					productionView.ModifierChips.Add(new UiChipView(
						"DreamsOutposts.Ui.Chip.ProductionOffset".Translate(modifier.offset.ToString("0.##"), sourceLabel).ToString(),
						UiChipKind.Neutral,
						"DreamsOutposts.Ui.Chip.ProductionModifierTip".Translate(sourceLabel).ToString()));
				}
			}
		}

		private string FacilityTooltip(UiFacilityView view)		{
			if (view == null)
			{
				return string.Empty;
			}
			StringBuilder builder = new StringBuilder();
			builder.Append(view.Label);
			if (!string.IsNullOrEmpty(view.Description))
			{
				builder.Append("\n\n").Append(view.Description);
			}
			if (view.Productions.Count > 0)
			{
				builder.Append("\n\n").Append("DreamsOutposts.Production".Translate());
				for (int i = 0; i < view.Productions.Count; i++)
				{
					UiProductionView production = view.Productions[i];
					builder.Append("\n- ").Append(production.ProductLabel);
					if (production.Output > 0f)
					{
						builder.Append(" ×").Append(production.Output.ToString("0.#"));
					}
					builder.Append(" / ").Append(production.IntervalText);
				}
			}
			return builder.ToString();
		}

		// ---------------------------------------------------------------
		// 每 tick 刷新
		// ---------------------------------------------------------------

		private void RefreshDynamic()
		{
			DaysSinceEstablished = outpost.DaysSinceEstablished;
			DaysText = "DreamsOutposts.Ui.DaysSinceEstablished".Translate(DaysSinceEstablished.ToString("0.#")).ToString();
			Defense = outpost.Defense;
			DefenseText = "DreamsOutposts.Ui.Chip.DefenseValue".Translate(Defense.ToString("0.#")).ToString();
			RefreshDefense();
			RefreshEvents();
			// 等级卡 chips
			LevelChips.Clear();
			LevelChips.Add(new UiChipView("DreamsOutposts.Ui.Chip.CurrentSlots".Translate(outpost.SlotCountForLevel).ToString()));
			LevelChips.Add(new UiChipView("DreamsOutposts.Ui.Chip.CoreFacility".Translate(Core?.Label ?? "DreamsOutposts.None".Translate()).ToString()));
			LevelChips.Add(new UiChipView(DaysText));
			LevelChips.Add(new UiChipView(DefenseText, UiChipKind.Info, "DreamsOutposts.Ui.Chip.DefenseTip".Translate().ToString()));
			// 升级检查行
			RefreshUpgrade();
			// 生产
			RefreshProductions(Core);
			RefreshSections(Core);
			RefreshPowerChip(Core);
			for (int i = 0; i < Slots.Count; i++)
			{
				RefreshProductions(Slots[i]);
				RefreshSections(Slots[i]);
				RefreshPowerChip(Slots[i]);
			}
		}

		private void RefreshSections(UiFacilityView view)
		{
			if (view == null) return;
			view.Sections.Clear();
			for (int i = 0; i < (view.Facility?.comps?.Count ?? 0); i++)
				view.Facility.comps[i]?.BuildUiSections(outpost, view.Sections);
		}

		private void RefreshPowerChip(UiFacilityView view)
		{
			if (view == null || view.PowerChipIndex < 0 || view.PowerChipIndex >= view.Chips.Count) return;
			OutpostFacilityComp_PowerGenerator power = view.Facility?.GetComp<OutpostFacilityComp_PowerGenerator>();
			if (power == null) return;
			string status;
			if (power.linkedReceiver == null) status = "DreamsOutposts.RemotePower.StatusUnbound".Translate();
			else if (!power.IsPoweredNow) status = "DreamsOutposts.RemotePower.StatusWaitingFuel".Translate(power.Props.fuel.LabelCap, power.Props.fuelPerCycle);
			else
			{
				RemotePowerSource source = new RemotePowerSource { Outpost = outpost, Facility = view.Facility, Comp = power };
				status = "DreamsOutposts.RemotePower.StatusActive".Translate(RemotePowerUtility.PowerOutput(source, power.linkedReceiver).ToString("0"), (power.poweredUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod());
			}
			view.Chips[view.PowerChipIndex] = new UiChipView(status.ToString(), power.IsPoweredNow ? UiChipKind.Good : UiChipKind.Warn);
		}

		/// <summary>防卫页数据：人员分解（按防卫值降序）+ 两侧合计；顺带维护仓库页要的两个分组。</summary>
		/// <summary>事件结构签名：谁在场、定义是什么（数量相同但换了一个事件也要重建）。</summary>
		private int EventSignature()
		{
			int signature = 7;
			List<OutpostEventInstance> events = outpost.events;
			signature = signature * 31 + ((events != null) ? events.Count : 0);
			if (events != null)
			{
				for (int i = 0; i < events.Count; i++)
				{
					OutpostEventInstance instance = events[i];
					signature = signature * 31 + ((instance?.def != null) ? instance.def.shortHash : 0);
					signature = signature * 31 + ((instance != null) ? instance.createdTick : 0);
				}
			}
			List<OutpostScheduledEvent> scheduled = outpost.scheduledEvents;
			signature = signature * 31 + ((scheduled != null) ? scheduled.Count : 0);
			if (scheduled != null)
			{
				for (int i = 0; i < scheduled.Count; i++)
				{
					signature = signature * 31 + ((scheduled[i]?.eventDef != null) ? scheduled[i].eventDef.shortHash : 0);
				}
			}
			return signature;
		}

		/// <summary>事件结构（标题、描述、选项、效果预览）只在结构变化时重建。</summary>
		private void RebuildEvents()
		{
			Events.Clear();
			ScheduledCount = 0;
			List<OutpostEventInstance> instances = outpost.events;
			if (instances != null)
			{
				for (int i = 0; i < instances.Count; i++)
				{
					OutpostEventInstance instance = instances[i];
					if (instance?.def == null)
					{
						continue;
					}
					OutpostEventDef def = instance.def;
					UiEventView view = new UiEventView();
					view.Instance = instance;
					view.Label = def.LabelCap.ToString();
					view.Description = def.description;
					view.Category = def.category;
					view.CategoryLabel = (def.category != null) ? def.category.LabelCap.ToString() : null;
					view.DurationText = def.durationTicks.ToStringTicksToPeriod().ToString();
					OutpostEventContext context = new OutpostEventContext { outpost = outpost, instance = instance };
					if (!def.options.NullOrEmpty())
					{
						for (int j = 0; j < def.options.Count; j++)
						{
							OutpostEventOption option = def.options[j];
							if (option == null)
							{
								continue;
							}
							UiEventOptionView optionView = new UiEventOptionView();
							optionView.Option = option;
							optionView.Id = option.id;
							optionView.Label = option.label ?? option.id ?? "DreamsOutposts.UnknownOption".Translate().ToString();
							optionView.Description = option.description;
							optionView.PlayerSelectable = option.playerSelectable;
							if (!option.effects.NullOrEmpty())
							{
								for (int e = 0; e < option.effects.Count; e++)
								{
									OutpostEventEffect effect = option.effects[e];
									if (effect == null)
									{
										continue;
									}
									optionView.EffectLines.Add(effect.GetPreview(context));
									optionView.EffectKinds.Add(EffectKind(effect));
								}
							}
							if (option.id == def.defaultOptionId)
							{
								view.HasDefault = true;
								view.DefaultOptionLabel = optionView.Label;
							}
							view.Options.Add(optionView);
						}
					}
					Events.Add(view);
				}
			}
			List<OutpostScheduledEvent> scheduled = outpost.scheduledEvents;
			if (scheduled != null)
			{
				for (int i = 0; i < scheduled.Count; i++)
				{
					OutpostScheduledEvent entry = scheduled[i];
					if (entry?.eventDef == null)
					{
						continue;
					}
					ScheduledCount++;
				}
			}
		}

		private static string EffectKind(OutpostEventEffect effect)
		{
			if (effect is OutpostEventEffect_AddItem)
			{
				return "add";
			}
			if (effect is OutpostEventEffect_RemoveItem)
			{
				return "remove";
			}
			if (effect is OutpostEventEffect_ScheduleEvent)
			{
				return "schedule";
			}
			if (effect is OutpostEventEffect_ImproveSkill)
			{
				return "skill";
			}
			return "neutral";
		}

		/// <summary>每 tick 刷新：剩余时间、剩余比例、选项条件是否仍然满足。</summary>
		private void RefreshEvents()
		{
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < Events.Count; i++)
			{
				UiEventView view = Events[i];
				if (view?.Instance?.def == null)
				{
					continue;
				}
				int remaining = Mathf.Max(view.Instance.expireTick - now, 0);
				view.TicksRemaining = remaining;
				view.RemainingText = remaining.ToStringTicksToPeriod().ToString();
				view.RemainingChipText = "DreamsOutposts.Ui.EventChipRemaining".Translate(view.RemainingText).ToString();
				int duration = Mathf.Max(view.Instance.def.durationTicks, 0);
				view.HasProgress = duration > 0;
				view.Progress = (duration > 0) ? Mathf.Clamp01((float)remaining / duration) : 0f;
				OutpostEventContext context = new OutpostEventContext { outpost = outpost, instance = view.Instance };
				for (int j = 0; j < view.Options.Count; j++)
				{
					UiEventOptionView option = view.Options[j];
					string reason;
					option.RequirementsMet = OutpostEventUtility.CheckRequirements(option.Option, context, out reason);
					option.FailureReason = reason;
					option.Selectable = option.PlayerSelectable && option.RequirementsMet;
				}
			}
		}

		private void RefreshDefense()
		{
			DefenseFromPawns = OutpostDefenseUtility.PawnDefenseTotal(outpost);
			DefenseFromFacilities = OutpostDefenseUtility.FacilityDefenseTotal(outpost);
			DefensePawns.Clear();
			Colonists.Clear();
			OtherPawns.Clear();
			List<Pawn> pawns = outpost.PawnsListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn == null)
				{
					continue;
				}
				UiPawnView view = new UiPawnView();
				view.Pawn = pawn;
				view.Name = pawn.LabelShortCap.ToString();
				view.IsColonist = pawn.IsColonist;
				view.Shooting = OutpostDefenseUtility.AvailableSkillLevel(pawn, SkillDefOf.Shooting);
				view.Melee = OutpostDefenseUtility.AvailableSkillLevel(pawn, SkillDefOf.Melee);
				view.Defense = OutpostDefenseUtility.PawnDefense(pawn);
				DefensePawns.Add(view);
				(view.IsColonist ? Colonists : OtherPawns).Add(view);
			}
			DefensePawns.Sort(ComparePawnDefense);
			Colonists.Sort(ComparePawnName);
			OtherPawns.Sort(ComparePawnName);
		}

		private static int ComparePawnName(UiPawnView a, UiPawnView b)
		{
			if (a == b)
			{
				return 0;
			}
			if (a == null)
			{
				return 1;
			}
			if (b == null)
			{
				return -1;
			}
			return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>仓库物品按 ThingDef 聚合（件数 + 堆数）。库存变化会触发结构重建。</summary>
		private void RebuildInventory()
		{
			Inventory.Clear();
			inventoryLookup.Clear();
			List<Thing> items = outpost.InventoryItems;
			for (int i = 0; i < items.Count; i++)
			{
				Thing thing = items[i];
				if (thing == null || thing.Destroyed || thing.def == null)
				{
					continue;
				}
				UiItemStackView row;
				if (!inventoryLookup.TryGetValue(thing.def, out row))
				{
					row = new UiItemStackView();
					row.Def = thing.def;
					inventoryLookup[thing.def] = row;
					Inventory.Add(row);
				}
				row.Count += thing.stackCount;
				row.Stacks++;
			}
			Inventory.Sort(CompareItemRows);
		}

		private static int CompareItemRows(UiItemStackView a, UiItemStackView b)
		{
			if (a == b)
			{
				return 0;
			}
			if (a == null)
			{
				return 1;
			}
			if (b == null)
			{
				return -1;
			}
			string left = (a.Def != null) ? a.Def.label : null;
			string right = (b.Def != null) ? b.Def.label : null;
			return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
		}

		private static int ComparePawnDefense(UiPawnView a, UiPawnView b)
		{
			if (a == b)
			{
				return 0;
			}
			if (a == null)
			{
				return 1;
			}
			if (b == null)
			{
				return -1;
			}
			return b.Defense.CompareTo(a.Defense);
		}

		private void RefreshUpgrade()		{
			Upgrade.Checks.Clear();
			if (IsMaxLevel)
			{
				return;
			}
			OutpostLevelProperties next = outpost.NextLevelProperties;
			if (next == null)
			{
				Upgrade.CanUpgrade = false;
				Upgrade.Reason = "DreamsOutposts.NoNextLevel".Translate();
				return;
			}
			UiUpgradeCheck daysCheck = default(UiUpgradeCheck);
			daysCheck.Name = "DreamsOutposts.Ui.Upgrade.Days".Translate();
			daysCheck.Ok = outpost.DaysSinceEstablished >= next.daysRequired;
			daysCheck.ValueText = "DreamsOutposts.Ui.Upgrade.DaysValue".Translate(DaysSinceEstablished.ToString("0.#"), next.daysRequired.ToString("0.#")).ToString();
			daysCheck.Live = true;
			Upgrade.Checks.Add(daysCheck);
			if (!next.cost.NullOrEmpty())
			{
				for (int i = 0; i < next.cost.Count; i++)
				{
					ThingDefCountClass entry = next.cost[i];
					if (entry?.thingDef == null || entry.count <= 0)
					{
						continue;
					}
					UiUpgradeCheck check = default(UiUpgradeCheck);
					check.Thing = entry.thingDef;
					check.Name = entry.thingDef.LabelCap;
					int have = OutpostStockUtility.CountInStock(outpost, entry.thingDef);
					check.Ok = have >= entry.count;
					check.ValueText = "DreamsOutposts.Ui.Upgrade.MaterialValue".Translate(have, entry.count).ToString();
					Upgrade.Checks.Add(check);
				}
			}
			string reason;
			Upgrade.CanUpgrade = OutpostUpgradeUtility.CanUpgrade(outpost, out reason);
			if (Upgrade.CanUpgrade)
			{
				Upgrade.Reason = null;
				return;
			}
			// 原因只出现在升级按钮的 tooltip 里，这里把它说成人话
			if (outpost.DaysSinceEstablished < next.daysRequired)
			{
				Upgrade.Reason = "DreamsOutposts.NeedsMoreDays".Translate((next.daysRequired - outpost.DaysSinceEstablished).ToString("0.#")).ToString();
			}
			else
			{
				missingBuffer.Clear();
				OutpostBuildUtility.CanAfford(outpost, next.cost, missingBuffer);
				Upgrade.Reason = (missingBuffer.Count > 0)
					? "DreamsOutposts.Ui.Upgrade.Missing".Translate(OutpostBuildUtility.CostLabel(missingBuffer)).ToString()
					: reason;
			}
		}

		private void RefreshProductions(UiFacilityView view)
		{
			if (view == null)
			{
				return;
			}
			for (int i = 0; i < view.Productions.Count; i++)
			{
				UiProductionView production = view.Productions[i];
				if (production.Props == null)
				{
					continue;
				}
				OutpostProductionUtility.TryGetProductionProduct(view.Facility, production.Props, out ThingDef product);
				production.Product = product;
				production.ProductLabel = (product != null)
					? product.LabelCap.ToString()
					: (!string.IsNullOrEmpty(production.Props.outputLabelKey)
						? production.Props.outputLabelKey.Translate().ToString()
						: (production.UsesDynamicProduct ? "DreamsOutposts.NoCrop".Translate().ToString() : production.Props.id));
				production.IntervalText = production.Props.intervalTicks.ToStringTicksToPeriod().ToString();
				float capacity = 0f;
				// 没写 capacityStat 的设施走固定产能：不再读 pawn 属性，所以也不调用产能计算
				bool hasCapacityStat = production.Props.capacityStat != null;
				bool hasCapacity = hasCapacityStat && OutpostProductionUtility.TryCalculatePersonnelCapacity(outpost, production.Props, out capacity);
				production.PersonnelCapacity = capacity;
				// 数值用 StatDef 自己的格式（PercentZero → "120%"），和原版人物面板显示一致
				production.CapacityText = hasCapacity
					? "DreamsOutposts.Ui.Rule.Capacity".Translate(production.Props.capacityStat.LabelCap,
						production.Props.capacityStat.ValueToString(production.PersonnelCapacity)).ToString()
					: (hasCapacityStat ? null : "DreamsOutposts.Ui.Rule.FixedCapacity".Translate().ToString());
				float expected;
				if (OutpostProductionUtility.TryCalculateExpectedOutput(outpost, view.Facility, production.Props, out expected))
				{
					production.Output = expected;
				}
				else
				{
					production.Output = 0f;
				}
				float progress;
				int ticksRemaining;
				production.HasProgress = OutpostProductionUtility.TryGetCycleProgress(view.Facility, production.Props, out progress, out ticksRemaining);
				production.Progress = production.HasProgress ? Mathf.Clamp01(progress) : 0f;
				production.TicksRemaining = production.HasProgress ? ticksRemaining : 0;
				string amount = (production.Output > 0f)
					? "DreamsOutposts.Ui.ProductionAmount".Translate(production.ProductLabel, production.Output.ToString("0.#")).ToString()
					: production.ProductLabel;
				production.AmountText = amount;
				production.MetaText = production.HasProgress
					? "DreamsOutposts.ProductionRemaining".Translate(amount, production.TicksRemaining.ToStringTicksToPeriod()).ToString()
					: amount;
				if (!string.IsNullOrEmpty(production.CapacityText))
				{
					// 能力值放在最前面：窗口窄被省略号截断时，先丢的是尾巴而不是它
					production.MetaText = "DreamsOutposts.Ui.ProductionMetaWithCapacity"
						.Translate(production.CapacityText, production.MetaText).ToString();
				}
				if (production.HasConfiguration)
				{
					production.ConfigurationSummary = production.Props.Worker.ConfigurationSummary(production.Props, view.Facility.GetProductionState(production.Props.id));
				}
			}
		}

		// ---------------------------------------------------------------
		// 安装候选（打开安装弹窗时才算）
		// ---------------------------------------------------------------

		/// <summary>
		/// 安装候选。一次同时建好「全部」与「只看可建造」两张表，
		/// 这样切换筛选不会重建，也能让弹窗按「全部候选」定死高度。
		/// </summary>
		public List<UiInstallCardView> InstallCandidates(OutpostSlot slot, bool onlyAvailable)
		{
			if (installAll != null && installSlot == slot)
			{
				return onlyAvailable ? installAvailable : installAll;
			}
			installSlot = slot;
			List<UiInstallCardView> all = new List<UiInstallCardView>();
			List<UiInstallCardView> available = new List<UiInstallCardView>();
			List<OutpostFacilityDef> candidates = OutpostUtility.InstallableFacilities(outpost?.outpostTypeDef);
			if (candidates != null)
			{
				for (int i = 0; i < candidates.Count; i++)
				{
					OutpostFacilityDef def = candidates[i];
					if (def == null)
					{
						continue;
					}
					UiInstallCardView card = new UiInstallCardView();
					card.Def = def;
					card.Label = def.LabelCap;
					card.Description = def.description;
					card.Icon = UiIconMap.ForFacility(def);
					AcceptanceReport report = (slot != null) ? slot.CanInstall(def, outpost) : new AcceptanceReport("no slot");
					card.Allowed = report.Accepted;
					card.Reason = report.Reason;
					card.TooltipId = GenText.StableStringHash("install-tip-" + def.defName);
					card.Tooltip = BuildInstallTooltip(def);
					// 造价
					List<ThingDefCountClass> cost = def.BuildCost;
					if (!cost.NullOrEmpty())
					{
						for (int c = 0; c < cost.Count; c++)
						{
							ThingDefCountClass entry = cost[c];
							if (entry?.thingDef == null || entry.count <= 0)
							{
								continue;
							}
							UiCostLine line = default(UiCostLine);
							line.Thing = entry.thingDef;
							line.Need = entry.count;
							line.Have = OutpostStockUtility.CountInStock(outpost, entry.thingDef);
							line.Ok = line.Have >= line.Need;
							card.Cost.Add(line);
						}
					}
					// chips
					if (def.maxPerOutpost > 0)
					{
						int installed = OutpostUtility.CountInstalled(outpost, def);
						card.Chips.Add(new UiChipView(
							"DreamsOutposts.Ui.Chip.Limit".Translate(def.maxPerOutpost, installed).ToString(),
							(installed >= def.maxPerOutpost) ? UiChipKind.Bad : UiChipKind.Neutral));
					}
					if (OutpostTrainingUtility.Trains(def))
					{
						OutpostTrainingProperties training = OutpostTrainingUtility.GetTraining(def);
						SkillDef trainingSkill = training.skill;
						int trainees = OutpostTrainingUtility.CountTrainees(outpost, def);
						card.Chips.Add(new UiChipView(
							"DreamsOutposts.Ui.Chip.Training".Translate(trainingSkill.LabelCap, training.xpPerHour.ToString("0.#")).ToString(),
							(trainees > 0) ? UiChipKind.Good : UiChipKind.Bad));
					}
					OutpostFacilityCompProperties_PowerGenerator powerProps = def.GetCompProperties<OutpostFacilityCompProperties_PowerGenerator>();
					if (powerProps != null)
						card.Chips.Add(new UiChipView("DreamsOutposts.RemotePower.FuelCycle".Translate(powerProps.fuel.LabelCap, powerProps.fuelPerCycle, powerProps.cycleTicks.ToStringTicksToPeriod()).ToString(), UiChipKind.Info));
					if (!def.researchPrerequisites.NullOrEmpty())
					{
						card.Chips.Add(def.IsResearchUnlocked
							? new UiChipView("DreamsOutposts.Ui.Chip.ResearchDone".Translate().ToString(), UiChipKind.Good)
							: new UiChipView("DreamsOutposts.Ui.Chip.ResearchMissing".Translate(def.FirstMissingResearch?.LabelCap ?? "DreamsOutposts.Unknown".Translate()).ToString(), UiChipKind.Bad));
					}
					if (def.minOutpostLevel > 1)
					{
						int outpostLevel = outpost?.level ?? 1;
						card.Chips.Add(def.IsLevelRequirementMetBy(outpostLevel)
							? new UiChipView("DreamsOutposts.Ui.Chip.LevelOk".Translate(def.minOutpostLevel).ToString(), UiChipKind.Good)
							: new UiChipView("DreamsOutposts.Ui.Chip.LevelMissing".Translate(outpostLevel, def.minOutpostLevel).ToString(), UiChipKind.Bad));
					}
					if (def.defense > 0f)
					{
						card.Chips.Add(new UiChipView("DreamsOutposts.Ui.Chip.Defense".Translate(def.defense.ToString("0.#")).ToString(), UiChipKind.Info));
					}
					if (def.bombardment != null)
					{
						card.Chips.Add(new UiChipView("DreamsOutposts.Ui.Chip.Bombardable".Translate().ToString(), UiChipKind.Warn));
					}
					if (def.bombardmentShellBonus > 0)
					{
						card.Chips.Add(new UiChipView("DreamsOutposts.Ui.Chip.ShellBonus".Translate(def.bombardmentShellBonus).ToString(), UiChipKind.Warn));
					}
					// mods 行
					if (!def.productionModifiers.NullOrEmpty())
					{
						for (int m = 0; m < def.productionModifiers.Count; m++)
						{
							OutpostProductionModifier modifier = def.productionModifiers[m];
							if (modifier == null)
							{
								continue;
							}
							card.ModLines.Add("DreamsOutposts.Ui.ModProductionFactor".Translate("×" + modifier.factor.ToString("0.##")).ToString());
						}
					}
					if (!def.Productions.NullOrEmpty())
					{
						for (int p = 0; p < def.Productions.Count; p++)
						{
							OutpostProductionProperties production = def.Productions[p];
							if (production == null)
							{
								continue;
							}
							string productLabel = (production.product != null)
								? production.product.LabelCap.ToString()
								: (!string.IsNullOrEmpty(production.outputLabelKey)
									? production.outputLabelKey.Translate().ToString()
									: (production.Worker.UsesDynamicProduct ? "DreamsOutposts.ProductChosenAfterInstallation".Translate().ToString() : production.id));
							string line = "DreamsOutposts.Ui.ModProduction".Translate(productLabel, production.intervalTicks.ToStringTicksToPeriod()).ToString();
							if (production.HasInputs)
							{
								line += " · " + "DreamsOutposts.Ui.ModProductionInputs".Translate(OutpostBuildUtility.CostLabel(production.inputs)).ToString();
							}
							card.ModLines.Add(line);
						}
					}
					all.Add(card);
					if (card.Allowed)
					{
						available.Add(card);
					}
				}
			}
			all.Sort(CompareInstallCards);
			available.Sort(CompareInstallCards);
			installAll = all;
			installAvailable = available;
			return onlyAvailable ? available : all;
		}

		private static int CompareInstallCards(UiInstallCardView a, UiInstallCardView b)
		{
			if (a == b)
			{
				return 0;
			}
			if (a == null)
			{
				return 1;
			}
			if (b == null)
			{
				return -1;
			}
			return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
		}

		private string BuildInstallTooltip(OutpostFacilityDef def)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(def.LabelCap);
			if (!string.IsNullOrEmpty(def.description))
			{
				builder.Append("\n\n").Append(def.description);
			}
			builder.Append("\n\n").Append("DreamsOutposts.BuildCost".Translate(OutpostBuildUtility.CostLabel(def)));
			if (def.maxPerOutpost > 0)
			{
				builder.Append("\n").Append("DreamsOutposts.LimitPerOutpost".Translate(def.maxPerOutpost));
			}
			if (!def.IsResearchUnlocked)
			{
				builder.Append("\n").Append("DreamsOutposts.RequiresResearch".Translate(def.FirstMissingResearch?.LabelCap ?? "DreamsOutposts.Unknown".Translate()));
			}
			if (def.minOutpostLevel > 1)
			{
				builder.Append("\n").Append("DreamsOutposts.RequiresOutpostLevel".Translate(def.minOutpostLevel));
			}
			if (OutpostTrainingUtility.Trains(def))
			{
				OutpostTrainingProperties training = OutpostTrainingUtility.GetTraining(def);
				builder.Append("\n").Append("DreamsOutposts.TrainingTooltip".Translate(
					training.skill.LabelCap,
					training.xpPerHour.ToString("0.#"),
					OutpostTrainingUtility.CountTrainees(outpost, def)));
			}
			return builder.ToString();
		}

		// ---------------------------------------------------------------
		// 详情弹窗数据
		// ---------------------------------------------------------------

		public UiDetailsView BuildDetails(UiFacilityView view)
		{
			if (view == null)
			{
				return null;
			}
			OutpostFacilityDef def = view.Facility?.def;
			UiDetailsView details = new UiDetailsView();
			details.Title = view.Label;
			details.Subtitle = view.SubLabel;
			details.Description = view.Description;
			details.Source = view;
			details.CanRemove = view.CanRemove;
			details.RefundLabel = view.RefundLabel;
			details.DemolishTooltip = view.RemoveTooltipGetter?.Invoke();
			// 生产规则
			for (int i = 0; i < view.Productions.Count; i++)
			{
				UiProductionView production = view.Productions[i];
				OutpostProductionProperties props = production.Props;
				UiRuleView rule = new UiRuleView();
				rule.Product = production.Product;
				rule.ProductLabel = production.ProductLabel;
				rule.HasProgress = production.HasProgress;
				rule.Progress = production.Progress;
				if (production.HasProgress)
				{
					rule.ProgressText = "DreamsOutposts.Ui.Rule.Progress".Translate(
						Mathf.RoundToInt(production.Progress * 100f), production.TicksRemaining.ToStringTicksToPeriod()).ToString();
				}
				rule.Facts.Add("DreamsOutposts.Ui.Rule.Expected".Translate(production.Output.ToString("0.#"), production.IntervalText).ToString());
				rule.FactKinds.Add("good");
				if (props.capacityStat != null)
				{
					float capacity;
					if (OutpostProductionUtility.TryCalculatePersonnelCapacity(outpost, props, out capacity))
					{
						rule.Facts.Add("DreamsOutposts.Ui.Rule.Capacity".Translate(props.capacityStat.LabelCap,
							props.capacityStat.ValueToString(capacity)).ToString());
						rule.FactKinds.Add("neutral");
					}
				}
				else
				{
					// 没有产能属性：产能固定为 1，产量就是 outputPerCapacity
					rule.Facts.Add("DreamsOutposts.Ui.Rule.FixedCapacity".Translate().ToString());
					rule.FactKinds.Add("neutral");
				}
				if (props.HasSkillRequirement)
				{
					rule.Facts.Add("DreamsOutposts.Ui.Rule.SkillFilter".Translate(props.requiredSkill.LabelCap, props.requiredSkillLevel).ToString());
					rule.FactKinds.Add("warn");
				}
				if (!props.tags.NullOrEmpty())
				{
					rule.Facts.Add("DreamsOutposts.Ui.Rule.Tags".Translate(string.Join("/", props.tags.ToArray())).ToString());
					rule.FactKinds.Add("neutral");
				}
				if (production.HasConfiguration && !string.IsNullOrEmpty(production.ConfigurationSummary))
				{
					rule.Facts.Add(production.ConfigurationSummary);
					rule.FactKinds.Add("neutral");
				}
				if (props.HasInputs)
				{
					for (int c = 0; c < props.inputs.Count; c++)
					{
						ThingDefCountClass entry = props.inputs[c];
						if (entry?.thingDef == null || entry.count <= 0)
						{
							continue;
						}
						UiCostLine line = default(UiCostLine);
						line.Thing = entry.thingDef;
						line.Need = entry.count;
						line.Have = OutpostStockUtility.CountInStock(outpost, entry.thingDef);
						line.Ok = line.Have >= line.Need;
						rule.Inputs.Add(line);
					}
					int maxUnits = OutpostStockUtility.MaxCraftableUnits(outpost, props.inputs);
					rule.MaxCraftableText = (maxUnits == int.MaxValue)
						? "DreamsOutposts.Ui.Rule.MaxCraftableInfinite".Translate().ToString()
						: "DreamsOutposts.Ui.Rule.MaxCraftable".Translate(maxUnits).ToString();
				}
				details.Rules.Add(rule);
			}
			// 炮击
			if (def != null && def.bombardment != null)
			{
				OutpostBombardmentProperties bombardment = def.bombardment;
				int shells = OutpostBombardmentUtility.ShellsPerStrike(outpost);
				string shellLabel = (bombardment.shellDef != null) ? bombardment.shellDef.LabelCap : "DreamsOutposts.Unknown".Translate();
				details.Bombardment.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.ShellsPerStrike".Translate().ToString(),
					"DreamsOutposts.Ui.Kv.ShellsPerStrikeValue".Translate(shells, shellLabel).ToString()));
				details.Bombardment.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.Range".Translate().ToString(), bombardment.maxRangeTiles.ToString()));
				details.Bombardment.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.Cooldown".Translate().ToString(), bombardment.CooldownTicks.ToStringTicksToPeriod().ToString()));
				if (bombardment.HasSkillRequirement)
				{
					details.Bombardment.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.SkillReq".Translate().ToString(),
						"DreamsOutposts.Ui.Rule.SkillFilter".Translate(bombardment.requiredSkill.LabelCap, bombardment.requiredSkillLevel).ToString()));
				}
				details.Bombardment.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.PerStrikeCost".Translate().ToString(),
					OutpostBuildUtility.CostLabel(bombardment.CostForShells(shells))));
			}
			return details;
		}
	}
}
