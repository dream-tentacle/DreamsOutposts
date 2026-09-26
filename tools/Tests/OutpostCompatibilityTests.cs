// Standalone regression harness. Compile against the production migration and
// state classes; the small Verse/component doubles isolate save-data handling.
using System;
using System.Collections.Generic;
using System.Xml;
using DreamsOutposts;

namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public enum LoadSaveMode { LoadingVars, Saving, PostLoadInit }
    public class Loader { public XmlNode curXmlParent; }
    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static Loader loader = new Loader();
    }
    public class ThingDef { public string defName; }
    public static class Scribe_Defs
    {
        public static void Look(ref ThingDef value, string name)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                XmlNode node = Scribe.loader.curXmlParent[name];
                value = node == null ? null : new ThingDef { defName = node.InnerText };
            }
        }
    }
    public static class Scribe_Values
    {
        public static void Look(ref string value, string name)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.loader.curXmlParent[name]?.InnerText;
            else if (Scribe.mode == LoadSaveMode.Saving && value != null) Write(name, value);
        }
        public static void Look(ref int value, string name, int defaultValue)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Scribe.loader.curXmlParent[name] == null ? defaultValue :
                    int.Parse(Scribe.loader.curXmlParent[name].InnerText);
            else if (Scribe.mode == LoadSaveMode.Saving) Write(name, value.ToString());
        }
        private static void Write(string name, string value)
        {
            XmlNode node = Scribe.loader.curXmlParent.OwnerDocument.CreateElement(name);
            node.InnerText = value;
            Scribe.loader.curXmlParent.AppendChild(node);
        }
    }
}

namespace DreamsOutposts
{
    public class OutpostFacilityCompProperties { public Type compClass; }
    public class OutpostFacilityDef
    {
        public List<OutpostFacilityCompProperties> comps = new List<OutpostFacilityCompProperties>();
    }
    public class OutpostFacility
    {
        public OutpostFacilityDef def = new OutpostFacilityDef();
        public List<OutpostFacilityComp> comps = new List<OutpostFacilityComp>();
    }
    public class OutpostFacilityComp
    {
        public OutpostFacility parent;
        public OutpostFacilityCompProperties props;
        public virtual void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
        {
            if (GetType() != props.compClass) throw new InvalidCastException("Mismatched component properties");
            this.parent = parent;
            this.props = props;
        }
        public virtual void ExposeData() { }
    }
    public class OutpostProcessWorker
    {
        public Type StateClass = typeof(OutpostProcessState);
        public OutpostProcessState CreateState(string id, int tick)
        {
            return (OutpostProcessState)Activator.CreateInstance(StateClass, id, tick);
        }
    }
    public class OutpostProcessProperties
    {
        public string id;
        public OutpostProcessWorker Worker = new OutpostProcessWorker();
    }
    public class OutpostProductionProperties : OutpostProcessProperties { }
    public abstract class OutpostFacilityCompProperties_ProcessBase : OutpostFacilityCompProperties
    {
        public abstract IEnumerable<OutpostProcessProperties> Processes { get; }
        protected virtual string ProcessCollectionName { get { return "processes"; } }
    }
    public class OutpostFacilityCompProperties_Process : OutpostFacilityCompProperties_ProcessBase
    {
        public List<OutpostProcessProperties> processes = new List<OutpostProcessProperties>();
        public OutpostFacilityCompProperties_Process() { compClass = typeof(OutpostFacilityComp_Process); }
        public override IEnumerable<OutpostProcessProperties> Processes { get { return processes; } }
    }
    public class OutpostFacilityComp_Process : OutpostFacilityComp
    {
        public List<OutpostProcessState> states = new List<OutpostProcessState>();
        protected virtual OutpostFacilityCompProperties_ProcessBase ProcessProps
        {
            get { return (OutpostFacilityCompProperties_ProcessBase)props; }
        }
        public IEnumerable<OutpostProcessProperties> Processes { get { return ProcessProps.Processes; } }
        public OutpostProcessState GetState(string id) { return states.Find(s => s.processId == id); }
        public override void Initialize(OutpostFacility parent, OutpostFacilityCompProperties props)
        {
            base.Initialize(parent, props);
            foreach (OutpostProcessProperties rule in Processes)
            {
                OutpostProcessState state = GetState(rule.id);
                if (state == null) states.Add(rule.Worker.CreateState(rule.id, 900000));
                else if (!rule.Worker.StateClass.IsInstanceOfType(state))
                    throw new Exception("Invalid state reached Initialize");
            }
        }
        public override void ExposeData()
        {
            if (Verse.Scribe.mode != Verse.LoadSaveMode.LoadingVars) return;
            states = new List<OutpostProcessState>();
            XmlNode container = Verse.Scribe.loader.curXmlParent;
            foreach (XmlNode node in container.SelectNodes("states/li"))
            {
                // Like Scribe's deep loader, use the saved Class when present.
                Type type = node.Attributes["Class"] == null ? typeof(OutpostProcessState) :
                    typeof(OutpostProcessState).Assembly.GetType(node.Attributes["Class"].Value, true);
                var state = (OutpostProcessState)Activator.CreateInstance(type);
                Verse.Scribe.loader.curXmlParent = node;
                state.ExposeData();
                states.Add(state);
            }
            Verse.Scribe.loader.curXmlParent = container;
        }
    }
    public class ResidentComp : OutpostFacilityComp { public int retainedValue = 73; }
    public class PowerComp : OutpostFacilityComp { }
}

public static class OutpostCompatibilityTests
{
    private static int passed;
    private static void Assert(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
    private static void Load(Verse.IExposable value, string xml)
    {
        SetXml(xml);
        value.ExposeData();
    }
    private static void SetXml(string xml)
    {
        var doc = new XmlDocument(); doc.LoadXml(xml);
        Verse.Scribe.loader.curXmlParent = doc.DocumentElement;
        Verse.Scribe.mode = Verse.LoadSaveMode.LoadingVars;
    }
    private static void Test(string name, Action test)
    {
        test(); passed++; Console.WriteLine("PASS " + name);
    }
    private static OutpostFacilityCompProperties_Process Process(string id)
    {
        var props = new OutpostFacilityCompProperties_Process();
        props.processes.Add(new OutpostProcessProperties { id = id });
        return props;
    }
    private static OutpostFacilityCompProperties_Production Production(string id, Type stateType)
    {
        var props = new OutpostFacilityCompProperties_Production();
        props.productions.Add(new OutpostProductionProperties {
            id = id, Worker = new OutpostProcessWorker { StateClass = stateType }
        });
        return props;
    }
    public static int Main()
    {
        Test("old untyped production state reads legacy fields", () => {
            var state = new OutpostProcessState();
            Load(state, "<li><productionId>capturedAnimals</productionId><nextProductionTick>123456</nextProductionTick></li>");
            Assert(state.processId == "capturedAnimals" && state.nextProcessTick == 123456, "Legacy fields lost");
        });
        Test("current fields take precedence, including zero", () => {
            var state = new OutpostProductionState();
            Load(state, "<li><processId>new</processId><productionId>old</productionId><nextProcessTick>0</nextProcessTick><nextProductionTick>999</nextProductionTick></li>");
            Assert(state.processId == "new" && state.nextProcessTick == 0, "Current fields overwritten");
        });
        Test("save and reload writes canonical fields", () => {
            var state = new OutpostProductionState("fuel", 82000);
            SetXml("<li/>"); Verse.Scribe.mode = Verse.LoadSaveMode.Saving;
            state.ExposeData();
            string xml = Verse.Scribe.loader.curXmlParent.OuterXml;
            Assert(!xml.Contains("productionId") && !xml.Contains("nextProductionTick"), "Legacy names written");
            var reloaded = new OutpostProductionState(); Load(reloaded, xml);
            Assert(reloaded.processId == "fuel" && reloaded.nextProcessTick == 82000, "Round trip lost state");
        });
        Test("old taming Production becomes Process without losing timer", () => {
            var old = new OutpostFacilityComp_Production();
            SetXml("<comp><states><li><productionId>capturedAnimals</productionId><nextProductionTick>456789</nextProductionTick></li></states></comp>");
            old.ExposeData();
            var facility = new OutpostFacility(); facility.comps.Add(old);
            facility.def.comps.Add(Process("capturedAnimals"));
            OutpostFacilityCompMigration.Restore(facility);
            Assert(facility.comps[0].GetType() == typeof(OutpostFacilityComp_Process), "Wrong runtime type retained");
            Assert(((OutpostFacilityComp_Process)facility.comps[0]).GetState("capturedAnimals").nextProcessTick == 456789, "Taming timer reset");
        });
        Test("old research class resolves and retains nextResearchTick", () => {
            Type type = typeof(OutpostProcessState).Assembly.GetType("DreamsOutposts.OutpostFacilityComp_Research", true);
            var old = (OutpostFacilityComp_Research)Activator.CreateInstance(type);
            SetXml("<comp><nextResearchTick>345678</nextResearchTick></comp>"); old.ExposeData();
            var facility = new OutpostFacility(); facility.comps.Add(old);
            facility.def.comps.Add(Process("research"));
            OutpostFacilityCompMigration.Restore(facility);
            Assert(facility.comps[0].GetType() == typeof(OutpostFacilityComp_Process), "Research bridge survived migration");
            Assert(((OutpostFacilityComp_Process)facility.comps[0]).GetState("research").nextProcessTick == 345678, "Research timer lost");
        });
        foreach (var item in new[] {
            Tuple.Create(typeof(OutpostProductionState_AdaptiveMining), "selectedMineral", "Gold"),
            Tuple.Create(typeof(OutpostProductionState_Farming), "selectedPlant", "Plant_Rice"),
            Tuple.Create(typeof(OutpostProductionState_MechMachining), "selectedProduct", "ComponentIndustrial"),
            Tuple.Create(typeof(OutpostProductionState_MiliraSolar), "selectedProduct", "Milira_SunPlateSteel"),
            Tuple.Create(typeof(OutpostProductionState_XianluQi), "selectedProduct", "RI_Resource_SpiritualCrystal")
        })
        {
            Test("keeps selection and timer: " + item.Item1.Name, () => {
                var old = new OutpostFacilityComp_Production();
                SetXml("<comp><states><li Class=\"" + item.Item1.FullName + "\"><productionId>output</productionId><nextProductionTick>654321</nextProductionTick><" + item.Item2 + ">" + item.Item3 + "</" + item.Item2 + "></li></states></comp>");
                old.ExposeData(); var before = old.states[0];
                var facility = new OutpostFacility(); facility.comps.Add(old);
                facility.def.comps.Add(Production("output", item.Item1));
                OutpostFacilityCompMigration.Restore(facility);
                var after = ((OutpostFacilityComp_Process)facility.comps[0]).GetState("output");
                Assert(ReferenceEquals(before, after) && after.nextProcessTick == 654321, "State replaced/reset");
                Assert(((Verse.ThingDef)item.Item1.GetField(item.Item2).GetValue(after)).defName == item.Item3, "Selection lost");
            });
        }
        Test("reordered/added/removed/null components preserve the matching object", () => {
            var resident = new ResidentComp();
            var facility = new OutpostFacility();
            facility.comps.Add(null); facility.comps.Add(new OutpostFacilityComp_Production()); facility.comps.Add(resident);
            facility.def.comps.Add(new OutpostFacilityCompProperties { compClass = typeof(PowerComp) });
            facility.def.comps.Add(new OutpostFacilityCompProperties { compClass = typeof(ResidentComp) });
            facility.def.comps.Add(Process("research"));
            OutpostFacilityCompMigration.Restore(facility);
            Assert(facility.comps.Count == 3 && facility.comps[0] is PowerComp, "Definitions not authoritative");
            Assert(ReferenceEquals(resident, facility.comps[1]) && resident.retainedValue == 73, "Unrelated component reset");
        });
        Test("processes moved between components retain distinct timers", () => {
            var old = new OutpostFacilityComp_Production();
            old.states.Add(new OutpostProductionState("a", 110)); old.states.Add(new OutpostProductionState("b", 220));
            var facility = new OutpostFacility(); facility.comps.Add(old);
            facility.def.comps.Add(Process("b")); facility.def.comps.Add(Process("a"));
            OutpostFacilityCompMigration.Restore(facility);
            Assert(((OutpostFacilityComp_Process)facility.comps[0]).GetState("b").nextProcessTick == 220, "Timer b lost");
            Assert(((OutpostFacilityComp_Process)facility.comps[1]).GetState("a").nextProcessTick == 110, "Timer a lost");
        });
        Test("state type conversion retains the due tick", () => {
            var old = new OutpostFacilityComp_Process(); old.states.Add(new OutpostProcessState("fuel", 777));
            var facility = new OutpostFacility(); facility.comps.Add(old);
            facility.def.comps.Add(Production("fuel", typeof(OutpostProductionState)));
            OutpostFacilityCompMigration.Restore(facility);
            Assert(((OutpostFacilityComp_Production)facility.comps[0]).GetState("fuel").nextProductionTick == 777, "Converted timer reset");
        });
        Test("migration is idempotent and creates missing components", () => {
            var facility = new OutpostFacility(); facility.comps = null;
            facility.def.comps.Add(Process("research")); OutpostFacilityCompMigration.Restore(facility);
            var comp = facility.comps[0]; var state = ((OutpostFacilityComp_Process)comp).states[0];
            state.nextProcessTick = 765432; OutpostFacilityCompMigration.Restore(facility);
            Assert(ReferenceEquals(comp, facility.comps[0]) && ReferenceEquals(state, ((OutpostFacilityComp_Process)comp).states[0]) && state.nextProcessTick == 765432, "Repeated migration reset state");
        });
        Console.WriteLine(passed + " compatibility regression cases passed.");
        return 0;
    }
}
