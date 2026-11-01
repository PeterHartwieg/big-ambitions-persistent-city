using System;
using System.Collections.Generic;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    internal sealed partial class PhysicalPopulation
    {
        // Explicit diagnostics only. Do not call Valid/Position on car leases: their ownership
        // validation is outside this metadata observation. Read the already-owned native bodies.
        internal List<StreetProxyBodyMetadata> ReadStreetProxyBodies()
        {
            if (bindings.Count > StreetInventoryBudget.MaximumRows) throw new InvalidOperationException("body cap");
            var rows = new List<StreetProxyBodyMetadata>();
            foreach (var binding in bindings.Values)
            {
                if (binding.Person != null)
                {
                    var character = binding.Person.Character;
                    if (character == null || character.navmeshAgent == null) throw new InvalidOperationException("resident body unreadable");
                    rows.Add(new StreetProxyBodyMetadata { Instance = character.transform.GetInstanceID(), Resident = binding.Citizen.Identity.Id,
                        Kind = "resident", Position = NativeStreetProxyDiagnostics.Point(character.transform.position),
                        Speed = character.navmeshAgent.velocity.magnitude, Active = character.gameObject.activeInHierarchy && character.navmeshAgent.enabled });
                }
                if (binding.Car != null)
                {
                    var car = binding.Car.Vehicle;
                    if (car == null || car.rb == null) throw new InvalidOperationException("vehicle body unreadable");
                    rows.Add(new StreetProxyBodyMetadata { Instance = car.transform.GetInstanceID(), Resident = binding.Citizen.Identity.Id,
                        Kind = "owned-vehicle", Position = NativeStreetProxyDiagnostics.Point(car.transform.position),
                        Speed = car.rb.velocity.magnitude, Active = car.gameObject.activeInHierarchy });
                }
            }
            return rows;
        }
    }
}
