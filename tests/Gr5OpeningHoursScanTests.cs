using System;
using System.Collections.Generic;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

public sealed class Gr5OpeningHoursScanTests
{
    private static List<OpeningWindow> RandomHours(Random random)
    {
        var hours = new List<OpeningWindow>();
        int count = random.Next(0, 12);
        for (int i = 0; i < count; i++)
        {
            int from = random.Next(0, 1441), to;
            switch (random.Next(5))
            {
                case 0: to = from; break;
                case 1: to = random.Next(0, Math.Max(1, from)); break;
                case 2: from = 0; to = 1440; break;
                default: to = random.Next(from, 1441); break;
            }
            hours.Add(new OpeningWindow { Day = random.Next(0, 7), FromMinute = from, ToMinute = to });
        }
        return hours;
    }

    private static Place PlaceOf(List<OpeningWindow> hours) => new Place { Kind = PlaceKind.Shop, OpenMinute = 0, CloseMinute = 0, WeeklyHours = hours };

    [Fact]
    public void WeeklyHoursAnswerNextOpenNextCloseAndOpenLikeTheCachedPlaceIntervals()
    {
        var random = new Random(4021);
        int checkedCases = 0, open = 0, infinite = 0;
        for (int sample = 0; sample < 3000; sample++)
        {
            var hours = RandomHours(random);
            if (sample % 10 == 0)
                for (int d = 0; d < 7; d++) hours.Add(new OpeningWindow { Day = d, FromMinute = 0, ToMinute = 1440 });
            var place = PlaceOf(hours);
            for (int probe = 0; probe < 12; probe++)
            {
                double minute = random.Next(0, 40 * 1440) + (random.Next(4) == 0 ? 0 : random.NextDouble());
                double before = minute + (random.Next(6) == 0 ? 0 : random.NextDouble() * 4 * 1440);
                Assert.Equal(DailyRoutine.NextOpen(place, minute, before), DailyRoutine.NextOpen(hours, minute, before));
                var close = DailyRoutine.NextClose(place, minute);
                Assert.Equal(close, DailyRoutine.NextClose(hours, minute));
                Assert.Equal(DailyRoutine.Open(place, minute), DailyRoutine.Open(hours, minute));
                checkedCases++;
                if (close.HasValue) open++;
                if (close == double.PositiveInfinity) infinite++;
            }
        }
        Assert.True(checkedCases == 36000 && open > 3000 && infinite > 100, checkedCases + " cases, " + open + " open, " + infinite + " always open");
    }

    [Fact]
    public void ChainedAndOvernightWindowsMergeAcrossMidnightLikeThePlaceIntervals()
    {
        var hours = new List<OpeningWindow>
        {
            new OpeningWindow { Day = 1, FromMinute = 1200, ToMinute = 120 },
            new OpeningWindow { Day = 2, FromMinute = 120, ToMinute = 600 },
            new OpeningWindow { Day = 2, FromMinute = 600, ToMinute = 600 },
            new OpeningWindow { Day = 2, FromMinute = 540, ToMinute = 900 },
            new OpeningWindow { Day = 3, FromMinute = 0, ToMinute = 1440 },
        };
        var place = PlaceOf(hours);
        for (double minute = 0; minute < 14 * 1440; minute += 17.25)
        {
            Assert.Equal(DailyRoutine.NextClose(place, minute), DailyRoutine.NextClose(hours, minute));
            Assert.Equal(DailyRoutine.NextOpen(place, minute, minute + 2000), DailyRoutine.NextOpen(hours, minute, minute + 2000));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyRoutine.NextOpen(hours, 100, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyRoutine.NextClose(hours, double.NaN));
        Assert.Null(DailyRoutine.NextOpen(new List<OpeningWindow>(), 10, 20));
        Assert.Null(DailyRoutine.NextClose(new List<OpeningWindow>(), 10));
    }

    [Fact]
    public void WeeklyHourScansAllocateNothing()
    {
        var hours = RandomHours(new Random(7));
        hours.Add(new OpeningWindow { Day = 2, FromMinute = 480, ToMinute = 1320 });
        double sink = 0;
        for (int i = 0; i < 100; i++) sink += DailyRoutine.NextOpen(hours, 3 * 1440 + i, 6 * 1440) ?? 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20000; i++)
        {
            sink += DailyRoutine.NextOpen(hours, 3 * 1440 + i % 1440, 6 * 1440) ?? 0;
            sink += DailyRoutine.NextClose(hours, 3 * 1440 + i % 1440) ?? 0;
            if (DailyRoutine.Open(hours, 3 * 1440 + i % 1440)) sink++;
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sink > 0);
    }
}
