using PersistentCity.Runtime;
int checks = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
void Refuses(Action call) { try { call(); } catch (ArgumentException) { checks++; return; } throw new Exception("Expected refusal"); }
var native = new NeighborhoodData { neighbourhood = "home", workingClassPercentage = 70, middleClassPercentage = 22, upperClassClassPercentage = 8 };
NeighborhoodHelper.NeighborhoodsData = new[] { native, new NeighborhoodData { neighbourhood = "shop", workingClassPercentage = 20, middleClassPercentage = 45, upperClassClassPercentage = 35 } };
var first = FoodHouseholdCatalogue.Read(new[] { "home" });
Check(first.Count == 1 && first[0].DistrictId == "home" && first[0].WorkingPercent == 70, "Reads the selected home, not shopping district");
first[0].WorkingPercent = 1; Check(native.workingClassPercentage == 70, "Snapshot does not write native composition");
native.workingClassPercentage = 69; native.middleClassPercentage = 23;
Check(FoodHouseholdCatalogue.Read(new[] { "home" })[0].MiddlePercent == 23, "Fresh observation sees source changes");
Check(first[0].MiddlePercent == 22, "Old snapshot stays detached");
Check(FoodHouseholdCatalogue.Read(new[] { "missing" }).Count == 0, "No fallback to a global or shopping district");
Check(FoodHouseholdCatalogue.Read(Array.Empty<string>()).Count == 0, "Empty selection remains empty");
native.upperClassClassPercentage = 9; Refuses(() => FoodHouseholdCatalogue.Read(new[] { "home" }));
native.upperClassClassPercentage = 8;
NeighborhoodHelper.NeighborhoodsData = new[] { native, native }; Refuses(() => FoodHouseholdCatalogue.Read(new[] { "home" }));
NeighborhoodHelper.NeighborhoodsData = null; Check(FoodHouseholdCatalogue.Read(new[] { "home" }).Count == 0, "Unloaded data is unknown");
Refuses(() => FoodHouseholdCatalogue.Read(new[] { "" }));
Refuses(() => FoodHouseholdCatalogue.Read(Enumerable.Range(0, 101).Select(i => "d" + i).ToArray()));
Console.WriteLine($"{checks} actual native household-reader checks passed against field doubles; no game execution.");
public sealed class NeighborhoodData
{ public string neighbourhood = ""; public int workingClassPercentage, middleClassPercentage, upperClassClassPercentage; }
public static class NeighborhoodHelper { public static NeighborhoodData[]? NeighborhoodsData; }
