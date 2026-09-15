using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostProductionProperties
	{
		public string id;

		public ThingDef product;

		/// <summary>用于无实体产物（例如科研点数）的本地化显示键。</summary>
		public string outputLabelKey;

		public StatDef capacityStat;

		public int intervalTicks = 60000;

		public float outputPerCapacity = 1f;

		public List<ThingDefCountClass> inputs = new List<ThingDefCountClass>();

		public List<string> tags = new List<string>();

		public SkillDef requiredSkill;

		public int requiredSkillLevel;

		public Type workerClass = typeof(OutpostProductionWorker);

		[Unsaved(false)]
		private OutpostProductionWorker worker;

		public bool HasInputs => !inputs.NullOrEmpty();

		public bool HasSkillRequirement => requiredSkill != null && requiredSkillLevel > 0;

		public OutpostProductionWorker Worker
		{
			get
			{
				if (worker == null)
				{
					worker = (OutpostProductionWorker)Activator.CreateInstance(workerClass);
				}
				// 开发模式提醒：worker 实例按 Def 共享，在实例字段里攒状态会串到别的据点去。
				if (Prefs.DevMode)
				{
					OutpostProductionWorker.WarnIfStateful(worker.GetType());
				}
				return worker;
			}
		}

		public bool PawnMeetsSkillRequirement(Pawn pawn)
		{
			if (!HasSkillRequirement)
			{
				return true;
			}
			if (pawn?.skills == null)
			{
				return false;
			}
			Pawn_SkillTracker tracker = pawn.skills;
			SkillRecord record = tracker.GetSkill(requiredSkill);
			return record != null && record.Level >= requiredSkillLevel;
		}

		public bool HasTag(string tag)
		{
			string wanted = tag?.Trim();
			if (string.IsNullOrEmpty(wanted) || tags == null)
			{
				return false;
			}
			for (int i = 0; i < tags.Count; i++)
			{
				string candidate = tags[i]?.Trim();
				if (!string.IsNullOrEmpty(candidate) && string.Equals(candidate, wanted, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		public IEnumerable<string> ConfigErrors()
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				yield return "id is required.";
			}
			if (intervalTicks <= 0)
			{
				yield return "intervalTicks must be positive.";
			}
			if (float.IsNaN(outputPerCapacity) || float.IsInfinity(outputPerCapacity) || outputPerCapacity < 0f)
			{
				yield return "outputPerCapacity must be a finite, non-negative number.";
			}
			foreach (string item in InputErrors())
			{
				yield return item;
			}
			foreach (string item2 in TagErrors())
			{
				yield return item2;
			}
			foreach (string item3 in SkillRequirementErrors())
			{
				yield return item3;
			}
			if (workerClass == null || !typeof(OutpostProductionWorker).IsAssignableFrom(workerClass) || workerClass.IsAbstract || workerClass.ContainsGenericParameters || workerClass.GetConstructor(Type.EmptyTypes) == null)
			{
				yield return "workerClass must be a concrete OutpostProductionWorker with a public parameterless constructor.";
				yield break;
			}
			if (product == null && !Worker.UsesDynamicProduct)
			{
				yield return "product is required unless the worker provides a dynamic product (UsesDynamicProduct).";
			}
			foreach (string item4 in Worker.ConfigErrors(this))
			{
				yield return item4;
			}
		}

		private IEnumerable<string> SkillRequirementErrors()
		{
			if (requiredSkill == null)
			{
				if (requiredSkillLevel > 0)
				{
					yield return "requiredSkillLevel is set but requiredSkill is missing; nobody would be filtered out.";
				}
			}
			else if (requiredSkillLevel < 0 || requiredSkillLevel > 20)
			{
				yield return "requiredSkillLevel must be between 0 and 20.";
			}
		}

		private IEnumerable<string> InputErrors()
		{
			HashSet<ThingDef> seen = new HashSet<ThingDef>();
			for (int i = 0; i < (inputs?.Count ?? 0); i++)
			{
				ThingDefCountClass input = inputs[i];
				if (input == null)
				{
					yield return "inputs[" + i + "] is null.";
					continue;
				}
				if (input.thingDef == null)
				{
					yield return "inputs[" + i + "] has no thingDef.";
					continue;
				}
				if (input.count <= 0)
				{
					yield return "inputs[" + i + "] (" + input.thingDef.defName + ") must have a positive count.";
				}
				if (!seen.Add(input.thingDef))
				{
					yield return "Duplicate inputs entry for " + input.thingDef.defName + ".";
				}
			}
		}

		private IEnumerable<string> TagErrors()
		{
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < (tags?.Count ?? 0); i++)
			{
				string tag = tags[i]?.Trim();
				if (string.IsNullOrEmpty(tag))
				{
					yield return "tags[" + i + "] is empty.";
				}
				else if (!seen.Add(tag))
				{
					yield return "Duplicate tag: " + tag;
				}
			}
		}
	}
}
