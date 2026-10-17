using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Source: ぷろえんあき.xlsx / 強敵ロボット.
public static class BossEncounterSetup
{
    public const string ScenePath = "Assets/Scenes/Game/ExplorationArea.unity";
    public const string PrefabPath = "Assets/Prefabs/Enemy/SpiderBoss.prefab";
    public const string DataPath = "Assets/ScriptableObjects/Enemies/SpiderBoss.asset";
    public const string AttackPath = "Assets/ScriptableObjects/Battle/SpiderBossAttacks.asset";
    public static readonly Vector3 ArenaCenter = new Vector3(67.6f, 0, 67.6f);
    private static Material bodyMaterial, legMaterial, eyeMaterial, floorMaterial, wallMaterial, trimMaterial, warningMaterial;

    [MenuItem("Tools/Enemies/Create Spider Boss and Arena")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder("Assets/Materials/Boss");
        bodyMaterial = MakeMaterial("Chitin", new Color(.14f, .18f, .28f));
        legMaterial = MakeMaterial("Legs", new Color(.29f, .39f, .49f));
        eyeMaterial = MakeMaterial("Eyes", new Color(1f, .16f, .08f));
        floorMaterial = MakeMaterial("Floor", new Color(.19f, .23f, .28f));
        wallMaterial = MakeMaterial("Walls", new Color(.11f, .14f, .19f));
        trimMaterial = MakeMaterial("Trim", new Color(1f, .52f, .12f));
        warningMaterial = MakeMaterial("AttackWarning", new Color(1f, .1f, .05f, .45f));
        warningMaterial.SetFloat("_Surface", 1);
        warningMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        warningMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        warningMaterial.SetFloat("_ZWrite", 0);
        warningMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        warningMaterial.renderQueue = 3000;

        BossAttackSettings attacks = AssetDatabase.LoadAssetAtPath<BossAttackSettings>(AttackPath);
        if (attacks == null)
        {
            attacks = ScriptableObject.CreateInstance<BossAttackSettings>();
            AssetDatabase.CreateAsset(attacks, AttackPath);
        }
        var attackSettings = new SerializedObject(attacks);
        attackSettings.FindProperty("strongDamage").intValue = 40;
        attackSettings.FindProperty("strongDuration").floatValue = 2;
        attackSettings.FindProperty("strongInterval").floatValue = 4;
        attackSettings.FindProperty("strongArea").vector2Value = Vector2.one * 2;
        attackSettings.FindProperty("strongProbability").floatValue = .3f;
        attackSettings.ApplyModifiedPropertiesWithoutUndo();

        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EnemyData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        var dataSettings = new SerializedObject(data);
        dataSettings.FindProperty("enemyName").stringValue = "Spider Guardian";
        dataSettings.FindProperty("type1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TypeData>(
            "Assets/ScriptableObjects/Types/Insect.asset");
        dataSettings.FindProperty("type2").objectReferenceValue = null;
        dataSettings.FindProperty("maxHP").intValue = 400;
        dataSettings.FindProperty("attack").intValue = 20;
        dataSettings.FindProperty("moveSpeed").floatValue = 3;
        dataSettings.FindProperty("attackDuration").floatValue = .5f;
        dataSettings.FindProperty("attackInterval").floatValue = 2;
        dataSettings.FindProperty("attackArea").vector2Value = Vector2.one * 1.5f;
        dataSettings.FindProperty("attackRange").floatValue = 1.5f;
        dataSettings.FindProperty("attackHeight").floatValue = 4;
        dataSettings.FindProperty("experience").intValue = 500;
        dataSettings.FindProperty("bossAttacks").objectReferenceValue = attacks;
        dataSettings.ApplyModifiedPropertiesWithoutUndo();

        BuildBossPrefab(data);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject oldArena = GameObject.Find("BossArena");
        if (oldArena != null) Object.DestroyImmediate(oldArena);
        BuildArena();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Spider boss and connected arena saved to ExplorationArea.");
    }

    private static void BuildBossPrefab(EnemyData data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy/EnemyE.prefab");
        try
        {
            root.name = "SpiderBoss";
            root.GetComponent<BoxCollider>().size = Vector3.one * 3;
            root.GetComponent<Rigidbody>().useGravity = true;
            Assign(root.GetComponent<EnemyController>(), "enemyData", data);
            Assign(root.GetComponent<EnemyHealth>(), "enemyData", data);
            Assign(root.GetComponent<EnemyCombat>(), "enemyData", data);
            var controller = new SerializedObject(root.GetComponent<EnemyController>());
            controller.FindProperty("isFlying").boolValue = false;
            controller.FindProperty("returnDistance").floatValue = 1000;
            controller.ApplyModifiedPropertiesWithoutUndo();
            Transform old = root.transform.Find("AlphaVisual");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform model = new GameObject("SpiderVisual").transform;
            model.SetParent(root.transform, false);
            Piece(model, "Abdomen", PrimitiveType.Sphere, V(0, .25f, -.45f), V(1.65f, 1.4f, 1.65f), bodyMaterial);
            Piece(model, "Carapace", PrimitiveType.Sphere, V(0, -.15f, .35f), V(1.35f, .9f, 1.3f), bodyMaterial);
            Piece(model, "ArmourStripe", PrimitiveType.Cube, V(0, .88f, -.45f), V(.18f, .08f, 1.2f), trimMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int leg = 0; leg < 4; leg++)
                {
                    float z = .8f - leg * .52f;
                    Vector3 start = V(side * .5f, -.2f, z * .65f);
                    Vector3 knee = V(side * 1.15f, .35f, z);
                    Vector3 foot = V(side * 1.43f, -1.47f, z * 1.25f);
                    Segment(model, "LegUpper", start, knee, .1f, legMaterial);
                    Segment(model, "LegLower", knee, foot, .07f, bodyMaterial);
                    Piece(model, "Joint", PrimitiveType.Sphere, knee, Vector3.one * .22f, trimMaterial);
                }
                for (int eye = 0; eye < 3; eye++)
                    Piece(model, "Eye", PrimitiveType.Sphere,
                        V(side * (.16f + eye * .16f), .06f + eye * .1f, .91f - eye * .08f),
                        Vector3.one * (.19f - eye * .025f), eyeMaterial);
                Segment(model, "Fang", V(side * .25f, -.3f, .9f), V(side * .18f, -.7f, 1.2f), .08f, eyeMaterial);
            }
            Assign(root.GetComponent<EnemyVisualFeedback>(), "modelRoot", model);
            Transform warning = Piece(root.transform, "AttackWarning", PrimitiveType.Cube,
                Vector3.zero, Vector3.one, warningMaterial, false);
            warning.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            warning.gameObject.SetActive(false);
            Assign(root.AddComponent<BossAttackTelegraph>(), "warningBox", warning);
            Transform hud = root.transform.Find("EnemyWorldCanvas");
            if (hud != null) hud.localPosition = V(0, 2.05f, 0);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BuildArena()
    {
        Transform root = new GameObject("BossArena").transform;
        root.position = ArenaCenter;
        var encounter = root.gameObject.AddComponent<BossEncounter>();
        Transform spawn = new GameObject("BossSpawnPoint").transform;
        spawn.SetParent(root, false);
        spawn.localPosition = V(0, 1.5f, 5);
        spawn.localRotation = Quaternion.Euler(0, 180, 0);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) throw new InvalidOperationException("Exploration player is missing.");
        player.layer = LayerMask.NameToLayer("Player");
        Assign(encounter, "player", player.transform);
        Assign(encounter, "spawnPoint", spawn);
        Assign(encounter, "bossPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<EnemyController>());

        Piece(root, "ArenaFloor", PrimitiveType.Cube, V(0, -.5f, 0), V(30, 1, 30), floorMaterial, true);
        Piece(root, "NorthWall", PrimitiveType.Cube, V(0, 1.1f, 15), V(30.8f, 2.2f, .8f), wallMaterial, true);
        Piece(root, "WestWall", PrimitiveType.Cube, V(-15, 1.1f, 0), V(.8f, 2.2f, 30), wallMaterial, true);
        Piece(root, "EastWall", PrimitiveType.Cube, V(15, 1.1f, 0), V(.8f, 2.2f, 30), wallMaterial, true);
        for (int side = -1; side <= 1; side += 2)
        {
            Piece(root, "SouthWall", PrimitiveType.Cube, V(side * 9.5f, 1.1f, -15), V(11, 2.2f, .8f), wallMaterial, true);
            Piece(root, "GatePillar", PrimitiveType.Cube, V(side * 4.5f, 2, -15), V(1, 4, 1), wallMaterial, true);
            Piece(root, "GateLight", PrimitiveType.Cube, V(side * 4.5f, 3.2f, -15.52f), V(.6f, .3f, .05f), trimMaterial);
            Piece(root, "BridgeRail", PrimitiveType.Cube, V(side * 4.3f, .6f, -20.5f), V(.4f, 1.2f, 11), wallMaterial, true);
        }
        Piece(root, "GateHeader", PrimitiveType.Cube, V(0, 4, -15), V(10, .9f, 1), wallMaterial, true);
        Sign(root, "ArenaSign", "BOSS ARENA", V(0, 4.05f, -15.55f), .09f, Color.white);
        Piece(root, "BridgeFloor", PrimitiveType.Cube, V(0, -.5f, -20.5f), V(8, 1, 12), floorMaterial, true);

        for (int step = 0; step < 6; step++)
            Piece(root, "BridgeStripe", PrimitiveType.Cube, V(0, .015f, -25 + step * 2), V(6, .02f, .14f), trimMaterial);
        for (int cornerX = -1; cornerX <= 1; cornerX += 2)
            for (int cornerZ = -1; cornerZ <= 1; cornerZ += 2)
            {
                Piece(root, "CornerTower", PrimitiveType.Cylinder, V(cornerX * 13.5f, 1, cornerZ * 13.5f),
                    V(.75f, 1, .75f), wallMaterial, true);
                Piece(root, "Beacon", PrimitiveType.Sphere, V(cornerX * 13.5f, 2.15f, cornerZ * 13.5f),
                    Vector3.one * .4f, eyeMaterial);
            }
        for (int edge = -1; edge <= 1; edge += 2)
        {
            Piece(root, "EdgeMark", PrimitiveType.Cube, V(edge * 14, .02f, 0), V(.12f, .025f, 27), trimMaterial);
            Piece(root, "EdgeMark", PrimitiveType.Cube, V(0, .02f, edge * 14), V(27, .025f, .12f), trimMaterial);
        }
        Transform ring = new GameObject("EncounterRing").transform;
        ring.SetParent(root, false);
        var line = ring.gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 64;
        line.widthMultiplier = .08f;
        line.sharedMaterial = trimMaterial;
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64f;
            line.SetPosition(i, V(Mathf.Cos(angle) * 7, .035f, Mathf.Sin(angle) * 7));
        }

        // The start is at (81.8, -1.7); point toward the northern bridge from there.
        Transform directions = new GameObject("ArenaDirections").transform;
        directions.SetParent(root, false);
        directions.position = V(78, 0, 6);
        Piece(directions, "SignBoard", PrimitiveType.Cube, V(0, 1.7f, .1f), V(6.5f, 2, .2f), wallMaterial, true);
        Sign(directions, "Wayfinding", "BOSS ARENA\nNORTH PATH  <", V(0, 1.7f, -.02f), .06f, Color.white);
        for (int step = 0; step < 6; step++)
            Piece(root, "ApproachMark", PrimitiveType.Cube, V(0, .025f, -58 + step * 5), V(2, .025f, .18f), trimMaterial);
        BuildHUD(root, encounter);
    }

    private static void BuildHUD(Transform parent, BossEncounter encounter)
    {
        var canvasRoot = new GameObject("BossHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasRoot.transform.SetParent(parent, false);
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        RectTransform panel = Rect("BossPanel", canvasRoot.transform, new Vector2(620, 104), new Vector2(0, -82));
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1);
        panel.gameObject.AddComponent<Image>().color = new Color(.05f, .07f, .1f, .94f);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        Text status = Label("BossStatus", panel, new Vector2(580, 32), new Vector2(0, 30), 21, new Color(1, .65f, .25f));
        status.text = "SPIDER GUARDIAN";
        RectTransform sliderRect = Rect("BossHP", panel, new Vector2(570, 14), new Vector2(0, 2));
        sliderRect.gameObject.AddComponent<Image>().color = new Color(.15f, .12f, .14f);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.maxValue = slider.value = 400;
        RectTransform fill = Rect("Fill", sliderRect, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = new Color(.85f, .24f, .12f);
        slider.fillRect = fill;
        Text hp = Label("BossHPText", panel, new Vector2(160, 24), new Vector2(202, -29), 15, Color.white);
        hp.text = "400 / 400";
        Text reward = Label("BossReward", panel, new Vector2(390, 24), new Vector2(-90, -29), 15, Color.white);
        reward.text = "INSECT TYPE";
        BossEncounterHUD hud = canvasRoot.AddComponent<BossEncounterHUD>();
        Assign(hud, "encounter", encounter);
        Assign(hud, "panel", group);
        Assign(hud, "hpSlider", slider);
        Assign(hud, "hpText", hp);
        Assign(hud, "statusText", status);
        Assign(hud, "rewardText", reward);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Text Label(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color)
    {
        Text text = Rect(name, parent, size, position).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Sign(Transform parent, string name, string value, Vector3 position, float size, Color color)
    {
        Transform sign = new GameObject(name).transform;
        sign.SetParent(parent, false);
        sign.localPosition = position;
        var text = sign.gameObject.AddComponent<TextMesh>();
        text.text = value;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.characterSize = size;
        text.color = color;
        sign.GetComponent<Renderer>().sharedMaterial = text.font.material;
    }

    private static Transform Piece(Transform parent, string name, PrimitiveType shape,
        Vector3 position, Vector3 scale, Material material, bool solid = false)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        if (!solid) Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.GetComponent<Renderer>().sharedMaterial = material;
        return piece.transform;
    }

    private static void Segment(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
    {
        Transform segment = Piece(parent, name, PrimitiveType.Cylinder, (start + end) * .5f,
            V(radius * 2, (end - start).magnitude * .5f, radius * 2), material);
        segment.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
    }

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    private static Material MakeMaterial(string name, Color color)
    {
        string path = $"Assets/Materials/Boss/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", .2f);
        material.SetFloat("_Smoothness", .35f);
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void Assign(Object target, string name, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
}
