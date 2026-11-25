using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyRobotModelSetup
{
    public static readonly string[] Roles = { "Balance", "Power", "Gun" };
    public static readonly string[] Colors = { "Purple", "Blue", "Yellow", "Red", "Green" };
    private static readonly string[] Types = { "Flying", "Aquatic", "Crustacean", "Beast", "Insect" };
    private static readonly string[] Motions = { "Walk", "Run", "Attack", "Damage", "Death" };
    public static string PrefabPath(int type, int role) => role == 0 ? $"Assets/Prefabs/Enemy/Enemy{(char)('A' + type)}.prefab" :
        $"Assets/Prefabs/Enemy/Enemy{(char)('A' + type)}_{Roles[role]}.prefab";
    public static string DataPath(int type, int role) => role == 0 ? $"Assets/ScriptableObjects/Enemies/Enemy {(char)('A' + type)}.asset" :
        $"Assets/ScriptableObjects/Enemies/Enemy {(char)('A' + type)} {Roles[role]}.asset";
    public static string ModelPath(int type, int role)
    {
        string folder = $"Assets/Models/EnemyModel/{Roles[role]}Enemy";
        return System.IO.Directory.GetFiles(folder, "*.glb").Single(p =>
            System.IO.Path.GetFileNameWithoutExtension(p).Equals($"{Roles[role]}Enemy_{Colors[type]}", StringComparison.OrdinalIgnoreCase)).Replace('\\', '/');
    }
    public static string AnimationFolder(int role) => $"Assets/Animations/EnemyRobots/{Roles[role]}";

    [MenuItem("Tools/Enemies/Apply Power Balance Gun Models")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAssets();
        foreach (string path in new[] { "Assets/Scenes/System/Boot.unity", BossEncounterSetup.ScenePath })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var group in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemySpawnGroup>(true)))
            {
                var settings = new SerializedObject(group);
                var list = settings.FindProperty("enemyPrefabs");
                var replacements = new List<GameObject>();
                for (int j = 0; j < list.arraySize; j++)
                {
                    var old = list.GetArrayElementAtIndex(j).objectReferenceValue as GameObject;
                    var data = old != null ? old.GetComponent<EnemyHealth>()?.EnemyData : null;
                    int type = data == null || data.IsBoss ? -1 : Array.FindIndex(Types, n => data.Type1 != null && data.Type1.name == n);
                    if (type < 0) { if (old != null && !replacements.Contains(old)) replacements.Add(old); continue; }
                    for (int role = 0; role < 3; role++)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(type, role));
                        if (!replacements.Contains(prefab)) replacements.Add(prefab);
                    }
                }
                list.arraySize = replacements.Count;
                for (int j = 0; j < replacements.Count; j++) list.GetArrayElementAtIndex(j).objectReferenceValue = replacements[j];
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Enemy robot models, fifteen stat profiles and existing spawn pools updated.");
    }

    public static void BuildAssets()
    {
        EnsureFolder("Assets/Animations/EnemyRobots");
        int[] hp = { 100, 100, 200, 100, 50 }, attack = { 10, 10, 10, 20, 5 };
        float[] speed = { 3, 3, 1.5f, 3, 6 }, duration = { .5f, .5f, 1, .5f, .5f }, interval = { 2, 2, 3, 2, 1 }, range = { 2, 1.5f, 1.5f, 1.5f, 1 };
        for (int role = 0; role < 3; role++)
        {
            string folder = AnimationFolder(role);
            EnsureFolder(folder);
            var imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath(3, role)).OfType<AnimationClip>().ToArray();
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string motion in Motions)
            {
                var source = imported.Single(c => c.name == motion);
                string path = folder + "/" + motion + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
                else EditorUtility.CopySerialized(source, clip);
                clip.name = motion;
                clip.legacy = false;
                bool loop = motion == "Walk" || motion == "Run";
                clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                clipSettings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
                // Motion is supplied by the Rigidbody, never by cumulative root translation.
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.type == typeof(Transform) &&
                        (binding.path.Split('/').Last() == "Root" || binding.path.Split('/').Last() == "RigRoot") &&
                        (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, 0f));
                EditorUtility.SetDirty(clip);
                clips.Add(motion, clip);
            }
            string controllerPath = folder + "/Enemy.controller";
            var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = animatorController.layers[0].stateMachine;
            foreach (string motion in Motions.Concat(new[] { "Idle" }))
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == motion) ?? machine.AddState(motion);
                state.motion = clips[motion == "Idle" ? "Walk" : motion];
                state.speed = motion == "Idle" ? 0f : 1f;
                if (motion == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(animatorController);

            for (int type = 0; type < 5; type++)
            {
                string dataPath = DataPath(type, role);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
                if (data == null) { data = ScriptableObject.CreateInstance<EnemyData>(); AssetDatabase.CreateAsset(data, dataPath); }
                var values = new SerializedObject(data);
                values.FindProperty("enemyName").stringValue = $"Enemy{(char)('A' + type)}_{Roles[role]}";
                values.FindProperty("archetype").enumValueIndex = role;
                values.FindProperty("attackKnockbackArmor").boolValue = true;
                values.FindProperty("type1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TypeData>($"Assets/ScriptableObjects/Types/{Types[type]}.asset");
                values.FindProperty("type2").objectReferenceValue = null;
                values.FindProperty("bossAttacks").objectReferenceValue = null;
                values.FindProperty("maxHP").intValue = role == 2 ? hp[type] / 2 : hp[type];
                values.FindProperty("attack").intValue = Mathf.RoundToInt(attack[type] * (role == 1 ? 1.5f : 1f));
                values.FindProperty("moveSpeed").floatValue = speed[type] * (role == 1 ? 2f / 3f : 1f);
                values.FindProperty("attackDuration").floatValue = duration[type];
                values.FindProperty("attackInterval").floatValue = interval[type];
                values.FindProperty("attackRange").floatValue = range[type] * (role == 2 ? 2 : 1);
                values.FindProperty("attackArea").vector2Value = Vector2.one * range[type] * (role == 2 ? 2 : 1);
                values.FindProperty("attackHeight").floatValue = 4f;
                values.FindProperty("experience").intValue = 100;
                values.ApplyModifiedPropertiesWithoutUndo();
                string prefabPath = PrefabPath(type, role);
                GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath(type, 0));
                try
                {
                    root.name = $"Enemy{(char)('A' + type)}" + (role == 0 ? "" : "_" + Roles[role]);
                    root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    root.transform.localScale = Vector3.one;
                    foreach (string name in new[] { "AlphaVisual", "RobotVisual" })
                    {
                        var old = root.transform.Find(name);
                        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    }
                    var oldFeedback = root.GetComponent<EnemyVisualFeedback>();
                    if (oldFeedback != null) UnityEngine.Object.DestroyImmediate(oldFeedback);
                    var oldRenderer = root.GetComponent<MeshRenderer>();
                    if (oldRenderer != null) oldRenderer.enabled = false;
                    foreach (Component component in new Component[] { root.GetComponent<EnemyController>(), root.GetComponent<EnemyCombat>(), root.GetComponent<EnemyHealth>() })
                    {
                        var settings = new SerializedObject(component);
                        settings.FindProperty("enemyData").objectReferenceValue = data;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var pivot = new GameObject("RobotVisual").transform;
                    pivot.SetParent(root.transform, false);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(type, role)), root.scene);
                    model.transform.SetParent(pivot, false);
                    var renderers = model.GetComponentsInChildren<Renderer>();
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    float scale = 1.9f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    pivot.localScale = Vector3.one * scale;
                    pivot.localPosition = new Vector3(-bounds.center.x * scale, -1f - bounds.min.y * scale, -bounds.center.z * scale);
                    foreach (Transform child in pivot.GetComponentsInChildren<Transform>()) child.gameObject.layer = root.layer;
                    var animator = model.GetComponentInChildren<Animator>();
                    if (animator == null) animator = model.AddComponent<Animator>();
                    animator.runtimeAnimatorController = animatorController;
                    animator.applyRootMotion = false;
                    var animation = root.GetComponent<EnemyRobotAnimation>() ?? root.AddComponent<EnemyRobotAnimation>();
                    var animationSettings = new SerializedObject(animation);
                    animationSettings.FindProperty("visualRoot").objectReferenceValue = pivot;
                    animationSettings.FindProperty("animator").objectReferenceValue = animator;
                    animationSettings.FindProperty("attackClip").objectReferenceValue = clips["Attack"];
                    animationSettings.FindProperty("damageClip").objectReferenceValue = clips["Damage"];
                    animationSettings.FindProperty("deathClip").objectReferenceValue = clips["Death"];
                    animationSettings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        AssetDatabase.SaveAssets();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
