using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Core;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnityEngine;

namespace UnderStatic.Economy
{
    public enum EconomyBalanceScenario
    {
        Successful,
        Mixed,
        LossHeavy
    }

    public readonly struct EconomyBalanceRun
    {
        public EconomyBalanceRun(
            EconomyBalanceScenario scenario,
            int endingFunds,
            int lowestFunds,
            bool recoveredFromLoss,
            bool canContinue,
            int affordableActionCount)
        {
            Scenario = scenario;
            EndingFunds = Mathf.Max(0, endingFunds);
            LowestFunds = lowestFunds;
            RecoveredFromLoss = recoveredFromLoss;
            CanContinue = canContinue;
            AffordableActionCount = Mathf.Max(0, affordableActionCount);
        }

        public EconomyBalanceScenario Scenario { get; }
        public int EndingFunds { get; }
        public int LowestFunds { get; }
        public bool RecoveredFromLoss { get; }
        public bool CanContinue { get; }
        public int AffordableActionCount { get; }
    }

    public sealed class EconomyBalanceAudit
    {
        public int StartingFunds { get; internal set; }
        public int CurrentFunds { get; internal set; }
        public int Reputation { get; internal set; }
        public int ScrapCount { get; internal set; }
        public int ScrapLiquidationValue { get; internal set; }
        public int LooseInventorySaleValue { get; internal set; }
        public int FleetComponentValue { get; internal set; }
        public int BasicReconPartCost { get; internal set; }
        public int CheapestReadyAircraftCost { get; internal set; }
        public int CheapestDamagedAircraftCost { get; internal set; }
        public int PayloadCost { get; internal set; }
        public int RoutineRepairReserve { get; internal set; }
        public bool HasContinuousBasicReconStock { get; internal set; }
        public IReadOnlyList<string> ViableNextActions { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<EconomyBalanceRun> RepresentativeRuns { get; internal set; }
            = Array.Empty<EconomyBalanceRun>();

        public string ToWorksheet()
        {
            var lines = new List<string>
            {
                "# Sustainable Workshop Economy Audit",
                string.Empty,
                $"Starting funds: {StartingFunds}",
                $"Basic reconnaissance parts: {BasicReconPartCost} (continuous: {HasContinuousBasicReconStock})",
                $"Ready aircraft: {CheapestReadyAircraftCost}",
                $"Damaged aircraft: {CheapestDamagedAircraftCost}",
                $"Sealed payload: {PayloadCost}",
                $"Routine repair reserve: {RoutineRepairReserve}",
                $"Owned value: loose {LooseInventorySaleValue} + fleet {FleetComponentValue} + scrap {ScrapLiquidationValue}",
                string.Empty,
                "Representative three-day runs:"
            };
            lines.AddRange(RepresentativeRuns.Select(run =>
                $"- {run.Scenario}: end {run.EndingFunds}, low {run.LowestFunds}, " +
                $"continue {run.CanContinue}, actions {run.AffordableActionCount}"));
            lines.Add(string.Empty);
            lines.Add("Viable next actions:");
            lines.AddRange(ViableNextActions.Select(action => $"- {action}"));
            return string.Join("\n", lines);
        }
    }

    public static class EconomyBalanceSimulator
    {
        public static EconomyBalanceAudit Audit(
            MarketSystem market,
            InventorySystem inventory,
            FleetSystem fleet,
            MissionEconomyDefinition missionEconomy)
        {
            if (market?.Definition == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            missionEconomy ??= MissionEconomyDefinition.CreatePrototype();
            var basicCost = market.CalculateBasicReconPartCost(out var continuousStock);
            var readyCost = CheapestListing(market, MarketListingCategory.CompleteDrone);
            var damagedCost = CheapestListing(market, MarketListingCategory.SalvageDrone);
            var payloadCost = market.CheapestRenewablePartPrice(PartCategory.Payload);
            var routineReserve = market.CheapestRenewablePartPrice(PartCategory.Motor)
                + market.CheapestRenewablePartPrice(PartCategory.Propeller);
            var currentFunds = market.Funds;
            var actions = ViableActions(
                market,
                inventory,
                fleet,
                currentFunds,
                readyCost,
                damagedCost,
                payloadCost,
                routineReserve);
            var inputs = new SimulationInputs(
                market.Definition.StartingFunds,
                readyCost,
                damagedCost,
                payloadCost,
                routineReserve,
                market.Definition.DailySalvageCount * market.ScrapTokenValue,
                missionEconomy.IntelligenceReward + missionEconomy.ReconReward,
                missionEconomy.InfantryReward,
                Mathf.RoundToInt(missionEconomy.ArtilleryReward * missionEconomy.PartialRewardMultiplier),
                continuousStock);

            return new EconomyBalanceAudit
            {
                StartingFunds = market.Definition.StartingFunds,
                CurrentFunds = currentFunds,
                Reputation = market.Reputation,
                ScrapCount = inventory?.ScrapCount ?? 0,
                ScrapLiquidationValue = (inventory?.ScrapCount ?? 0) * market.ScrapTokenValue,
                LooseInventorySaleValue = inventory?.Parts
                    .Where(part => part != null && !part.Runtime.isSalvaged
                        && part.Runtime.storageLocation != StorageLocationId.MarketStock)
                    .Sum(market.CalculateLoosePartSaleValue) ?? 0,
                FleetComponentValue = fleet?.Actors.Sum(actor => actor?.Stats.ComponentValue ?? 0) ?? 0,
                BasicReconPartCost = basicCost,
                CheapestReadyAircraftCost = readyCost,
                CheapestDamagedAircraftCost = damagedCost,
                PayloadCost = payloadCost,
                RoutineRepairReserve = routineReserve,
                HasContinuousBasicReconStock = continuousStock,
                ViableNextActions = actions,
                RepresentativeRuns = new[]
                {
                    Simulate(EconomyBalanceScenario.Successful, inputs),
                    Simulate(EconomyBalanceScenario.Mixed, inputs),
                    Simulate(EconomyBalanceScenario.LossHeavy, inputs)
                }
            };
        }

        private static EconomyBalanceRun Simulate(
            EconomyBalanceScenario scenario,
            SimulationInputs inputs)
        {
            var funds = inputs.StartingFunds;
            var lowest = funds;
            var recovered = scenario != EconomyBalanceScenario.LossHeavy;
            switch (scenario)
            {
                case EconomyBalanceScenario.Successful:
                    Apply(ref funds, ref lowest,
                        inputs.ReconIncome + inputs.InfantryReward - inputs.PayloadCost - inputs.RoutineReserve);
                    Apply(ref funds, ref lowest,
                        inputs.ReconIncome + inputs.InfantryReward - inputs.PayloadCost - inputs.RoutineReserve);
                    Apply(ref funds, ref lowest,
                        inputs.ReconIncome - inputs.RoutineReserve);
                    break;
                case EconomyBalanceScenario.Mixed:
                    Apply(ref funds, ref lowest, inputs.ReconIncome - inputs.RoutineReserve);
                    Apply(ref funds, ref lowest, -inputs.PayloadCost - inputs.RoutineReserve);
                    Apply(ref funds, ref lowest,
                        inputs.ReconIncome + inputs.PartialArtilleryReward - inputs.PayloadCost
                        - inputs.RoutineReserve + inputs.DailySalvageCash);
                    break;
                case EconomyBalanceScenario.LossHeavy:
                    Apply(ref funds, ref lowest, -inputs.ReadyAircraftCost);
                    recovered = funds >= 0 && inputs.ReadyAircraftCost > 0;
                    Apply(ref funds, ref lowest, inputs.ReconIncome - inputs.RoutineReserve);
                    Apply(ref funds, ref lowest,
                        inputs.ReconIncome + inputs.PartialArtilleryReward - inputs.PayloadCost
                        - inputs.RoutineReserve + inputs.DailySalvageCash);
                    Apply(ref funds, ref lowest, inputs.ReconIncome - inputs.RoutineReserve);
                    break;
            }

            var availableFunds = Mathf.Max(0, funds);
            var actions = AffordableActionCount(availableFunds, inputs);
            var canContinue = lowest >= 0
                && inputs.HasContinuousBasicStock
                && availableFunds >= Mathf.Max(1, inputs.RoutineReserve);
            return new EconomyBalanceRun(
                scenario,
                availableFunds,
                lowest,
                recovered,
                canContinue,
                actions);
        }

        private static IReadOnlyList<string> ViableActions(
            MarketSystem market,
            InventorySystem inventory,
            FleetSystem fleet,
            int funds,
            int readyCost,
            int damagedCost,
            int payloadCost,
            int routineReserve)
        {
            var actions = new List<string>();
            if (routineReserve > 0 && funds >= routineReserve)
            {
                actions.Add("Replace a field motor and propeller");
            }
            if (readyCost > 0 && funds >= readyCost && fleet?.HasFreeLockerSlot == true)
            {
                actions.Add("Buy a ready field aircraft");
            }
            if (damagedCost > 0 && funds >= damagedCost && fleet?.HasFreeLockerSlot == true)
            {
                actions.Add("Buy a damaged donor aircraft");
            }
            if (payloadCost > 0 && funds >= payloadCost
                && market.Listings.Any(item => item.category == MarketListingCategory.Part
                    && item.isRenewable
                    && market.ResolvePart(item)?.Definition?.Category == PartCategory.Payload))
            {
                actions.Add("Reserve a sealed payload");
            }
            if (inventory?.Parts.Any(part => part != null && part.Compromise.IsPresent) == true)
            {
                actions.Add("Install, hold, strip, or sell compromised salvage");
            }
            return actions;
        }

        private static int AffordableActionCount(int funds, SimulationInputs inputs)
        {
            var costs = new[]
            {
                inputs.RoutineReserve,
                inputs.PayloadCost,
                inputs.DamagedAircraftCost,
                inputs.ReadyAircraftCost
            };
            return costs.Count(cost => cost > 0 && funds >= cost);
        }

        private static int CheapestListing(MarketSystem market, MarketListingCategory category) =>
            market.Listings.Where(item => item.category == category
                    && item.isAvailable
                    && market.IsUnlocked(item))
                .Select(item => item.askingPrice)
                .DefaultIfEmpty(0)
                .Min();

        private static void Apply(ref int funds, ref int lowest, int delta)
        {
            funds += delta;
            lowest = Mathf.Min(lowest, funds);
        }

        private readonly struct SimulationInputs
        {
            public SimulationInputs(
                int startingFunds,
                int readyAircraftCost,
                int damagedAircraftCost,
                int payloadCost,
                int routineReserve,
                int dailySalvageCash,
                int reconIncome,
                int infantryReward,
                int partialArtilleryReward,
                bool hasContinuousBasicStock)
            {
                StartingFunds = startingFunds;
                ReadyAircraftCost = readyAircraftCost;
                DamagedAircraftCost = damagedAircraftCost;
                PayloadCost = payloadCost;
                RoutineReserve = routineReserve;
                DailySalvageCash = dailySalvageCash;
                ReconIncome = reconIncome;
                InfantryReward = infantryReward;
                PartialArtilleryReward = partialArtilleryReward;
                HasContinuousBasicStock = hasContinuousBasicStock;
            }

            public int StartingFunds { get; }
            public int ReadyAircraftCost { get; }
            public int DamagedAircraftCost { get; }
            public int PayloadCost { get; }
            public int RoutineReserve { get; }
            public int DailySalvageCash { get; }
            public int ReconIncome { get; }
            public int InfantryReward { get; }
            public int PartialArtilleryReward { get; }
            public bool HasContinuousBasicStock { get; }
        }
    }
}
