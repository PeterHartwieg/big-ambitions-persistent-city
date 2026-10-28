using System;
using System.Linq;
using Newtonsoft.Json;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;
public sealed class FoodLiveChoiceTests
{
    [Fact]
    public void DirectChoiceMatchesValidatedExperimentAcrossCompetingOffersAndRefusals()
    {
        for (int seed = 0; seed < 240; seed++)
        {
            var person = new FoodConsumer { Id="r", Name="Resident", OriginPlaceId="home", DailyAllowance=Money.FromCents(500+seed%20*100),
                ValueOfTimeCentsPerMinute=seed%10, ServiceQualityValueCents=200, DistrictAppealValueCents=100 };
            var intent = new FoodIntent { Id=FoodDemand.IntentId("r",1), ConsumerId="r", Day=1, OriginPlaceId="home", OriginActivity=Activity.Home,
                RequestedAt=2160, DesiredAt=2160, Deadline=2160+30+seed%90, EatingMinutes=15, MaximumQueueMinutes=10,
                TimeCostMultiplier=1.2, OutsideOption=FoodOutsideOption.HomePreparation, OutsideOptionScoreCents=700+seed%30*100 };
            var shops=Enumerable.Range(0,8).Select(n=>new FoodBusiness { Id="b"+n, PlaceId="shop"+n, Name="Shop "+n, DistrictId="district",
                MealsPerHour=(seed+n)%17==0?null:(seed+n)%5*15, CustomerServicePercent=n*10, NextServiceMinute=2160+(seed+n)%30,
                Hours=(seed+n)%11==0?new():Enumerable.Range(0,7).Select(d=>new OpeningWindow {Day=d,FromMinute=480,ToMinute=1080}).ToList(),
                Offers=new() {new FoodOffer {ProductId="pastry", Price=Money.FromCents(500+(seed+n)%20*100),Units=(seed+n)%19==0?null:(seed+n)%4,DistrictDemandPercent=50}} }).ToArray();
            var quotes=shops.Where((_,n)=>(seed+n)%13!=0).Select((b,n)=>new FoodTravelQuote { ConsumerId="r", IntentId=intent.Id,
                OriginPlaceId="home", BusinessId=b.Id, Minutes=(seed+n)%23==0?null:(seed+n)%35+.01,ReturnMinutes=(seed+n)%29==0?null:(seed+n)%30+.01,Evidence="Controlled paths" }).ToArray();
            string inputs=JsonConvert.SerializeObject(new {person,shops,quotes});
            var shadow=FoodDemand.CreateObserved("choice-test","Controlled comparison",1,1,new[]{person},shops,quotes,new[]{intent});
            new FoodDemand(shadow).AdvanceTo(2160,1);
            FoodDemand.ChooseObserved(person,shops,quotes,intent,2160);
            Assert.Equal(JsonConvert.SerializeObject(shadow.Intents.Single()),JsonConvert.SerializeObject(intent));
            Assert.Equal(inputs,JsonConvert.SerializeObject(new {person,shops,quotes}));
        }
    }
}
