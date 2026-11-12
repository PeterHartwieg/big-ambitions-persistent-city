using System;
using System.Collections;
using System.Reflection;

namespace PersistentCity.Runtime
{
    public sealed partial class NativeCampaignReader
    {
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
