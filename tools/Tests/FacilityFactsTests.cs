// Runs the real fact model and selected cache methods against controlled game inputs.
// Game rendering, translation lookup and engine calculations still require an in-game check.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DreamsOutposts;
using Verse;

internal static class FacilityFactsTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Call(OutpostUiCache cache, string method, params object[] args)
    {
        return typeof(OutpostUiCache).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Invoke(cache, args);
    }
    private static UiFacilityInfoItem Fact(UiFacilityView view, string id) { return view.Facts.Single(f => f.Id == id); }
    private static void AssertProjection(UiFacilityView view)
    {
        var facts = view.Info.CompactFacts.ToList();
        Check(view.Facts.All(f => !string.IsNullOrEmpty(f.Id)), "Every facility fact needs an ID.");
        Check(view.Facts.Select(f => f.Id).Distinct().Count() == view.Facts.Count, "Duplicate fact IDs.");
        Check(facts.Count == view.DisplayChips.Count, "Card and detail disagree about compact fact count.");
        for (int i = 0; i < facts.Count; i++)
            Check(view.DisplayChips[i].Label == facts[i].DisplayText && view.DisplayChips[i].Tooltip == facts[i].Tooltip && view.DisplayChips[i].Kind == facts[i].Tone,
                "Chip projection lost text, explanation or tone.");
    }
    public static void Main()
    {
        var model = new UiFacilityInfoModel();
        model.AddCompactFact(UiFacilityFactIds.PowerStatus, "old power");
        model.Facts.Insert(0, new UiFacilityInfoItem { Id = "unrelated", Value = "keep" });
        model.AddCompactFact(UiFacilityFactIds.PowerStatus, "new power", UiChipKind.Warn, "reason");
        Check(model.Facts.Count == 2 && model.Facts[0].Value == "keep" && model.Facts.Single(f => f.Id == UiFacilityFactIds.PowerStatus).Value == "new power", "ID update depended on position.");
        Console.WriteLine("PASS fact replacement is independent of display position");

        var outpost = new Outpost();
        var cache = new OutpostUiCache(outpost);
        var skill = new SkillDef { defName = "Plants", LabelCap = "Plants" };
        var requirementA = new OutpostFacilityRequirement { Label = "A" };
        var requirementB = new OutpostFacilityRequirement { Label = "B" };
        var def = new OutpostFacilityDef {
            defense = 10, bombardment = new object(), bombardmentShellBonus = 2,
            Training = new OutpostTrainingProperties { skill = skill },
            operatingRequirements = new List<OutpostFacilityRequirement> { requirementA, requirementB }
        };
        var power = new OutpostFacilityComp_PowerGenerator();
        var facility = new OutpostFacility { def = def, Power = power };
        var view = new UiFacilityView { Facility = facility, Defense = 17 };
        view.CompInfo.AddCompactFact("capacity.research.all", "Research total 19%", UiChipKind.Info, "research detail");
        Call(cache, "RefreshFacilityFacts", view);
        AssertProjection(view);
        Check(Fact(view, UiFacilityFactIds.Defense).DisplayText.Contains("17"), "Defense used static Def value instead of current defense.");
        Check(Fact(view, UiFacilityFactIds.PowerStatus).DisplayText.Contains("StatusUnbound"), "Unbound power not represented.");
        var expectedOrder = new[] { "defense.total", "bombardment.available", "bombardment.shellBonus", "training.Plants", "power.status", "capacity.research.all", "operation.status" };
        Check(view.Facts.Take(7).Select(f => f.Id).SequenceEqual(expectedOrder), "Existing card fact order changed.");
        var requirementIds = view.Facts.Where(f => f.Id.StartsWith("operation.requirement.")).Select(f => f.Id).ToArray();
        power.linkedReceiver = new object();
        Call(cache, "RefreshFacilityFacts", view);
        Check(Fact(view, UiFacilityFactIds.PowerStatus).DisplayText.Contains("StatusWaitingFuel"), "Fuel wait state not refreshed.");
        power.IsPoweredNow = true;
        Call(cache, "RefreshFacilityFacts", view);
        Check(Fact(view, UiFacilityFactIds.PowerStatus).Value == "950 W", "Active power not refreshed.");
        facility.Enabled = false;
        requirementA.Accepted = false;
        def.operatingRequirements.Reverse();
        Call(cache, "RefreshFacilityFacts", view);
        AssertProjection(view);
        Check(Fact(view, UiFacilityFactIds.OperationStatus).Tone == UiChipKind.Bad, "Operation state did not change.");
        Check(view.Facts.Where(f => f.Id.StartsWith("operation.requirement.")).Select(f => f.Id).SequenceEqual(requirementIds.Reverse()), "Requirement IDs followed list positions.");
        Console.WriteLine("PASS current defense, power transitions, card order and stable requirement IDs");

        def.defense = 0; def.bombardment = null; def.bombardmentShellBonus = 0; def.Training = null;
        facility.Power = null; def.operatingRequirements.Clear(); view.CompInfo.Clear();
        Call(cache, "RefreshFacilityFacts", view);
        AssertProjection(view);
        Check(view.Facts.Count == 1 && view.Facts[0].Id == UiFacilityFactIds.OperationStatus, "Removed state left stale facts/chips.");
        Console.WriteLine("PASS snapshot refresh removes stale defense, power, training, capacity and requirements");

        var processA = new OutpostProductionProperties();
        var processB = new OutpostProductionProperties();
        def.Processes.Add(processA); def.Processes.Add(processB);
        var modifier = new OutpostProductionModifier { factor = 2, offset = 3 };
        var boostDef = new OutpostFacilityDef { LabelCap = "Same booster" };
        var sourceA = new OutpostFacility { def = boostDef };
        var sourceB = new OutpostFacility { def = boostDef };
        outpost.Sources.Add(new OutpostProcessModifierSource { SourceInstance = sourceA, SourceFacility = boostDef, Modifier = modifier });
        outpost.Sources.Add(new OutpostProcessModifierSource { SourceInstance = sourceB, SourceFacility = boostDef, Modifier = modifier });
        Call(cache, "AddFacilityProductionModifierFacts", view);
        Check(view.CompInfo.Facts.Count == 4, "Two identical installed sources must retain separate factor/offset facts, without duplicating across processes.");
        var idsBefore = view.CompInfo.Facts.Select(f => f.Id).OrderBy(id => id).ToArray();
        view.CompInfo.Clear(); outpost.Sources.Reverse();
        Call(cache, "AddFacilityProductionModifierFacts", view);
        Check(idsBefore.SequenceEqual(view.CompInfo.Facts.Select(f => f.Id).OrderBy(id => id)), "Source IDs changed with enumeration order.");
        var productionView = new UiProductionView { Props = processA };
        view.Productions.Add(productionView);
        Call(cache, "RefreshFacilityFacts", view);
        Check(productionView.ModifierChips.Count == 4 && idsBefore.SequenceEqual(productionView.ModifierInfo.Facts.Select(f => f.Id).OrderBy(id => id)), "Production-row and facility bonus identities differ.");
        outpost.Sources.RemoveAt(0); view.CompInfo.Clear();
        Call(cache, "AddFacilityProductionModifierFacts", view);
        Call(cache, "RefreshFacilityFacts", view);
        Check(view.CompInfo.Facts.Count == 2 && productionView.ModifierChips.Count == 2, "Removed source left duplicate bonuses.");
        Console.WriteLine("PASS duplicate installations, shared modifiers, process deduplication and source removal");

        outpost.Gate = new OutpostFacilityComp_ProductionSupervisor { AllowsLevelFactor = false };
        outpost.CurrentLevelProperties.productionModifiers.Add(new OutpostProductionModifier { factor = 3 });
        Call(cache, "RefreshFacilityFacts", view);
        var levelFact = view.Facts.Single(f => f.Id.StartsWith("production.level."));
        Check(levelFact.Tone == UiChipKind.Warn && levelFact.Tooltip.Contains("inactive"), "Suppressed multiplier explanation missing.");
        outpost.Gate.AllowsLevelFactor = true;
        Call(cache, "RefreshFacilityFacts", view);
        Check(view.Facts.Single(f => f.Id == levelFact.Id).Tone == UiChipKind.Good, "Level multiplier identity/state not stable.");
        outpost.CurrentLevelProperties.productionModifiers.Clear();
        Call(cache, "RefreshFacilityFacts", view);
        Check(!view.Facts.Any(f => f.Id == levelFact.Id), "Removed level modifier remains.");
        Console.WriteLine("PASS level multiplier suppression, reactivation and removal");

        def.maxPerOutpost = 2; def.minOutpostLevel = 2; def.researchPrerequisites.Add(new object());
        def.IsResearchUnlocked = false; outpost.level = 1;
        def.operatingRequirements.Add(requirementA);
        var card = new UiInstallCardView { Def = def };
        Call(cache, "RefreshInstallCardFacts", card);
        var installRequirementId = card.Info.Facts.Single(f => f.Id.StartsWith("operation.requirement.")).Id;
        Check(card.Info.Facts.Single(f => f.Id == "install.research").Tone == UiChipKind.Bad, "Initial research requirement incorrect.");
        outpost.Installed = 2; outpost.level = 3; def.IsResearchUnlocked = true; requirementA.Accepted = true;
        Call(cache, "RefreshInstallCardFacts", card);
        Check(card.Info.Facts.Single(f => f.Id == "install.research").Tone == UiChipKind.Good && card.Info.Facts.Single(f => f.Id == "install.level").Tone == UiChipKind.Good, "Install facts did not refresh requirements.");
        Check(card.Info.Facts.Single(f => f.Id == "install.limit").Tone == UiChipKind.Bad && card.Info.Facts.Single(f => f.Id == installRequirementId).Tone == UiChipKind.Good, "Install fact identity or count refresh incorrect.");
        def.maxPerOutpost = 0; def.minOutpostLevel = 1; def.researchPrerequisites.Clear(); def.operatingRequirements.Clear();
        Call(cache, "RefreshInstallCardFacts", card);
        Check(card.Info.Facts.Count == 0 && card.Chips.Count == 0, "Installation card kept stale facts.");
        Console.WriteLine("PASS installation candidates refresh by facts and remove missing conditions");

        var existing = new List<UiChipView> { new UiChipView("preserve") };
        OutpostUiCache.AddOperationChips(existing, outpost, def, facility);
        Check(existing.Count == 2 && existing[0].Label == "preserve", "Bandwidth window append helper changed behavior.");
        model.Clear();
        model.AddFact(new UiFacilityInfoItem { Id = "secondary", Value = "detail only", Importance = UiFacilityInfoImportance.Detail });
        model.AddCompactFact("blank", "");
        Check(!model.CompactFacts.Any() && model.Facts.Count == 2, "Card policy changed or detail facts lost.");
        Console.WriteLine("PASS secondary detail retention and external chip append helper");

        // Modern policy preserves all core facts, including more than four, and ignores input order.
        model.Clear();
        model.AddCompactFact("research.speed", "Research 19%", UiChipKind.Info, "calculation", UiFacilityCardPriority.Core);
        model.AddCompactFact("training.Plants", "Training 100/h", cardPriority: UiFacilityCardPriority.Core);
        model.AddCompactFact("power.status", "Waiting fuel", UiChipKind.Warn, "fuel reason", UiFacilityCardPriority.Alert);
        model.AddCompactFact("automation.factor", "Automation x3");
        model.AddCompactFact("defense.total", "Defense 50");
        model.AddCompactFact("operation.status", "Disabled", UiChipKind.Bad, "reason", UiFacilityCardPriority.Alert);
        model.AddCompactFact("empty", "");
        model.AddFact(new UiFacilityInfoItem { Id = "detail", Value = "explanation", CardPlacement = UiFacilityCardPlacement.Detail });
        var orderedIds = model.CardFacts.Select(item => item.Id).ToArray();
        Check(orderedIds.Length == 6 && orderedIds.Take(2).SequenceEqual(new[] { "operation.status", "power.status" }), "Alerts or core facts were hidden by a count cap or wrong priority.");
        Check(model.CardFacts.Single(item => item.Id == "research.speed").Tooltip == "calculation", "Core fact lost its detail.");
        model.Facts.Reverse();
        Check(orderedIds.SequenceEqual(model.CardFacts.Select(item => item.Id)), "Card order depends on provider enumeration.");
        Check(model.Facts.Any(item => item.Id == "detail"), "Detail-only information was destroyed.");
        Console.WriteLine("PASS modern priority, stable order, retained explanations and no core-fact truncation");

        def.operatingRequirements.Add(requirementA);
        requirementA.Accepted = true; facility.Enabled = true;
        Call(cache, "RefreshFacilityFacts", view);
        var reqFact = view.Facts.Single(item => item.Kind == UiFacilityInfoKind.Requirement);
        Check(!view.Info.CardFacts.Contains(reqFact) && view.Info.CompactFacts.Contains(reqFact), "Met requirement must remain in details, not the modern summary.");
        requirementA.Accepted = false; facility.Enabled = false;
        Call(cache, "RefreshFacilityFacts", view);
        reqFact = view.Facts.Single(item => item.Kind == UiFacilityInfoKind.Requirement);
        Check(view.Info.CardFacts.Contains(reqFact) && view.Info.CardFacts.First().CardPriority == UiFacilityCardPriority.Alert, "Failed requirement was not promoted to the card.");
        Check(view.ModernDisplayChips.Select(chip => chip.Label).SequenceEqual(view.Info.CardFacts.Select(item => item.DisplayText)), "Cache did not project modern visibility changes.");
        requirementA.Accepted = true;
        Call(cache, "RefreshFacilityFacts", view);
        Check(!view.Info.CardFacts.Any(item => item.Kind == UiFacilityInfoKind.Requirement), "Recovered requirement left a stale warning.");
        Console.WriteLine("PASS live requirement failure and recovery with complete classic/detail projection");

        var group = new UiFacilityInfoGroup();
        group.Items.Add(new UiFacilityInfoItem { Id = "body.b", Value = "second", CardPlacement = UiFacilityCardPlacement.Body });
        group.Items.Add(new UiFacilityInfoItem { Id = "body.a", Value = "first", CardPlacement = UiFacilityCardPlacement.Body });
        group.Items.Add(new UiFacilityInfoItem { Id = "detail", Value = "full explanation", Importance = UiFacilityInfoImportance.Primary });
        group.Items.Add(new UiFacilityInfoItem { Id = "header", Value = "10 W", Importance = UiFacilityInfoImportance.Detail, CardPlacement = UiFacilityCardPlacement.Header });
        group.Items.Add(new UiFacilityInfoItem { Id = "action", CardPlacement = UiFacilityCardPlacement.Action, CardPriority = UiFacilityCardPriority.Secondary, Action = () => { } });
        Check(group.CardItems.Count == 4 && group.CardItems.Count(item => item.CardPlacement == UiFacilityCardPlacement.Body) == 2, "Additional body facts or actions were dropped.");
        Check(group.CardItems.Any(item => item.Id == "header") && group.DetailItems.Single().Id == "detail", "Modern placement incorrectly followed legacy importance.");
        var groupIds = group.CardItems.Select(item => item.Id).ToArray();
        group.Items.Reverse();
        Check(groupIds.SequenceEqual(group.CardItems.Select(item => item.Id)), "Group placement depends on item position.");
        Console.WriteLine("PASS explicit group roles retain multiple body facts, actions and secondary detail");
    }
}

namespace Verse
{
    public class ThingDef { public string LabelCap = "fuel"; }
    public class SkillDef { public string defName; public string LabelCap; }
    public struct AcceptanceReport
    {
        public bool Accepted; public string Reason;
        public AcceptanceReport(bool accepted) { Accepted = accepted; Reason = accepted ? null : "disabled"; }
    }
    public static class Extensions
    {
        public static string Translate(this string key, params object[] args) { return key + (args.Length == 0 ? "" : " " + string.Join("|", args)); }
        public static bool NullOrEmpty<T>(this List<T> list) { return list == null || list.Count == 0; }
        public static string ToStringTicksToPeriod(this int value) { return value.ToString(); }
    }
}
namespace UnityEngine { public static class Mathf { public static bool Approximately(float a, float b) { return Math.Abs(a-b) < 0.0001f; } } }
namespace DreamsOutposts
{
    public enum UiChipKind { Neutral, Good, Bad, Warn, Info }
    public struct UiChipView
    {
        public string Label, Tooltip; public UiChipKind Kind;
        public UiChipView(string label, UiChipKind kind = UiChipKind.Neutral, string tooltip = null) { Label = label; Kind = kind; Tooltip = tooltip; }
    }
    public class UiFacilityView
    {
        public OutpostFacility Facility; public float Defense;
        public readonly UiFacilityInfoModel Info = new UiFacilityInfoModel(), CompInfo = new UiFacilityInfoModel();
        public List<UiFacilityInfoItem> Facts => Info.Facts;
        public readonly List<UiChipView> DisplayChips = new List<UiChipView>();
        public readonly List<UiChipView> ModernDisplayChips = new List<UiChipView>();
        public readonly List<UiProductionView> Productions = new List<UiProductionView>();
    }
    public class UiInstallCardView { public OutpostFacilityDef Def; public readonly UiFacilityInfoModel Info = new UiFacilityInfoModel(); public readonly List<UiChipView> Chips = new List<UiChipView>(); }
    public class UiProductionView { public OutpostProductionProperties Props; public readonly UiFacilityInfoModel ModifierInfo = new UiFacilityInfoModel(); public readonly List<UiChipView> ModifierChips = new List<UiChipView>(); }
    public class Outpost
    {
        public int level = 1, Installed; public OutpostFacilityComp_ProductionSupervisor Gate;
        public OutpostTypeDef outpostTypeDef = new OutpostTypeDef();
        public OutpostLevelProperties CurrentLevelProperties = new OutpostLevelProperties();
        public List<OutpostProcessModifierSource> Sources = new List<OutpostProcessModifierSource>();
    }
    public class OutpostTypeDef { public string LabelCap = "Outpost"; }
    public class OutpostLevelProperties { public List<OutpostProductionModifier> productionModifiers = new List<OutpostProductionModifier>(); }
    public class OutpostFacility
    {
        public OutpostFacilityDef def; public OutpostFacilityComp_PowerGenerator Power; public bool Enabled = true;
        public T GetComp<T>() where T : class { return Power as T; }
        public AcceptanceReport CanOperate(Outpost value) { return new AcceptanceReport(Enabled); }
    }
    public class OutpostFacilityDef
    {
        public string LabelCap = "Facility"; public float defense; public object bombardment; public int bombardmentShellBonus, maxPerOutpost, minOutpostLevel = 1;
        public bool IsResearchUnlocked = true; public ThingDef FirstMissingResearch = new ThingDef();
        public OutpostTrainingProperties Training; public OutpostFacilityCompProperties_PowerGenerator PowerProps;
        public List<object> researchPrerequisites = new List<object>();
        public List<OutpostProductionModifier> productionModifiers = new List<OutpostProductionModifier>();
        public List<OutpostEventCategoryModifier> eventCategoryModifiers = new List<OutpostEventCategoryModifier>();
        public List<OutpostFacilityRequirement> operatingRequirements = new List<OutpostFacilityRequirement>();
        public List<OutpostProcessProperties> Processes = new List<OutpostProcessProperties>();
        public bool HasProcesses => Processes.Count > 0;
        public T GetCompProperties<T>() where T : class { return PowerProps as T; }
        public bool IsLevelRequirementMetBy(int level) { return level >= minOutpostLevel; }
    }
    public class OutpostFacilityRequirement
    {
        public string Label, Description = "requirement detail"; public bool Accepted = true;
        public AcceptanceReport Check(Outpost value) { return new AcceptanceReport(Accepted); }
    }
    public class OutpostTrainingProperties { public SkillDef skill; }
    public static class OutpostTrainingUtility
    {
        public static bool Trains(OutpostFacilityDef def) { return def.Training != null; }
        public static OutpostTrainingProperties GetTraining(OutpostFacilityDef def) { return def.Training; }
        public static int CountTrainees(Outpost o, OutpostFacilityDef d) { return 2; }
        public static float EffectiveXpPerHour(Outpost o, OutpostFacilityDef d) { return 30; }
        public static bool ReceivesTrainingFactor(OutpostFacilityDef d) { return true; }
        public static float TrainingFactor(Outpost o) { return 1; }
    }
    public static class OutpostUtility { public static int CountInstalled(Outpost o, OutpostFacilityDef d) { return o.Installed; } }
    public class OutpostFacilityCompProperties_PowerGenerator { public ThingDef fuel = new ThingDef(); public int cycleTicks = 60; public float fuelPerCycle = 2; public bool requiresFuel = true; }
    public class OutpostFacilityComp_PowerGenerator { public object linkedReceiver; public bool IsPoweredNow; public OutpostFacilityCompProperties_PowerGenerator Props = new OutpostFacilityCompProperties_PowerGenerator(); }
    public class RemotePowerSource { public Outpost Outpost; public OutpostFacility Facility; public OutpostFacilityComp_PowerGenerator Comp; }
    public static class RemotePowerUtility { public static float PowerOutput(RemotePowerSource source, object receiver) { return 950; } }
    public class OutpostProcessProperties { }
    public class OutpostProductionProperties : OutpostProcessProperties { }
    public class OutpostProductionModifier { public float factor = 1, offset; public bool Matches(OutpostProcessProperties p, OutpostFacilityDef d) { return true; } }
    public class OutpostEventCategoryModifier { public ThingDef category; public float offset, factor = 1; }
    public class OutpostProcessModifierSource { public OutpostProductionModifier Modifier; public OutpostFacilityDef SourceFacility; public OutpostFacility SourceInstance; public bool IsLevelModifier; }
    public static class OutpostProcessUtility { public static IEnumerable<OutpostProcessModifierSource> MatchingModifiers(Outpost o, OutpostFacility f, OutpostProcessProperties p) { return o.Sources; } }
    public class OutpostFacilityComp_ProductionSupervisor
    {
        public bool AllowsLevelFactor = true;
        public static OutpostFacilityComp_ProductionSupervisor GateFor(Outpost o) { return o.Gate; }
        public bool Gates(OutpostProductionModifier m) { return true; }
        public string InactiveReasons() { return "inactive"; }
        public bool SuppressesLevelFactorFor(Outpost o, OutpostFacility f, OutpostProductionProperties p) { return !AllowsLevelFactor; }
    }
}
