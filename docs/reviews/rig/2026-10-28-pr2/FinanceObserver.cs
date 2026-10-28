using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PersistentCity.Core;
using PersistentCity.Runtime;
using UnityEngine;

namespace PR2Rig {
// Rig-side read-only measurement. No sale, price, fixture guard or verifier tolerance is changed.
public static class FinanceObserver {
 static Harmony patches; static string output; static bool recording;
 static readonly Dictionary<string,JObject> attempts=new Dictionary<string,JObject>();
 static readonly List<string> faults=new List<string>();
 static int serial;
 static readonly JsonSerializer serializer=JsonSerializer.Create(new JsonSerializerSettings {
  ReferenceLoopHandling=ReferenceLoopHandling.Ignore, Converters={new RawFloatConverter()}});
 public static void Attach(string root){
  output=root; patches=new Harmony("persistentcity.pr2-rig-measurement");
  patches.Patch(AccessTools.Method(typeof(NativeFoodMarket),"Buy"),prefix:new HarmonyMethod(typeof(FinanceObserver),nameof(BeforeBuy)),postfix:new HarmonyMethod(typeof(FinanceObserver),nameof(AfterBuy)));
  patches.Patch(AccessTools.Method(typeof(FoodCommerce),"ConfirmPurchase"),postfix:new HarmonyMethod(typeof(FinanceObserver),nameof(Confirmed)));
 }
 public static void Detach(){recording=false;patches?.UnpatchAll("persistentcity.pr2-rig-measurement");}
 static void Guard(){if(!Environment.GetCommandLineArgs().Contains("-agent-tests") || !Application.persistentDataPath.EndsWith("Big Ambitions Agent Tests") || !SaveGameManager.Current.SaveGameName.StartsWith("FT Food Routines PR2 ",StringComparison.Ordinal))throw new Exception("PR2 disposable agent1 FT fixture required");}
 public static CitySimulator Simulation(){var r=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();return (CitySimulator)AccessTools.Field(typeof(PopulationRuntime),"simulation").GetValue(r);}
 public static object Status(){Guard();var sim=Simulation();double native=SaveGameManager.Current.Day*1440.0+SaveGameManager.Current.Hour*60+SaveGameManager.Current.Minute;return new{minute=sim.State.Minute,nativeMinute=native,caughtUp=sim.IsCaughtUpTo(native),routines=new{citizens=sim.State.Citizens.Count,consumers=sim.State.Food.Residents.Count,journeys=sim.State.Citizens.Sum(c=>c.CompletedJourneys)},active=sim.State.Food.Residents.Where(m=>m.Status==MealStatus.Serving || m.Status==MealStatus.Paid || m.Status==MealStatus.Uncertain).ToArray(),held=sim.State.Food.Residents.Sum(m=>m.Budget==null?0:Convert.ToDecimal(m.Budget.HeldPurchaseCents))};}
 static JToken Token(object value)=>value==null?JValue.CreateNull():JToken.FromObject(value,serializer);
 static void Append(string kind,object value){File.AppendAllText(output,JsonConvert.SerializeObject(new{kind,value},Formatting.None)+Environment.NewLine);}
 public static object Start(string root,string id){Guard();output=Path.Combine(root,id+"-financial.jsonl");attempts.Clear();faults.Clear();serial=0;recording=true;Append("start",Snapshot());return new{recording,output,fixture=SaveGameManager.Current.SaveGameName};}
 public static object Stop(){Guard();Append("end",Snapshot());recording=false;return new{recording,output,confirmed=serial,observerFaults=faults.ToArray(),pendingAttempts=attempts.Keys.ToArray()};}
 public static object Snapshot(){
  Guard();var sim=Simulation();var current=SaveGameManager.Current;
  var places=sim.State.Food.Shops.Select(s=>s.PlaceId).ToArray();
  var regs=CityManager.Instance.cityBuildingControllers.Where(c=>c!=null && c.buildingRegistration!=null && places.Contains(NativeFoodMarket.PlaceId(c.buildingRegistration))).Select(c=>c.buildingRegistration).ToArray();
  var runtime=UnityEngine.Object.FindObjectOfType<PopulationRuntime>();
  return new {minute=sim.State.Minute, nativeDay=current.Day,nativeHour=current.Hour,nativeMinute=Token(current.Minute),cash=Token(current.Money),
   transactions=Token(current.Transactions),accounts=Token(runtime.NativeEconomyAccounts),
   statements=Token(current.financialSummaries.Where(s=>s.dayNumber>=current.Day-3).Select(s=>new{s.dayNumber,s.businessIncomeStatements})),
   shops=regs.Select(r=>new{place=NativeFoodMarket.PlaceId(r),r.BusinessName,prices=Token(r.retailPrices),pending=Token(r.unprocessedCompletedOrders),history=Token(r.orderHistory),dailyIncomes=Token(r.dailyIncomes),lastDeposit=Token(r.lastDeposit)}).ToArray(),
   wallets=new{consumers=sim.State.Food.Residents.Count,purchases=sim.State.Food.Residents.Sum(m=>m.Purchases),spentCents=sim.State.Food.Residents.Sum(m=>Convert.ToDecimal(m.SpentCents)),
    preparedCents=sim.State.Food.Residents.Sum(m=>m.Budget==null?0:Convert.ToDecimal(m.Budget.PreparedFoodSpentCents)),heldCents=sim.State.Food.Residents.Sum(m=>m.Budget==null?0:Convert.ToDecimal(m.Budget.HeldPurchaseCents)),
    ledgerFailures=sim.State.Food.Residents.Count(m=>m.Budget!=null && Convert.ToDecimal(m.Budget.OpeningBalanceCents)+Convert.ToDecimal(m.Budget.IncomeReceivedCents)!=Convert.ToDecimal(m.Budget.BalanceCents)+Convert.ToDecimal(m.Budget.EssentialsPaidCents)+Convert.ToDecimal(m.Budget.HomeFoodPaidCents)+Convert.ToDecimal(m.Budget.PreparedFoodSpentCents)+Convert.ToDecimal(m.Budget.HeldPurchaseCents))},
   history=Token(sim.State.Food.History),receipts=current.modData.Where(p=>p.Key.StartsWith("persistentcity:food-sale-",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value),
   preventAutoSave=GameManager.preventAutoSave,saving=SaveGameManager.SavingGameInProgress,observerFaults=faults.ToArray()};
 }
 static void BeforeBuy(ResidentMeal meal){if(!recording)return;try{Guard();
  var reg=CityManager.Instance.cityBuildingControllers.Select(c=>c.buildingRegistration).First(r=>r!=null && NativeFoodMarket.PlaceId(r)==meal.PlaceId);
  attempts[meal.IntentId]=new JObject{["intent"]=meal.IntentId,["resident"]=meal.ResidentId,["priceCents"]=Token(meal.PriceCents),["heldCents"]=Token(meal.Budget?.HeldPurchaseCents),["balanceCents"]=Token(meal.Budget?.BalanceCents),["spentBeforeCents"]=Token(meal.Budget?.PreparedFoodSpentCents),["ordersBefore"]=reg.unprocessedCompletedOrders.Count,["nativePrice"]=Token(reg.retailPrices.Single(p=>p.itemName==meal.ProductId).price)};
 }catch(Exception e){faults.Add(e.ToString());}}
 static void AfterBuy(ResidentMeal meal,FoodSale __result){if(!recording)return;try{Guard();
  if(!attempts.TryGetValue(meal.IntentId,out var row))return;
  var reg=CityManager.Instance.cityBuildingControllers.Select(c=>c.buildingRegistration).First(r=>r!=null && NativeFoodMarket.PlaceId(r)==meal.PlaceId);
  row["paidResult"]=__result.Paid;row["ordersAfter"]=reg.unprocessedCompletedOrders.Count;
  row["newNativeOrders"]=Token(reg.unprocessedCompletedOrders.Skip((int)row["ordersBefore"]).ToArray());
  row["receipt"]=Token(SaveGameManager.Current.modData.Where(p=>p.Key.EndsWith(meal.IntentId,StringComparison.Ordinal) && p.Key.StartsWith("persistentcity:food-sale-",StringComparison.Ordinal)).ToDictionary(p=>p.Key,p=>p.Value));
  Append("checkout",row);
 }catch(Exception e){faults.Add(e.ToString());}}
 static void Confirmed(ResidentMeal meal){if(!recording)return;try{Guard();serial++;
  attempts.TryGetValue(meal.IntentId,out var row);
  Append("confirmed",new{sequence=serial,meal.IntentId,meal.ResidentId,day=SaveGameManager.Current.Day,hour=SaveGameManager.Current.Hour,minute=Token(SaveGameManager.Current.Minute),priceCents=Token(meal.PriceCents),spentCents=Token(meal.SpentCents),meal.Purchases,budget=Token(meal.Budget),checkout=row});
  attempts.Remove(meal.IntentId);
 }catch(Exception e){faults.Add(e.ToString());}}
 sealed class RawFloatConverter:JsonConverter {
  public override bool CanConvert(Type t)=>t==typeof(float)||t==typeof(double);
  public override void WriteJson(JsonWriter w,object value,JsonSerializer s){
   if(value is float f)new JObject{["type"]="binary32",["bits"]=BitConverter.ToUInt32(BitConverter.GetBytes(f),0),["roundTrip"]=f.ToString("R",CultureInfo.InvariantCulture)}.WriteTo(w);
   else {double d=(double)value;new JObject{["type"]="binary64",["bits"]=BitConverter.DoubleToInt64Bits(d),["roundTrip"]=d.ToString("R",CultureInfo.InvariantCulture)}.WriteTo(w);}
  }
  public override object ReadJson(JsonReader r,Type t,object old,JsonSerializer s)=>throw new NotSupportedException();
 }
}}
