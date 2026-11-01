using System;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerShooting.Gameplay.Combat
{
    public class HealthController : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private Slider healthSlider;

        public event Action<bool> DeadChanged; // true = died, false = alive again

        public int CurrentHealth { get; private set; } = 100;
        public bool IsDead { get; private set; }

        public void SetHealth(int health)
        {
            bool wasDead = IsDead;
            CurrentHealth = Mathf.Clamp(health, 0, maxHealth);
            IsDead = CurrentHealth <= 0;

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = CurrentHealth;
            }

            if (IsDead != wasDead) DeadChanged?.Invoke(IsDead);
        }
    }
}