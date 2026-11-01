using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        // Not an Update participant. Cache belongs to this runtime/city host, not a process-wide native catalogue.
        private readonly NativeCampaignReader campaignReader = new NativeCampaignReader();

        /// <summary>Main-thread bridge dump of explicit existing save-registration indices. No discovery scan.
        /// All units finish synchronously in this call, so UI/native writers cannot interleave the observation.</summary>
        public CampaignSnapshot ReadCampaignSnapshot(IReadOnlyList<int> registrationIndices)
        {
            var save = SaveGameManager.Current;
            if (save == null || failed || !ReferenceEquals(saveIdentity, save) || simulation == null)
                throw new InvalidOperationException("A ready current population is required for campaign observation.");
            if (registrationIndices == null || registrationIndices.Count < 1 || registrationIndices.Count > NativeCampaignReader.MaximumBusinesses)
                throw new ArgumentException("Select one to 1024 existing registration indices.");
            var registrations = save.BuildingRegistrations;
            var sources = new CampaignBusinessSource[registrationIndices.Count];
            for (int i = 0; i < sources.Length; i++)
            {
                int index = registrationIndices[i];
                if (index < 0 || index >= registrations.Count) throw new ArgumentException("Registration index is outside the current save.");
                var reg = registrations[index];
                if (reg == null) throw new ArgumentException("Selected registration is missing.");
                sources[i] = new CampaignBusinessSource(FoodBusinessCatalogue.BusinessIdentity(reg), reg);
            }
            double minute = save.Day * 1440.0 + save.Hour * 60.0 + save.Minute;
            var refresh = campaignReader.Begin(sources, minute, 0);
            while (refresh.Cursor < sources.Length) refresh.Step(NativeCampaignReader.MaximumBusinessesPerUnit, minute, 0);
            return refresh.Snapshot ?? throw new InvalidOperationException("Campaign observation was refused.");
        }
    }
}
