using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TigerCompanionSetup
{
    public const string ModelPath = "Assets/Models/RobotTiger/robot_tiger_animated.glb";
    public const string PrefabPath = "Assets/Prefabs/Player/Tiger.prefab";
    public const string DataPath = "Assets/ScriptableObjects/Robots/Tiger.asset";
    public const string AnimationFolder = "Assets/Animations/RobotTiger";
    public const string ControllerPath = AnimationFolder + "/Tiger.controller";
    private static readonly string[] MotionNames = { "Idle", "Walk", "Run", "Run_Forward", "Bite", "Roar", "ClawSwipe" };

    [MenuItem("Tools/Companions/Apply Tiger To Mole Slot")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        CreateAssets();
        foreach (string path in new[] { "Assets/Scenes/System/Boot.unity", BossEncounterSetup.ScenePath })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CompanionManager manager = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<CompanionManager>(true)).Single();
            var settings = new SerializedObject(manager);
            SerializedProperty roster = settings.FindProperty("companions");
            if (roster.arraySize != 3) throw new InvalidOperationException("Expected the existing three-companion roster.");
            var old = roster.GetArrayElementAtIndex(2).objectReferenceValue as CompanionController;
            if (old == null) throw new InvalidOperationException("The third companion slot is empty.");
            if (old.GetComponent<TigerAnimation>() == null)
            {
                if (old.RobotData == null || old.RobotData.RobotName != "Mole")
                    throw new InvalidOperationException("The third slot is no longer Mole; refusing to replace another companion.");
                GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                replacement.transform.SetParent(old.transform.parent, false);
                replacement.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
                replacement.SetActive(old.gameObject.activeSelf);
                var companion = replacement.GetComponent<CompanionController>();
                var companionSettings = new SerializedObject(companion);
                companionSettings.FindProperty("player").objectReferenceValue = settings.FindProperty("player").objectReferenceValue;
                companionSettings.ApplyModifiedPropertiesWithoutUndo();
                roster.GetArrayElementAtIndex(2).objectReferenceValue = companion;
                settings.ApplyModifiedPropertiesWithoutUndo();
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Tiger companion installed in the existing third slot (Boot and ExplorationArea).");
    }

    public static void CreateAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(AnimationFolder)) AssetDatabase.CreateFolder("Assets/Animations", "RobotTiger");
        AnimationClip[] imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
        var clips = new AnimationClip[MotionNames.Length];
        for (int i = 0; i < MotionNames.Length; i++)
        {
            string name = MotionNames[i];
            AnimationClip source = imported.Single(c => c.name == name);
            string path = AnimationFolder + "/" + name + ".anim";
            clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clips[i] == null)
            {
                clips[i] = UnityEngine.Object.Instantiate(source);
                AssetDatabase.CreateAsset(clips[i], path);
            }
            else EditorUtility.CopySerialized(source, clips[i]);
            clips[i].name = name;
            clips[i].legacy = false;
            bool loop = i < 4;
            clips[i].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clips[i]);
            clipSettings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clips[i], clipSettings);
            if (name == "Run_Forward")
            {
                // The source travels 0.9m per cycle. Keep the run in place so the visual
                // cannot run ahead of its Rigidbody and snap back at the loop boundary.
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clips[i]))
                    if (binding.type == typeof(Transform) && binding.path.Split('/').Last() == "Root" &&
                        (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                        AnimationUtility.SetEditorCurve(clips[i], binding, AnimationCurve.Constant(0f, clips[i].length, 0f));
            }
            EditorUtility.SetDirty(clips[i]);
        }
        var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (animatorController == null) animatorController = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = animatorController.layers[0].stateMachine;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == MotionNames[i]);
            if (state == null) state = machine.AddState(MotionNames[i]);
            state.motion = clips[i];
            state.writeDefaultValues = true;
            if (i == 0) machine.defaultState = state;
        }
        EditorUtility.SetDirty(animatorController);

        RobotData data = AssetDatabase.LoadAssetAtPath<RobotData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<RobotData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        var values = new SerializedObject(data);
        values.FindProperty("robotName").stringValue = "Tiger";
        values.FindProperty("type1").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TypeData>("Assets/ScriptableObjects/Types/Beast.asset");
        values.FindProperty("type2").objectReferenceValue = null;
        values.FindProperty("maxHP").intValue = 200;
        values.FindProperty("attack").intValue = 45;
        values.FindProperty("moveSpeed").floatValue = 4f;
        values.FindProperty("attackInterval").floatValue = 2f;
        values.FindProperty("attackRange").floatValue = 1.5f;
        values.FindProperty("maxLevel").intValue = 5;
        values.ApplyModifiedPropertiesWithoutUndo();

        GameObject root = new GameObject("Tiger");
        try
        {
            root.layer = LayerMask.NameToLayer("Companion");
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(.7f, 1f, 1.8f);
            var companion = root.AddComponent<CompanionController>();
            var companionSettings = new SerializedObject(companion);
            companionSettings.FindProperty("robotData").objectReferenceValue = data;
            companionSettings.ApplyModifiedPropertiesWithoutUndo();
            var combat = root.AddComponent<CompanionCombat>();
            var combatSettings = new SerializedObject(combat);
            var attacks = combatSettings.FindProperty("attackAnimations");
            attacks.arraySize = 2;
            attacks.GetArrayElementAtIndex(0).objectReferenceValue = clips[4];
            attacks.GetArrayElementAtIndex(1).objectReferenceValue = clips[6];
            combatSettings.ApplyModifiedPropertiesWithoutUndo();
            var experience = root.AddComponent<ExperienceComponent>();
            var experienceSettings = new SerializedObject(experience);
            experienceSettings.FindProperty("levelData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/ScriptableObjects/Levels/Robot_LevelData.asset");
            experienceSettings.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<RobotRuntimeStats>();
            root.AddComponent<FrozenCompanionKnockback>();
            var pivot = new GameObject("TigerVisual").transform;
            pivot.SetParent(root.transform, false);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            model.transform.SetParent(pivot, false);
            // glTF's +X-facing tiger becomes -X in Unity; align its head with gameplay +Z.
            model.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = 1.8f / bounds.size.z;
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = new Vector3(-bounds.center.x * scale,
                -.5f - bounds.min.y * scale, -bounds.center.z * scale);
            foreach (Transform child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = root.layer;
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var animation = root.AddComponent<TigerAnimation>();
            var animationSettings = new SerializedObject(animation);
            animationSettings.FindProperty("animator").objectReferenceValue = animator;
            animationSettings.FindProperty("roarDuration").floatValue = clips[5].length;
            animationSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
    }
}
