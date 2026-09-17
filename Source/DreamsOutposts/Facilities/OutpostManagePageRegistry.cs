using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostManagePageRegistry
	{
		private static readonly List<OutpostManagePageDef> registeredDefs = new List<OutpostManagePageDef>();

		public static IReadOnlyList<OutpostManagePageDef> RegisteredDefs => registeredDefs;

		public static void RegisterPage(Type pageClass, string label, string tooltip = null, float order = 0f)
		{
			if (pageClass == null || !typeof(OutpostManagePage).IsAssignableFrom(pageClass))
			{
				Log.Error("[DreamsOutposts] OutpostManagePageRegistry.RegisterPage: " + (pageClass?.FullName ?? "null") + " is not an OutpostManagePage subclass; nothing was registered.");
				return;
			}
			OutpostManagePageDef def = new OutpostManagePageDef
			{
				defName = pageClass.FullName,
				label = (string.IsNullOrEmpty(label) ? pageClass.Name : label),
				description = tooltip,
				order = order,
				pageClass = pageClass
			};
			RegisterPage(def);
		}

		public static void RegisterPage(OutpostManagePageDef def)
		{
			if (def == null || def.pageClass == null)
			{
				Log.Error("[DreamsOutposts] OutpostManagePageRegistry.RegisterPage: the def or its pageClass is null; nothing was registered.");
				return;
			}
			for (int i = 0; i < registeredDefs.Count; i++)
			{
				if (registeredDefs[i].pageClass == def.pageClass)
				{
					registeredDefs[i] = def;
					return;
				}
			}
			registeredDefs.Add(def);
		}

		public static bool UnregisterPage(Type pageClass)
		{
			if (pageClass == null)
			{
				return false;
			}
			for (int i = 0; i < registeredDefs.Count; i++)
			{
				if (registeredDefs[i].pageClass == pageClass)
				{
					registeredDefs.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		public static List<OutpostManagePage> BuildPages(Outpost outpost)
		{
			List<OutpostManagePage> pages = new List<OutpostManagePage>();
			if (outpost == null)
			{
				return pages;
			}
			OutpostTypeDef outpostTypeDef = outpost.outpostTypeDef;
			List<OutpostManagePageDef> candidates = new List<OutpostManagePageDef>(DefDatabase<OutpostManagePageDef>.AllDefsListForReading);
			candidates.AddRange(registeredDefs);
			candidates.Sort(ComparePageDefs);
			for (int i = 0; i < candidates.Count; i++)
			{
				OutpostManagePageDef def = candidates[i];
				if (def == null || def.hidden || !def.IsAllowedIn(outpostTypeDef))
				{
					continue;
				}
				OutpostManagePage page = Instantiate(def);
				if (page != null)
				{
					page.outpost = outpost;
					page.def = def;
					if (page.IsVisible)
					{
						pages.Add(page);
					}
				}
			}
			return pages;
		}

		private static OutpostManagePage Instantiate(OutpostManagePageDef def)
		{
			Type pageClass = def.pageClass;
			if (pageClass == null || !typeof(OutpostManagePage).IsAssignableFrom(pageClass) || pageClass.IsAbstract)
			{
				Log.Error("[DreamsOutposts] OutpostManagePageDef " + def.defName + ": pageClass " + (pageClass?.FullName ?? "null") + " is not a concrete OutpostManagePage subclass; the page is skipped.");
				return null;
			}
			try
			{
				return (OutpostManagePage)Activator.CreateInstance(pageClass);
			}
			catch (Exception ex)
			{
				Log.Error("[DreamsOutposts] OutpostManagePageDef " + def.defName + ": could not create " + pageClass.FullName + " (a parameterless constructor is required); the page is skipped. " + ex);
				return null;
			}
		}

		private static int ComparePageDefs(OutpostManagePageDef a, OutpostManagePageDef b)
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
			int byOrder = a.order.CompareTo(b.order);
			if (byOrder != 0)
			{
				return byOrder;
			}
			return string.Compare(a.LabelCap, b.LabelCap, StringComparison.OrdinalIgnoreCase);
		}
	}
}
