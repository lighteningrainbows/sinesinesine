using System;
using UnityEditor;
using UnityEngine;

// Source: ぷろえんあき.xlsx / 敵ロボット, rows 68-113.
// Explicit menu command: importing scripts never rewrites project assets.
public static class EnemyAlphaSetup
{
    private const string MaterialFolder = "Assets/Materials/EnemyAlpha";
    private static Material dark, silver, blue, shell, red, brown, eye;

    [MenuItem("Tools/Enemies/Apply Alpha Specification")]
    public static void Apply()
    {
        EnsureFolder(MaterialFolder);
        dark = Material("Dark", new Color(0.07f, 0.10f, 0.16f));
        silver = Material("Silver", new Color(0.48f, 0.59f, 0.68f));
        blue = Material("Aquatic", new Color(0.12f, 0.58f, 0.78f));
        shell = Material("Shell", new Color(0.81f, 0.58f, 0.32f));
        red = Material("Crab", new Color(0.75f, 0.23f, 0.16f));
        brown = Material("Beetle", new Color(0.29f, 0.16f, 0.09f));
        eye = Material("Eyes", new Color(1f, 0.76f, 0.16f));

        int[] hp = { 100, 100, 200, 100, 50 };
        int[] attack = { 10, 10, 10, 20, 5 };
        float[] speed = { 3f, 3f, 1.5f, 3f, 6f };
        float[] duration = { 0.5f, 0.5f, 1f, 0.5f, 0.5f };
        float[] interval = { 2f, 2f, 3f, 2f, 1f };
        float[] area = { 2f, 1.5f, 1.5f, 1.5f, 1f };
        for (int i = 0; i < 5; i++)
        {
            char id = (char)('A' + i);
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(
                $"Assets/ScriptableObjects/Enemies/Enemy {id}.asset");
            if (data == null) throw new InvalidOperationException($"Missing Enemy {id} data.");
            var settings = new SerializedObject(data);
            settings.FindProperty("maxHP").intValue = hp[i];
            settings.FindProperty("attack").intValue = attack[i];
            settings.FindProperty("moveSpeed").floatValue = speed[i];
            settings.FindProperty("attackDuration").floatValue = duration[i];
            settings.FindProperty("attackInterval").floatValue = interval[i];
            settings.FindProperty("attackRange").floatValue = area[i];
            settings.FindProperty("attackArea").vector2Value = Vector2.one * area[i];
            settings.FindProperty("attackHeight").floatValue = 4f;
            settings.FindProperty("experience").intValue = 100;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);

            string path = $"Assets/Prefabs/Enemy/Enemy{id}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.transform.localScale = Vector3.one;
                MeshRenderer oldRenderer = root.GetComponent<MeshRenderer>();
                if (oldRenderer != null) oldRenderer.enabled = false;
                foreach (Collider collider in root.GetComponents<Collider>())
                    UnityEngine.Object.DestroyImmediate(collider);
                root.AddComponent<BoxCollider>().size = Vector3.one * 2f;
                Rigidbody body = root.GetComponent<Rigidbody>();
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.useGravity = i != 0 && i != 1;

                var controller = new SerializedObject(root.GetComponent<EnemyController>());
                controller.FindProperty("isFlying").boolValue = i == 0 || i == 1;
                controller.FindProperty("hoverHeight").floatValue = 1.5f;
                controller.ApplyModifiedPropertiesWithoutUndo();

                Transform existing = root.transform.Find("AlphaVisual");
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                Transform model = new GameObject("AlphaVisual").transform;
                model.SetParent(root.transform, false);
                BuildModel(model, id);
                EnemyVisualFeedback feedback = root.GetComponent<EnemyVisualFeedback>();
                if (feedback == null) feedback = root.AddComponent<EnemyVisualFeedback>();
                var feedbackSettings = new SerializedObject(feedback);
                feedbackSettings.FindProperty("modelRoot").objectReferenceValue = model;
                feedbackSettings.ApplyModifiedPropertiesWithoutUndo();
                Transform hud = root.transform.Find("EnemyWorldCanvas");
                if (hud != null) hud.localPosition = new Vector3(0f, 1.55f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy alpha data and all five prefabs updated.");
    }

    private static void BuildModel(Transform root, char id)
    {
        switch (id)
        {
            case 'A': // Crow: dark body, spread wings and a beak.
                Part(root, "Body", PrimitiveType.Sphere, V(0, 0, 0), V(.65f, .8f, 1f), dark);
                Part(root, "Head", PrimitiveType.Sphere, V(0, .45f, .42f), V(.55f, .55f, .55f), dark);
                Part(root, "Beak", PrimitiveType.Cube, V(0, .38f, .8f), V(.22f, .16f, .35f), shell);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(root, "Wing", PrimitiveType.Cube, V(side * .58f, .05f, -.05f),
                        V(.8f, .12f, .75f), dark, V(0, side * 18f, side * 12f));
                    Part(root, "Eye", PrimitiveType.Sphere, V(side * .23f, .48f, .58f), V(.1f, .1f, .1f), eye);
                    Segment(root, "Foot", V(side * .2f, -.3f, 0), V(side * .2f, -.8f, .12f), .06f, shell);
                }
                Part(root, "Tail", PrimitiveType.Cube, V(0, -.05f, -.65f), V(.35f, .12f, .55f), dark);
                break;
            case 'B': // Floating fish.
                Part(root, "Body", PrimitiveType.Sphere, V(0, 0, 0), V(.85f, .85f, 1.4f), blue);
                Part(root, "TailFin", PrimitiveType.Cube, V(0, 0, -.76f), V(.12f, .75f, .4f), blue, V(0, 0, 35));
                Part(root, "DorsalFin", PrimitiveType.Cube, V(0, .5f, -.08f), V(.1f, .5f, .65f), silver);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(root, "SideFin", PrimitiveType.Cube, V(side * .48f, -.1f, 0),
                        V(.55f, .08f, .45f), silver, V(0, side * -25, 0));
                    Part(root, "Eye", PrimitiveType.Sphere, V(side * .32f, .15f, .45f), V(.16f, .16f, .16f), eye);
                }
                break;
            case 'C': // Hermit crab: shell, low body, claws and legs.
                Part(root, "Shell", PrimitiveType.Sphere, V(0, .15f, -.22f), V(1.3f, 1.4f, 1.3f), shell);
                Part(root, "ShellTip", PrimitiveType.Sphere, V(.25f, .65f, -.4f), V(.55f, .55f, .55f), brown);
                Part(root, "Body", PrimitiveType.Sphere, V(0, -.45f, .25f), V(1.2f, .5f, 1f), red);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(root, "Claw", PrimitiveType.Sphere, V(side * .7f, -.3f, .65f), V(.45f, .3f, .5f), red);
                    Segment(root, "EyeStalk", V(side * .25f, -.3f, .5f), V(side * .3f, .1f, .65f), .06f, dark);
                    Part(root, "Eye", PrimitiveType.Sphere, V(side * .3f, .1f, .65f), V(.14f, .14f, .14f), eye);
                    for (int leg = 0; leg < 3; leg++)
                        Segment(root, "Leg", V(side * .4f, -.5f, .3f - leg * .3f),
                            V(side * .9f, -.9f, .45f - leg * .4f), .07f, red);
                }
                break;
            case 'D': // Wolf silhouette.
                Part(root, "Body", PrimitiveType.Sphere, V(0, .02f, -.1f), V(.75f, .75f, 1.3f), silver);
                Part(root, "Head", PrimitiveType.Sphere, V(0, .45f, .5f), V(.6f, .6f, .65f), silver);
                Part(root, "Muzzle", PrimitiveType.Cube, V(0, .35f, .8f), V(.35f, .25f, .35f), dark);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(root, "Ear", PrimitiveType.Cube, V(side * .2f, .8f, .45f),
                        V(.17f, .3f, .18f), dark, V(-15, 0, side * -12));
                    Part(root, "Eye", PrimitiveType.Sphere, V(side * .23f, .5f, .7f), V(.09f, .09f, .09f), eye);
                    for (int leg = -1; leg <= 1; leg += 2)
                        Segment(root, "Leg", V(side * .26f, -.1f, leg * .42f),
                            V(side * .3f, -.95f, leg * .45f), .1f, dark);
                }
                Segment(root, "Tail", V(0, .1f, -.65f), V(0, .5f, -.95f), .12f, silver);
                break;
            case 'E': // Rhinoceros beetle: split shell, six legs and raised horn.
                Part(root, "Shell", PrimitiveType.Sphere, V(0, 0, -.15f), V(1.2f, .7f, 1.4f), brown);
                Part(root, "ShellSeam", PrimitiveType.Cube, V(0, .33f, -.15f), V(.04f, .04f, 1.1f), dark);
                Part(root, "Head", PrimitiveType.Sphere, V(0, 0, .58f), V(.65f, .5f, .6f), dark);
                Segment(root, "Horn", V(0, .15f, .65f), V(0, .65f, .85f), .1f, shell);
                Segment(root, "HornTip", V(0, .65f, .85f), V(0, .8f, .65f), .08f, shell);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(root, "Eye", PrimitiveType.Sphere, V(side * .25f, .1f, .72f), V(.11f, .11f, .11f), eye);
                    for (int leg = 0; leg < 3; leg++)
                    {
                        Vector3 knee = V(side * .85f, -.3f, .45f - leg * .45f);
                        Segment(root, "Leg", V(side * .4f, -.1f, .3f - leg * .3f), knee, .06f, dark);
                        Segment(root, "Foot", knee, V(side * .95f, -.95f, .45f - leg * .45f), .06f, dark);
                    }
                }
                break;
        }
    }

    private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    private static Transform Part(Transform parent, string name, PrimitiveType shape,
        Vector3 position, Vector3 scale, Material material, Vector3 angles = default)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.layer = LayerMask.NameToLayer("Enemy");
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = Quaternion.Euler(angles);
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part.transform;
    }

    private static void Segment(Transform parent, string name, Vector3 start, Vector3 end,
        float radius, Material material)
    {
        Transform segment = Part(parent, name, PrimitiveType.Cylinder, (start + end) * .5f,
            V(radius * 2f, (end - start).magnitude * .5f, radius * 2f), material);
        segment.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
    }

    private static Material Material(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.name = name;
        material.SetFloat("_Metallic", .25f);
        material.SetFloat("_Smoothness", .3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
