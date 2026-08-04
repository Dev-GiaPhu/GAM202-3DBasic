using UnityEngine;

namespace ZombieInfinite
{
    [DisallowMultipleComponent]
    public sealed class ZombieHitZone : MonoBehaviour
    {
        public enum Zone
        {
            Body,
            Head
        }

        [SerializeField] private Zone zone = Zone.Body;

        public bool IsHead => zone == Zone.Head;

        public void Configure(Zone hitZone)
        {
            zone = hitZone;
        }
    }
}
