# Unity Project Context

- Analyzed: 2026-08-02
- Unity: 6000.0.70f1, Windows Editor
- Render pipeline: URP is active; HDRP packages/assets are also present.
- Input: Input System 1.19.0 with `Assets/_Project/Input/InputSystem_Actions.inputactions`.
- Navigation: AI Navigation 2.0.11 is installed. Runtime types `NavMeshSurface`, `NavMeshLink`, and `NavMeshModifierVolume` are available.
- Project code: no first-party runtime scripts or asmdefs existed before the zombie prototype. Imported packages live under `Assets/ThirdParty`; native/managed plugins remain under `Assets/Plugins`.
- Scenes: build index 0 is `Assets/_Project/Scenes/1.unity`; scene `2.unity` is a development scene.
- Architecture: small MonoBehaviour-oriented prototype. New gameplay code belongs under `Assets/_Project/Scripts` and reusable objects under `Assets/_Project/Prefabs`.
- Testing: Unity Test Framework 1.6.0 is installed; no first-party tests were found.
- Tooling: Unity MCP is connected and supports editor state, assets, scenes, navigation components, Console, tests, and Play Mode.
- Constraints: preserve imported package folder internals and asset GUIDs. Treat scenes, prefabs, input assets, and ProjectSettings as high-impact serialized data.

Sources: `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `ProjectSettings/EditorBuildSettings.asset`, Unity MCP project/editor/tool resources.
