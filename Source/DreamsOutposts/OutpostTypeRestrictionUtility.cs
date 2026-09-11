using System.Collections.Generic;

namespace DreamsOutposts
{
	public static class OutpostTypeRestrictionUtility
	{
		public static bool IsAllowedIn(List<OutpostTypeDef> allowedOutpostTypes, List<OutpostTypeDef> disallowedOutpostTypes, bool whitelistOnly, OutpostTypeDef outpostTypeDef)
		{
			if (outpostTypeDef == null)
			{
				return false;
			}
			if (allowedOutpostTypes != null && allowedOutpostTypes.Contains(outpostTypeDef))
			{
				return true;
			}
			if (disallowedOutpostTypes != null && disallowedOutpostTypes.Contains(outpostTypeDef))
			{
				return false;
			}
			return !whitelistOnly;
		}
	}
}
