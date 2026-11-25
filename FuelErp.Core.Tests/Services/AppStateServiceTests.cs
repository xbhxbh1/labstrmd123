using FluentAssertions;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.Tests.TestSupport;

namespace FuelErp.Tests.Services;

public class AppStateServiceTests
{
    [Fact]
    public void SaveThenRestore_BringsBackTheUnsavedForm()
    {
        var settings = new FakeSettings();
        var first = new AppStateService(settings)
        {
            Draft = new RefuelingDraft { VehicleId = 3, TankId = 2, DriverName = "Чернетка", LitersText = "45", Latitude = 50.1 }
        };
        first.Save();

        var second = new AppStateService(settings);
        var restored = 0;
        second.StateRestored += (_, _) => restored++;
        second.Restore();

        second.Draft.Should().NotBeNull();
        second.Draft!.VehicleId.Should().Be(3);
        second.Draft.DriverName.Should().Be("Чернетка");
        second.Draft.LitersText.Should().Be("45");
        second.Draft.Latitude.Should().Be(50.1);
        restored.Should().Be(1);
    }

    [Fact]
    public void Save_EmptyDraft_ClearsTheStoredValue()
    {
        var settings = new FakeSettings();
        new AppStateService(settings) { Draft = new RefuelingDraft { DriverName = "X" } }.Save();

        new AppStateService(settings) { Draft = new RefuelingDraft() }.Save();
        var restored = new AppStateService(settings);
        restored.Restore();

        restored.Draft.Should().BeNull();
    }

    [Fact]
    public void Restore_CorruptedJson_IsIgnored()
    {
        var settings = new FakeSettings();
        settings.Set("refueling_draft_json", "{ це не json");
        var state = new AppStateService(settings);

        state.Restore();

        state.Draft.Should().BeNull();
    }

    [Fact]
    public void PendingDeepLink_RaisesTheEventOnlyWhenSet()
    {
        var state = new AppStateService(new FakeSettings());
        var raised = 0;
        state.DeepLinkReceived += (_, _) => raised++;

        state.PendingDeepLink = "fuelerp://refueling/1";
        state.PendingDeepLink = null;

        raised.Should().Be(1);
    }

    [Theory]
    [InlineData(null, null, "", "", "", "", true)]
    [InlineData(1, null, "", "", "", "", false)]
    [InlineData(null, null, "Іван", "", "", "", false)]
    [InlineData(null, null, "", "  ", "", "", true)]
    public void RefuelingDraft_IsEmpty_ReflectsTheFilledFields(int? vehicle, int? tank, string driver, string liters, string price, string notes, bool expected)
    {
        new RefuelingDraft { VehicleId = vehicle, TankId = tank, DriverName = driver, LitersText = liters, PriceText = price, Notes = notes }
            .IsEmpty.Should().Be(expected);
    }
}
