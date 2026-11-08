using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistentCity.Core
{
    /// <summary>F6j: same-visit extras on the committed main dish. Nothing here is saved: a committed meal keeps
    /// only its main, and checkout plans extras again from the live menu, stock, prices and wallet.</summary>
    public sealed partial class FoodCommerce
    {
        private FoodAlternativeObservation? matchedRival;

        private int ExtraTaste(ResidentMeal meal, CitizenState person, string product)
            => FoodMenuPolicy.Taste(Traits(meal, person), city.State.Seed, meal.ResidentId, product);

        private FoodMealTicket PlanTicket(ResidentMeal meal, CitizenState person, string businessId, string product, Money price,
            IEnumerable<FoodExtraOffer> menu, Money allowance)
            => FoodMealExtras.Plan(city.State.Seed, meal.ResidentId, meal.Day, meal.Kind, businessId,
                new FoodTicketLine(product, price), menu, allowance);

        /// <summary>Null when the seller sells the main dish only or nothing extra is wanted and affordable.
        /// Units already held by other committed meals are not available as extras.</summary>
        private FoodMealTicket? CheckoutTicket(ResidentMeal meal, double now)
        {
            if (!(market is IFoodTicketMarket tickets) || !tickets.SellsExtras(meal.BusinessId)) return null;
            var person = Owner(meal);
            var business = person == null ? null : ReadBusiness(meal.BusinessId, now);
            if (business == null || business.Id != meal.BusinessId || business.PlaceId != meal.PlaceId) return null;
            var scope = state.Shops.FirstOrDefault(s => s.PlaceId == meal.PlaceId && s.BusinessId == meal.BusinessId);
            if (scope == null) return null;
            var menu = new List<FoodExtraOffer>(business.Offers.Count);
            foreach (var offer in business.Offers)
            {
                // A player shop's extra needs a known sellable unit, like its main dish.
                if (!offer.Units.HasValue || !FoodProducts.Known(offer.ProductId) || !scope.ContainsProduct(offer.ProductId)) continue;
                reservations.TryGetValue(ReservationKey(meal.PlaceId, offer.ProductId), out int held);
                menu.Add(new FoodExtraOffer(offer.ProductId, offer.Price, Math.Max(0, offer.Units.Value - held), ExtraTaste(meal, person!, offer.ProductId)));
            }
            var ticket = PlanTicket(meal, person!, meal.BusinessId, meal.ProductId, meal.Price, menu, AvailableForMeal(meal));
            return ticket.Extras.Count == 0 ? null : ticket;
        }

        /// <summary>A seller may sell fewer extras than planned, never others: sold lines must be a subsequence of
        /// the ticket's. Anything else leaves the meal uncertain with its planned total held.</summary>
        private static IReadOnlyList<FoodTicketLine> SoldExtras(FoodMealTicket? ticket, FoodSale result)
        {
            var sold = result.SoldExtras;
            if (sold.Count == 0) return sold;
            if (ticket == null || !result.Paid) throw new InvalidOperationException("Seller reported extras outside the ticket.");
            int next = 0;
            foreach (var line in sold)
            {
                while (next < ticket.Extras.Count && !(ticket.Extras[next].ProductId == line.ProductId && ticket.Extras[next].Price == line.Price)) next++;
                if (next == ticket.Extras.Count) throw new InvalidOperationException("Seller reported extras outside the ticket.");
                next++;
            }
            return sold;
        }

        /// <summary>Modeled rival ticket from the advertised menu just matched for settlement. Rival stock is
        /// unpublished, so every advertised known product other than the main may be an extra.</summary>
        private FoodMealTicket? RivalTicket(ResidentMeal meal)
        {
            var observation = matchedRival; var person = Owner(meal);
            if (observation == null || person == null || observation.Id != meal.BusinessId) return null;
            var menu = observation.Offers.Where(o => FoodProducts.Known(o.ProductId))
                .Select(o => new FoodExtraOffer(o.ProductId, o.Price, null, ExtraTaste(meal, person, o.ProductId))).ToList();
            var ticket = PlanTicket(meal, person, meal.BusinessId, meal.ProductId, meal.Price, menu, AvailableForMeal(meal));
            return ticket.Extras.Count == 0 ? null : ticket;
        }

        /// <summary>Diagnostic: each feasible candidate main's ticket from its captured menu, the same policy checkout
        /// applies. Built only for an observed decision; nothing is saved or scored.</summary>
        private IReadOnlyList<FoodMealTicket> CandidateTickets(ResidentMeal meal, CitizenState person, FoodDecisionState d, FoodIntent intent)
        {
            var tickets = new List<FoodMealTicket>();
            // Read-only: an observed decision must not change wallet state an unobserved one would not.
            Money allowance = meal.Budget != null ? HouseholdFinance.SpendingLimit(meal.Budget) : Money.Max(Money.Zero, meal.Allowance - meal.DayAllowanceUsed);
            foreach (var choice in intent.Choices)
            {
                if (!choice.ScoreCents.HasValue) continue;
                var shop = d.Shops.FirstOrDefault(s => s.BusinessId == choice.BusinessId);
                if (shop == null) continue;
                bool rival = shop.AlternativeKind == FoodAlternativeKind.Rival;
                if (!rival && !(market is IFoodTicketMarket t && t.SellsExtras(shop.BusinessId))) continue;
                var menu = shop.Offers.Where(o => rival || o.Units >= 0)
                    .Select(o => new FoodExtraOffer(o.ProductId, o.Price, rival ? (int?)null : o.Units, ExtraTaste(meal, person, o.ProductId)));
                tickets.Add(PlanTicket(meal, person, shop.BusinessId, choice.ProductId, choice.Price, menu, allowance));
            }
            return tickets;
        }
    }
}
