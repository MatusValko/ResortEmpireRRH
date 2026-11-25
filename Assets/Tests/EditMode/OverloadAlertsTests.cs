using NUnit.Framework;
using ResortEmpire.Buildings;

namespace ResortEmpire.Tests
{
    public sealed class OverloadAlertsTests
    {
        [Test]
        public void NoWarningWithoutOverload()
        {
            OverloadAlerts alerts = new(3);

            Assert.That(alerts.Refresh(new[] { false, false, false }, new[] { true, true, true }), Is.EqualTo(-1));
        }


        [Test]
        public void ShowsOverloadedBuildingWhereThePlayerCanHelp()
        {
            OverloadAlerts alerts = new(3);

            Assert.That(alerts.Refresh(new[] { true, true, false }, new[] { false, true, true }), Is.EqualTo(1));
        }


        [Test]
        public void KeepsTheShownWarningWhileItStaysValid()
        {
            OverloadAlerts alerts = new(3);
            alerts.Refresh(new[] { false, true, false }, new[] { true, true, true });

            Assert.That(alerts.Refresh(new[] { true, true, false }, new[] { true, true, true }), Is.EqualTo(1));
        }


        [Test]
        public void DismissedWarningStaysHiddenUntilTheQueueRecovers()
        {
            OverloadAlerts alerts = new(2);
            alerts.Refresh(new[] { true, false }, new[] { true, true });

            alerts.DismissCurrent();
            Assert.That(alerts.Refresh(new[] { true, false }, new[] { true, true }), Is.EqualTo(-1));

            alerts.Refresh(new[] { false, false }, new[] { true, true });
            Assert.That(alerts.Refresh(new[] { true, false }, new[] { true, true }), Is.EqualTo(0));
        }


        [Test]
        public void DismissingShowsTheNextOverloadedBuilding()
        {
            OverloadAlerts alerts = new(2);
            alerts.Refresh(new[] { true, true }, new[] { true, true });

            alerts.DismissCurrent();

            Assert.That(alerts.Refresh(new[] { true, true }, new[] { true, true }), Is.EqualTo(1));
        }


        [Test]
        public void ResetForgetsDismissedWarnings()
        {
            OverloadAlerts alerts = new(1);
            alerts.Refresh(new[] { true }, new[] { true });
            alerts.DismissCurrent();

            alerts.Reset();

            Assert.That(alerts.Refresh(new[] { true }, new[] { true }), Is.EqualTo(0));
        }


        [Test]
        public void StateCountMustMatchBuildings()
        {
            OverloadAlerts alerts = new(2);

            Assert.Throws<System.ArgumentException>(() => alerts.Refresh(new[] { true }, new[] { true }));
        }
    }
}
