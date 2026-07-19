using System;
using System.Collections.Generic;
using System.Linq;
using UnderStatic.Fleet;
using UnderStatic.Inventory;
using UnderStatic.Missions;
using UnityEngine;

namespace UnderStatic.Workshop
{
    public enum FieldSiteAttentionState
    {
        Safe,
        Exposed,
        Danger,
        Search,
        ForcedRetreat
    }

    [Serializable]
    public sealed class FieldSiteRuntimeData
    {
        public string siteId = "remote-cache";
        public BattlefieldMapPoint position = new(new Vector2(0.30f, 0.18f));
        public float attention;
        public int visits;
        public int hotUntilDay;
        public bool relayActive;
        public string cachedDroneId = string.Empty;

        public FieldSiteRuntimeData Copy() => (FieldSiteRuntimeData)MemberwiseClone();
    }

    [Serializable]
    public sealed class SalvageCacheRuntimeData
    {
        public string cacheId = string.Empty;
        public BattlefieldMapPoint position;
        public int remainingTokens;
        public int expiresAfterDay;
        public float attention;
        public int visits;
        public int hotUntilDay;

        public SalvageCacheRuntimeData Copy() => (SalvageCacheRuntimeData)MemberwiseClone();
    }

    [Serializable]
    public sealed class FieldOperationsRuntimeData
    {
        public FieldSiteRuntimeData remoteSite = new();
        public SalvageCacheRuntimeData[] salvageCaches = Array.Empty<SalvageCacheRuntimeData>();
        public bool remoteDeploymentPlanned;

        public FieldOperationsRuntimeData Copy() => new()
        {
            remoteSite = remoteSite?.Copy() ?? new FieldSiteRuntimeData(),
            salvageCaches = salvageCaches?.Where(item => item != null).Select(item => item.Copy()).ToArray()
                ?? Array.Empty<SalvageCacheRuntimeData>(),
            remoteDeploymentPlanned = remoteDeploymentPlanned
        };
    }

    [DisallowMultipleComponent]
    public sealed class FieldOperationsSystem : MonoBehaviour
    {
        public const string WorkshopSiteId = "workshop";
        public const string RemoteSiteId = "remote-cache";
        public const int CarryCapacity = 4;

        [SerializeField] private BattlefieldSystem battlefield;
        [SerializeField] private MissionSystem missions;
        [SerializeField] private FleetSystem fleet;
        [SerializeField] private InventorySystem inventory;
        [SerializeField] private WorkshopRiskSystem workshopRisk;
        [SerializeField] private OperationalDaySystem day;
        [SerializeField] private FieldOperationsRuntimeData runtime = new();
        [SerializeField] private FieldExcursionDirector director;

        public FieldOperationsRuntimeData Runtime => runtime;
        public FieldSiteRuntimeData RemoteSite => runtime.remoteSite;
        public IReadOnlyList<SalvageCacheRuntimeData> SalvageCaches => runtime.salvageCaches;
        public string LastStatus { get; private set; } = "Field operations available";

        public void Configure(
            BattlefieldSystem battlefieldSystem,
            MissionSystem missionSystem,
            FleetSystem fleetSystem,
            InventorySystem inventorySystem,
            WorkshopRiskSystem riskSystem,
            OperationalDaySystem daySystem)
        {
            Unsubscribe();
            battlefield = battlefieldSystem;
            missions = missionSystem;
            fleet = fleetSystem;
            inventory = inventorySystem;
            workshopRisk = riskSystem;
            day = daySystem;
            runtime = new FieldOperationsRuntimeData();
            runtime.remoteSite.position = new BattlefieldMapPoint(new Vector2(0.30f, 0.18f));
            if (missions != null) missions.MissionResolved += HandleMissionResolved;
            if (day != null) day.DayBegan += HandleDayBegan;
        }

        public void ConfigureDirector(FieldExcursionDirector excursionDirector) => director = excursionDirector;

        public bool TryGetLaunchPosition(string siteId, out Vector2 position)
        {
            if (string.Equals(siteId, RemoteSiteId, StringComparison.Ordinal))
            {
                position = runtime.remoteSite.position.ToVector2();
                return true;
            }
            position = BattlefieldSystem.WorkshopPosition;
            return string.Equals(siteId, WorkshopSiteId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(siteId);
        }

        public bool CanUseRemoteSite => day != null
            && runtime.remoteSite.hotUntilDay < day.Runtime.dayIndex
            && string.IsNullOrWhiteSpace(runtime.remoteSite.cachedDroneId)
            && missions?.ActiveMission == null;

        public DroneActor StagedDrone => fleet?.ReadyDrone;

        public bool StageRemoteDeployment()
        {
            if (!CanUseRemoteSite || fleet?.ReadyDrone == null
                || !string.Equals(missions?.Draft.launchSiteId, RemoteSiteId, StringComparison.Ordinal)
                || missions.EvaluateDraft().Eligible == false)
            {
                LastStatus = "Remote plan cannot be staged";
                return false;
            }
            runtime.remoteDeploymentPlanned = true;
            LastStatus = "Remote plan staged · carry the deployment case to the concealed exit";
            return true;
        }

        public bool BeginRemoteDeployment()
        {
            if (!runtime.remoteDeploymentPlanned || !CanUseRemoteSite || fleet?.ReadyDrone == null || director == null)
            {
                LastStatus = "Remote deployment unavailable";
                return false;
            }
            Enter(runtime.remoteSite);
            return director.BeginDeployment(this, missions, runtime.remoteSite);
        }

        public bool BeginRemoteDroneRecovery()
        {
            var actor = fleet?.FieldDrones.FirstOrDefault(item =>
                string.Equals(item.Runtime.droneInstanceId, runtime.remoteSite.cachedDroneId, StringComparison.Ordinal));
            if (actor == null || director == null || runtime.remoteSite.hotUntilDay >= day.Runtime.dayIndex
                || fleet.HasWorkshopStorageForFieldRecovery == false)
            {
                LastStatus = "Remote drone recovery unavailable";
                return false;
            }
            Enter(runtime.remoteSite);
            return director.BeginDroneRecovery(this, runtime.remoteSite, actor);
        }

        public bool BeginSalvageRecovery(string cacheId)
        {
            var cache = FindCache(cacheId);
            if (cache == null || cache.remainingTokens <= 0 || cache.hotUntilDay >= day.Runtime.dayIndex || director == null)
            {
                LastStatus = "Salvage recovery unavailable";
                return false;
            }
            Enter(cache);
            return director.BeginSalvage(this, cache);
        }

        public void AddSiteAttention(string siteId, float amount)
        {
            if (string.Equals(siteId, RemoteSiteId, StringComparison.Ordinal))
            {
                runtime.remoteSite.attention = Mathf.Clamp(runtime.remoteSite.attention + Mathf.Max(0f, amount), 0f, 100f);
                if (runtime.remoteSite.attention >= 100f) runtime.remoteSite.hotUntilDay = day.Runtime.dayIndex;
                return;
            }
            var cache = FindCache(siteId);
            if (cache == null) return;
            cache.attention = Mathf.Clamp(cache.attention + Mathf.Max(0f, amount), 0f, 100f);
            if (cache.attention >= 100f) cache.hotUntilDay = day.Runtime.dayIndex;
        }

        public float AttentionFor(string siteId) => string.Equals(siteId, RemoteSiteId, StringComparison.Ordinal)
            ? runtime.remoteSite.attention
            : FindCache(siteId)?.attention ?? 0f;

        public FieldSiteAttentionState AttentionState(string siteId)
        {
            return StateForAttention(AttentionFor(siteId));
        }

        public FieldSiteAttentionState ForecastAttentionState(string siteId) =>
            StateForAttention(ForecastEntryAttention(siteId));

        private static FieldSiteAttentionState StateForAttention(float value) =>
            value >= 100f ? FieldSiteAttentionState.ForcedRetreat
                : value >= 75f ? FieldSiteAttentionState.Search
                : value >= 50f ? FieldSiteAttentionState.Danger
                : value >= 25f ? FieldSiteAttentionState.Exposed
                : FieldSiteAttentionState.Safe;

        public void CompleteRemoteLaunch(MissionRuntimeData mission)
        {
            runtime.remoteDeploymentPlanned = false;
            runtime.remoteSite.relayActive = true;
            AddSiteAttention(RemoteSiteId, 15f);
            LastStatus = $"Remote launch active · {mission?.assignedDroneId}";
        }

        public bool CacheReturnedDrone(DroneActor actor, string siteId)
        {
            if (!string.Equals(siteId, RemoteSiteId, StringComparison.Ordinal)
                || fleet?.TryRecoverDeployedToField(actor, siteId) != true)
            {
                return false;
            }
            runtime.remoteSite.cachedDroneId = actor.Runtime.droneInstanceId;
            runtime.remoteSite.relayActive = false;
            LastStatus = $"{actor.FrameDefinition.DisplayName} awaiting field recovery";
            return true;
        }

        public bool RecoverDrone(DroneActor actor)
        {
            if (actor == null || fleet?.TryRecoverFieldDroneToWorkshop(actor) != true)
            {
                LastStatus = fleet?.LastStatus ?? "Drone recovery failed";
                return false;
            }
            runtime.remoteSite.cachedDroneId = string.Empty;
            AddSiteAttention(RemoteSiteId, 15f);
            LastStatus = fleet.LastStatus;
            return true;
        }

        public bool AbandonStagedDrone(DroneActor actor)
        {
            if (actor == null || fleet?.TryCacheReadyAtField(actor, RemoteSiteId) != true)
            {
                return false;
            }
            runtime.remoteDeploymentPlanned = false;
            runtime.remoteSite.cachedDroneId = actor.Runtime.droneInstanceId;
            runtime.remoteSite.relayActive = false;
            LastStatus = $"{actor.FrameDefinition.DisplayName} left at the compromised field site";
            return true;
        }

        public SalvageCacheRuntimeData CreateSalvageCache(
            string missionId,
            Vector2 position,
            int tokens)
        {
            if (tokens <= 0) return null;
            var id = $"salvage.{missionId}";
            var existing = FindCache(id);
            if (existing != null) return existing;
            var cache = new SalvageCacheRuntimeData
            {
                cacheId = id,
                position = new BattlefieldMapPoint(position),
                remainingTokens = tokens,
                expiresAfterDay = day.Runtime.dayIndex + 1
            };
            runtime.salvageCaches = runtime.salvageCaches.Append(cache).ToArray();
            LastStatus = $"Recoverable salvage marked · {tokens} token(s)";
            return cache;
        }

        public int RecoverSalvage(string cacheId, int requested)
        {
            var cache = FindCache(cacheId);
            var recovered = Mathf.Clamp(requested, 0, Mathf.Min(CarryCapacity, cache?.remainingTokens ?? 0));
            if (recovered <= 0) return 0;
            cache.remainingTokens -= recovered;
            inventory?.AwardScrap(recovered, $"field cache {cacheId}");
            AddSiteAttention(cacheId, recovered * 5f);
            LastStatus = $"Recovered {recovered} salvage token(s)";
            return recovered;
        }

        public bool SecureSalvage(string cacheId)
        {
            var cache = FindCache(cacheId);
            if (cache == null || cache.remainingTokens <= 0) return false;
            cache.remainingTokens--;
            AddSiteAttention(cacheId, 5f);
            LastStatus = "Salvage secured for return";
            return true;
        }

        public int CommitSecuredSalvage(string cacheId, int count)
        {
            var committed = Mathf.Max(0, count);
            if (committed <= 0) return 0;
            inventory?.AwardScrap(committed, $"field cache {cacheId}");
            LastStatus = $"Returned with {committed} salvage token(s)";
            return committed;
        }

        public float ForecastEntryAttention(string siteId)
        {
            if (string.Equals(siteId, RemoteSiteId, StringComparison.Ordinal))
            {
                return Mathf.Clamp(runtime.remoteSite.attention + 10f
                    + Mathf.Min(20f, runtime.remoteSite.visits * 5f)
                    + NearbyKnownThreats(runtime.remoteSite.position.ToVector2()) * 10f, 0f, 100f);
            }
            var cache = FindCache(siteId);
            return cache == null ? 0f : Mathf.Clamp(cache.attention + 10f
                + Mathf.Min(20f, cache.visits * 5f)
                + NearbyKnownThreats(cache.position.ToVector2()) * 10f, 0f, 100f);
        }

        public float CompleteExcursion(string siteId, bool forced)
        {
            var attention = AttentionFor(siteId);
            var trace = forced ? 12f : attention >= 75f ? 7f : attention >= 50f ? 3f : 0f;
            workshopRisk?.AddExposure(trace, WorkshopExposureSource.FieldTrace);
            if (forced)
            {
                if (string.Equals(siteId, RemoteSiteId, StringComparison.Ordinal))
                    runtime.remoteSite.hotUntilDay = day.Runtime.dayIndex;
                else if (FindCache(siteId) is { } cache) cache.hotUntilDay = day.Runtime.dayIndex;
            }
            return trace;
        }

        public FieldOperationsRuntimeData CaptureState() => runtime.Copy();
        public bool CanRestore(FieldOperationsRuntimeData restored) => restored?.remoteSite != null
            && restored.salvageCaches != null
            && string.Equals(restored.remoteSite.siteId, RemoteSiteId, StringComparison.Ordinal)
            && restored.remoteSite.attention is >= 0f and <= 100f
            && restored.salvageCaches.All(item => item != null && item.remainingTokens >= 0
                && item.expiresAfterDay >= 0 && !string.IsNullOrWhiteSpace(item.cacheId)
                && item.attention is >= 0f and <= 100f)
            && restored.salvageCaches.Select(item => item.cacheId)
                .Distinct(StringComparer.Ordinal).Count() == restored.salvageCaches.Length;

        public bool RestoreState(FieldOperationsRuntimeData restored)
        {
            if (!CanRestore(restored))
            {
                LastStatus = "Field operations load rejected";
                return false;
            }
            runtime = restored.Copy();
            LastStatus = "Field operations restored";
            return true;
        }

        private void Update()
        {
            var active = missions?.ActiveMission;
            if (active?.state == MissionRuntimeState.Active
                && string.Equals(active.plan.launchSiteId, RemoteSiteId, StringComparison.Ordinal)
                && runtime.remoteSite.relayActive)
            {
                AddSiteAttention(RemoteSiteId, 0.15f * Time.deltaTime);
            }
        }

        private void Enter(FieldSiteRuntimeData site)
        {
            site.attention = Mathf.Clamp(site.attention + 10f + Mathf.Min(20f, site.visits * 5f)
                + NearbyKnownThreats(site.position.ToVector2()) * 10f, 0f, 100f);
            site.visits++;
        }

        private void Enter(SalvageCacheRuntimeData site)
        {
            site.attention = Mathf.Clamp(site.attention + 10f + Mathf.Min(20f, site.visits * 5f)
                + NearbyKnownThreats(site.position.ToVector2()) * 10f, 0f, 100f);
            site.visits++;
        }

        private int NearbyKnownThreats(Vector2 position) => battlefield?.VisibleContacts.Count(item =>
            item.IntelState is BattlefieldIntelState.Current or BattlefieldIntelState.Stale
            && BattlefieldSystem.MapDistanceKilometres(position, item.Position) <= 0.5f) ?? 0;

        private SalvageCacheRuntimeData FindCache(string id) => runtime.salvageCaches.FirstOrDefault(item =>
            string.Equals(item.cacheId, id, StringComparison.Ordinal));

        private void HandleMissionResolved(MissionRuntimeData mission)
        {
            if (mission?.plan != null && string.Equals(mission.plan.launchSiteId, RemoteSiteId, StringComparison.Ordinal))
            {
                runtime.remoteSite.relayActive = false;
            }
        }

        private void HandleDayBegan(int currentDay)
        {
            runtime.remoteSite.attention = Mathf.Max(0f, runtime.remoteSite.attention - 20f);
            runtime.salvageCaches = runtime.salvageCaches
                .Where(item => item.remainingTokens > 0 && item.expiresAfterDay >= currentDay)
                .Select(item =>
                {
                    item.attention = Mathf.Max(0f, item.attention - 20f);
                    return item;
                }).ToArray();
        }

        private void Unsubscribe()
        {
            if (missions != null) missions.MissionResolved -= HandleMissionResolved;
            if (day != null) day.DayBegan -= HandleDayBegan;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
