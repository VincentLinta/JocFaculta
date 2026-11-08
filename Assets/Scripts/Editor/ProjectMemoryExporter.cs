using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Hide And Seek > Export Project Memory Snapshot.
/// Writes Assets/ProjectMemory/10_SCENE_SNAPSHOT.md from the open scene: root hierarchy, exits, keys,
/// hiding spots, waypoints and the main public field values. Read-only: it never changes the scene.
/// </summary>
public static class ProjectMemoryExporter
{
    const string Folder = "Assets/ProjectMemory";
    const string FileName = "10_SCENE_SNAPSHOT.md";

    [MenuItem("Tools/Hide And Seek/Export Project Memory Snapshot")]
    public static string Export()
    {
        var sb = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        sb.AppendLine("# 10 - Scene snapshot (auto-generated)");
        sb.AppendLine();
        sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm} from scene `{scene.path}` (edit mode values).");
        sb.AppendLine("Regenerate with **Tools > Hide And Seek > Export Project Memory Snapshot**. Do not edit by hand.");
        sb.AppendLine();

        sb.AppendLine("## Root hierarchy (2 levels)");
        sb.AppendLine();
        sb.AppendLine("```");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            sb.AppendLine($"{root.name}  [{Components(root)}]  children={root.transform.childCount}");
            foreach (Transform c in root.transform)
                sb.AppendLine($"  {c.name}  children={c.childCount}");
        }
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Exits");
        sb.AppendLine();
        sb.AppendLine("| Name | Position |");
        sb.AppendLine("|---|---|");
        foreach (ExitDoor e in Object.FindObjectsByType<ExitDoor>()) sb.AppendLine($"| {e.name} | {V(e.transform.position)} |");
        sb.AppendLine();

        sb.AppendLine("## Keys (scene positions; at Play the KeyPlacer moves them to random fair tiles)");
        sb.AppendLine();
        sb.AppendLine("| Name | Position |");
        sb.AppendLine("|---|---|");
        foreach (KeyPickup k in Object.FindObjectsByType<KeyPickup>()) sb.AppendLine($"| {k.name} | {V(k.transform.position)} |");
        KeyPlacer placer = Object.FindAnyObjectByType<KeyPlacer>();
        if (placer)
        {
            sb.AppendLine();
            sb.AppendLine($"KeyPlacer: randomizeKeys={placer.randomizeKeys}, minDistanceFromPlayerSpawn={placer.minDistanceFromPlayerSpawn}, " +
                          $"minDistanceFromKillerSpawn={placer.minDistanceFromKillerSpawn}, minDistanceBetweenKeys={placer.minDistanceBetweenKeys}, " +
                          $"requireDifferentRooms={placer.requireDifferentRooms}, originalPositions={string.Join(" ", placer.originalPositions.ConvertAll(V))}");
        }
        sb.AppendLine();

        sb.AppendLine("## Hiding spots");
        sb.AppendLine();
        sb.AppendLine("| Name | Type | Object position | Enter point (before tile snap) |");
        sb.AppendLine("|---|---|---|---|");
        var spots = Object.FindObjectsByType<HidingSpot>();
        System.Array.Sort(spots, (a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (HidingSpot s in spots)
            sb.AppendLine($"| {s.name} | {s.spotType} | {V(s.transform.position)} | {(s.enterPoint ? V(s.enterPoint.position) : "-")} |");
        sb.AppendLine();

        sb.AppendLine("## Killer waypoints");
        sb.AppendLine();
        KillerAI killer = Object.FindAnyObjectByType<KillerAI>();
        if (killer && killer.waypoints != null)
        {
            sb.AppendLine("| Name | Position |");
            sb.AppendLine("|---|---|");
            foreach (Transform w in killer.waypoints) if (w) sb.AppendLine($"| {w.name} | {V(w.position)} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Main public field values");
        sb.AppendLine();
        sb.AppendLine("```");
        Dump(sb, Object.FindAnyObjectByType<GridManager>(), "origin", "width", "height", "tileSize", "probeHalfSize", "propHalfSize");
        Dump(sb, killer, "tilesPerTurn", "tilesPerKey", "maxTilesPerTurn", "stepTime", "viewDistance", "viewAngle", "hearDashTiles",
            "sameRoomTiles", "blindFindChance", "seenFindChance", "minSpawnDistance", "spawnSafetyChecks", "searchRadiusTiles", "spotsToCheck", "chaseLostTurns");
        Dump(sb, Object.FindAnyObjectByType<PlayerController>(), "normalRange", "dashRange", "stepTime", "maxStamina", "dashCost", "hiddenRegen");
        Dump(sb, Object.FindAnyObjectByType<AudioManager>(), "musicVolume", "sfxVolume", "pausedMusicLevel", "baseLayerLevel", "calmMusicLevel",
            "musicTempo", "musicRootNote", "musicClipOverride", "heartbeatFarTiles", "heartbeatMidTiles", "heartbeatMidVolume", "heartbeatNearTiles",
            "heartbeatNearVolume", "heartbeatCloseTiles", "heartbeatMinBpm", "heartbeatMaxBpm", "heartbeatHiddenBoost", "heartbeatLevel",
            "heartbeatDamping", "tensionLevel", "chaseMusicLevel", "creepLevel", "ambientLevel", "eventLevel");
        Dump(sb, Object.FindAnyObjectByType<GameUI>(), "dangerDistance", "bannerTime", "exitHintTime");
        Dump(sb, Object.FindAnyObjectByType<GameManager>(), "keysRequired", "killerTurnDelay");
        Dump(sb, Object.FindAnyObjectByType<RuntimeNavMeshBaker>(), "size");
        RuntimeNavMeshBaker baker = Object.FindAnyObjectByType<RuntimeNavMeshBaker>();
        if (baker) sb.AppendLine($"RuntimeNavMeshBaker.position = {V(baker.transform.position)}");
        sb.AppendLine("```");

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "ProjectMemory");
        string path = Path.Combine(Folder, FileName);
        File.WriteAllText(path, sb.ToString());
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[ProjectMemory] Snapshot written to {path}");
        return path;
    }

    static string V(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

    static string Components(GameObject go)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (Component c in go.GetComponents<Component>())
            if (c != null && !(c is Transform)) names.Add(c.GetType().Name);
        return string.Join(", ", names);
    }

    static void Dump(StringBuilder sb, Object target, params string[] fields)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        foreach (string f in fields)
        {
            SerializedProperty p = so.FindProperty(f);
            if (p == null) continue;
            string value;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: value = p.floatValue.ToString("0.###"); break;
                case SerializedPropertyType.Integer: value = p.intValue.ToString(); break;
                case SerializedPropertyType.Boolean: value = p.boolValue.ToString(); break;
                case SerializedPropertyType.Vector2: value = p.vector2Value.ToString(); break;
                case SerializedPropertyType.Vector3: value = p.vector3Value.ToString(); break;
                case SerializedPropertyType.ObjectReference: value = p.objectReferenceValue ? p.objectReferenceValue.name : "none"; break;
                default: value = p.propertyType.ToString(); break;
            }
            sb.AppendLine($"{target.GetType().Name}.{f} = {value}");
        }
    }
}
