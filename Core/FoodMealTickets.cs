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
            // E5b4: checkout re-plans from the live menu, so a dynamic extra uses the market's current policy.
            var policy = ProductPolicy();
            foreach (var offer in business.Offers)
            {
                // A player shop's extra needs a known sellable unit, like its main dish.
                if (!offer.Units.HasValue || !policy.Executable(offer.ProductId) || !scope.ContainsProduct(offer.ProductId)) continue;
                reservations.TryGetValue(ReservationKey(meal.PlaceId, offer.ProductId), out int held);
                int taste = FoodProducts.Known(offer.ProductId) ? ExtraTaste(meal, person!, offer.ProductId)
                    : policy.TasteBand(offer.ProductId, Traits(meal, person!), city.State.Seed, meal.ResidentId);
                menu.Add(new FoodExtraOffer(offer.ProductId, offer.Price, Math.Max(0, offer.Units.Value - held), taste));
            }
            var ticket = PlanTicket(meal, person!, meal.BusinessId, meal.ProductId, meal.Price, menu, CurrentAllowance(meal));
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
            var ticket = PlanTicket(meal, person, meal.BusinessId, meal.ProductId, meal.Price, menu, CurrentAllowance(meal));
            return ticket.Extras.Count == 0 ? null : ticket;
        }

        /// <summary>A wallet limit read that changes nothing: a refused checkout must keep its saved Price within
        /// the saved Allowance, and an observed decision must not change state an unobserved one would not.</summary>
        private static Money CurrentAllowance(ResidentMeal meal)
            => meal.Budget != null ? HouseholdFinance.SpendingLimit(meal.Budget) : Money.Max(Money.Zero, meal.Allowance - meal.DayAllowanceUsed);

        /// <summary>The ticket a candidate main would carry, from the shop's captured menu: the policy and filters
        /// checkout applies (captured units already exclude other committed meals' reservations). Null when that
        /// seller sells the main only.</summary>
        private FoodMealTicket? CandidateTicket(ResidentMeal meal, CitizenState person, FoodDecisionShop shop, string product, Money price, Money allowance)
        {
            bool rival = shop.AlternativeKind == FoodAlternativeKind.Rival;
            if (!rival && !(market is IFoodTicketMarket t && t.SellsExtras(shop.BusinessId))) return null;
            var menu = new List<FoodExtraOffer>(shop.Offers.Count);
            foreach (var o in shop.Offers)
            {
                // E5b4: the captured menu replays as held. A revision-2 Food offer is an extra with its held taste;
                // the current policy is never read here.
                bool dynamic = o.Metadata != null && o.Metadata.Revision == 2 && o.Metadata.MealClass == FoodMealClass.Food;
                if ((FoodProducts.Known(o.ProductId) || dynamic) && (rival || o.Units >= 0))
                    menu.Add(new FoodExtraOffer(o.ProductId, o.Price, rival ? (int?)null : o.Units,
                        dynamic && !FoodProducts.Known(o.ProductId) ? o.Metadata!.TasteBand : ExtraTaste(meal, person, o.ProductId)));
            }
            return PlanTicket(meal, person, shop.BusinessId, product, price, menu, allowance);
        }

        /// <summary>Diagnostic: each feasible candidate main's ticket, as used for its expense in the choice.</summary>
        private IReadOnlyList<FoodMealTicket> CandidateTickets(ResidentMeal meal, CitizenState person, FoodDecisionState d, FoodIntent intent)
        {
            var tickets = new List<FoodMealTicket>();
            Money allowance = CurrentAllowance(meal);
            foreach (var choice in intent.Choices)
            {
                if (!choice.ScoreCents.HasValue) continue;
                var shop = d.Shops.FirstOrDefault(s => s.BusinessId == choice.BusinessId);
                var ticket = shop == null ? null : CandidateTicket(meal, person, shop, choice.ProductId, choice.Price, allowance);
                if (ticket != null) tickets.Add(ticket);
            }
            return tickets;
        }

        /// <summary>Evidence that an uncertain meal's checkout completed. A ticket-aware seller reports what sold;
        /// otherwise a confirmed receipt means the whole held Price sold as one product.</summary>
        private bool ConfirmedEvidence(ResidentMeal meal, out Money total, out IReadOnlyList<FoodTicketLine> extras)
        {
            total = meal.Price; extras = Array.Empty<FoodTicketLine>();
            if (market is IFoodTicketEvidence tickets)
            {
                if (!tickets.TryConfirmedTicket(meal, out total, out extras)) return false;
                var main = total;
                foreach (var line in extras) main -= line.Price;
                if (total > meal.Price || main <= Money.Zero || extras.Count > FoodMealExtras.MaximumExtras)
                    throw new InvalidOperationException("Confirmed ticket exceeds the held purchase.");
                return true;
            }
            return market is IFoodSaleEvidence evidence && evidence.IsConfirmedPaid(meal);
        }

        /// <summary>Settle a confirmed uncertain meal: return unsold funds, book the sold total and every sold line once.</summary>
        private void SettleConfirmed(ResidentMeal meal, Money total, IReadOnlyList<FoodTicketLine> extras, string reason)
        {
            var unsold = meal.Price - total;
            if (meal.Budget != null) { HouseholdFinance.ReleasePart(meal.Budget, unsold); HouseholdFinance.ConfirmPurchase(meal.Budget); }
            else meal.DayAllowanceUsed -= unsold;
            meal.Price = total;
            ConfirmPurchase(meal, reason, extras);
        }
    }
}
