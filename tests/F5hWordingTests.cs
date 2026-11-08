using System;
using System.Linq;
using System.Text.RegularExpressions;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests;

/// <summary>F5h (#92): panel and inspector words for persons, households, workplaces and visitors. Words only: the panel
/// prints the three numbers it printed before, and the inspector adds no count of its own.</summary>
public sealed class F5hWordingTests
{
    [Fact]
    public void PanelHeadlineShowsOnlyTheNumbersThePanelAlreadyHad()
    {
        Assert.Equal("Day 3 · 957 residents · 1575 visitors · 2532 people with meals", PopulationWords.Headline(3, 957, 1575, 2532));
        Assert.Equal("Day 1 · 1 resident · 1 visitor · 1 person with meals", PopulationWords.Headline(1, 1, 1, 1));
        Assert.Equal("Day 0 · 12 residents · 0 visitors · 12 people with meals", PopulationWords.Headline(0, 12, 0, 12));
        // The only digits are the four values passed in.
        Assert.Equal(new[] { "3", "957", "1575", "2532" }, Regex.Matches(PopulationWords.Headline(3, 957, 1575, 2532), "[0-9]+").Select(m => m.Value));
    }

    [Fact]
    public void PanelLinesDescribeHouseholdsWorkplacesAndVisitorsInWordsWithoutCounts()
    {
        string withVisitors = PopulationWords.Lines(1575, 2);
        Assert.Contains(PopulationWords.Households, withVisitors);
        Assert.Contains(PopulationWords.Workplaces, withVisitors);
        Assert.Contains(PopulationWords.Visitors, withVisitors);
        Assert.Contains(PopulationWords.Wallets, withVisitors);
        Assert.DoesNotContain(PopulationWords.NoStation, withVisitors);
        // A city without a saved portal says why it has no visitors; one with a portal and no visitors yet says nothing.
        string noStation = PopulationWords.Lines(0, 0);
        Assert.Contains(PopulationWords.NoStation, noStation); Assert.DoesNotContain(PopulationWords.Visitors, noStation);
        string notYet = PopulationWords.Lines(0, 2);
        Assert.DoesNotContain(PopulationWords.NoStation, notYet); Assert.DoesNotContain(PopulationWords.Visitors, notYet);
        // Words only: no number, ID or key appears in any line.
        foreach (string text in new[] { withVisitors, noStation, notYet })
        {
            Assert.DoesNotMatch("[0-9]", text);
            Assert.DoesNotContain("pv:", text); Assert.DoesNotContain("h:", text); Assert.DoesNotContain("=", text);
        }
        // The visitor sentence states the F5g rules and that visitors have no body before F5i.
        Assert.Contains("by subway", PopulationWords.Visitors);
        Assert.Contains("weekdays", PopulationWords.Visitors); Assert.Contains("weekends", PopulationWords.Visitors);
        Assert.Contains("not shown on the street", PopulationWords.Visitors);
    }

    [Fact]
    public void InspectorNamesTheSharedHomeAndNeverSelectsAVisitor()
    {
        var (state, market) = F5gPopulationFillTests.City(12, 1, seed: 5150);
        var city = F5gPopulationFillTests.Start(state, market, F5gPopulationFillTests.Catalogue(12, F5gPopulationFillTests.Base, F5gPopulationFillTests.Since(3)));
        F5gPopulationFillTests.Advance(city, 2 * 1440 + 600);
        Assert.NotEmpty(state.Visitors);
        bool shared = false, alone = false;
        foreach (var person in state.Citizens)
        {
            var insight = city.Inspect(person.Identity.Id)!;
            int household = state.Citizens.Count(c => c.HomeId == person.HomeId);
            Assert.Equal(household, insight.HouseholdSize);
            Assert.StartsWith("Resident", insight.PersonWords);
            if (household == 1) { alone = true; Assert.Contains("; lives alone", insight.PersonWords); }
            else { shared = true; Assert.Contains("; household of " + household + " sharing one home", insight.PersonWords); }
            if (person.DayPlaceId.Length > 0)
                Assert.Contains((person.Identity.Persona == Persona.Student ? "; studying" : "; employed") + " since day " + person.EmploymentStartDay, insight.PersonWords);
            Assert.EndsWith(".", insight.PersonWords);
            Assert.DoesNotContain(person.HomeId, insight.PersonWords); Assert.DoesNotContain("h:", insight.PersonWords);
        }
        Assert.True(shared);
        // A resident whose home no one else shares lives alone (the fixture's seven-bed homes are all shared).
        if (!alone)
        {
            var solo = state.Citizens[0]; solo.HomeId = "home:solo";
            Assert.Equal("Resident; lives alone" + (solo.DayPlaceId.Length > 0 ? "; " + (solo.Identity.Persona == Persona.Student ? "studying" : "employed")
                + " since day " + solo.EmploymentStartDay : "") + ".", city.Inspect(solo.Identity.Id)!.PersonWords);
        }
        // Visitors have no body before F5i, so the inspector never opens on one.
        Assert.All(state.Visitors, v => Assert.Null(city.Inspect(v.Id)));
        // The panel's headline counts on this city are the ones the panel printed before F5h.
        Assert.Equal("Day 2 · " + state.Citizens.Count + " residents · " + state.Visitors.Count + " visitors · " + state.Food!.Residents.Count + " people with meals",
            PopulationWords.Headline((int)(state.Minute / 1440), state.Citizens.Count, state.Visitors.Count, state.Food.Residents.Count));
        Assert.Equal(state.Citizens.Count + state.Visitors.Count, state.Food.Residents.Count);
    }
}
