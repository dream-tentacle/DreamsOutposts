using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	public class OutpostManagePageDef : Def
	{
		public Type pageClass = typeof(OutpostManagePage);

		public float order;

		public bool hidden;

		public List<OutpostTypeDef> allowedOutpostTypes = new List<OutpostTypeDef>();

		public List<OutpostTypeDef> disallowedOutpostTypes = new List<OutpostTypeDef>();

		public bool whitelistOnly;

		public bool IsAllowedIn(OutpostTypeDef outpostTypeDef)
		{
			return OutpostTypeRestrictionUtility.IsAllowedIn(allowedOutpostTypes, disallowedOutpostTypes, whitelistOnly, outpostTypeDef);
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string item in base.ConfigErrors())
			{
				yield return item;
			}
			if (pageClass == null)
			{
				yield return "pageClass is null: every manage page def must point at an OutpostManagePage subclass.";
			}
			else if (!typeof(OutpostManagePage).IsAssignableFrom(pageClass))
			{
				yield return "pageClass " + pageClass.FullName + " does not derive from OutpostManagePage.";
			}
			else if (pageClass.IsAbstract)
			{
				yield return "pageClass " + pageClass.FullName + " is abstract: point at a concrete page class.";
			}
			else if (pageClass.GetConstructor(Type.EmptyTypes) == null)
			{
				yield return "pageClass " + pageClass.FullName + " has no parameterless constructor, so it cannot be instantiated.";
			}
			for (int i = 0; i < (allowedOutpostTypes?.Count ?? 0); i++)
			{
				OutpostTypeDef allowed = allowedOutpostTypes[i];
				if (allowed == null)
				{
					yield return "allowedOutpostTypes[" + i + "] is null.";
				}
				else if (disallowedOutpostTypes != null && disallowedOutpostTypes.Contains(allowed))
				{
					yield return "Outpost type " + allowed.defName + " is in both allowedOutpostTypes and disallowedOutpostTypes; the whitelist wins, but the config is contradictory.";
				}
			}
			for (int j = 0; j < (disallowedOutpostTypes?.Count ?? 0); j++)
			{
				if (disallowedOutpostTypes[j] == null)
				{
					yield return "disallowedOutpostTypes[" + j + "] is null.";
				}
			}
			if (!hidden && whitelistOnly && allowedOutpostTypes.NullOrEmpty())
			{
				yield return "whitelistOnly is true but allowedOutpostTypes is empty, so this page can never show up in any outpost type.";
			}
		}
	}
}
