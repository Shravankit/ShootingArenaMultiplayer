using MultiplayerShooting.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MultiplayerShooting.Gameplay.Combat
{
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private Transform muzzle; // optional: gun muzzle transform
        [SerializeField] private Vector3 fallbackMuzzleOffset = new Vector3(0.4f, 0.5f, 0.6f);
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private AudioSource fireAudio;

        private INetworkService net;
        private Camera cam;
        private Transform player;
        private float nextShotTime;
        private int shotId;
        private int ammo = 30;
        private int reserveAmmo = 90;

        public bool Locked { get; set; }
        public int Ammo => ammo;
        public int ReserveAmmo => reserveAmmo;

        public void Init(INetworkService service)
        {
            net = service;
            player = GetComponentInParent<LocalPlayer>().transform;
            if (weapon == null)
                Debug.LogError("[Weapon] Assign Rifle.asset to WeaponController in LocalPlayer.prefab");
        }

        private void Update()
        {
            if (weapon == null || net == null || Locked) return;

            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            if (Input.GetButton("Fire1")) TryFire();
            if (Input.GetKeyDown(KeyCode.R)) net.Send(new JObject { ["t"] = "reload_request" });
        }

        private Vector3 MuzzlePosition() =>
            muzzle != null ? muzzle.position : player.TransformPoint(fallbackMuzzleOffset);

        // Point under the crosshair, ignoring the player's own colliders.
        private Vector3 AimPoint()
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, weapon.range + 10f, ~0, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            Vector3 point = ray.origin + ray.direction * weapon.range;

            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(player)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    point = h.point;
                }
            }
            return point;
        }

        private void TryFire()
        {
            if (Time.time < nextShotTime || ammo <= 0) return;
            nextShotTime = Time.time + 1f / weapon.fireRate;
            ammo--; // predicted; the server value overwrites it

            if (muzzleFlash != null) muzzleFlash.Play();
            if (fireAudio != null) fireAudio.Play();

            Vector3 origin = MuzzlePosition();
            Vector3 aim = AimPoint();
            Vector3 dir = aim - origin;

            // Target is behind or on top of the muzzle: fall back to the camera direction.
            if (dir.sqrMagnitude < 0.25f || Vector3.Dot(dir, cam.transform.forward) < 0f)
                dir = cam.transform.forward;
            dir.Normalize();

            shotId++;

            net.Send(new JObject
            {
                ["t"] = "fire_request",
                ["data"] = new JObject
                {
                    ["weaponId"] = weapon.weaponId,
                    ["shotId"] = shotId,
                    ["origin"] = new JObject { ["x"] = origin.x, ["y"] = origin.y, ["z"] = origin.z },
                    ["direction"] = new JObject { ["x"] = dir.x, ["y"] = dir.y, ["z"] = dir.z }
                }
            });
        }

        public void ApplyCombatState(JObject data)
        {
            if (data["ammo"] != null) ammo = data["ammo"].Value<int>();
            if (data["reserveAmmo"] != null) reserveAmmo = data["reserveAmmo"].Value<int>();
        }
    }
}