using System;
using System.Globalization;
using System.Reflection;
using PersistentCity.Core;

namespace PersistentCity.Compatibility
{
    /// <summary>
    /// Street Food's public vendor contract (<c>FoodTrucks.ModApi.VendorApi</c>, E6 plan 3.2), bound by name through
    /// public members only, the same way <see cref="RloBridge"/> binds RLO. Exactly contract 1 is accepted; any other
    /// version, or a missing or changed member, refuses vehicle scopes with a reason. There is no fallback into
    /// Street Food's private members. Main thread only.
    /// </summary>
    internal sealed class StreetFoodBridge : IVendorApi
    {
        public const string ApiTypeName = "FoodTrucks.ModApi.VendorApi";
        public const int SupportedContract = 1;
        public const string MissingReason = "Street Food vendor API missing.";

        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private readonly Type vehicleType, saleType;
        private readonly Func<long> revision;
        private readonly Func<int> version;
        private readonly MethodInfo read, requestOwner, renew, releaseAll, sell;
        private readonly FieldInfo vehicleId, kind, open, openingId, openingOwner, openedByCrew, eventOrCatering, openedDay, openedMinute, closesDay, closesMinute,
            x, y, z, queueX, queueY, queueZ, queueHeading, mealsPerHour, menuOverflow, items, priceCents, units;
        private readonly FieldInfo bookedDay, saleCents, unitsBefore, unitsAfter, sequence;
        private readonly Array view;
        private readonly object?[] readArgs = new object?[2], ownerArgs = new object?[4], renewArgs = new object?[5], releaseArgs = new object?[1], sellArgs = new object?[10];
        private int viewCount;

        public int ContractVersion { get; }

        private StreetFoodBridge(Type api)
        {
            string space = api.Namespace + ".";
            Type status = api.Assembly.GetType(space + "VendorApiStatus", false) ?? throw Invalid("missing public type VendorApiStatus");
            vehicleType = api.Assembly.GetType(space + "VendorApiVehicle", false) ?? throw Invalid("missing public type VendorApiVehicle");
            saleType = api.Assembly.GetType(space + "VendorApiSale", false) ?? throw Invalid("missing public type VendorApiSale");
            if (!api.IsPublic || !api.IsAbstract || !api.IsSealed) throw Invalid("VendorApi must be a public static class");
            if (!status.IsPublic || !status.IsEnum || Enum.GetUnderlyingType(status) != typeof(int)) throw Invalid("VendorApiStatus must be a public Int32 enum");
            foreach (VendorApiStatus known in Enum.GetValues(typeof(VendorApiStatus)))
                if (!Enum.IsDefined(status, known.ToString()) || Convert.ToInt32(Enum.Parse(status, known.ToString()), CultureInfo.InvariantCulture) != (int)known)
                    throw Invalid("VendorApiStatus." + known + " is missing or has another value");
            foreach (var dto in new[] { vehicleType, saleType })
                if (!dto.IsPublic || !dto.IsClass || !dto.IsSealed) throw Invalid(dto.Name + " must be a public sealed class");

            version = Getter<int>(api, "ContractVersion");
            ContractVersion = version();
            if (ContractVersion != SupportedContract) throw Unsupported(ContractVersion);
            revision = Getter<long>(api, "Revision");

            vehicleId = Field(vehicleType, "VehicleId", typeof(string)); kind = Field(vehicleType, "Kind", typeof(string));
            open = Field(vehicleType, "Open", typeof(bool)); openingId = Field(vehicleType, "OpeningId", typeof(long));
            openingOwner = Field(vehicleType, "OpeningOwner", typeof(string)); openedByCrew = Field(vehicleType, "OpenedByCrew", typeof(bool));
            eventOrCatering = Field(vehicleType, "EventOrCatering", typeof(bool));
            openedDay = Field(vehicleType, "OpenedDay", typeof(int)); openedMinute = Field(vehicleType, "OpenedMinute", typeof(int));
            closesDay = Field(vehicleType, "ClosesDay", typeof(int)); closesMinute = Field(vehicleType, "ClosesMinute", typeof(int));
            x = Field(vehicleType, "X", typeof(float)); y = Field(vehicleType, "Y", typeof(float)); z = Field(vehicleType, "Z", typeof(float));
            queueX = Field(vehicleType, "QueueX", typeof(float)); queueY = Field(vehicleType, "QueueY", typeof(float));
            queueZ = Field(vehicleType, "QueueZ", typeof(float)); queueHeading = Field(vehicleType, "QueueHeading", typeof(float));
            mealsPerHour = Field(vehicleType, "MealsPerHour", typeof(int)); menuOverflow = Field(vehicleType, "MenuOverflow", typeof(bool));
            items = Field(vehicleType, "Items", typeof(string[])); priceCents = Field(vehicleType, "PriceCents", typeof(long[])); units = Field(vehicleType, "Units", typeof(int[]));
            bookedDay = Field(saleType, "BookedDay", typeof(int)); saleCents = Field(saleType, "Cents", typeof(long));
            unitsBefore = Field(saleType, "UnitsBefore", typeof(int)); unitsAfter = Field(saleType, "UnitsAfter", typeof(int));
            sequence = Field(saleType, "Sequence", typeof(long));

            Type outInt = typeof(int).MakeByRefType(), outString = typeof(string).MakeByRefType();
            read = Method(api, "Read", status, new[] { 1 }, vehicleType.MakeArrayType(), outInt);
            requestOwner = Method(api, "RequestOwner", status, new[] { 3 }, typeof(string), typeof(string), typeof(bool), outString);
            renew = Method(api, "Renew", status, new[] { 3, 4 }, typeof(string), typeof(int), typeof(int), outInt, outInt);
            releaseAll = Method(api, "ReleaseAll", status, new int[0], typeof(string));
            sell = Method(api, "Sell", status, new[] { 7, 8, 9 }, typeof(string), typeof(string), typeof(long), typeof(string), typeof(long), typeof(int), typeof(int),
                saleType.MakeByRefType(), typeof(bool).MakeByRefType(), outString);
            view = Array.CreateInstance(vehicleType, VendorImage.MaximumVehicles);
            readArgs[0] = view;
        }

        /// <summary>Null when the assembly has no VendorApi (a Street Food release from before the contract). Throws when it
        /// has one that Persistent City cannot use.</summary>
        public static StreetFoodBridge? Find(Assembly assembly)
        {
            Type? api = assembly.GetType(ApiTypeName, false);
            return api == null ? null : Bind(api);
        }

        // Tests bind contract-shaped doubles from their own assembly; the game always goes through Find or Open.
        internal static StreetFoodBridge Bind(Type api)
        {
            try { return new StreetFoodBridge(api); }
            catch (Exception error) when (!(error is InvalidOperationException)) { throw Invalid("could not bind", error); }
        }

        /// <summary>The bridge, or null with the reason vehicles stay with Street Food. Never throws.</summary>
        public static StreetFoodBridge? Open(Assembly? streetFood, out string reason)
        {
            if (streetFood == null) { reason = "Street Food is not loaded; " + MissingReason; return null; }
            try
            {
                var bridge = Find(streetFood);
                reason = bridge == null ? MissingReason : "";
                return bridge;
            }
            catch (InvalidOperationException error) { reason = error.Message; return null; }
        }

        public long Revision => revision();

        /// <summary>Asks Street Food for its contract version again. Throws, as binding does, if it has changed.</summary>
        public void CheckContractVersion()
        {
            int current;
            try { current = version(); }
            catch (Exception error) { throw Invalid("could not bind", error); }
            if (current != SupportedContract) throw Unsupported(current);
        }

        public VendorApiStatus BeginRead(out int count)
        {
            Array.Clear(view, 0, view.Length);
            readArgs[1] = 0;
            var status = Call(read, readArgs);
            count = (int)readArgs[1]!;
            if (status == VendorApiStatus.Refused && count != 0) throw Invalid("Read refused with a non-zero count");
            if (status != VendorApiStatus.Ready) { viewCount = 0; count = 0; return status; }
            if (count < 0 || count > VendorImage.MaximumVehicles) throw Invalid("Read returned " + count.ToString(CultureInfo.InvariantCulture) + " vehicles");
            viewCount = count;
            return status;
        }

        public void CopyVehicle(int index, VendorVehicle into)
        {
            if ((uint)index >= (uint)viewCount) throw new ArgumentOutOfRangeException(nameof(index));
            object? v = view.GetValue(index);
            if (v == null || v.GetType() != vehicleType) throw Invalid("Read returned an unexpected element");
            into.VehicleId = (string?)vehicleId.GetValue(v) ?? ""; into.Kind = (string?)kind.GetValue(v) ?? "";
            into.Open = (bool)open.GetValue(v)!; into.OpeningId = (long)openingId.GetValue(v)!;
            into.OpeningOwner = (string?)openingOwner.GetValue(v) ?? "\0"; // null owner fails the image check
            into.OpenedByCrew = (bool)openedByCrew.GetValue(v)!; into.EventOrCatering = (bool)eventOrCatering.GetValue(v)!;
            into.OpenedDay = (int)openedDay.GetValue(v)!; into.OpenedMinute = (int)openedMinute.GetValue(v)!;
            into.ClosesDay = (int)closesDay.GetValue(v)!; into.ClosesMinute = (int)closesMinute.GetValue(v)!;
            into.X = (float)x.GetValue(v)!; into.Y = (float)y.GetValue(v)!; into.Z = (float)z.GetValue(v)!;
            into.QueueX = (float)queueX.GetValue(v)!; into.QueueY = (float)queueY.GetValue(v)!; into.QueueZ = (float)queueZ.GetValue(v)!;
            into.QueueHeading = (float)queueHeading.GetValue(v)!;
            into.MealsPerHour = (int)mealsPerHour.GetValue(v)!; into.MenuOverflow = (bool)menuOverflow.GetValue(v)!;
            var names = (string[]?)items.GetValue(v); var prices = (long[]?)priceCents.GetValue(v); var counts = (int[]?)units.GetValue(v);
            into.ShapeFault = "";
            if (names == null || prices == null || counts == null) { into.ItemCount = 0; into.ShapeFault = "null menu arrays"; return; }
            if (names.Length != prices.Length || names.Length != counts.Length) { into.ItemCount = 0; into.ShapeFault = "menu arrays of different lengths"; return; }
            into.ItemCount = names.Length;
            if (names.Length > VendorVehicle.MaximumItems) return; // the image check refuses it
            for (int i = 0; i < names.Length; i++) { into.Items[i] = names[i]; into.PriceCents[i] = prices[i]; into.Units[i] = counts[i]; }
            for (int i = names.Length; i < VendorVehicle.MaximumItems; i++) { into.Items[i] = null!; into.PriceCents[i] = 0; into.Units[i] = 0; }
        }

        public VendorApiStatus RequestOwner(string vehicleId, bool want, out string reason)
        {
            ownerArgs[0] = VendorNames.Owner; ownerArgs[1] = vehicleId; ownerArgs[2] = want; ownerArgs[3] = null;
            var status = Call(requestOwner, ownerArgs);
            reason = (string?)ownerArgs[3] ?? "";
            return status;
        }

        public VendorApiStatus Renew(int nativeDay, int nativeMinute, out int leaseUntilDay, out int leaseUntilMinute)
        {
            renewArgs[0] = VendorNames.Owner; renewArgs[1] = nativeDay; renewArgs[2] = nativeMinute; renewArgs[3] = 0; renewArgs[4] = 0;
            var status = Call(renew, renewArgs);
            leaseUntilDay = (int)renewArgs[3]!; leaseUntilMinute = (int)renewArgs[4]!;
            return status;
        }

        public VendorApiStatus ReleaseAll()
        {
            releaseArgs[0] = VendorNames.Owner;
            return Call(releaseAll, releaseArgs);
        }

        public VendorApiStatus Sell(string vehicleId, long openingId, string item, long priceCents, int nativeDay, int nativeMinute,
            out VendorSaleResult? sale, out bool uncertain, out string reason)
        {
            sellArgs[0] = VendorNames.Owner; sellArgs[1] = vehicleId; sellArgs[2] = openingId; sellArgs[3] = item; sellArgs[4] = priceCents;
            sellArgs[5] = nativeDay; sellArgs[6] = nativeMinute; sellArgs[7] = null; sellArgs[8] = false; sellArgs[9] = null;
            var status = Call(sell, sellArgs);
            object? result = sellArgs[7];
            uncertain = (bool)sellArgs[8]!; reason = (string?)sellArgs[9] ?? "";
            if (result == null) { sale = null; return status; }
            if (result.GetType() != saleType) throw Invalid("Sell returned an unexpected sale");
            sale = new VendorSaleResult((int)bookedDay.GetValue(result)!, (long)saleCents.GetValue(result)!, (int)unitsBefore.GetValue(result)!,
                (int)unitsAfter.GetValue(result)!, (long)sequence.GetValue(result)!);
            return status;
        }

        private static VendorApiStatus Call(MethodInfo method, object?[] args)
        {
            object? result;
            try { result = method.Invoke(null, args); }
            catch (TargetInvocationException error) { throw Invalid(method.Name + " threw, which the contract forbids", error.InnerException ?? error); }
            int value = Convert.ToInt32(result, CultureInfo.InvariantCulture);
            if (!Enum.IsDefined(typeof(VendorApiStatus), value)) throw Invalid(method.Name + " returned unknown status " + value.ToString(CultureInfo.InvariantCulture));
            return (VendorApiStatus)value;
        }

        // A static property read through a typed delegate: the per-minute revision poll does not box or allocate.
        private static Func<T> Getter<T>(Type api, string name)
        {
            var property = api.GetProperty(name, PublicStatic);
            if (property == null || property.PropertyType != typeof(T) || property.GetMethod == null || !property.GetMethod.IsPublic || property.GetIndexParameters().Length != 0)
                throw Invalid("missing public static " + typeof(T).Name + " VendorApi." + name);
            return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), property.GetMethod);
        }

        private static FieldInfo Field(Type type, string name, Type expected)
        {
            var field = type.GetField(name, PublicInstance);
            if (field == null || field.FieldType != expected) throw Invalid("missing or changed field " + type.Name + "." + name);
            return field;
        }

        private static MethodInfo Method(Type api, string name, Type returns, int[] outs, params Type[] parameters)
        {
            var method = api.GetMethod(name, PublicStatic, null, parameters, null);
            if (method == null || method.ReturnType != returns) throw Invalid("missing or changed method VendorApi." + name);
            var declared = method.GetParameters();
            for (int i = 0; i < declared.Length; i++)
                if (declared[i].IsOut != (Array.IndexOf(outs, i) >= 0)) throw Invalid("missing or changed method VendorApi." + name);
            return method;
        }

        private static InvalidOperationException Unsupported(int contract) =>
            Invalid("unsupported contract " + contract.ToString(CultureInfo.InvariantCulture) + "; Persistent City reads contract " + SupportedContract.ToString(CultureInfo.InvariantCulture));

        internal static InvalidOperationException Invalid(string message, Exception? inner = null) =>
            new InvalidOperationException("Street Food VendorApi: " + message + ".", inner);
    }
}
