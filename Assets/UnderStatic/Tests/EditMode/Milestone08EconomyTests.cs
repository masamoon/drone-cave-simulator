using System.Collections.Generic;
using NUnit.Framework;
using UnderStatic.Core;
using UnderStatic.Economy;
using UnderStatic.Missions;
using UnderStatic.Parts;
using UnderStatic.UI;
using UnityEngine;

namespace UnderStatic.Tests.EditMode
{
    public sealed class Milestone08EconomyTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    Object.DestroyImmediate(created[index]);
                }
            }
            created.Clear();
        }

        [Test]
        public void SustainableProfile_ExposesRecoveryScarcityAndSalvageCadence()
        {
            var definition = Track(MarketDefinition.CreateTransient());

            Assert.That(definition.StartingFunds, Is.EqualTo(1100));
            Assert.That(definition.ScrapTokenValue, Is.EqualTo(18));
            Assert.That(definition.SaleFraction, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(definition.CompromisedSaleMultiplier, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(definition.TrustedReputation, Is.EqualTo(200));
            Assert.That(definition.ProfessionalReputation, Is.EqualTo(1000));
            Assert.That(definition.InitialSalvageCount, Is.EqualTo(3));
            Assert.That(definition.SortiesPerSalvageDelivery, Is.EqualTo(2));
            Assert.That(definition.SortieSalvageCount, Is.EqualTo(2));
            Assert.That(definition.DailySalvageCount, Is.EqualTo(2));
        }

        [Test]
        public void Compromise_ReducesResaleAndIsExplicitInServiceReadout()
        {
            var clean = CreatePart("clean", 200, 0.7f);
            var compromised = CreatePart("compromised", 200, 0.7f);
            compromised.SetCompromise(PartCompromiseRuntimeData.Create(
                PartCompromiseType.ReliabilityPenalty, 15));
            var market = Track(new GameObject("Market")).AddComponent<MarketSystem>();
            market.Configure(
                Track(MarketDefinition.CreateTransient()),
                null,
                null,
                new[] { clean, compromised },
                null,
                null);

            var cleanValue = market.CalculateLoosePartSaleValue(clean);
            var compromisedValue = market.CalculateLoosePartSaleValue(compromised);
            var inspection = ServiceInspectionPresenter.ForPart(compromised, true);

            Assert.That(compromisedValue, Is.LessThan(cleanValue));
            Assert.That(compromisedValue, Is.EqualTo(Mathf.RoundToInt(cleanValue * 0.7f)).Within(1));
            Assert.That(inspection.Severity, Is.EqualTo(ServiceInspectionSeverity.Compromised));
            Assert.That(inspection.Status, Is.EqualTo("COMPROMISED"));
            Assert.That(inspection.Detail, Does.Contain("-15% RELIABILITY"));
        }

        [Test]
        public void TunedMissionRewards_MatchFallbackAndUnlockTrustedStockAfterRecon()
        {
            var economy = Track(MissionEconomyDefinition.CreatePrototype());

            Assert.That(economy.IntelligenceReward + economy.ReconReward, Is.EqualTo(205));
            Assert.That(economy.InfantryReward, Is.EqualTo(950));
            Assert.That(economy.TankReward, Is.EqualTo(1450));
            Assert.That(economy.ArtilleryReward, Is.EqualTo(1350));
            Assert.That(economy.EnemyBaseReward, Is.EqualTo(2300));
            Assert.That(economy.RewardFor(EnemyActivityType.Artillery, false),
                Is.EqualTo(FrontlineSystem.StrikeRewardFor(EnemyActivityType.Artillery, false)));
            Assert.That(economy.IntelligenceReward + economy.ReconReward,
                Is.GreaterThanOrEqualTo(Track(MarketDefinition.CreateTransient()).TrustedReputation));
        }

        private InstallablePart CreatePart(string id, int value, float condition)
        {
            var definition = Track(PartDefinition.CreateTransient(
                $"definition.{id}",
                id,
                PartCategory.Motor,
                new[] { "motor.standard" },
                value: value));
            var root = Track(new GameObject(id));
            root.AddComponent<Rigidbody>();
            var part = root.AddComponent<InstallablePart>();
            part.Initialize(definition, id);
            part.SetCondition(condition);
            return part;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
