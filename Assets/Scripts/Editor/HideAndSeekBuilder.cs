using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the hide-and-seek horror scene from Unity primitives.
/// Tools > Hide And Seek > 1..5 builds a fresh scene (v2: grid, turns, clown, detailed map, steady lights).
/// Tools > Hide And Seek > Upgrade v2 > Part 1..4 upgrades an existing v1 scene in place.
/// </summary>
public static class HideAndSeekBuilder
{
    // Building layout (x: -30..30, z: -20..20). Walls sit on whole numbers = tile edges.
    const float WallH = 2.6f;
    const float WallT = 0.3f;
    const float Door = 2f; // exactly one 2x2 tile
    const float WestExitZ = 7f; // second exit (Storage, west wall), opposite the east exit (z = -13)
    const int IgnoreRaycastLayer = 2;

    static readonly Color Warm = new Color(1f, 0.68f, 0.4f);

    static Material mFloor, mOutside, mWall, mWood, mDarkWood, mMetal, mRust, mFabric, mCloth, mTile;
    static Material mConcrete, mDarkConcrete, mWoodFloor, mTileFloor, mBaseboard, mRug, mPuddle, mStain, mCobweb;
    static Material mCardboard, mShade, mBulb, mEmergency, mGlass, mTrash;
    static Material mPlayer, mKiller, mMask, mKey, mExit, mSign;
    static Material mSuitA, mSuitB, mClownFace, mClownRed, mClownNose, mClownBlue, mShoe, mGlove;
    static Material mGridLine, mTileReach, mTileHover, mTilePath, mSpotOutline;

    // ======================================================================
    // PHASE 1 - MAP
    // ======================================================================
    [MenuItem("Tools/Hide And Seek/1 - Build Map")]
    public static void BuildMap()
    {
        LoadMaterials();
        DestroyRoot("Map");

        Transform map = new GameObject("Map").transform;
        Transform structure = Child(map, "Structure");
        Transform props = Child(map, "Props");
        Transform lights = Child(map, "Lights");

        // Floor (top at y = 0) and dark ground outside.
        Box("Floor", structure, new Vector3(0f, -0.1f, 0f), new Vector3(60.6f, 0.2f, 40.6f), mFloor);
        Box("Ground_Outside", structure, new Vector3(0f, -0.26f, 0f), new Vector3(140f, 0.2f, 110f), mOutside);

        BuildWalls(structure);

        BuildClutter(props);
        BuildMapDetails(map);
        BuildNorthWing(map);
        BuildLights(lights);

        // Player spawn in the safe room (west), exit on the far east side. Tile centers end in .5
        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.SetParent(map, false);
        spawn.transform.localPosition = new Vector3(-25.5f, 1f, -1.5f);

        GameObject exit = Box("ExitDoor", structure, new Vector3(30f, 1.2f, -13f), new Vector3(0.3f, 2.4f, Door), mExit);
        Box("ExitSign", structure, new Vector3(29.75f, 2.45f, -13f), new Vector3(0.1f, 0.25f, 1.2f), mSign, false);
        Light exitLight = PointLight("ExitLight", lights, new Vector3(28.6f, 2.2f, -13f), new Color(1f, 0.15f, 0.1f), 6f, 7f);
        exit.AddComponent<ExitDoor>().doorLight = exitLight;
        BuildWestExit(structure, lights);

        SetupAtmosphere();
        Save("Phase 1 - Map");
    }

    /// <summary>
    /// All walls sit on even coordinates = the lines between 2x2 tiles.
    /// Every door gap is exactly one tile (2 units), centered on an odd coordinate = one tile.
    /// </summary>
    static void BuildWalls(Transform structure)
    {
        // Outer walls. East wall has the exit gap at z = -13 (tile -14..-12).
        WallX(structure, "Wall_North", 20f, -30.15f, 30.15f, NorthDoorsX); // doorways into the north wing
        WallX(structure, "Wall_South", -20f, -30.15f, 30.15f);
        WallZ(structure, "Wall_West", -30f, -20f, 20f, WestExitZ); // second exit (Storage, NW)
        WallZ(structure, "Wall_East", 30f, -20f, 20f, -13f);

        // North row: Storage | Office | Bedroom | Bathroom, corridor below.
        WallX(structure, "Wall_CorridorNorth", 6f, -30f, 30f, -23f, -7f, 7f, 23f);
        WallZ(structure, "Wall_Storage_Office", -14f, 6f, 20f, 17f);
        WallZ(structure, "Wall_Office_Bedroom", 0f, 6f, 20f, 17f);
        WallZ(structure, "Wall_Bedroom_Bathroom", 14f, 6f, 20f);

        // South row: SafeRoom/Basement | Boiler | Main Hall | Loading.
        WallX(structure, "Wall_CorridorSouth", 2f, -30f, 30f, -23f, -11f, 3f, 21f);
        WallZ(structure, "Wall_Safe_Boiler", -18f, -20f, 2f, -3f, -13f);
        WallX(structure, "Wall_Safe_Basement", -8f, -30f, -18f, -23f);
        WallZ(structure, "Wall_Boiler_Hall", -6f, -20f, 2f, -9f);
        WallZ(structure, "Wall_Hall_Loading", 14f, -20f, 2f, -7f, -15f);
    }

    /// <summary>
    /// Upgrade for an existing scene: re-lays only the walls, baseboards and exit on the tile lines
    /// and nudges the few props / hiding spot that touched the moved walls. Nothing else is rebuilt.
    /// </summary>
    public static void AlignWallsToGrid()
    {
        LoadMaterials();
        Transform structure = GameObject.Find("Map/Structure").transform;
        Transform map = structure.parent;

        for (int i = structure.childCount - 1; i >= 0; i--)
            if (structure.GetChild(i).name.StartsWith("Wall_")) Object.DestroyImmediate(structure.GetChild(i).gameObject);
        BuildWalls(structure);
        DestroyChild(map, "Trim");
        BuildTrim(Child(map, "Trim"), structure);

        Transform exit = structure.Find("ExitDoor");
        exit.localPosition = new Vector3(30f, 1.2f, -13f);
        exit.localScale = new Vector3(0.3f, 2.4f, Door);
        structure.Find("ExitSign").localPosition = new Vector3(29.75f, 2.45f, -13f);
        Transform exitLight = map.Find("Lights/ExitLight");
        if (exitLight) exitLight.localPosition = new Vector3(28.6f, 2.2f, -13f);

        // Props that touched the walls moved from x = -15 / 15 to x = -14 / 14.
        MoveProp(map, "FileCabinet", new Vector2(-14.5f, 19.2f), new Vector2(-13.5f, 19.2f));
        MoveProp(map, "Cobweb", new Vector2(-15.2f, 19.8f), new Vector2(-14.2f, 19.8f));
        MoveProp(map, "Cobweb", new Vector2(-14.8f, 6.2f), new Vector2(-13.8f, 6.2f));
        MoveProp(map, "Cobweb", new Vector2(14.8f, 19.8f), new Vector2(13.8f, 19.8f));
        MoveProp(map, "Cobweb", new Vector2(15.2f, 6.2f), new Vector2(14.2f, 6.2f));
        MoveProp(map, "Wardrobe_Fallen", new Vector2(13.2f, 18.6f), new Vector2(12.7f, 18.6f));
        MoveProp(map, "Sink", new Vector2(15.5f, 14f), new Vector2(14.5f, 14f));
        MoveProp(map, "Sink", new Vector2(15.5f, 16.2f), new Vector2(14.5f, 16.2f));
        MoveProp(map, "Mirror", new Vector2(15.18f, 14f), new Vector2(14.18f, 14f));
        MoveProp(map, "Mirror_Broken", new Vector2(15.18f, 16.2f), new Vector2(14.18f, 16.2f));
        GameObject hs7 = GameObject.Find("HidingSpots/HidingSpot_07");
        if (hs7) hs7.transform.position = new Vector3(13.45f, 0f, 12f);
    }

    /// <summary>Second exit in the west wall: same door, sign and red/green light as the east exit.</summary>
    static void BuildWestExit(Transform structure, Transform lights)
    {
        GameObject door = Box("ExitDoor_West", structure, new Vector3(-30f, 1.2f, WestExitZ), new Vector3(0.3f, 2.4f, Door), mExit);
        Box("ExitSign_West", structure, new Vector3(-29.75f, 2.45f, WestExitZ), new Vector3(0.1f, 0.25f, 1.2f), mSign, false);
        Light l = PointLight("ExitLight_West", lights, new Vector3(-28.6f, 2.2f, WestExitZ), new Color(1f, 0.15f, 0.1f), 6f, 7f);
        door.AddComponent<ExitDoor>().doorLight = l;
    }

    /// <summary>
    /// Upgrade for an existing scene: cuts one tile-wide gap in the west wall and adds the second exit.
    /// Only the west wall pieces and their baseboards are replaced; nothing else moves.
    /// </summary>
    public static void AddSecondExit()
    {
        LoadMaterials();
        Transform structure = GameObject.Find("Map/Structure").transform;
        Transform map = structure.parent;
        Transform lights = map.Find("Lights");
        // Remove an older second exit first, so this can be re-run safely.
        DestroyChild(structure, "ExitDoor_West");
        DestroyChild(structure, "ExitSign_West");
        if (lights) DestroyChild(lights, "ExitLight_West");

        for (int i = structure.childCount - 1; i >= 0; i--)
            if (structure.GetChild(i).name.StartsWith("Wall_West")) Object.DestroyImmediate(structure.GetChild(i).gameObject);
        WallZ(structure, "Wall_West", -30f, -20f, 20f, WestExitZ);

        // Replace only the west wall baseboard(s).
        Transform trim = map.Find("Trim");
        if (trim)
        {
            for (int i = trim.childCount - 1; i >= 0; i--)
            {
                Transform b = trim.GetChild(i);
                if (Mathf.Abs(b.position.x + 30f) < 0.01f && b.localScale.z > b.localScale.x) Object.DestroyImmediate(b.gameObject);
            }
            foreach (Transform w in structure)
            {
                if (!w.name.StartsWith("Wall_West")) continue;
                Vector3 s = w.localScale;
                Box("Baseboard", trim, new Vector3(w.localPosition.x, 0.08f, w.localPosition.z), new Vector3(s.x + 0.06f, 0.16f, s.z), mBaseboard, false);
            }
        }
        BuildWestExit(structure, lights);
    }

    // ======================================================================
    // NORTH WING (z 20..30): Garage | Kitchen | Bedroom 2 | Cellar corridor
    // ======================================================================

    /// <summary>Doorways (one 2x2 tile each) in the old north wall at z = 20: Storage, Office, Bathroom.</summary>
    static readonly float[] NorthDoorsX = { -15f, -7f, 21f };

    /// <summary>
    /// Builds the north wing under Map/NorthWing: walls on tile lines, one-tile doors, floors, baseboards,
    /// props (never on a door tile or the tile in front of it) and steady lights.
    /// </summary>
    public static void BuildNorthWing(Transform map)
    {
        LoadMaterials();
        Material garageFloor = Mat("HS_FloorGarage", new Color(0.1f, 0.1f, 0.11f), null, 0.35f);
        Material oil = Mat("HS_OilStain", new Color(0.02f, 0.02f, 0.025f), null, 0.9f);
        Material carPaint = Mat("HS_CarPaint", new Color(0.12f, 0.16f, 0.14f), null, 0.5f);

        DestroyChild(map, "NorthWing");
        Transform wing = Child(map, "NorthWing");
        Transform s = Child(wing, "Structure");
        Transform floors = Child(wing, "Floors");
        Transform p = Child(wing, "Props");
        Transform lights = Child(wing, "Lights");

        // Floor slab (collider) + room floors (no collider).
        Box("Floor_Wing", s, new Vector3(0f, -0.1f, 25.225f), new Vector3(60.6f, 0.2f, 10.15f), mFloor);
        RoomFloor(floors, "Floor_Garage", -30f, -12f, 20f, 30f, garageFloor);
        RoomFloor(floors, "Floor_Kitchen", -12f, 4f, 20f, 30f, mTileFloor);
        RoomFloor(floors, "Floor_Bedroom2", 4f, 18f, 20f, 30f, mWoodFloor);
        RoomFloor(floors, "Floor_Cellar", 18f, 30f, 20f, 30f, mDarkConcrete);

        // Walls: all on even coordinates (tile lines), doors centered on odd coordinates (one tile).
        WallX(s, "Wall_WingNorth", 30f, -30.15f, 30.15f);
        WallZ(s, "Wall_WingWest", -30f, 20f, 30f);
        WallZ(s, "Wall_WingEast", 30f, 20f, 30f);
        WallZ(s, "Wall_Garage_Kitchen", -12f, 20f, 30f, 25f);
        WallZ(s, "Wall_Kitchen_Bedroom2", 4f, 20f, 30f, 23f);
        WallZ(s, "Wall_Bedroom2_Cellar", 18f, 20f, 30f, 27f);
        BuildTrim(Child(wing, "Trim"), s);

        // ---- Garage (doors: (-15,21) from Storage, (-13,25) to Kitchen)
        Transform car = Group(p, "OldCar", new Vector3(-22f, 0f, 25f), 0f);
        Box("Body", car, new Vector3(0f, 0.6f, 0f), new Vector3(3.8f, 0.8f, 1.8f), carPaint);
        Box("Cabin", car, new Vector3(-0.3f, 1.3f, 0f), new Vector3(2f, 0.6f, 1.6f), carPaint, false);
        Box("Windows", car, new Vector3(-0.3f, 1.3f, 0f), new Vector3(2.02f, 0.45f, 1.62f), mGlass, false);
        foreach (Vector2 w in new[] { new Vector2(-1.25f, 0.85f), new Vector2(1.25f, 0.85f), new Vector2(-1.25f, -0.85f), new Vector2(1.25f, -0.85f) })
            Cyl("Wheel", car, new Vector3(w.x, 0.3f, w.y), new Vector3(0.6f, 0.1f, 0.6f), mKiller, new Vector3(90f, 0f, 0f), false);
        Box("Hood_Open", car, new Vector3(1.6f, 1.25f, 0f), new Vector3(0.9f, 0.05f, 1.6f), carPaint, false, new Vector3(0f, 0f, 55f));
        Workbench(p, new Vector3(-27f, 0f, 29.45f), 0f, 2.6f);
        for (int i = 0; i < 3; i++)
            Cyl("Tire", p, new Vector3(-13f, 0.15f + i * 0.3f, 28.9f), new Vector3(0.9f, 0.15f, 0.9f), mKiller, null, i == 0);
        Cyl("OilBarrel", p, new Vector3(-29f, 0.6f, 21.1f), new Vector3(0.8f, 0.6f, 0.8f), mRust);
        Cyl("OilBarrel", p, new Vector3(-28.9f, 0.4f, 22.2f), new Vector3(0.8f, 0.6f, 0.8f), mRust, new Vector3(90f, 20f, 0f), false);
        CardboardBoxes(p, new Vector3(-13.2f, 0f, 21.2f));
        Cyl("OilStain", p, new Vector3(-21f, 0.014f, 23.4f), new Vector3(1.6f, 0.002f, 1f), oil, null, false);
        Puddle(p, new Vector3(-17f, 0f, 23f), 1f);
        Cobweb(p, new Vector3(-29.8f, 0f, 29.8f));
        Cobweb(p, new Vector3(-12.2f, 0f, 29.8f));
        HangingLamp(lights, "Lamp_Garage", new Vector3(-22f, 0f, 23f), new Color(0.9f, 0.85f, 0.7f), 4.5f, 11f);
        HangingLamp(lights, "Lamp_Garage2", new Vector3(-15f, 0f, 26f), Warm, 3.5f, 9f);

        // ---- Kitchen (doors: (-7,21) from Office, (-11,25) from Garage, (3,23) to Bedroom 2)
        Box("Counter", p, new Vector3(-6f, 0.45f, 29.5f), new Vector3(7.6f, 0.9f, 0.8f), mDarkWood);
        Box("CounterTop", p, new Vector3(-6f, 0.92f, 29.48f), new Vector3(7.7f, 0.05f, 0.86f), mTile, false);
        Box("Sink", p, new Vector3(-4f, 0.9f, 29.45f), new Vector3(0.8f, 0.06f, 0.5f), mMetal, false);
        Box("UpperCabinets", p, new Vector3(-6f, 2.0f, 29.7f), new Vector3(7.6f, 0.7f, 0.4f), mDarkWood, false);
        Box("Cabinet_DoorOpen", p, new Vector3(-8.2f, 1.9f, 29.35f), new Vector3(0.5f, 0.6f, 0.03f), mWood, false, new Vector3(0f, 50f, 0f));
        Stove(p, new Vector3(-11f, 0f, 29.3f));
        Transform fridge = Group(p, "Fridge", new Vector3(-0.9f, 0f, 29.4f), 0f);
        Box("Body", fridge, new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 1.9f, 0.8f), mTile);
        Box("Handle", fridge, new Vector3(0.35f, 1.1f, 0.42f), new Vector3(0.04f, 0.5f, 0.04f), mMetal, false);
        BrokenChair(p, new Vector3(-7.4f, 0f, 25.2f), 80f);
        BrokenChair(p, new Vector3(-2.6f, 0f, 24.6f), -100f);
        TrashPile(p, new Vector3(1.4f, 0f, 21.2f));
        Stain(p, new Vector3(-9f, 0f, 22.5f), 0.9f);
        Puddle(p, new Vector3(-3f, 0f, 27.6f), 0.8f);
        Cobweb(p, new Vector3(3.8f, 0f, 29.8f));
        HangingLamp(lights, "Lamp_Kitchen", new Vector3(-5f, 0f, 23.5f), Warm, 4.5f, 10f);

        // ---- Bedroom 2 (doors: (5,23) from Kitchen, (17,27) to Cellar)
        Rug(p, new Vector3(11f, 0f, 24f), new Vector2(3.5f, 2.4f));
        Box("Dresser", p, new Vector3(15.3f, 0.45f, 29.6f), new Vector3(1.4f, 0.9f, 0.5f), mDarkWood);
        Box("Nightstand", p, new Vector3(13.1f, 0.3f, 29.5f), Vector3.one * 0.6f, mDarkWood);
        TableLamp(lights, "TableLamp_Bedroom2", new Vector3(13.1f, 0.6f, 29.5f));
        BrokenChair(p, new Vector3(6.6f, 0f, 25.2f), 30f); // stays fully inside tile (7,25)
        Box("Clothes", p, new Vector3(9f, 0.02f, 22f), new Vector3(0.6f, 0.03f, 0.4f), mCloth, false, new Vector3(0f, 25f, 0f));
        Stain(p, new Vector3(13f, 0f, 22.5f), 1f);
        WindowFrame(p, new Vector3(15.3f, 1.6f, 29.82f), 0f);
        Cobweb(p, new Vector3(4.2f, 0f, 29.8f));
        Cobweb(p, new Vector3(17.8f, 0f, 20.2f));
        HangingLamp(lights, "Lamp_Bedroom2", new Vector3(10f, 0f, 24.5f), new Color(1f, 0.6f, 0.4f), 4f, 9f);

        // ---- Cellar corridor (doors: (21,21) from Bathroom, (19,27) from Bedroom 2)
        Cyl("Barrel", p, new Vector3(19.2f, 0.6f, 29.2f), new Vector3(0.8f, 0.6f, 0.8f), mRust);
        Box("Crate", p, new Vector3(29.1f, 0.5f, 21.0f), Vector3.one * 1f, mWood);
        Box("Crate", p, new Vector3(29.1f, 1.4f, 21.0f), Vector3.one * 0.8f, mWood, false, new Vector3(0f, 25f, 0f));
        Shelf(p, new Vector3(29.6f, 0f, 25f), 90f, 1.6f);
        Cyl("Pipe_Cellar", p, new Vector3(24f, 2.35f, 29.7f), new Vector3(0.14f, 5.8f, 0.14f), mRust, new Vector3(0f, 0f, 90f), false);
        Cyl("Pipe_Cellar", p, new Vector3(29.7f, 2.2f, 25f), new Vector3(0.1f, 4.8f, 0.1f), mMetal, new Vector3(90f, 0f, 0f), false);
        Puddle(p, new Vector3(23f, 0f, 22.6f), 1.2f);
        Puddle(p, new Vector3(26.5f, 0f, 26f), 0.8f);
        Stain(p, new Vector3(20.5f, 0f, 24.5f), 0.8f);
        Cobweb(p, new Vector3(29.8f, 0f, 29.8f));
        Cobweb(p, new Vector3(18.2f, 0f, 20.2f));
        HangingLamp(lights, "Lamp_Cellar", new Vector3(24f, 0f, 24f), new Color(0.75f, 0.8f, 1f), 3.5f, 10f);
    }

    /// <summary>Two hiding spots per wing room: HidingSpot_21 .. HidingSpot_28.</summary>
    static void BuildWingSpots(Transform root)
    {
        Spot(root, 21, HidingSpot.SpotType.Locker, -29.5f, 25f, 90f);       // Garage
        Spot(root, 22, HidingSpot.SpotType.BehindCrate, -18f, 28.6f, 180f); // Garage
        Spot(root, 23, HidingSpot.SpotType.UnderTable, -5f, 25f, 0f);       // Kitchen (kitchen table)
        Spot(root, 24, HidingSpot.SpotType.Wardrobe, 3.45f, 27.4f, 270f);   // Kitchen (pantry)
        Spot(root, 25, HidingSpot.SpotType.UnderBed, 11f, 28f, 180f);       // Bedroom 2
        Spot(root, 26, HidingSpot.SpotType.Curtain, 7f, 29f, 180f);         // Bedroom 2
        Spot(root, 27, HidingSpot.SpotType.Barrel, 28.8f, 28.8f, 180f);     // Cellar
        Spot(root, 28, HidingSpot.SpotType.UnderStairs, 25f, 29.1f, 180f);  // Cellar
    }

    /// <summary>
    /// Upgrade for the existing scene: cuts the three north-wall doorways, adds the north wing,
    /// spots 21..28, 4 waypoints, and grows the grid + NavMesh bake area. Nothing old is moved.
    /// </summary>
    public static string UpgradeNorthWing()
    {
        LoadMaterials();
        Transform structure = GameObject.Find("Map/Structure").transform;
        Transform map = structure.parent;

        // North wall with three one-tile doorways (+ its baseboards).
        for (int i = structure.childCount - 1; i >= 0; i--)
            if (structure.GetChild(i).name.StartsWith("Wall_North")) Object.DestroyImmediate(structure.GetChild(i).gameObject);
        WallX(structure, "Wall_North", 20f, -30.15f, 30.15f, NorthDoorsX);
        Transform trim = map.Find("Trim");
        if (trim)
        {
            for (int i = trim.childCount - 1; i >= 0; i--)
            {
                Transform b = trim.GetChild(i);
                if (Mathf.Abs(b.position.z - 20f) < 0.01f && b.localScale.x > b.localScale.z) Object.DestroyImmediate(b.gameObject);
            }
            foreach (Transform w in structure)
            {
                if (!w.name.StartsWith("Wall_North")) continue;
                Vector3 sc = w.localScale;
                Box("Baseboard", trim, new Vector3(w.localPosition.x, 0.08f, w.localPosition.z), new Vector3(sc.x, 0.16f, sc.z + 0.06f), mBaseboard, false);
            }
        }

        BuildNorthWing(map);

        Transform spots = GameObject.Find("HidingSpots").transform;
        for (int i = 21; i <= 28; i++) DestroyChild(spots, $"HidingSpot_{i:00}");
        BuildWingSpots(spots);

        Transform wpRoot = GameObject.Find("KillerWaypoints").transform;
        var wing = new (string, Vector3)[]
        {
            ("WP_Garage", new Vector3(-21f, 0f, 22f)), ("WP_Kitchen", new Vector3(-5f, 0f, 23f)),
            ("WP_Bedroom2", new Vector3(9f, 0f, 24f)), ("WP_Cellar", new Vector3(23f, 0f, 24f)),
        };
        foreach (var (name, pos) in wing)
        {
            DestroyChild(wpRoot, name);
            var wp = new GameObject(name).transform;
            wp.SetParent(wpRoot, false);
            wp.localPosition = pos;
        }
        var ai = GameObject.Find("Killer").GetComponent<KillerAI>();
        var list = new List<Transform>();
        foreach (Transform t in wpRoot) list.Add(t);
        ai.waypoints = list.ToArray();
        EditorUtility.SetDirty(ai);

        var grid = GameObject.Find("Grid").GetComponent<GridManager>();
        grid.height = 25;
        EditorUtility.SetDirty(grid);
        var baker = GameObject.Find("NavMeshBaker").GetComponent<RuntimeNavMeshBaker>();
        baker.transform.position = new Vector3(0f, 0f, 5f);
        baker.size = new Vector3(70f, 10f, 64f);
        EditorUtility.SetDirty(baker);

        return $"waypoints={list.Count} spots={spots.childCount} grid={grid.width}x{grid.height}";
    }

    /// <summary>Upgrade for the existing scene: adds the random key placer to the Keys root.</summary>
    public static string AddKeyPlacer()
    {
        GameObject keys = GameObject.Find("Keys");
        var placer = keys.GetComponent<KeyPlacer>();
        if (!placer) placer = keys.AddComponent<KeyPlacer>();
        placer.originalPositions.Clear();
        foreach (Transform k in keys.transform) placer.originalPositions.Add(k.position);
        EditorUtility.SetDirty(placer);
        return "original key positions: " + string.Join(" ", placer.originalPositions);
    }

    static void MoveProp(Transform root, string name, Vector2 from, Vector2 to)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != name) continue;
            if (Vector2.Distance(new Vector2(t.position.x, t.position.z), from) > 0.05f) continue;
            t.position = new Vector3(to.x, t.position.y, to.y);
        }
    }

    /// <summary>Room floors, baseboards and detailed props (Part 4).</summary>
    static void BuildMapDetails(Transform map)
    {
        DestroyChild(map, "Floors");
        DestroyChild(map, "Trim");
        DestroyChild(map, "Details");
        BuildRoomFloors(Child(map, "Floors"));
        Transform structure = map.Find("Structure");
        if (structure) BuildTrim(Child(map, "Trim"), structure);
        BuildDetails(Child(map, "Details"));
    }

    static void BuildRoomFloors(Transform p)
    {
        // Thin slabs without colliders: different floor per room.
        RoomFloor(p, "Floor_Storage", -30f, -15f, 6f, 20f, mConcrete);
        RoomFloor(p, "Floor_Office", -15f, 0f, 6f, 20f, mWoodFloor);
        RoomFloor(p, "Floor_Bedroom", 0f, 15f, 6f, 20f, mWoodFloor);
        RoomFloor(p, "Floor_Bathroom", 15f, 30f, 6f, 20f, mTileFloor);
        RoomFloor(p, "Floor_Corridor", -30f, 30f, 2f, 6f, mDarkConcrete);
        RoomFloor(p, "Floor_SafeRoom", -30f, -18f, -8f, 2f, mWoodFloor);
        RoomFloor(p, "Floor_Basement", -30f, -18f, -20f, -8f, mDarkConcrete);
        RoomFloor(p, "Floor_Boiler", -18f, -6f, -20f, 2f, mDarkConcrete);
        RoomFloor(p, "Floor_MainHall", -6f, 14f, -20f, 2f, mWoodFloor);
        RoomFloor(p, "Floor_Loading", 14f, 30f, -20f, 2f, mConcrete);
    }

    static void RoomFloor(Transform p, string name, float x0, float x1, float z0, float z1, Material mat)
    {
        Box(name, p, new Vector3((x0 + x1) / 2f, 0.005f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.01f, z1 - z0), mat, false);
    }

    static void BuildTrim(Transform p, Transform structure)
    {
        // A dark baseboard on every wall piece.
        foreach (Transform w in structure)
        {
            if (!w.name.StartsWith("Wall_")) continue;
            Vector3 s = w.localScale;
            bool alongX = s.x > s.z;
            Vector3 size = alongX ? new Vector3(s.x, 0.16f, s.z + 0.06f) : new Vector3(s.x + 0.06f, 0.16f, s.z);
            Box("Baseboard", p, new Vector3(w.localPosition.x, 0.08f, w.localPosition.z), size, mBaseboard, false);
        }
    }

    static void BuildClutter(Transform p)
    {
        // Storage
        Box("Crate", p, new Vector3(-16.4f, 0.6f, 8f), Vector3.one * 1.2f, mWood);
        Box("Crate", p, new Vector3(-16.4f, 0.5f, 9.5f), Vector3.one * 1f, mWood, true, new Vector3(0f, 15f, 0f));
        Box("Crate", p, new Vector3(-16.4f, 1.6f, 8f), Vector3.one * 0.8f, mWood, true, new Vector3(0f, 30f, 0f));
        Box("Crate", p, new Vector3(-24f, 0.55f, 9.5f), Vector3.one * 1.1f, mWood, true, new Vector3(0f, 10f, 0f));
        Box("Shelf", p, new Vector3(-23.5f, 1f, 19.55f), new Vector3(2.6f, 2f, 0.5f), mDarkWood);
        Cyl("Barrel", p, new Vector3(-28.6f, 0.6f, 14f), new Vector3(0.8f, 0.6f, 0.8f), mRust);
        Cyl("Barrel_Tipped", p, new Vector3(-27.4f, 0.4f, 15.6f), new Vector3(0.8f, 0.6f, 0.8f), mRust, new Vector3(90f, 30f, 0f));

        // Office
        BrokenChair(p, new Vector3(-10f, 0f, 14.5f), 40f);
        Box("FileCabinet", p, new Vector3(-13.5f, 0.7f, 19.2f), new Vector3(0.6f, 1.4f, 0.8f), mMetal);
        Box("FileCabinet_Fallen", p, new Vector3(-12.5f, 0.3f, 8.2f), new Vector3(0.8f, 0.6f, 1.4f), mMetal, true, new Vector3(0f, 25f, 0f));
        Box("Bookcase_Fallen", p, new Vector3(-3f, 0.2f, 8.6f), new Vector3(2.4f, 0.4f, 0.9f), mDarkWood, true, new Vector3(0f, 20f, 0f));

        // Bedroom
        Box("Nightstand", p, new Vector3(6.6f, 0.3f, 19.2f), Vector3.one * 0.6f, mDarkWood);
        Box("Dresser_Tipped", p, new Vector3(2.2f, 0.3f, 8.5f), new Vector3(1.6f, 0.6f, 0.8f), mDarkWood, true, new Vector3(0f, -15f, 0f));
        Box("Box", p, new Vector3(12.5f, 0.45f, 7.5f), Vector3.one * 0.9f, mCardboard, true, new Vector3(0f, 35f, 0f));

        // Bathroom
        Box("Bathtub", p, new Vector3(17.3f, 0.35f, 9.6f), new Vector3(2.4f, 0.7f, 1.1f), mTile);
        Box("Toilet", p, new Vector3(17f, 0.4f, 19.4f), new Vector3(0.6f, 0.8f, 0.8f), mTile);
        Box("Toilet", p, new Vector3(19f, 0.4f, 19.4f), new Vector3(0.6f, 0.8f, 0.8f), mTile);
        Box("Sink", p, new Vector3(14.5f, 0.45f, 14f), new Vector3(0.5f, 0.9f, 0.8f), mTile);
        Box("ShowerTray", p, new Vector3(27f, 0.05f, 19.3f), new Vector3(1.8f, 0.1f, 1f), mTile);

        // Corridor
        Box("Plank", p, new Vector3(-10f, 0.04f, 5.4f), new Vector3(2f, 0.08f, 0.25f), mWood, false, new Vector3(0f, 12f, 0f));
        Box("Crate", p, new Vector3(28.6f, 0.45f, 4f), Vector3.one * 0.9f, mWood);

        // Safe room
        Box("Mattress", p, new Vector3(-20f, 0.12f, -6.6f), new Vector3(1.4f, 0.25f, 2f), mFabric);
        Box("SmallTable", p, new Vector3(-28.5f, 0.4f, 0.8f), new Vector3(1f, 0.8f, 0.8f), mWood);

        // Basement
        Box("Crate", p, new Vector3(-29f, 0.5f, -9.5f), Vector3.one * 1f, mWood);
        Box("Crate", p, new Vector3(-28.8f, 0.4f, -11f), Vector3.one * 0.8f, mWood, true, new Vector3(0f, 20f, 0f));
        Box("Crate", p, new Vector3(-21f, 0.5f, -11.5f), Vector3.one * 1f, mWood, true, new Vector3(0f, 40f, 0f));
        BrokenChair(p, new Vector3(-22.5f, 0f, -16f), 120f);

        // Boiler room
        Cyl("Boiler", p, new Vector3(-15.5f, 1.2f, -17.5f), new Vector3(2f, 1.2f, 2f), mRust);
        Cyl("Boiler", p, new Vector3(-8.5f, 1.2f, -17.5f), new Vector3(2f, 1.2f, 2f), mRust);
        Cyl("Pipe", p, new Vector3(-12f, 2.25f, -19.6f), new Vector3(0.25f, 5.5f, 0.25f), mMetal, new Vector3(0f, 0f, 90f), false);
        Cyl("Barrel", p, new Vector3(-16.8f, 0.6f, 0.5f), new Vector3(0.8f, 0.6f, 0.8f), mRust);
        Cyl("Barrel", p, new Vector3(-16.8f, 0.6f, -6.5f), new Vector3(0.8f, 0.6f, 0.8f), mRust);

        // Main hall
        foreach (Vector2 pos in new[] { new Vector2(-1f, -4f), new Vector2(9f, -4f), new Vector2(-1f, -15f), new Vector2(9f, -15f) })
            Cyl("Pillar", p, new Vector3(pos.x, WallH / 2f, pos.y), new Vector3(0.8f, WallH / 2f, 0.8f), mWall);
        BrokenChair(p, new Vector3(1.6f, 0f, -10f), 200f);
        BrokenChair(p, new Vector3(6.6f, 0f, -11.5f), 70f);
        Box("Bench_Fallen", p, new Vector3(9f, 0.22f, -18.5f), new Vector3(2.5f, 0.45f, 0.6f), mWood, true, new Vector3(0f, 8f, 0f));
        Box("Crate", p, new Vector3(12.5f, 0.5f, 0.5f), Vector3.one * 1f, mWood);

        // Loading area
        Box("Container", p, new Vector3(22f, 1.2f, -18.6f), new Vector3(5f, 2.4f, 2.5f), mRust);
        Box("Crate", p, new Vector3(17f, 0.6f, -18.8f), Vector3.one * 1.2f, mWood);
        Box("Crate", p, new Vector3(17f, 0.5f, -17.3f), Vector3.one * 1f, mWood, true, new Vector3(0f, 18f, 0f));
        Box("Crate", p, new Vector3(17f, 1.6f, -18.8f), Vector3.one * 0.8f, mWood, true, new Vector3(0f, 40f, 0f));
        Box("Pallet", p, new Vector3(24f, 0.08f, -8f), new Vector3(1.2f, 0.15f, 1.2f), mWood);
        Box("Pallet", p, new Vector3(24f, 0.23f, -8f), new Vector3(1.2f, 0.15f, 1.2f), mWood, true, new Vector3(0f, 10f, 0f));
        Box("Crate", p, new Vector3(20f, 0.55f, -10f), Vector3.one * 1.1f, mWood, true, new Vector3(0f, 25f, 0f));
        Cyl("Barrel", p, new Vector3(16f, 0.6f, -1f), new Vector3(0.8f, 0.6f, 0.8f), mRust);
        Cyl("Barrel", p, new Vector3(17f, 0.6f, -0.2f), new Vector3(0.8f, 0.6f, 0.8f), mRust);
    }

    /// <summary>Extra abandoned-house detail. Nothing here blocks a door gap or the corridor.</summary>
    static void BuildDetails(Transform p)
    {
        // ---- Storage
        Shelf(p, new Vector3(-29.6f, 0f, 11.3f), 90f, 1.8f);
        CardboardBoxes(p, new Vector3(-17f, 0f, 19f));
        TrashPile(p, new Vector3(-23.2f, 0f, 18.6f)); // next to the shelf, keeps the NW corner reachable on 2x2 tiles
        Box("Shelf_Fallen", p, new Vector3(-26f, 0.2f, 12.5f), new Vector3(2.2f, 0.4f, 0.6f), mDarkWood, true, new Vector3(0f, 15f, 0f));
        Box("Box_Spilled", p, new Vector3(-25.2f, 0.15f, 13.3f), Vector3.one * 0.3f, mCardboard, false, new Vector3(0f, 40f, 0f));
        Stain(p, new Vector3(-21f, 0f, 15.5f), 1f);
        Puddle(p, new Vector3(-18f, 0f, 10.5f), 1f);
        WindowFrame(p, new Vector3(-22f, 1.6f, 19.82f), 0f);
        Cobweb(p, new Vector3(-29.8f, 0f, 19.8f));
        Cobweb(p, new Vector3(-14.2f, 0f, 19.8f));

        // ---- Office
        Rug(p, new Vector3(-7.5f, 0f, 14f), new Vector2(4f, 3f));
        Sofa(p, new Vector3(-11.5f, 0f, 19.35f), 180f);
        Box("SideTable", p, new Vector3(-1.2f, 0.3f, 10.5f), Vector3.one * 0.6f, mWood);
        Shelf(p, new Vector3(-12.8f, 0f, 6.4f), 0f, 1.8f);
        Stain(p, new Vector3(-5f, 0f, 11f), 0.8f);
        Box("Papers", p, new Vector3(-6f, 0.015f, 12f), new Vector3(0.3f, 0.01f, 0.4f), mCobweb, false, new Vector3(0f, 20f, 0f));
        Box("Papers", p, new Vector3(-9f, 0.015f, 10.5f), new Vector3(0.3f, 0.01f, 0.4f), mCobweb, false, new Vector3(0f, -35f, 0f));
        WindowFrame(p, new Vector3(-10f, 1.6f, 19.82f), 0f);
        Cobweb(p, new Vector3(-0.2f, 0f, 19.8f));
        Cobweb(p, new Vector3(-13.8f, 0f, 6.2f));

        // ---- Bedroom
        TornBed(p, new Vector3(2.6f, 0f, 11.5f));
        Rug(p, new Vector3(7.5f, 0f, 13f), new Vector2(3f, 2f));
        BrokenChair(p, new Vector3(12f, 0f, 15.5f), -30f);
        Box("Wardrobe_Fallen", p, new Vector3(12.7f, 0.4f, 18.6f), new Vector3(2.2f, 0.8f, 1.4f), mDarkWood);
        WindowFrame(p, new Vector3(2.5f, 1.6f, 19.82f), 0f);
        Stain(p, new Vector3(9f, 0f, 9f), 1.2f);
        Cobweb(p, new Vector3(13.8f, 0f, 19.8f));
        Cobweb(p, new Vector3(0.2f, 0f, 6.2f));

        // ---- Bathroom
        Box("StallWall", p, new Vector3(18f, 0.9f, 19.1f), new Vector3(0.08f, 1.8f, 1.5f), mMetal);
        Box("Sink", p, new Vector3(14.5f, 0.45f, 16.2f), new Vector3(0.5f, 0.9f, 0.8f), mTile);
        Box("Mirror", p, new Vector3(14.18f, 1.5f, 14f), new Vector3(0.03f, 0.7f, 0.6f), mGlass, false);
        Box("Mirror_Broken", p, new Vector3(14.18f, 1.5f, 16.2f), new Vector3(0.03f, 0.5f, 0.6f), mGlass, false, new Vector3(12f, 0f, 0f));
        Box("TubWater", p, new Vector3(17.3f, 0.71f, 9.6f), new Vector3(2.1f, 0.02f, 0.8f), mPuddle, false);
        Puddle(p, new Vector3(21f, 0f, 15f), 1.1f);
        Puddle(p, new Vector3(25f, 0f, 12.5f), 0.8f);
        TrashPile(p, new Vector3(24f, 0f, 17.8f));
        Stain(p, new Vector3(19.5f, 0f, 12f), 0.7f);
        Cobweb(p, new Vector3(29.8f, 0f, 19.8f));
        Cobweb(p, new Vector3(14.2f, 0f, 6.2f));

        // ---- Corridor (only flat or high things: never blocks)
        Cyl("Pipe_Corridor", p, new Vector3(0f, 2.35f, 5.7f), new Vector3(0.12f, 29.8f, 0.12f), mRust, new Vector3(0f, 0f, 90f), false);
        Cyl("Pipe_Corridor", p, new Vector3(0f, 2.15f, 5.75f), new Vector3(0.07f, 29.8f, 0.07f), mMetal, new Vector3(0f, 0f, 90f), false);
        Puddle(p, new Vector3(5f, 0f, 4f), 1f);
        Stain(p, new Vector3(-3f, 0f, 3.5f), 0.9f);
        Stain(p, new Vector3(18f, 0f, 3f), 0.6f);
        Cobweb(p, new Vector3(-29.8f, 0f, 2.2f));
        Cobweb(p, new Vector3(29.8f, 0f, 5.8f));

        // ---- Safe room
        Rug(p, new Vector3(-24f, 0f, -3f), new Vector2(3f, 2.4f));
        Shelf(p, new Vector3(-20.2f, 0f, 1.6f), 180f, 1.6f);

        // ---- Basement
        Workbench(p, new Vector3(-29.4f, 0f, -14f), 90f, 2.2f);
        TrashPile(p, new Vector3(-27.5f, 0f, -12.3f));
        CardboardBoxes(p, new Vector3(-19.2f, 0f, -9f));
        Puddle(p, new Vector3(-24f, 0f, -17.2f), 1.2f);
        Stain(p, new Vector3(-21f, 0f, -14.5f), 1f);
        Cyl("Pipe_Vertical", p, new Vector3(-29.7f, 1.3f, -8.4f), new Vector3(0.15f, 1.3f, 0.15f), mRust, null, false);
        Cobweb(p, new Vector3(-29.8f, 0f, -19.8f));
        Cobweb(p, new Vector3(-18.2f, 0f, -8.2f));

        // ---- Boiler room
        foreach (float bx in new[] { -15.5f, -8.5f })
        {
            Cyl("BoilerPipe", p, new Vector3(bx, 2.6f, -17.5f), new Vector3(0.2f, 0.25f, 0.2f), mMetal, null, false);
            Cyl("Gauge", p, new Vector3(bx, 1.6f, -16.47f), new Vector3(0.25f, 0.02f, 0.25f), mGlass, new Vector3(90f, 0f, 0f), false);
        }
        Workbench(p, new Vector3(-8f, 0f, 1.4f), 0f, 2f);
        Cyl("Pipe_Wall", p, new Vector3(-6.4f, 2.3f, -9f), new Vector3(0.12f, 10.5f, 0.12f), mRust, new Vector3(90f, 0f, 0f), false);
        Puddle(p, new Vector3(-14f, 0f, -9f), 1.3f);
        Stain(p, new Vector3(-10f, 0f, -6f), 0.9f);
        Cobweb(p, new Vector3(-17.8f, 0f, -19.8f));
        Cobweb(p, new Vector3(-6.2f, 0f, 1.8f));

        // ---- Main hall
        Rug(p, new Vector3(4f, 0f, -10f), new Vector2(5f, 3.5f));
        Sofa(p, new Vector3(-5.35f, 0f, -4.5f), 90f);
        Stove(p, new Vector3(13.4f, 0f, -1.7f));
        Box("Counter", p, new Vector3(13.4f, 0.45f, -3.2f), new Vector3(0.8f, 0.9f, 1f), mDarkWood);
        Box("Table_Broken", p, new Vector3(-3f, 0.45f, -17f), new Vector3(1.6f, 0.06f, 0.9f), mWood, true, new Vector3(70f, 15f, 0f));
        Box("TableLeg", p, new Vector3(-2.4f, 0.05f, -16.4f), new Vector3(0.08f, 0.08f, 0.8f), mWood, false, new Vector3(0f, 30f, 0f));
        CardboardBoxes(p, new Vector3(12.2f, 0f, -19.2f));
        Stain(p, new Vector3(2f, 0f, -14f), 1.3f);
        Stain(p, new Vector3(7f, 0f, -6f), 0.8f);
        Puddle(p, new Vector3(-2f, 0f, -12f), 0.9f);
        WindowFrame(p, new Vector3(5f, 1.6f, -19.82f), 0f);
        Cobweb(p, new Vector3(-5.8f, 0f, -19.8f));
        Cobweb(p, new Vector3(13.8f, 0f, 1.8f));

        // ---- Loading area
        Shelf(p, new Vector3(18.2f, 0f, 1.6f), 180f, 2f);
        Box("Crate", p, new Vector3(27.3f, 0.6f, -7.2f), Vector3.one * 1.2f, mWood);
        Box("Crate", p, new Vector3(27.3f, 1.7f, -7.2f), Vector3.one * 1f, mWood, true, new Vector3(0f, 20f, 0f));
        for (int i = 0; i < 3; i++)
            Box("Pallet", p, new Vector3(19.5f, 0.08f + i * 0.15f, -4f), new Vector3(1.2f, 0.15f, 1.2f), mWood, true, new Vector3(0f, i * 7f, 0f));
        Puddle(p, new Vector3(24f, 0f, -12f), 1.2f);
        Puddle(p, new Vector3(18f, 0f, -9f), 0.8f);
        TrashPile(p, new Vector3(27f, 0f, -19f));
        Stain(p, new Vector3(27.5f, 0f, -14f), 1f);
        Stain(p, new Vector3(22f, 0f, -13f), 0.7f);
        WindowFrame(p, new Vector3(29.82f, 1.6f, -8f), 90f);
        Cobweb(p, new Vector3(29.8f, 0f, 1.8f));
        Cobweb(p, new Vector3(14.2f, 0f, -19.8f));
    }

    // ---------------------------------------------------------------- prop helpers

    static void BrokenChair(Transform p, Vector3 pos, float yaw)
    {
        Transform chair = Group(p, "BrokenChair", pos, yaw);
        Box("Seat", chair, new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.08f, 0.6f), mWood, true, new Vector3(0f, 0f, 18f));
        Box("Back", chair, new Vector3(0f, 0.12f, -0.6f), new Vector3(0.6f, 0.06f, 0.7f), mWood, false, new Vector3(0f, 25f, 0f));
        Box("Leg", chair, new Vector3(0.25f, 0.2f, 0.25f), new Vector3(0.06f, 0.4f, 0.06f), mWood, false);
        Box("Leg", chair, new Vector3(-0.25f, 0.2f, 0.25f), new Vector3(0.06f, 0.4f, 0.06f), mWood, false);
    }

    /// <summary>Shelf unit with boxes. Width runs along local X, back faces local -Z.</summary>
    static void Shelf(Transform p, Vector3 pos, float yaw, float width)
    {
        Transform s = Group(p, "Shelf", pos, yaw);
        foreach (float x in new[] { -width / 2f + 0.03f, width / 2f - 0.03f })
            Box("Post", s, new Vector3(x, 0.95f, 0f), new Vector3(0.06f, 1.9f, 0.45f), mDarkWood, false);
        for (int i = 0; i < 3; i++)
        {
            float y = 0.15f + i * 0.7f;
            Box("Board", s, new Vector3(0f, y, 0f), new Vector3(width, 0.05f, 0.45f), mDarkWood);
            Box("ShelfBox", s, new Vector3(-width * 0.25f + i * 0.1f, y + 0.2f, 0f), new Vector3(0.4f, 0.35f, 0.35f), mCardboard, false, new Vector3(0f, i * 12f, 0f));
            if (i != 1) Box("ShelfBox", s, new Vector3(width * 0.25f, y + 0.15f, 0.02f), new Vector3(0.3f, 0.25f, 0.3f), mCardboard, false, new Vector3(0f, -i * 9f, 0f));
        }
    }

    static void CardboardBoxes(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "CardboardBoxes", pos, 0f);
        Box("Box", g, new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.6f), mCardboard, true, new Vector3(0f, 10f, 0f));
        Box("Box", g, new Vector3(0.1f, 0.8f, 0.05f), new Vector3(0.5f, 0.4f, 0.45f), mCardboard, false, new Vector3(0f, -15f, 0f));
        Box("Box_Open", g, new Vector3(-0.55f, 0.2f, 0.25f), new Vector3(0.4f, 0.4f, 0.4f), mCardboard, false, new Vector3(0f, 35f, 8f));
    }

    static void TrashPile(Transform p, Vector3 pos)
    {
        Transform g = Group(p, "TrashPile", pos, 0f);
        Prim(PrimitiveType.Sphere, "Trash", g, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.3f, 0.7f), mTrash, true, null);
        Prim(PrimitiveType.Sphere, "Trash", g, new Vector3(0.35f, 0.1f, 0.25f), new Vector3(0.45f, 0.25f, 0.4f), mCardboard, false, null);
        Prim(PrimitiveType.Sphere, "Trash", g, new Vector3(-0.3f, 0.18f, -0.15f), new Vector3(0.4f, 0.35f, 0.35f), mCloth, false, null);
        Box("Can", g, new Vector3(0.1f, 0.3f, -0.2f), new Vector3(0.12f, 0.2f, 0.12f), mRust, false, new Vector3(70f, 0f, 20f));
    }

    static void Puddle(Transform p, Vector3 pos, float size)
    {
        Cyl("Puddle", p, new Vector3(pos.x, 0.013f, pos.z), new Vector3(1.2f * size, 0.003f, 0.9f * size), mPuddle, new Vector3(0f, size * 37f, 0f), false);
    }

    static void Stain(Transform p, Vector3 pos, float size)
    {
        Cyl("BloodStain", p, new Vector3(pos.x, 0.014f, pos.z), new Vector3(0.8f * size, 0.002f, 0.55f * size), mStain, new Vector3(0f, size * 53f, 0f), false);
        Cyl("BloodDrop", p, new Vector3(pos.x + 0.5f * size, 0.014f, pos.z + 0.3f), new Vector3(0.2f * size, 0.002f, 0.15f * size), mStain, null, false);
    }

    static void Rug(Transform p, Vector3 pos, Vector2 size)
    {
        Box("Rug", p, new Vector3(pos.x, 0.016f, pos.z), new Vector3(size.x, 0.006f, size.y), mRug, false);
        Box("RugTear", p, new Vector3(pos.x + size.x * 0.3f, 0.02f, pos.z - size.y * 0.3f), new Vector3(0.5f, 0.004f, 0.3f), mFloor, false, new Vector3(0f, 25f, 0f));
    }

    static void Cobweb(Transform p, Vector3 corner)
    {
        // Three thin strands spanning the top of a wall corner.
        Transform g = Group(p, "Cobweb", new Vector3(corner.x, 2.3f, corner.z), 0f);
        float sx = corner.x > 0f ? -1f : 1f;
        float sz = corner.z > 0f ? -1f : 1f;
        Box("Strand", g, new Vector3(0.25f * sx, 0f, 0.25f * sz), new Vector3(0.75f, 0.01f, 0.01f), mCobweb, false, new Vector3(0f, 45f * sx * sz, 0f));
        Box("Strand", g, new Vector3(0.2f * sx, 0.12f, 0.1f * sz), new Vector3(0.5f, 0.01f, 0.01f), mCobweb, false, new Vector3(0f, 20f * sx * sz, 25f));
        Box("Strand", g, new Vector3(0.1f * sx, 0.12f, 0.2f * sz), new Vector3(0.5f, 0.01f, 0.01f), mCobweb, false, new Vector3(0f, 70f * sx * sz, -25f));
    }

    static void WindowFrame(Transform p, Vector3 pos, float yaw)
    {
        Transform w = Group(p, "WindowFrame_Broken", pos, yaw);
        Box("Glass", w, Vector3.zero, new Vector3(1.5f, 0.9f, 0.02f), mGlass, false);
        Box("Top", w, new Vector3(0f, 0.5f, 0f), new Vector3(1.7f, 0.1f, 0.08f), mDarkWood, false);
        Box("Bottom", w, new Vector3(0f, -0.5f, 0f), new Vector3(1.7f, 0.1f, 0.1f), mDarkWood, false);
        Box("Left", w, new Vector3(-0.8f, 0f, 0f), new Vector3(0.1f, 1f, 0.08f), mDarkWood, false);
        Box("Right", w, new Vector3(0.8f, 0f, 0f), new Vector3(0.1f, 1f, 0.08f), mDarkWood, false);
        Box("BrokenBar", w, new Vector3(0.1f, 0f, 0f), new Vector3(0.06f, 0.95f, 0.06f), mDarkWood, false, new Vector3(0f, 0f, 25f));
        Box("Board", w, new Vector3(0f, 0.1f, -0.05f), new Vector3(1.8f, 0.15f, 0.04f), mWood, false, new Vector3(0f, 0f, -12f));
    }

    static void Sofa(Transform p, Vector3 pos, float yaw)
    {
        Transform s = Group(p, "OldSofa", pos, yaw);
        Box("Seat", s, new Vector3(0f, 0.25f, 0.05f), new Vector3(2.2f, 0.45f, 0.8f), mFabric);
        Box("Back", s, new Vector3(0f, 0.65f, -0.35f), new Vector3(2.2f, 0.6f, 0.2f), mFabric);
        Box("ArmL", s, new Vector3(-1.05f, 0.45f, 0f), new Vector3(0.18f, 0.5f, 0.9f), mFabric);
        Box("ArmR", s, new Vector3(1.05f, 0.45f, 0f), new Vector3(0.18f, 0.5f, 0.9f), mFabric);
        Box("Cushion_Torn", s, new Vector3(0.45f, 0.5f, 0.15f), new Vector3(0.8f, 0.1f, 0.5f), mCloth, false, new Vector3(0f, 15f, 8f));
        Box("Stuffing", s, new Vector3(-0.4f, 0.48f, 0.1f), new Vector3(0.35f, 0.08f, 0.3f), mCobweb, false);
    }

    static void TornBed(Transform p, Vector3 pos)
    {
        Transform b = Group(p, "Bed_Torn", pos, 0f);
        Box("Frame", b, new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.3f, 2.4f), mDarkWood);
        Box("MattressHalfA", b, new Vector3(-0.1f, 0.52f, 0.55f), new Vector3(1.4f, 0.16f, 1.1f), mFabric, false, new Vector3(0f, 4f, 0f));
        Box("MattressHalfB", b, new Vector3(0.05f, 0.5f, -0.6f), new Vector3(1.45f, 0.14f, 1f), mFabric, false, new Vector3(3f, -6f, 0f));
        Box("Stuffing", b, new Vector3(0.2f, 0.56f, -0.02f), new Vector3(0.5f, 0.06f, 0.2f), mCobweb, false);
        Box("Headboard", b, new Vector3(0f, 0.75f, -1.15f), new Vector3(1.6f, 0.9f, 0.1f), mDarkWood);
        Cyl("MattressStain", b, new Vector3(0.2f, 0.605f, 0.5f), new Vector3(0.45f, 0.003f, 0.35f), mStain, null, false);
    }

    static void Workbench(Transform p, Vector3 pos, float yaw, float length)
    {
        Transform w = Group(p, "Workbench", pos, yaw);
        Box("Top", w, new Vector3(0f, 0.9f, 0f), new Vector3(length, 0.08f, 0.8f), mWood);
        foreach (float x in new[] { -length / 2f + 0.1f, length / 2f - 0.1f })
            Box("Legs", w, new Vector3(x, 0.45f, 0f), new Vector3(0.1f, 0.9f, 0.7f), mDarkWood);
        Box("Toolbox", w, new Vector3(-0.3f, 1.04f, 0f), new Vector3(0.5f, 0.2f, 0.25f), mRust, false, new Vector3(0f, 10f, 0f));
        Cyl("Hammer", w, new Vector3(0.4f, 0.96f, 0.1f), new Vector3(0.04f, 0.18f, 0.04f), mWood, new Vector3(0f, 0f, 90f), false);
        Box("Saw", w, new Vector3(0.2f, 0.95f, -0.2f), new Vector3(0.5f, 0.01f, 0.12f), mMetal, false, new Vector3(0f, -20f, 0f));
    }

    static void Stove(Transform p, Vector3 pos)
    {
        Transform s = Group(p, "Stove", pos, 270f);
        Box("Body", s, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.8f), mMetal);
        Box("OvenDoor", s, new Vector3(0f, 0.4f, 0.41f), new Vector3(0.7f, 0.45f, 0.02f), mGlass, false);
        foreach (Vector2 b in new[] { new Vector2(-0.2f, -0.18f), new Vector2(0.2f, -0.18f), new Vector2(-0.2f, 0.18f), new Vector2(0.2f, 0.18f) })
            Cyl("Burner", s, new Vector3(b.x, 0.91f, b.y), new Vector3(0.22f, 0.01f, 0.22f), mKiller, null, false);
        Cyl("Pot", s, new Vector3(0.2f, 1f, 0.18f), new Vector3(0.25f, 0.08f, 0.25f), mRust, null, false);
    }

    static void BuildLights(Transform p)
    {
        // Steady warm lamps (no flicker) = soft pools of light.
        HangingLamp(p, "Lamp_Storage", new Vector3(-22.5f, 0f, 13f), Warm, 5f, 10f);
        HangingLamp(p, "Lamp_Office", new Vector3(-7.5f, 0f, 11.5f), Warm, 4.5f, 10f);
        HangingLamp(p, "Lamp_Bedroom", new Vector3(7.5f, 0f, 13f), new Color(1f, 0.6f, 0.4f), 4f, 9f);
        HangingLamp(p, "Lamp_Bathroom", new Vector3(22.5f, 0f, 13f), new Color(0.95f, 0.85f, 0.65f), 4.5f, 10f);
        HangingLamp(p, "Lamp_CorridorWest", new Vector3(-15f, 0f, 4f), Warm, 4f, 9f);
        HangingLamp(p, "Lamp_CorridorEast", new Vector3(15f, 0f, 4f), Warm, 3.5f, 9f);
        HangingLamp(p, "Lamp_SafeRoom", new Vector3(-24f, 0f, -3f), new Color(1f, 0.78f, 0.5f), 5.5f, 10f);
        HangingLamp(p, "Lamp_Boiler", new Vector3(-12f, 0f, -9f), new Color(1f, 0.5f, 0.25f), 5f, 12f);
        HangingLamp(p, "Lamp_MainHall", new Vector3(4f, 0f, -8f), Warm, 5f, 12f);
        HangingLamp(p, "Lamp_MainHall2", new Vector3(5f, 0f, -16f), Warm, 3.5f, 9f);
        HangingLamp(p, "Lamp_Loading", new Vector3(22f, 0f, -9f), Warm, 5f, 13f);

        // Table lamps.
        TableLamp(p, "TableLamp_Office", new Vector3(-1.2f, 0.6f, 10.5f));
        TableLamp(p, "TableLamp_Bedroom", new Vector3(6.6f, 0.6f, 19.2f));
        TableLamp(p, "TableLamp_SafeRoom", new Vector3(-28.5f, 0.8f, 0.8f));

        // One dim red emergency light in the basement.
        Box("EmergencyLamp", p, new Vector3(-29.78f, 2.2f, -14f), new Vector3(0.12f, 0.2f, 0.45f), mEmergency, false);
        PointLight("Light_Emergency", p, new Vector3(-29.2f, 2.1f, -14f), new Color(1f, 0.08f, 0.05f), 3.5f, 10f);
    }

    static void HangingLamp(Transform p, string name, Vector3 pos, Color color, float intensity, float range)
    {
        Transform lamp = Group(p, name, new Vector3(pos.x, 0f, pos.z), 0f);
        Cyl("Cord", lamp, new Vector3(0f, 2.45f, 0f), new Vector3(0.02f, 0.25f, 0.02f), mKiller, null, false);
        Cyl("Shade", lamp, new Vector3(0f, 2.15f, 0f), new Vector3(0.55f, 0.07f, 0.55f), mShade, null, false);
        Prim(PrimitiveType.Sphere, "Bulb", lamp, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.16f, mBulb, false, null);
        PointLight("Light", lamp, new Vector3(0f, 1.95f, 0f), color, intensity, range);
    }

    static void TableLamp(Transform p, string name, Vector3 pos)
    {
        Transform lamp = Group(p, name, pos, 0f);
        Cyl("Base", lamp, new Vector3(0f, 0.02f, 0f), new Vector3(0.18f, 0.02f, 0.18f), mMetal, null, false);
        Cyl("Pole", lamp, new Vector3(0f, 0.18f, 0f), new Vector3(0.03f, 0.16f, 0.03f), mMetal, null, false);
        Cyl("Shade", lamp, new Vector3(0f, 0.38f, 0f), new Vector3(0.28f, 0.1f, 0.28f), mBulb, null, false);
        PointLight("Light", lamp, new Vector3(0f, 0.45f, 0f), new Color(1f, 0.65f, 0.35f), 2.5f, 5f);
    }

    static void SetupAtmosphere()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.07f, 0.09f);
        RenderSettings.reflectionIntensity = 0.2f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.05f);
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 60f;

        GameObject sun = GameObject.Find("Directional Light");
        if (sun)
        {
            Light l = sun.GetComponent<Light>();
            l.intensity = 0.06f;
            l.color = new Color(0.45f, 0.55f, 0.8f);
            sun.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
        }

        GameObject camGO = GameObject.Find("Main Camera");
        if (camGO)
        {
            Camera cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 40f;
            cam.farClipPlane = 150f;
            camGO.transform.rotation = Quaternion.Euler(52f, 45f, 0f);
            camGO.transform.position = new Vector3(-25.5f, 1f, -1.5f) - camGO.transform.forward * 24f;
        }
    }

    // ======================================================================
    // PHASE 2 - HIDING SPOTS
    // ======================================================================
    [MenuItem("Tools/Hide And Seek/2 - Build Hiding Spots")]
    public static void BuildHidingSpots()
    {
        LoadMaterials();
        DestroyRoot("HidingSpots");
        Transform root = new GameObject("HidingSpots").transform;

        // yaw: the side the player enters from faces local +Z.
        Spot(root, 1, HidingSpot.SpotType.Locker, -29.5f, 9f, 90f);        // Storage
        Spot(root, 2, HidingSpot.SpotType.BehindCrate, -20f, 18.6f, 180f); // Storage
        Spot(root, 3, HidingSpot.SpotType.Barrel, -27f, 18.8f, 180f);      // Storage
        Spot(root, 4, HidingSpot.SpotType.UnderTable, -7.5f, 14f, 180f);   // Office
        Spot(root, 5, HidingSpot.SpotType.Wardrobe, -3f, 19.45f, 180f);    // Office
        Spot(root, 6, HidingSpot.SpotType.UnderBed, 5f, 17.5f, 180f);      // Bedroom
        Spot(root, 7, HidingSpot.SpotType.Wardrobe, 13.45f, 12f, 270f);    // Bedroom
        Spot(root, 8, HidingSpot.SpotType.Curtain, 10f, 19f, 180f);        // Bedroom
        Spot(root, 9, HidingSpot.SpotType.Curtain, 27f, 18.5f, 180f);      // Bathroom (shower)
        Spot(root, 10, HidingSpot.SpotType.Vent, 29.55f, 12f, 270f);       // Bathroom
        Spot(root, 11, HidingSpot.SpotType.Vent, -29.55f, 4f, 90f);        // Corridor
        Spot(root, 12, HidingSpot.SpotType.Wardrobe, -29.45f, -5f, 90f);   // Safe room
        Spot(root, 13, HidingSpot.SpotType.UnderStairs, -27f, -19.1f, 0f); // Basement
        Spot(root, 14, HidingSpot.SpotType.Barrel, -20f, -19f, 0f);        // Basement
        Spot(root, 15, HidingSpot.SpotType.Locker, -12f, -19.55f, 0f);     // Boiler
        Spot(root, 16, HidingSpot.SpotType.Vent, -6.45f, -4f, 270f);       // Boiler
        Spot(root, 17, HidingSpot.SpotType.UnderTable, 4f, -10f, 0f);      // Main hall
        Spot(root, 18, HidingSpot.SpotType.Curtain, 0f, -19f, 0f);         // Main hall (stage)
        Spot(root, 19, HidingSpot.SpotType.BehindCrate, 25f, 0.6f, 180f);  // Loading
        Spot(root, 20, HidingSpot.SpotType.Locker, 29.55f, -4f, 270f);     // Loading
        BuildWingSpots(root);                                                // 21..28 in the north wing

        Save("Phase 2 - Hiding Spots");
    }

    static void Spot(Transform parent, int index, HidingSpot.SpotType type, float x, float z, float yaw)
    {
        var go = new GameObject($"HidingSpot_{index:00}");
        go.layer = IgnoreRaycastLayer; // the trigger must not block sight or tiles
        Transform t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = new Vector3(x, 0f, z);
        t.localEulerAngles = new Vector3(0f, yaw, 0f);

        BuildSpotVisual(type, t, out Vector3 hideLocal, out Vector3 enterLocal);

        var enter = new GameObject("EnterPoint").transform;
        enter.SetParent(t, false);
        enter.localPosition = enterLocal;
        enter.localRotation = Quaternion.identity;

        var hide = new GameObject("HidePoint").transform;
        hide.SetParent(t, false);
        hide.localPosition = hideLocal;
        hide.localRotation = Quaternion.Euler(0f, 180f, 0f); // face out of the spot

        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1f, 0.5f);
        trigger.size = new Vector3(2.4f, 2.2f, 2.8f);

        var spot = go.AddComponent<HidingSpot>();
        spot.spotType = type;
        spot.enterPoint = enter;
        spot.hidePoint = hide;
        spot.isOccupied = false;
        spot.outlineMaterial = mSpotOutline;
    }

    /// <summary>Replaces the look of every existing hiding spot, keeping its component and points.</summary>
    static void RebuildSpotVisuals()
    {
        GameObject root = GameObject.Find("HidingSpots");
        if (!root) return;
        foreach (HidingSpot spot in root.GetComponentsInChildren<HidingSpot>(true))
        {
            Transform t = spot.transform;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Transform c = t.GetChild(i);
                if (c.name != "EnterPoint" && c.name != "HidePoint") Object.DestroyImmediate(c.gameObject);
            }
            BuildSpotVisual(spot.spotType, t, out Vector3 hide, out Vector3 enter);
            if (spot.hidePoint) spot.hidePoint.localPosition = hide;
            if (spot.enterPoint) spot.enterPoint.localPosition = enter;
        }
    }

    static void BuildSpotVisual(HidingSpot.SpotType type, Transform r, out Vector3 hide, out Vector3 enter)
    {
        hide = new Vector3(0f, 1f, 0f);
        enter = new Vector3(0f, 1f, 1.1f);
        switch (type)
        {
            case HidingSpot.SpotType.Wardrobe:
                Box("Body", r, new Vector3(0f, 1.15f, 0f), new Vector3(1.4f, 2.2f, 0.8f), mDarkWood);
                Box("Crown", r, new Vector3(0f, 2.3f, 0.02f), new Vector3(1.5f, 0.1f, 0.88f), mWood, false);
                Box("DoorGap", r, new Vector3(0f, 1.15f, 0.41f), new Vector3(0.03f, 2f, 0.02f), mKiller, false);
                Box("DoorPanelL", r, new Vector3(-0.35f, 1.15f, 0.41f), new Vector3(0.5f, 1.8f, 0.02f), mWood, false);
                Box("DoorPanelR", r, new Vector3(0.35f, 1.15f, 0.41f), new Vector3(0.5f, 1.8f, 0.02f), mWood, false);
                Prim(PrimitiveType.Sphere, "Handle", r, new Vector3(-0.08f, 1.15f, 0.44f), Vector3.one * 0.06f, mMetal, false, null);
                Prim(PrimitiveType.Sphere, "Handle", r, new Vector3(0.08f, 1.15f, 0.44f), Vector3.one * 0.06f, mMetal, false, null);
                enter = new Vector3(0f, 1f, 1.25f);
                break;

            case HidingSpot.SpotType.Locker:
                Box("Body", r, new Vector3(0f, 1f, 0f), new Vector3(0.7f, 2f, 0.6f), mMetal);
                for (int i = 0; i < 3; i++)
                    Box("Slot", r, new Vector3(0f, 1.55f + i * 0.08f, 0.31f), new Vector3(0.4f, 0.03f, 0.02f), mKiller, false);
                Box("Handle", r, new Vector3(0.25f, 1f, 0.32f), new Vector3(0.04f, 0.18f, 0.03f), mRust, false);
                Box("NumberPlate", r, new Vector3(0f, 1.85f, 0.31f), new Vector3(0.15f, 0.08f, 0.02f), mCobweb, false);
                enter = new Vector3(0f, 1f, 1f);
                break;

            case HidingSpot.SpotType.UnderBed:
                Box("Frame", r, new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.3f, 2.4f), mDarkWood);
                Box("Mattress", r, new Vector3(0f, 0.6f, 0f), new Vector3(1.5f, 0.2f, 2.3f), mFabric);
                Box("Pillow", r, new Vector3(0f, 0.75f, -0.85f), new Vector3(0.9f, 0.12f, 0.4f), mCobweb, false);
                Box("Headboard", r, new Vector3(0f, 0.8f, -1.15f), new Vector3(1.6f, 1f, 0.1f), mDarkWood);
                Box("SheetLeft", r, new Vector3(-0.82f, 0.4f, 0.3f), new Vector3(0.04f, 0.6f, 1.6f), mCloth, false);
                Box("SheetRight", r, new Vector3(0.82f, 0.4f, 0.3f), new Vector3(0.04f, 0.6f, 1.6f), mCloth, false);
                hide = new Vector3(0f, 0.5f, 0f);
                enter = new Vector3(0f, 1f, 1.9f);
                break;

            case HidingSpot.SpotType.BehindCrate:
                Box("CrateBig", r, new Vector3(0f, 0.65f, 0f), Vector3.one * 1.3f, mWood);
                Box("Plank", r, new Vector3(0f, 0.65f, 0.66f), new Vector3(1.3f, 0.12f, 0.02f), mDarkWood, false, new Vector3(0f, 0f, 35f));
                Box("CrateTop", r, new Vector3(0.1f, 1.75f, 0f), Vector3.one * 0.9f, mWood, true, new Vector3(0f, 15f, 0f));
                Box("CrateSide", r, new Vector3(1.1f, 0.45f, -0.3f), Vector3.one * 0.9f, mWood, true, new Vector3(0f, -20f, 0f));
                hide = new Vector3(0f, 1f, -1f);
                enter = new Vector3(0f, 1f, 1.3f);
                break;

            case HidingSpot.SpotType.Barrel:
                Cyl("Barrel", r, new Vector3(0f, 0.7f, 0f), new Vector3(0.95f, 0.7f, 0.95f), mRust);
                Cyl("Ring", r, new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.03f, 1f), mMetal, null, false);
                Cyl("Ring", r, new Vector3(0f, 1.05f, 0f), new Vector3(1f, 0.03f, 1f), mMetal, null, false);
                Cyl("Lid", r, new Vector3(0f, 1.41f, 0.1f), new Vector3(0.9f, 0.02f, 0.9f), mMetal, new Vector3(8f, 0f, 0f), false);
                enter = new Vector3(0f, 1f, 1f);
                break;

            case HidingSpot.SpotType.UnderTable:
                Box("Top", r, new Vector3(0f, 0.8f, 0f), new Vector3(1.8f, 0.08f, 1f), mWood);
                foreach (float lx in new[] { -0.8f, 0.8f })
                    foreach (float lz in new[] { -0.42f, 0.42f })
                        Box("Leg", r, new Vector3(lx, 0.4f, lz), new Vector3(0.08f, 0.8f, 0.08f), mWood);
                Box("ClothBack", r, new Vector3(0f, 0.45f, -0.52f), new Vector3(1.84f, 0.7f, 0.02f), mCloth, false);
                Box("ClothLeft", r, new Vector3(-0.92f, 0.45f, 0f), new Vector3(0.02f, 0.7f, 1.04f), mCloth, false);
                Box("ClothRight", r, new Vector3(0.92f, 0.45f, 0f), new Vector3(0.02f, 0.7f, 1.04f), mCloth, false);
                Box("ClothFront", r, new Vector3(0f, 0.6f, 0.52f), new Vector3(1.84f, 0.4f, 0.02f), mCloth, false);
                Box("Candle", r, new Vector3(0.5f, 0.92f, 0f), new Vector3(0.06f, 0.16f, 0.06f), mCobweb, false);
                hide = new Vector3(0f, 0.5f, 0f);
                enter = new Vector3(0f, 1f, 1.15f);
                break;

            case HidingSpot.SpotType.Curtain:
                Box("Rod", r, new Vector3(0f, 2.45f, 0f), new Vector3(2.1f, 0.05f, 0.05f), mMetal, false);
                Prim(PrimitiveType.Sphere, "RodEnd", r, new Vector3(-1.05f, 2.45f, 0f), Vector3.one * 0.08f, mMetal, false, null);
                Prim(PrimitiveType.Sphere, "RodEnd", r, new Vector3(1.05f, 2.45f, 0f), Vector3.one * 0.08f, mMetal, false, null);
                Box("Cloth", r, new Vector3(0f, 1.25f, 0f), new Vector3(1.9f, 2.3f, 0.06f), mFabric);
                for (int i = 0; i < 4; i++)
                    Box("Fold", r, new Vector3(-0.7f + i * 0.47f, 1.25f, 0.05f), new Vector3(0.12f, 2.3f, 0.05f), mCloth, false);
                hide = new Vector3(0f, 1f, -0.5f);
                enter = new Vector3(0f, 1f, 0.9f);
                break;

            case HidingSpot.SpotType.Vent:
                Box("Duct", r, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1f, 0.6f), mMetal);
                Box("Grate", r, new Vector3(0f, 0.5f, 0.31f), new Vector3(1f, 0.8f, 0.02f), mKiller, false);
                for (int i = 0; i < 5; i++)
                    Box("Bar", r, new Vector3(0f, 0.18f + i * 0.16f, 0.33f), new Vector3(1f, 0.04f, 0.02f), mMetal, false);
                Box("Frame", r, new Vector3(0f, 0.5f, 0.32f), new Vector3(1.1f, 0.9f, 0.01f), mRust, false);
                hide = new Vector3(0f, 0.5f, 0f);
                enter = new Vector3(0f, 1f, 1f);
                break;

            case HidingSpot.SpotType.UnderStairs:
                for (int i = 0; i < 5; i++)
                    Box($"Step{i}", r, new Vector3(-1.2f + i * 0.6f, (i + 1) * 0.5f - 0.05f, 0f), new Vector3(0.6f, 0.1f, 1.4f), mWood);
                Box("BackPanel", r, new Vector3(0f, 1.25f, -0.72f), new Vector3(3f, 2.5f, 0.06f), mDarkWood);
                Box("FrontBoards", r, new Vector3(-0.9f, 0.45f, 0.7f), new Vector3(1.2f, 0.9f, 0.05f), mDarkWood, false);
                for (int i = 0; i < 5; i++)
                    Box("RailPost", r, new Vector3(-1.2f + i * 0.6f, (i + 1) * 0.5f + 0.4f, 0.65f), new Vector3(0.05f, 0.8f, 0.05f), mDarkWood, false);
                Box("Rail", r, new Vector3(0f, 2.05f, 0.65f), new Vector3(3.1f, 0.06f, 0.06f), mDarkWood, false, new Vector3(0f, 0f, 39f));
                hide = new Vector3(0.6f, 0.8f, 0f);
                enter = new Vector3(0.6f, 1f, 1.3f);
                break;
        }
    }

    // ======================================================================
    // PHASE 3 - PLAYER + GRID
    // ======================================================================
    [MenuItem("Tools/Hide And Seek/3 - Build Player")]
    public static void BuildPlayer()
    {
        LoadMaterials();
        DestroyRoot("Player");

        GameObject spawn = GameObject.Find("Map/PlayerSpawn");
        Vector3 start = spawn ? spawn.transform.position : new Vector3(0.5f, 1f, 0.5f);

        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = start; // pivot = body center, 1 unit above the floor

        // Personal light circle around the player.
        Light own = PointLight("PlayerLight", player.transform, new Vector3(0f, 1.5f, 0f), new Color(0.8f, 0.85f, 1f), 2f, 7f);
        own.transform.localScale = Vector3.one;

        BuildHumanModel(player.transform);
        player.AddComponent<PlayerController>().stepTime = 0.32f;

        GameObject camGO = GameObject.Find("Main Camera");
        if (camGO)
        {
            var follow = camGO.GetComponent<CameraFollow>();
            if (!follow) follow = camGO.AddComponent<CameraFollow>();
            follow.target = player.transform;
            follow.pitch = 52f;
            follow.yaw = 45f;
            follow.distance = 24f;
            follow.SnapToTarget();
        }

        BuildGrid();
        Save("Phase 3 - Player");
    }

    static void BuildGrid()
    {
        DestroyRoot("Grid");
        var grid = new GameObject("Grid").AddComponent<GridManager>();
        // 2x2-unit tiles (30 x 20 over the 60 x 40 building).
        grid.origin = new Vector2(-30f, -20f);
        grid.tileSize = 2f;
        grid.width = 30;
        grid.height = 25; // 20 rows for the old building + 5 for the north wing
        grid.probeHalfSize = 0.41f;
        grid.propHalfSize = 0.49f;
        grid.lineWidth = 0.045f;
        grid.lineMaterial = mGridLine;
        grid.reachMaterial = mTileReach;
        grid.hoverMaterial = mTileHover;
        grid.pathMaterial = mTilePath;
    }

    /// <summary>
    /// Human player look from primitives under a "Model" child (animated by PlayerAnimator).
    /// Removes the old capsule look but keeps the PlayerLight and every component on the root.
    /// Feet at local y = -1 (floor), top of the hair at about +1.
    /// </summary>
    public static void BuildHumanModel(Transform player)
    {
        Material skin = Mat("HS_PlayerSkin", new Color(0.86f, 0.66f, 0.52f), null, 0.3f);
        Material hair = Mat("HS_PlayerHair", new Color(0.18f, 0.11f, 0.06f));
        Material jacket = Mat("HS_PlayerJacket", new Color(0.62f, 0.52f, 0.36f));
        Material shirt = Mat("HS_PlayerShirt", new Color(0.85f, 0.85f, 0.82f));
        Material jeans = Mat("HS_PlayerJeans", new Color(0.1f, 0.13f, 0.24f));
        Material shoes = Mat("HS_PlayerShoes", new Color(0.08f, 0.06f, 0.05f), null, 0.4f);
        Material lips = Mat("HS_PlayerMouth", new Color(0.4f, 0.12f, 0.1f));
        Material dark = Mat("HS_Killer", new Color(0.02f, 0.02f, 0.02f));
        Material metal = Mat("HS_Metal", new Color(0.2f, 0.22f, 0.24f), null, 0.4f);
        Material bulb = Mat("HS_Bulb", new Color(1f, 0.8f, 0.5f), new Color(1f, 0.65f, 0.3f) * 1.5f);

        // Remove the old capsule look (the light circle is kept exactly where it was).
        Transform light = player.Find("PlayerLight");
        Vector3 lightWorld = light ? light.position : Vector3.zero;
        var mr = player.GetComponent<MeshRenderer>();
        if (mr) Object.DestroyImmediate(mr);
        var mf = player.GetComponent<MeshFilter>();
        if (mf) Object.DestroyImmediate(mf);
        DestroyChild(player, "Nose");
        DestroyChild(player, "Model");
        player.localScale = Vector3.one;
        if (light)
        {
            light.position = lightWorld;
            light.localScale = Vector3.one;
        }

        Transform model = Child(player, "Model");

        // Legs (pivot at the hip) with shoes.
        Transform legL = Group(model, "LegL", new Vector3(-0.12f, -0.08f, 0f), 0f);
        Transform legR = Group(model, "LegR", new Vector3(0.12f, -0.08f, 0f), 0f);
        foreach (Transform leg in new[] { legL, legR })
        {
            Cap("Jeans", leg, new Vector3(0f, -0.42f, 0f), new Vector3(0.18f, 0.43f, 0.18f), jeans);
            Box("Shoe", leg, new Vector3(0f, -0.87f, 0.05f), new Vector3(0.17f, 0.1f, 0.3f), shoes, false);
        }

        // Torso: light jacket open over a white shirt.
        Box("Belt", model, new Vector3(0f, -0.06f, 0f), new Vector3(0.44f, 0.06f, 0.26f), shoes, false);
        GameObject torso = Box("Torso", model, new Vector3(0f, 0.24f, 0f), new Vector3(0.46f, 0.58f, 0.27f), jacket, false);
        Box("Shirt", model, new Vector3(0f, 0.25f, 0.137f), new Vector3(0.14f, 0.52f, 0.02f), shirt, false);
        Box("CollarL", model, new Vector3(-0.09f, 0.5f, 0.12f), new Vector3(0.1f, 0.08f, 0.04f), jacket, false, new Vector3(0f, 0f, 25f));
        Box("CollarR", model, new Vector3(0.09f, 0.5f, 0.12f), new Vector3(0.1f, 0.08f, 0.04f), jacket, false, new Vector3(0f, 0f, -25f));

        // Arms (pivot at the shoulder) with hands; small flashlight in the right hand (no extra light).
        Transform armL = Group(model, "ArmL", new Vector3(-0.3f, 0.48f, 0f), 0f);
        Transform armR = Group(model, "ArmR", new Vector3(0.3f, 0.48f, 0f), 0f);
        foreach (Transform arm in new[] { armL, armR })
        {
            Cap("Sleeve", arm, new Vector3(0f, -0.3f, 0f), new Vector3(0.13f, 0.32f, 0.13f), jacket);
            Prim(PrimitiveType.Sphere, "Hand", arm, new Vector3(0f, -0.62f, 0.02f), Vector3.one * 0.11f, skin, false, null);
        }
        Cyl("Flashlight", armR, new Vector3(0f, -0.64f, 0.1f), new Vector3(0.05f, 0.08f, 0.05f), metal, new Vector3(90f, 0f, 0f), false);
        Cyl("FlashlightLens", armR, new Vector3(0f, -0.64f, 0.18f), new Vector3(0.055f, 0.01f, 0.055f), bulb, new Vector3(90f, 0f, 0f), false);

        // Neck + head with a simple face and short hair.
        Cyl("Neck", model, new Vector3(0f, 0.57f, 0f), new Vector3(0.11f, 0.05f, 0.11f), skin, null, false);
        Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0f, 0.78f, 0f), new Vector3(0.33f, 0.36f, 0.33f), skin, false, null);
        Prim(PrimitiveType.Sphere, "EarL", model, new Vector3(-0.165f, 0.78f, 0f), new Vector3(0.04f, 0.07f, 0.05f), skin, false, null);
        Prim(PrimitiveType.Sphere, "EarR", model, new Vector3(0.165f, 0.78f, 0f), new Vector3(0.04f, 0.07f, 0.05f), skin, false, null);
        Prim(PrimitiveType.Sphere, "HairTop", model, new Vector3(0f, 0.89f, -0.015f), new Vector3(0.35f, 0.2f, 0.36f), hair, false, null);
        Prim(PrimitiveType.Sphere, "HairBack", model, new Vector3(0f, 0.8f, -0.1f), new Vector3(0.33f, 0.26f, 0.2f), hair, false, null);
        foreach (float x in new[] { -0.065f, 0.065f })
        {
            Prim(PrimitiveType.Sphere, "Eye", model, new Vector3(x, 0.8f, 0.155f), Vector3.one * 0.045f, dark, false, null);
            Box("Brow", model, new Vector3(x, 0.85f, 0.158f), new Vector3(0.07f, 0.015f, 0.015f), hair, false);
        }
        Prim(PrimitiveType.Sphere, "Nose", model, new Vector3(0f, 0.765f, 0.165f), new Vector3(0.04f, 0.05f, 0.04f), skin, false, null);
        Box("Mouth", model, new Vector3(0f, 0.705f, 0.155f), new Vector3(0.07f, 0.012f, 0.01f), lips, false);

        var anim = model.gameObject.AddComponent<PlayerAnimator>();
        anim.model = model;
        anim.torso = torso.transform;
        anim.legL = legL;
        anim.legR = legR;
        anim.armL = armL;
        anim.armR = armR;
    }

    // ======================================================================
    // PHASE 4 - KILLER (CLOWN)
    // ======================================================================
    [MenuItem("Tools/Hide And Seek/4 - Build Killer")]
    public static void BuildKiller()
    {
        LoadMaterials();
        DestroyRoot("Killer");
        DestroyRoot("KillerWaypoints");
        DestroyRoot("NavMeshBaker");

        // Patrol route through every room except the safe room.
        Transform wpRoot = new GameObject("KillerWaypoints").transform;
        var points = new (string name, Vector3 pos)[]
        {
            ("WP_Storage", new Vector3(-22f, 0f, 12f)), ("WP_Office", new Vector3(-7.5f, 0f, 11f)),
            ("WP_Bedroom", new Vector3(8f, 0f, 11f)), ("WP_Bathroom", new Vector3(22f, 0f, 11f)),
            ("WP_CorridorWest", new Vector3(-20f, 0f, 4f)), ("WP_CorridorEast", new Vector3(15f, 0f, 4f)),
            ("WP_Basement", new Vector3(-24f, 0f, -13f)), ("WP_Boiler", new Vector3(-12f, 0f, -8f)),
            ("WP_HallNorth", new Vector3(4f, 0f, -4f)), ("WP_HallSouth", new Vector3(4f, 0f, -15f)),
            ("WP_Loading", new Vector3(22f, 0f, -10f)),
            ("WP_Garage", new Vector3(-21f, 0f, 22f)), ("WP_Kitchen", new Vector3(-5f, 0f, 23f)),
            ("WP_Bedroom2", new Vector3(9f, 0f, 24f)), ("WP_Cellar", new Vector3(23f, 0f, 24f)),
        };
        var waypoints = new List<Transform>();
        foreach (var (name, pos) in points)
        {
            var wp = new GameObject(name).transform;
            wp.SetParent(wpRoot, false);
            wp.localPosition = pos;
            waypoints.Add(wp);
        }

        var killer = new GameObject("Killer");
        killer.transform.position = new Vector3(8.5f, 0f, 13.5f);
        BuildClown(killer.transform);
        var ai = killer.AddComponent<KillerAI>();
        ai.waypoints = waypoints.ToArray();
        // Tile-based values for 2x2 tiles (same world distances as before).
        ai.stepTime = 0.4f;
        ai.hearDashTiles = 3f;
        ai.searchRadiusTiles = 5f;
        ai.closeTiles = 2f;

        // NavMesh is (re)baked at play start over the map and hiding spots.
        var baker = new GameObject("NavMeshBaker").AddComponent<RuntimeNavMeshBaker>();
        baker.transform.position = new Vector3(0f, 0f, 5f); // covers z -27..37 (building + north wing)
        baker.size = new Vector3(70f, 10f, 64f);
        var roots = new List<Transform>();
        GameObject map = GameObject.Find("Map");
        GameObject spots = GameObject.Find("HidingSpots");
        if (map) roots.Add(map.transform);
        if (spots) roots.Add(spots.transform);
        baker.roots = roots.ToArray();

        Save("Phase 4 - Killer");
    }

    /// <summary>Creepy clown from primitives under a "Visual" child (bobbing). No colliders.</summary>
    static void BuildClown(Transform killer)
    {
        Transform v = Child(killer, "Visual");

        // Oversized shoes and baggy two-tone legs.
        Prim(PrimitiveType.Sphere, "ShoeL", v, new Vector3(-0.22f, 0.12f, 0.18f), new Vector3(0.38f, 0.24f, 0.78f), mShoe, false, null);
        Prim(PrimitiveType.Sphere, "ShoeR", v, new Vector3(0.22f, 0.12f, 0.18f), new Vector3(0.38f, 0.24f, 0.78f), mShoe, false, null);
        Cap("LegL", v, new Vector3(-0.2f, 0.62f, 0f), new Vector3(0.4f, 0.42f, 0.4f), mSuitA);
        Cap("LegR", v, new Vector3(0.2f, 0.62f, 0f), new Vector3(0.4f, 0.42f, 0.4f), mSuitB);

        // Baggy striped suit.
        Cap("Torso", v, new Vector3(0f, 1.55f, 0f), new Vector3(1f, 0.75f, 0.85f), mSuitA);
        for (int i = 0; i < 4; i++)
            Cyl("Stripe", v, new Vector3(0f, 1.22f + i * 0.2f, 0f), new Vector3(1.05f, 0.05f, 0.9f), mSuitB, null, false);
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Sphere, "PomPom", v, new Vector3(0f, 1.35f + i * 0.25f, 0.46f), Vector3.one * 0.12f, mClownRed, false, null);

        // Arms + dirty gloves.
        Cap("ArmL", v, new Vector3(-0.6f, 1.58f, 0.02f), new Vector3(0.26f, 0.45f, 0.26f), mSuitB, new Vector3(0f, 0f, -18f));
        Cap("ArmR", v, new Vector3(0.6f, 1.58f, 0.02f), new Vector3(0.26f, 0.45f, 0.26f), mSuitA, new Vector3(0f, 0f, 18f));
        Prim(PrimitiveType.Sphere, "GloveL", v, new Vector3(-0.74f, 1.12f, 0.06f), Vector3.one * 0.22f, mGlove, false, null);
        Prim(PrimitiveType.Sphere, "GloveR", v, new Vector3(0.74f, 1.12f, 0.06f), Vector3.one * 0.22f, mGlove, false, null);

        // Ruffled collar: ring of flattened white spheres.
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f;
            Prim(PrimitiveType.Sphere, "Ruffle", v, new Vector3(Mathf.Cos(a) * 0.42f, 2.28f + (i % 2) * 0.03f, Mathf.Sin(a) * 0.38f),
                new Vector3(0.32f, 0.1f, 0.32f), mGlove, false, new Vector3((i % 2) * 12f, -a * Mathf.Rad2Deg, 0f));
        }

        // Pale head.
        Prim(PrimitiveType.Sphere, "Face", v, new Vector3(0f, 2.65f, 0f), new Vector3(0.62f, 0.66f, 0.6f), mClownFace, false, null);

        // Hollow black eyes with dark blue paint lines through them.
        foreach (float x in new[] { -0.11f, 0.11f })
        {
            Box("EyePaint", v, new Vector3(x, 2.72f, 0.268f), new Vector3(0.035f, 0.3f, 0.02f), mClownBlue, false, new Vector3(-10f, 0f, 0f));
            Prim(PrimitiveType.Sphere, "Eye", v, new Vector3(x, 2.72f, 0.255f), new Vector3(0.12f, 0.15f, 0.08f), mKiller, false, null);
        }

        // Red nose with a faint glow.
        Prim(PrimitiveType.Sphere, "Nose", v, new Vector3(0f, 2.6f, 0.31f), Vector3.one * 0.15f, mClownNose, false, null);
        PointLight("NoseGlow", v, new Vector3(0f, 2.55f, 0.7f), new Color(1f, 0.08f, 0.04f), 0.25f, 1.2f);

        // Big red smile: small bars along an arc on the face, corners pulled up.
        const float headY = 2.65f;
        for (int i = -4; i <= 4; i++)
        {
            float x = i * 0.045f;
            float y = 2.47f + 0.09f * (i / 4f) * (i / 4f);
            float z = Mathf.Sqrt(Mathf.Max(0.001f, 0.3f * 0.3f - x * x - (y - headY) * (y - headY))) + 0.004f;
            float slope = 2f * 0.09f * (i / 4f) / 4f / 0.045f;
            Quaternion rot = Quaternion.LookRotation(new Vector3(x, (y - headY) * 0.5f, z).normalized) * Quaternion.Euler(0f, 0f, Mathf.Atan(slope) * Mathf.Rad2Deg);
            GameObject bar = Box("Smile", v, new Vector3(x, y, z), new Vector3(0.055f, 0.035f, 0.02f), mClownRed, false);
            bar.transform.localRotation = rot;
        }
        Box("SmileCornerL", v, new Vector3(-0.2f, 2.6f, 0.215f), new Vector3(0.025f, 0.12f, 0.02f), mClownRed, false, new Vector3(0f, -40f, 20f));
        Box("SmileCornerR", v, new Vector3(0.2f, 2.6f, 0.215f), new Vector3(0.025f, 0.12f, 0.02f), mClownRed, false, new Vector3(0f, 40f, -20f));

        // Tall messy hair tufts on both sides (red / blue).
        foreach (float sx in new[] { -1f, 1f })
        {
            Material a = sx < 0f ? mClownRed : mClownBlue;
            Material b = sx < 0f ? mClownBlue : mClownRed;
            Prim(PrimitiveType.Sphere, "Hair", v, new Vector3(0.32f * sx, 2.78f, -0.05f), Vector3.one * 0.26f, a, false, null);
            Prim(PrimitiveType.Sphere, "Hair", v, new Vector3(0.41f * sx, 2.93f, -0.12f), Vector3.one * 0.22f, b, false, null);
            Prim(PrimitiveType.Sphere, "Hair", v, new Vector3(0.42f * sx, 2.68f, 0.05f), Vector3.one * 0.18f, b, false, null);
            Prim(PrimitiveType.Sphere, "Hair", v, new Vector3(0.28f * sx, 3.02f, -0.02f), new Vector3(0.14f, 0.34f, 0.14f), a, false, new Vector3(0f, 0f, -25f * sx));
            Prim(PrimitiveType.Sphere, "HairTall", v, new Vector3(0.2f * sx, 3.12f, -0.1f), new Vector3(0.12f, 0.4f, 0.12f), b, false, new Vector3(0f, 0f, -15f * sx));
        }

        v.gameObject.AddComponent<KillerBob>();
    }

    // ======================================================================
    // PHASE 5 - GAME LOOP + UI
    // ======================================================================
    [MenuItem("Tools/Hide And Seek/5 - Build Game Loop")]
    public static void BuildGameLoop()
    {
        LoadMaterials();
        DestroyRoot("Keys");
        DestroyRoot("GameManager");
        DestroyRoot("GameUI");

        // Keys sit on tile centers (.5).
        Transform keys = new GameObject("Keys").transform;
        Key(keys, "Key_1_Storage", new Vector3(-19.5f, 1f, 12.5f));
        Key(keys, "Key_2_Bathroom", new Vector3(21.5f, 1f, 10.5f));
        Key(keys, "Key_3_Boiler", new Vector3(-11.5f, 1f, -12.5f));
        // Random fair key tiles every game (original positions above are kept for randomizeKeys = false).
        var placer = keys.gameObject.AddComponent<KeyPlacer>();
        foreach (Transform k in keys) placer.originalPositions.Add(k.position);

        new GameObject("GameManager").AddComponent<GameManager>().keysRequired = 3;
        new GameObject("GameUI").AddComponent<GameUI>();
        DestroyRoot("AudioManager");
        new GameObject("AudioManager").AddComponent<AudioManager>();

        // Scene must be in Build Settings for Restart (scene reload) to work.
        string path = SceneManager.GetActiveScene().path;
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!list.Exists(s => s.path == path))
        {
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        Save("Phase 5 - Game Loop");
    }

    static void Key(Transform parent, string name, Vector3 pos)
    {
        var key = new GameObject(name);
        key.transform.SetParent(parent, false);
        key.transform.position = pos;
        Cyl("Ring", key.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.35f, 0.04f, 0.35f), mKey, new Vector3(90f, 0f, 0f), false);
        Box("Shaft", key.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.08f, 0.4f, 0.08f), mKey, false);
        Box("Tooth", key.transform, new Vector3(0.08f, -0.3f, 0f), new Vector3(0.12f, 0.06f, 0.06f), mKey, false);
        PointLight("KeyGlow", key.transform, Vector3.zero, new Color(1f, 0.8f, 0.3f), 1.5f, 3f);
        key.AddComponent<KeyPickup>();
    }

    [MenuItem("Tools/Hide And Seek/Build All Phases")]
    public static void BuildAll()
    {
        BuildMap();
        BuildHidingSpots();
        BuildPlayer();
        BuildKiller();
        BuildGameLoop();
    }

    // ======================================================================
    // UPGRADE v1 -> v2 (in place, nothing rebuilt from scratch)
    // ======================================================================
    [MenuItem("Tools/Hide And Seek/Upgrade v2/Part 1 - Steady Lights")]
    public static void UpgradeLights()
    {
        LoadMaterials();
        // Remove every old flicker component (found by name, the script itself is gone).
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Name == "FlickerLight") Object.DestroyImmediate(mb);

        GameObject lightsGO = GameObject.Find("Map/Lights");
        if (lightsGO)
        {
            Transform lights = lightsGO.transform;
            for (int i = lights.childCount - 1; i >= 0; i--)
            {
                Transform c = lights.GetChild(i);
                if (c.name != "ExitLight") Object.DestroyImmediate(c.gameObject);
            }
            BuildLights(lights);
        }
        Save("Upgrade Part 1 - Steady Lights");
    }

    [MenuItem("Tools/Hide And Seek/Upgrade v2/Part 2 - Grid And Turns")]
    public static void UpgradeGrid()
    {
        LoadMaterials();
        GameObject spawn = GameObject.Find("Map/PlayerSpawn");
        if (spawn) spawn.transform.position = new Vector3(-25.5f, 1f, -1.5f);

        GameObject player = GameObject.Find("Player");
        if (player)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc) Object.DestroyImmediate(cc);
            if (spawn) player.transform.position = spawn.transform.position;
        }

        GameObject killer = GameObject.Find("Killer");
        if (killer)
        {
            var agent = killer.GetComponent<NavMeshAgent>();
            if (agent) Object.DestroyImmediate(agent);
            killer.transform.position = new Vector3(8.5f, 0f, 13.5f);
        }

        // Keys onto tile centers.
        GameObject keys = GameObject.Find("Keys");
        if (keys)
            foreach (Transform k in keys.transform)
                k.position = new Vector3(Mathf.Floor(k.position.x) + 0.5f, 1f, Mathf.Floor(k.position.z) + 0.5f);

        GameObject cam = GameObject.Find("Main Camera");
        if (cam && cam.GetComponent<CameraFollow>()) cam.GetComponent<CameraFollow>().SnapToTarget();

        BuildGrid();
        Save("Upgrade Part 2 - Grid And Turns");
    }

    [MenuItem("Tools/Hide And Seek/Upgrade v2/Part 3 - Clown Killer")]
    public static void UpgradeClown()
    {
        LoadMaterials();
        GameObject killer = GameObject.Find("Killer");
        if (!killer) return;
        for (int i = killer.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(killer.transform.GetChild(i).gameObject);
        BuildClown(killer.transform);
        Save("Upgrade Part 3 - Clown Killer");
    }

    [MenuItem("Tools/Hide And Seek/Upgrade v2/Part 4 - Map Details")]
    public static void UpgradeMapDetails()
    {
        LoadMaterials(); // also darkens wall colors
        GameObject map = GameObject.Find("Map");
        if (!map) return;
        BuildMapDetails(map.transform);
        RebuildSpotVisuals();
        Save("Upgrade Part 4 - Map Details");
    }

    // ======================================================================
    // Helpers
    // ======================================================================

    /// <summary>Wall along X at a fixed z, with door gaps centered at the given x values.</summary>
    static void WallX(Transform p, string name, float z, float x0, float x1, params float[] doors)
    {
        float start = x0;
        var sorted = new List<float>(doors);
        sorted.Sort();
        int i = 0;
        foreach (float d in sorted)
        {
            Segment(p, $"{name}_{i++}", new Vector3((start + d - Door / 2f) / 2f, WallH / 2f, z), (d - Door / 2f) - start, true);
            start = d + Door / 2f;
        }
        Segment(p, $"{name}_{i}", new Vector3((start + x1) / 2f, WallH / 2f, z), x1 - start, true);
    }

    /// <summary>Wall along Z at a fixed x, with door gaps centered at the given z values.</summary>
    static void WallZ(Transform p, string name, float x, float z0, float z1, params float[] doors)
    {
        float start = z0;
        var sorted = new List<float>(doors);
        sorted.Sort();
        int i = 0;
        foreach (float d in sorted)
        {
            Segment(p, $"{name}_{i++}", new Vector3(x, WallH / 2f, (start + d - Door / 2f) / 2f), (d - Door / 2f) - start, false);
            start = d + Door / 2f;
        }
        Segment(p, $"{name}_{i}", new Vector3(x, WallH / 2f, (start + z1) / 2f), z1 - start, false);
    }

    static void Segment(Transform p, string name, Vector3 center, float length, bool alongX)
    {
        if (length < 0.05f) return;
        Vector3 size = alongX ? new Vector3(length, WallH, WallT) : new Vector3(WallT, WallH, length);
        Box(name, p, center, size, mWall);
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static Transform Group(Transform parent, string name, Vector3 pos, float yaw)
    {
        Transform t = Child(parent, name);
        t.localPosition = pos;
        t.localEulerAngles = new Vector3(0f, yaw, 0f);
        return t;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider, Vector3? euler)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = true, Vector3? euler = null)
        => Prim(PrimitiveType.Cube, name, parent, pos, scale, mat, collider, euler);

    static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, bool collider = true)
        => Prim(PrimitiveType.Cylinder, name, parent, pos, scale, mat, collider, euler);

    static GameObject Cap(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        => Prim(PrimitiveType.Capsule, name, parent, pos, scale, mat, false, euler);

    static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static void DestroyRoot(string name)
    {
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) Object.DestroyImmediate(go);
    }

    static void DestroyChild(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    static void Save(string phase)
    {
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[HideAndSeek] {phase} built and scene saved.");
    }

    static void LoadMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        mFloor = Mat("HS_Floor", new Color(0.11f, 0.1f, 0.09f));
        mOutside = Mat("HS_Outside", new Color(0.04f, 0.045f, 0.04f));
        mWall = Mat("HS_Wall", new Color(0.13f, 0.125f, 0.12f));
        mWood = Mat("HS_Wood", new Color(0.24f, 0.15f, 0.08f));
        mDarkWood = Mat("HS_DarkWood", new Color(0.12f, 0.07f, 0.04f));
        mMetal = Mat("HS_Metal", new Color(0.2f, 0.22f, 0.24f), null, 0.4f);
        mRust = Mat("HS_Rust", new Color(0.25f, 0.11f, 0.05f), null, 0.2f);
        mFabric = Mat("HS_Fabric", new Color(0.25f, 0.05f, 0.05f));
        mCloth = Mat("HS_Cloth", new Color(0.16f, 0.14f, 0.18f));
        mTile = Mat("HS_Tile", new Color(0.4f, 0.42f, 0.4f), null, 0.5f);

        mConcrete = Mat("HS_FloorConcrete", new Color(0.15f, 0.15f, 0.14f));
        mDarkConcrete = Mat("HS_FloorConcreteDark", new Color(0.085f, 0.085f, 0.085f));
        mWoodFloor = Mat("HS_FloorWood", new Color(0.16f, 0.1f, 0.06f), null, 0.25f);
        mTileFloor = Mat("HS_FloorTile", new Color(0.22f, 0.24f, 0.23f), null, 0.55f);
        mBaseboard = Mat("HS_Baseboard", new Color(0.06f, 0.05f, 0.045f));
        mRug = Mat("HS_Rug", new Color(0.2f, 0.05f, 0.06f));
        mPuddle = Mat("HS_Puddle", new Color(0.02f, 0.025f, 0.03f), null, 0.95f);
        mStain = Mat("HS_BloodStain", new Color(0.17f, 0.01f, 0.01f), null, 0.4f);
        mCobweb = Mat("HS_Cobweb", new Color(0.5f, 0.5f, 0.48f));
        mCardboard = Mat("HS_Cardboard", new Color(0.28f, 0.2f, 0.12f));
        mShade = Mat("HS_LampShade", new Color(0.08f, 0.08f, 0.07f), null, 0.3f);
        mBulb = Mat("HS_Bulb", new Color(1f, 0.8f, 0.5f), new Color(1f, 0.65f, 0.3f) * 1.5f);
        mEmergency = Mat("HS_EmergencyLight", new Color(0.6f, 0.02f, 0.02f), new Color(1f, 0.05f, 0.02f) * 2f);
        mGlass = Mat("HS_DirtyGlass", new Color(0.05f, 0.07f, 0.08f), null, 0.95f);
        mTrash = Mat("HS_Trash", new Color(0.1f, 0.09f, 0.07f));

        mPlayer = Mat("HS_Player", new Color(0.35f, 0.45f, 0.6f));
        mKiller = Mat("HS_Killer", new Color(0.02f, 0.02f, 0.02f));
        mMask = Mat("HS_Mask", new Color(0.92f, 0.92f, 0.88f), null, 0.3f);
        mKey = Mat("HS_Key", new Color(0.9f, 0.7f, 0.2f), new Color(0.9f, 0.6f, 0.1f) * 1.5f, 0.7f);
        mExit = Mat("HS_ExitDoor", new Color(0.15f, 0.2f, 0.15f), new Color(0.05f, 0.25f, 0.08f));
        mSign = Mat("HS_ExitSign", new Color(0.8f, 0.1f, 0.1f), new Color(1f, 0.1f, 0.05f) * 2f);

        mSuitA = Mat("HS_ClownSuitA", new Color(0.3f, 0.03f, 0.05f));
        mSuitB = Mat("HS_ClownSuitB", new Color(0.36f, 0.34f, 0.28f));
        mClownFace = Mat("HS_ClownFace", new Color(0.86f, 0.84f, 0.78f), null, 0.4f);
        mClownRed = Mat("HS_ClownRed", new Color(0.55f, 0.02f, 0.02f), null, 0.5f);
        mClownNose = Mat("HS_ClownNose", new Color(0.7f, 0.03f, 0.02f), new Color(0.8f, 0.03f, 0.01f), 0.7f);
        mClownBlue = Mat("HS_ClownBlue", new Color(0.06f, 0.1f, 0.35f));
        mShoe = Mat("HS_ClownShoe", new Color(0.2f, 0.02f, 0.02f), null, 0.5f);
        mGlove = Mat("HS_ClownGlove", new Color(0.72f, 0.7f, 0.65f));

        mGridLine = UnlitMat("HS_GridLine", new Color(0.12f, 0.115f, 0.105f));
        mTileReach = UnlitMat("HS_TileReach", new Color(0.1f, 0.55f, 0.5f));
        mTileHover = UnlitMat("HS_TileHover", new Color(1f, 0.85f, 0.4f));
        mTilePath = UnlitMat("HS_TilePath", new Color(0.95f, 0.55f, 0.15f));
        mSpotOutline = UnlitMat("HS_SpotOutline", new Color(0.65f, 0.95f, 1f));
        if (mSpotOutline.HasProperty("_Cull")) mSpotOutline.SetFloat("_Cull", 1f); // inside-out shell
        AssetDatabase.SaveAssets();
    }

    static Material Mat(string name, Color color, Color? emission = null, float smoothness = 0.1f)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Unlit material for grid lines and highlights (always readable in the dark).</summary>
    static Material UnlitMat(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }
}
