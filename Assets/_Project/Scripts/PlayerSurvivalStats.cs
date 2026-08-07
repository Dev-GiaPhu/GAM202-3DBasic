using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace ZombieInfinite
{
    public sealed class PlayerSurvivalStats : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float medkitHealAmount = 45f;
        [SerializeField, Min(0f)] private float medkitUseDuration = 2f;
        [SerializeField, Min(0)] private int startingMedkits;

        [Header("Stamina")]
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float sprintDrainPerSecond = 24f;
        [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 18f;
        [SerializeField, Min(0f)] private float staminaRecoveryDelay = 1f;

        private float health;
        private float stamina;
        private float lastSprintTime = float.NegativeInfinity;
        private float medkitUseRemaining;
        private bool sprinting;
        private int medkits;

        public float Health => health;
        public float MaxHealth => maxHealth;
        public float Stamina => stamina;
        public float MaxStamina => maxStamina;
        public int MedkitCount => medkits;
        public bool IsUsingMedkit => medkitUseRemaining > 0f;
        public float MedkitUseRemaining => medkitUseRemaining;
        public float MedkitUseDuration => medkitUseDuration;
        public bool CanSprint => stamina > 0.01f && !IsUsingMedkit;
        public bool IsSprinting => sprinting;
        public event Action<float, float> Damaged;

        private void Awake()
        {
            health = maxHealth;
            stamina = maxStamina;
            medkits = startingMedkits;
        }

        private void Update()
        {
            UpdateStamina();
            UpdateMedkitUse();

            if (Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame)
            {
                TryUseMedkit();
            }
        }

        public void SetSprinting(bool value)
        {
            sprinting = value && CanSprint;
        }

        public void TakeDamage(float amount)
        {
            float appliedDamage = Mathf.Min(health, Mathf.Max(0f, amount));
            if (appliedDamage <= 0f)
            {
                return;
            }

            health -= appliedDamage;
            Damaged?.Invoke(appliedDamage, maxHealth);
        }

        public void AddMedkits(int amount)
        {
            medkits = Mathf.Max(0, medkits + Mathf.Max(0, amount));
        }

        public bool TryUseMedkit()
        {
            if (medkits <= 0 || IsUsingMedkit || health >= maxHealth)
            {
                return false;
            }

            medkitUseRemaining = Mathf.Max(0.01f, medkitUseDuration);
            return true;
        }

        private void UpdateStamina()
        {
            if (sprinting && stamina > 0f)
            {
                stamina = Mathf.Max(
                    0f,
                    stamina - sprintDrainPerSecond * Time.deltaTime);

                lastSprintTime = Time.time;
            }
            else if (Time.time >= lastSprintTime + staminaRecoveryDelay)
            {
                stamina = Mathf.Min(
                    maxStamina,
                    stamina + staminaRecoveryPerSecond * Time.deltaTime);
            }

            if (stamina <= 0f)
            {
                sprinting = false;
            }
        }

        private void UpdateMedkitUse()
        {
            if (medkitUseRemaining <= 0f)
            {
                return;
            }

            medkitUseRemaining -= Time.deltaTime;
            if (medkitUseRemaining > 0f)
            {
                return;
            }

            medkitUseRemaining = 0f;
            medkits--;
            health = Mathf.Min(maxHealth, health + medkitHealAmount);
            SpawnHealingEffect();
        }

        private void SpawnHealingEffect()
        {
            var effectObject = new GameObject("Green Healing Effect");
            effectObject.transform.position = transform.position + Vector3.up;
            effectObject.transform.SetParent(transform, true);

            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = 0.65f;
            main.startSpeed = 2f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.15f, 1f, 0.2f, 0.9f);
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f;

            particles.Play();
            Destroy(effectObject, 1.5f);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            medkitHealAmount = Mathf.Max(0f, medkitHealAmount);
            medkitUseDuration = Mathf.Max(0f, medkitUseDuration);
            startingMedkits = Mathf.Max(0, startingMedkits);
            maxStamina = Mathf.Max(1f, maxStamina);
            sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond);
            staminaRecoveryPerSecond = Mathf.Max(0f, staminaRecoveryPerSecond);
            staminaRecoveryDelay = Mathf.Max(0f, staminaRecoveryDelay);
        }
    }
}
