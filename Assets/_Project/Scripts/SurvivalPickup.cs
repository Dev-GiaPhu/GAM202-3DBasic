using UnityEngine;

namespace ZombieInfinite
{
    public sealed class SurvivalPickup : MonoBehaviour
    {
        public enum PickupType { Ammo, Medkit }

        [SerializeField] private PickupType type;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField, Min(0f)] private float lifetime = 30f;
        [SerializeField, Min(0f)] private float rotateSpeed = 90f;

        public void Configure(PickupType pickupType, int pickupAmount)
        {
            type = pickupType;
            amount = Mathf.Max(1, pickupAmount);
            ApplyColor();
        }

        private void Awake()
        {
            Collider pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null)
            {
                pickupCollider.isTrigger = true;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            if (body == null)
            {
                return;
            }

            body.isKinematic = true;
            body.useGravity = false;
            ApplyColor();

            if (lifetime > 0f)
            {
                Destroy(gameObject, lifetime);
            }
        }

        private void Update()
        {
            transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform root = other.transform.root;
            if (type == PickupType.Ammo)
            {
                WeaponAmmoInventory ammo = root.GetComponent<WeaponAmmoInventory>();
                if (ammo == null)
                {
                    return;
                }

                ammo.AddAmmoPickups(amount);
            }
            else
            {
                PlayerSurvivalStats stats = root.GetComponent<PlayerSurvivalStats>();
                if (stats == null)
                {
                    return;
                }

                stats.AddMedkits(amount);
            }

            Destroy(gameObject);
        }

        private void ApplyColor()
        {
            Renderer targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer == null)
            {
                return;
            }

            Material material = targetRenderer.material;
            material.color = type == PickupType.Ammo
                ? new Color(1f, 0.7f, 0.05f)
                : new Color(0.1f, 1f, 0.25f);
        }
    }
}
