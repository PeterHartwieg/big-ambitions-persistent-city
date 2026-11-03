using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public interface IFoodCampaignPreparation
    {
        bool HasPendingPublication { get; }
        bool ContinuePublication(int workBudget = 16);
    }

    public sealed partial class FoodCampaignSource : IFoodCampaignPreparation
    {
        private sealed class Publication
        {
            public CampaignSnapshot Snapshot = null!;
            public Dictionary<string,string> Places = null!;
        }
        private readonly Queue<Publication> publications = new Queue<Publication>();
        private IEnumerator<int>? publicationWork;
        public bool HasPendingPublication => publications.Count != 0;
        public long PublicationUnits { get; private set; }
        public int LastPublicationUnits { get; private set; }
        // Runtime owns containment: invalid campaign evidence disables observation, not the city.
        public Action<Exception>? PublicationFailed { get; set; }
        public void DiscardPublications()
        {
            publicationWork?.Dispose(); publicationWork = null; publications.Clear();
        }

        // Snapshot contains detached immutable values, never live registrations. Keep hourly order.
        // Saves must finish this transient work before capturing the native/mod epoch together.
        public void EnqueuePublication(CampaignSnapshot snapshot, IReadOnlyDictionary<string,string> places)
        {
            if (!settings.Enabled) return;
            if (snapshot == null || snapshot.Businesses.Count > 1024 || places.Count > 1024)
                throw new ArgumentException("Oversized campaign publication.");
            var copy = new Dictionary<string,string>(places.Count, StringComparer.Ordinal);
            foreach (var pair in places) copy.Add(pair.Key, pair.Value);
            publications.Enqueue(new Publication { Snapshot=snapshot, Places=copy });
        }
        public bool ContinuePublication(int workBudget = 16)
        {
            if (workBudget < 1 || workBudget > 64) throw new ArgumentOutOfRangeException(nameof(workBudget));
            LastPublicationUnits = 0;
            while (LastPublicationUnits < workBudget && publications.Count != 0)
            {
                var head = publications.Peek();
                publicationWork ??= PublishSteps(head.Snapshot, head.Places).GetEnumerator();
                bool more;
                try { more = publicationWork.MoveNext(); }
                catch (Exception error)
                {
                    DiscardPublications();
                    if (PublicationFailed == null) throw;
                    PublicationFailed(error);
                    return true;
                }
                LastPublicationUnits++; PublicationUnits++;
                if (more) continue;
                publicationWork.Dispose(); publicationWork=null; publications.Dequeue();
            }
            return !HasPendingPublication;
        }
        public void CompletePublicationsForSave()
        {
            // Explicit save/checkpoint path only; normal frame advancement never drains here.
            while (HasPendingPublication) ContinuePublication(64);
        }
        public void Publish(CampaignSnapshot snapshot, IReadOnlyDictionary<string,string> places)
        {
            EnqueuePublication(snapshot,places);
            CompletePublicationsForSave(); // explicit synchronous/offline API
        }
    }
}
