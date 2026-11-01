
using UnityEngine;

namespace MultiplayerShooting.Gameplay.Combat
{
    [CreateAssetMenu(
        fileName = "Rifle",
        menuName = "Multiplayer Shooting/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        public string weaponId = "rifle";
        public float fireRate = 10f;
        public int magazineSize = 30;
        public float range = 100f;
        public float damage = 25f;
    }
}
