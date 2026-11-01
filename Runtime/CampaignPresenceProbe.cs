using System;
using System.Collections;
using System.Reflection;

namespace PersistentCity.Runtime
{
    public sealed partial class NativeCampaignReader
    {
        /// <summary>Allocation-free after binding: preflight only, never exposure evidence.
        /// Use the same verified C1 fields-only binding instead of an unchecked native field access.
        /// Missing/changed list shapes cannot crash the hourly adapter or fabricate an empty image.</summary>
        public bool HasCampaignEntries(object? registration)
        {
            if (registration == null) return false;
            try
            {
                var raw = Field(registration, "marketingCampaigns");
                return raw is IList list && list.Count != 0;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is MemberAccessException || error is TargetException || error is AmbiguousMatchException)
            { return false; }
        }
    }
}
