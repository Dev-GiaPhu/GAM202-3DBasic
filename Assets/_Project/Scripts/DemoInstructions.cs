using UnityEngine;

namespace ZombieInfinite
{
    public sealed class DemoInstructions : MonoBehaviour
    {
        [TextArea(8, 18)]
        [SerializeField] private string instructions =
            "WASD: move player\nMouse: rotate toward cursor\nLeft mouse: shoot\n\n" +
            "Bai 01: Zombie chases inside 5 m and returns home outside 5 m.\n" +
            "Bai 02: Orange cylinder uses a carving NavMeshObstacle.\n" +
            "Bai 03: Blue platforms are connected by NavMeshLink.\n" +
            "Bai 04: Purple zone uses NavMeshModifierVolume (area 3).\n\n" +
            "Terrain uses editable prefabs TerrainChunk_A-D. A coordinate deterministically selects " +
            "the same prefab every time. Keep the outermost Terrain border equal between prefabs to preserve seams.";

        public string Instructions => instructions;
    }
}
