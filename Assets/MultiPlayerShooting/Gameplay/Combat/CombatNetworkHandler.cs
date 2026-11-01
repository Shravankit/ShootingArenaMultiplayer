using System;
using System.Collections.Generic;
using MultiplayerShooting.Core.Architecture;
using MultiplayerShooting.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace MultiplayerShooting.Gameplay.Combat
{
    public class CombatNetworkHandler : MonoBehaviour
    {
        public event Action LocalRespawned;
        public BulletTracer TracerPrefab { get; set; }

        private INetworkService net;
        private WeaponController localWeapon;
        private readonly Dictionary<string, HealthController> healthById = new();

        private ObjectPool<BulletTracer> tracerPool;

        private void Start()
        {
            net = ServiceRegistry.Get<INetworkService>();
            net.On("fire_result", OnFireResult);
            net.On("reload_result", OnReloadResult);
            net.On("combat_state", OnCombatState);
            net.On("health_update", OnHealthUpdate);
            net.On("player_died", OnPlayerDied);
            net.On("respawn", OnRespawn);
            net.On("shot_fired", OnShotFired);
        }

        private void OnDestroy()
        {
            if (net != null)
            {
                net.Off("fire_result", OnFireResult);
                net.Off("reload_result", OnReloadResult);
                net.Off("combat_state", OnCombatState);
                net.Off("health_update", OnHealthUpdate);
                net.Off("player_died", OnPlayerDied);
                net.Off("respawn", OnRespawn);
                net.Off("shot_fired", OnShotFired);
            }

            tracerPool?.Clear();
        }

        public void RegisterLocal(string id, WeaponController weapon, HealthController health)
        {
            localWeapon = weapon;
            Register(id, health);
        }

        public void Register(string id, HealthController health)
        {
            if (!string.IsNullOrEmpty(id) && health != null) healthById[id] = health;
        }

        public void Unregister(string id) => healthById.Remove(id);

        private ObjectPool<BulletTracer> GetTracerPool()
        {
            if (tracerPool != null) return tracerPool;

            tracerPool = new ObjectPool<BulletTracer>(
                createFunc: () => Instantiate(TracerPrefab),
                actionOnGet: t => t.gameObject.SetActive(true),
                actionOnRelease: t => t.gameObject.SetActive(false),
                actionOnDestroy: t => { if (t != null) Destroy(t.gameObject); },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 64);

            return tracerPool;
        }

        private void OnShotFired(JObject m)
        {
            if (TracerPrefab == null) return;

            JToken d = m["data"];
            JToken o = d?["origin"], dir = d?["direction"];
            if (o == null || dir == null) return;

            var origin = new Vector3((float)o["x"], (float)o["y"], (float)o["z"]);
            var forward = new Vector3((float)dir["x"], (float)dir["y"], (float)dir["z"]).normalized;

            // End the visual at the first thing it hits (player or wall), else at max range.
            Vector3 end = Physics.Raycast(origin, forward, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : origin + forward * 100f;

            ObjectPool<BulletTracer> pool = GetTracerPool();
            BulletTracer tracer = pool.Get();
            tracer.Launch(origin, end, pool.Release);
        }

        private void OnFireResult(JObject d)
        {
            if (d["accepted"] != null && !d["accepted"].Value<bool>())
            {
                Debug.Log("[Combat] shot rejected: " + d["reason"]);
                return;
            }
            localWeapon?.ApplyCombatState(d);
        }

        private void OnReloadResult(JObject d)
        {
            if (d["accepted"] != null && !d["accepted"].Value<bool>()) return;
            localWeapon?.ApplyCombatState(d);
        }

        private void OnCombatState(JObject d)
        {
            string id = (string)d["playerId"];
            if (string.IsNullOrEmpty(id)) return;

            if (d["health"] != null && healthById.TryGetValue(id, out var h))
                h.SetHealth(d["health"].Value<int>());

            if (id == net.PlayerId) localWeapon?.ApplyCombatState(d);
        }

        private void OnHealthUpdate(JObject d)
        {
            string id = (string)d["playerId"];
            if (id != null && d["health"] != null && healthById.TryGetValue(id, out var h))
                h.SetHealth(d["health"].Value<int>());
        }

        private void OnPlayerDied(JObject d) =>
            Debug.Log($"[Combat] {d["playerId"]} eliminated by {d["killerId"]}");

        private void OnRespawn(JObject d)
        {
            string id = (string)d["playerId"];
            if (string.IsNullOrEmpty(id)) return;

            if (healthById.TryGetValue(id, out var h))
                h.SetHealth(d["health"]?.Value<int>() ?? 100);

            if (id == net.PlayerId)
            {
                localWeapon?.ApplyCombatState(d);
                LocalRespawned?.Invoke();
            }
        }
    }
}