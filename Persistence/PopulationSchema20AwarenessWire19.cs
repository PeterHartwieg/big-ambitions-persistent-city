using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Persistence
{
    internal static partial class PopulationSchema20
    {
        private static int ValidateAwareness(FoodDecisionState decision,ResidentMeal meal,FoodCommerceState food)
        {
            var image=decision.Awareness;
            if(image==null)
            {
                Require(meal.Day<food.AwarenessStartsDay,"Missing post-activation awareness image.");
                return 0; // only pre-activation/migrated held contexts retain the original shortlist
            }
            Require(image.CatalogueRevision!=null && image.CatalogueRevision.Length==44,"Invalid awareness catalogue revision.");
            byte[] hash=Convert.FromBase64String(image.CatalogueRevision);
            Require(hash.Length==32 && Convert.ToBase64String(hash)==image.CatalogueRevision,"Noncanonical catalogue revision.");
            Require(image.Union!=null && image.Union.Count<=24,"Oversized awareness union.");
            string previous="";
            var sources=new int[5];
            var admitted=new HashSet<string>(StringComparer.Ordinal);
            foreach(var candidate in image.Union!)
            {
                Require(candidate!=null,"Null awareness candidate.");
                Text(candidate.BusinessId,"Awareness generation",512);
                Require(StringComparer.Ordinal.Compare(previous,candidate.BusinessId)<0,"Duplicate or unordered awareness generation.");
                previous=candidate.BusinessId;
                int flags=(int)candidate.Sources;
                Require(flags>0 && flags<32,"Unknown or unsupported awareness source.");
                for(int i=0;i<5;i++) if((flags & (1<<i))!=0) Require(++sources[i]<=(i==4 ? 8 : 4),"Oversized awareness source.");
                if(candidate.Eligible) admitted.Add(candidate.BusinessId);
            }
            foreach(var shop in decision.Shops) Require(admitted.Contains(shop.BusinessId),"Shortlist bypassed awareness admission.");
            return image.Union.Count;
        }
    }
}
