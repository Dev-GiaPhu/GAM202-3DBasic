using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieInfinite
{
    public sealed class WeaponAmmoInventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int magazineSize = 30;
        [SerializeField, Min(0)] private int startingReserveMagazines = 3;
        [SerializeField, Min(0f)] private float reloadDuration = 1.6f;
        [SerializeField, Min(1)] private int magazinesPerPickup = 1;

        private int roundsInMagazine;
        private int reserveMagazines;
        private float reloadRemaining;

        public int MagazineSize => magazineSize;
        public int RoundsInMagazine => roundsInMagazine;
        public int ReserveMagazines => reserveMagazines;
        public bool IsReloading => reloadRemaining > 0f;
        public float ReloadRemaining => reloadRemaining;
        public float ReloadDuration => reloadDuration;

        private void Awake()
        {
            roundsInMagazine = magazineSize;
            reserveMagazines = startingReserveMagazines;
        }

        private void Update()
        {
            if (IsReloading)
            {
                reloadRemaining -= Time.deltaTime;
                if (reloadRemaining <= 0f)
                {
                    CompleteReload();
                }
            }

            if (Keyboard.current != null &&
                Keyboard.current.rKey.wasPressedThisFrame)
            {
                TryStartReload();
            }
        }

        public bool TryConsumeRound()
        {
            if (IsReloading || roundsInMagazine <= 0)
            {
                return false;
            }

            roundsInMagazine--;
            return true;
        }

        public bool TryStartReload()
        {
            if (IsReloading || reserveMagazines <= 0 ||
                roundsInMagazine >= magazineSize)
            {
                return false;
            }

            reloadRemaining = Mathf.Max(0.01f, reloadDuration);
            return true;
        }

        public void AddAmmoPickups(int pickupCount)
        {
            reserveMagazines += Mathf.Max(0, pickupCount) * magazinesPerPickup;
        }

        private void CompleteReload()
        {
            reloadRemaining = 0f;
            if (reserveMagazines <= 0)
            {
                return;
            }

            reserveMagazines--;
            roundsInMagazine = magazineSize;
        }

        private void OnValidate()
        {
            magazineSize = Mathf.Max(1, magazineSize);
            startingReserveMagazines = Mathf.Max(0, startingReserveMagazines);
            reloadDuration = Mathf.Max(0f, reloadDuration);
            magazinesPerPickup = Mathf.Max(1, magazinesPerPickup);
        }
    }
}
