using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace UnderStatic.Tests.PlayMode
{
    public sealed class Milestone08EconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator SafeHouseAudit_ProvesContinuousPartsAndRecoverableRepresentativeRuns()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var inventory = Object.FindAnyObjectByType<InventorySystem>();
            var fleet = Object.FindAnyObjectByType<FleetSystem>();
            var economy = Object.FindAnyObjectByType<FrontlineSystem>().Economy;
            var audit = EconomyBalanceSimulator.Audit(market, inventory, fleet, economy);

            Assert.That(audit.StartingFunds, Is.EqualTo(1100));
            Assert.That(audit.CurrentFunds, Is.EqualTo(1100));
            Assert.That(audit.HasContinuousBasicReconStock, Is.True);
            Assert.That(audit.BasicReconPartCost, Is.EqualTo(1260));
            Assert.That(audit.CheapestReadyAircraftCost, Is.EqualTo(850));
            Assert.That(audit.CheapestDamagedAircraftCost, Is.EqualTo(220));
            Assert.That(audit.PayloadCost, Is.EqualTo(140));
            Assert.That(audit.RoutineRepairReserve, Is.EqualTo(145));
            Assert.That(audit.RepresentativeRuns.Count, Is.EqualTo(3));
            Assert.That(audit.RepresentativeRuns.All(run => run.CanContinue), Is.True,
                audit.ToWorksheet());
            var lossHeavy = audit.RepresentativeRuns.Single(run =>
                run.Scenario == EconomyBalanceScenario.LossHeavy);
            Assert.That(lossHeavy.RecoveredFromLoss, Is.True, audit.ToWorksheet());
            Assert.That(lossHeavy.LowestFunds, Is.GreaterThanOrEqualTo(0), audit.ToWorksheet());
            var successful = audit.RepresentativeRuns.Single(run =>
                run.Scenario == EconomyBalanceScenario.Successful);
            Assert.That(successful.AffordableActionCount, Is.GreaterThanOrEqualTo(2), audit.ToWorksheet());
            Assert.That(audit.ToWorksheet(), Does.Contain("LossHeavy"));
        }

        [UnityTest]
        public IEnumerator RenewableFieldStock_RemainsAvailableAndPriceStableAcrossManyDays()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var required = new[]
            {
                PartCategory.Motor,
                PartCategory.Propeller,
                PartCategory.Battery,
                PartCategory.Camera,
                PartCategory.Antenna,
                PartCategory.Esc,
                PartCategory.FlightController
            };
            var stock = market.Listings.Where(item => item.category == MarketListingCategory.Part
                    && item.isRenewable
                    && market.ResolvePart(item)?.Definition?.Grade == EquipmentGrade.Field
                    && required.Contains(market.ResolvePart(item).Definition.Category))
                .GroupBy(item => market.ResolvePart(item).Definition.Category)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.askingPrice).First());
            var prices = stock.ToDictionary(item => item.Key, item => item.Value.askingPrice);

            Assert.That(stock.Keys, Is.EquivalentTo(required));
            for (var cycle = 1; cycle <= 24; cycle++)
            {
                market.AdvanceMarketCycle(8100 + cycle);
                foreach (var category in required)
                {
                    Assert.That(stock[category].isAvailable, Is.True, $"{category} disappeared on cycle {cycle}");
                    Assert.That(stock[category].askingPrice, Is.EqualTo(prices[category]),
                        $"{category} drifted on cycle {cycle}");
                }
            }
        }

        [UnityTest]
        public IEnumerator DamagedAircraft_DiscloseBoundedRestoreEstimateWithoutBecomingGuaranteedBargains()
        {
            SceneManager.LoadScene("SafeHouse", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var market = Object.FindAnyObjectByType<MarketSystem>();
            var ready = market.FindListing("market.stock.scout-field-ready");
            var damaged = market.Listings.Where(item => item.category == MarketListingCategory.SalvageDrone
                    && market.ResolveDrone(item)?.FrameDefinition?.AirframeClass == DroneAirframeClass.Compact)
                .ToArray();

            Assert.That(damaged, Is.Not.Empty);
            foreach (var listing in damaged)
            {
                var restore = market.CalculateExpectedRestoreCost(market.ResolveDrone(listing));
                Assert.That(restore, Is.GreaterThan(0), listing.listingId);
                Assert.That(restore, Is.LessThanOrEqualTo(1260), listing.listingId);
                Assert.That(listing.exactFaultsDisclosed, Is.False);
            }
            Assert.That(damaged.Any(listing => listing.askingPrice
                    + market.CalculateExpectedRestoreCost(market.ResolveDrone(listing)) < ready.askingPrice),
                Is.True, "At least one salvage route should offer a meaningful projected saving");
            Assert.That(damaged.Any(listing => listing.askingPrice
                    + market.CalculateExpectedRestoreCost(market.ResolveDrone(listing)) >= ready.askingPrice),
                Is.True, "Severe salvage must remain a risk instead of a guaranteed bargain");
        }
    }
}
