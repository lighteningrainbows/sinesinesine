using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class EnemyRobotModelValidation
{
    public static void ValidateAssets()
    {
        int[] hp = { 100, 100, 200, 100, 50 }, damage = { 10, 10, 10, 20, 5 };
        float[] speed = { 3, 3, 1.5f, 3, 6 }, area = { 2, 1.5f, 1.5f, 1.5f, 1 };
        for (int role = 0; role < 3; role++)
            for (int type = 0; type < 5; type++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRobotModelSetup.PrefabPath(type, role));
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyRobotModelSetup.DataPath(type, role));
                string name = $"{(char)('A' + type)} {EnemyRobotModelSetup.Roles[role]}";
                Check(data.MaxHP == (role == 2 ? hp[type] / 2 : hp[type]) && data.Attack == Mathf.RoundToInt(damage[type] * (role == 1 ? 1.5f : 1)), name + " workbook HP and damage");
                Check(Mathf.Abs(data.MoveSpeed - speed[type] * (role == 1 ? 2f / 3f : 1)) < .001f &&
                    data.AttackArea == Vector2.one * area[type] * (role == 2 ? 2 : 1), name + " workbook speed and area");
                Check(data.AttackKnockbackArmor && data.Experience == 100 && (int)data.Archetype == role, name + " attack armor, EXP and archetype");
                Check(prefab.GetComponentsInChildren<Collider>(true).Length == 1 && prefab.GetComponent<BoxCollider>().size == Vector3.one * 2f, name + " single two-metre collider");
                Check(prefab.GetComponent<EnemyRobotAnimation>() != null && prefab.transform.Find("RobotVisual") != null &&
                    prefab.GetComponent<EnemyVisualFeedback>() == null, name + " imported animated model replaces procedural feedback");
                var model = prefab.transform.Find("RobotVisual").GetChild(0).gameObject;
                Check(PrefabUtility.GetCorrespondingObjectFromSource(model) == AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRobotModelSetup.ModelPath(type, role)), name + " correct attribute colour model");
            }
        foreach (string path in new[] { "Assets/Scenes/System/Boot.unity", BossEncounterSetup.ScenePath })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var group in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemySpawnGroup>(true)))
            {
                var list = new SerializedObject(group).FindProperty("enemyPrefabs");
                Check(list.arraySize >= 3, "Spawn group includes all three roles: " + group.name);
            }
        }
    }

    public static IEnumerator RunChecks()
    {
        EditorSceneManager.LoadSceneInPlayMode(BossEncounterSetup.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return .15f;
        foreach (StageArea area in Object.FindObjectsByType<StageArea>()) area.enabled = false;
        foreach (var group in Object.FindObjectsByType<EnemySpawnGroup>()) group.gameObject.SetActive(false);
        foreach (var enemy in Object.FindObjectsByType<EnemyController>()) enemy.gameObject.SetActive(false);
        var manager = CompanionManager.Instance;
        manager.enabled = false;
        for (int i = 0; i < manager.GetCompanionCount(); i++) manager.GetCompanion(i).gameObject.SetActive(false);
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<PlayerController>().enabled = false;
        player.GetComponent<PlayerCombat>().enabled = false;
        player.GetComponent<PlayerDodge>().enabled = false;
        var playerBody = player.GetComponent<Rigidbody>();
        playerBody.linearVelocity = Vector3.zero;
        playerBody.isKinematic = true;
        var playerHP = player.GetComponent<PlayerHealth>();
        Vector3 origin = new Vector3(70, 1.02f, 0);
        for (int role = 0; role < 3; role++)
        {
            Place(playerBody, origin + Vector3.forward * 2.2f);
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRobotModelSetup.PrefabPath(3, role)), origin, Quaternion.identity);
            var controller = enemy.GetComponent<EnemyController>();
            var combat = enemy.GetComponent<EnemyCombat>();
            var health = enemy.GetComponent<EnemyHealth>();
            var animation = enemy.GetComponent<EnemyRobotAnimation>();
            controller.DetectPlayer();
            yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking && animation.CurrentMotion == "Attack", "Robot starts synchronized attack: " + role);
            int before = playerHP.CurrentHP;
            int enemyHP = health.CurrentHP;
            health.TakeDamage(1, player.transform.position);
            Check(health.CurrentHP == enemyHP - 1 && !controller.IsStaggered && combat.IsAttacking, "Robot attack resists knockback without blocking damage: " + role);
            yield return .1f;
            Check(playerHP.CurrentHP == before,
                "Robot attack has no premature damage: " + role);
            yield return new EnemyAlphaValidation.Until(() => playerHP.CurrentHP < before, "Robot animation reaches damage frame: " + role);
            Check(before - playerHP.CurrentHP == health.EnemyData.Attack, "Robot deals the configured damage once: " + role);
            health.TakeDamage(1, player.transform.position);
            yield return .03f;
            Check(controller.IsStaggered && animation.CurrentMotion == "Damage", "Robot plays damage and knockback after attack: " + role);
            yield return .2f;
            health.TakeDamage(1, player.transform.position);
            yield return .08f;
            Check(animation.CurrentMotion == "Damage" && animation.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < .25f,
                "Repeated damage restarts the hit animation: " + role);
            if (role == 2)
            {
                controller.enabled = false;
                combat.CancelAttack();
                Place(enemy.GetComponent<Rigidbody>(), origin);
                Place(playerBody, origin + Vector3.forward * 3.6f);
                Check(combat.IsTargetInRange(player.transform), "Gun reaches a target outside standard range");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = origin + Vector3.forward * 1.6f;
                wall.transform.localScale = new Vector3(4, 4, .1f);
                Physics.SyncTransforms();
                Check(!combat.IsTargetInRange(player.transform), "Gun cannot fire through a wall");
                Object.Destroy(wall);
            }
            health.TakeDamage(health.CurrentHP, player.transform.position);
            Check(!enemy.activeSelf && !combat.IsAttacking, "Robot defeat immediately removes combat: " + role);
            GameObject corpse = GameObject.Find(enemy.name + "_DeathVisual");
            Check(corpse != null && corpse.GetComponentInChildren<Animator>() != null && corpse.GetComponentsInChildren<Collider>().Length == 0,
                "Robot defeat leaves only a non-interactive death animation: " + role);
            yield return new EnemyAlphaValidation.Until(() => corpse == null, "Robot death visual cleans itself up: " + role);
            Object.Destroy(enemy);
        }
        for (int role = 0; role < 3; role++)
            for (int type = 0; type < 5; type++)
            {
                var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRobotModelSetup.PrefabPath(type, role)),
                    new Vector3(62 + type * 4, 1.02f, 12 + role * 4), Quaternion.identity);
                enemy.GetComponent<EnemyController>().enabled = false;
                enemy.GetComponent<EnemyCombat>().enabled = false;
                enemy.GetComponent<EnemyRobotAnimation>().enabled = false;
                enemy.GetComponent<Rigidbody>().isKinematic = true;
                foreach (Canvas canvas in enemy.GetComponentsInChildren<Canvas>()) canvas.gameObject.SetActive(false);
                var animator = enemy.GetComponent<EnemyRobotAnimation>().Animator;
                Check(enemy.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported)),
                    "Supported robot materials: " + EnemyRobotModelSetup.Roles[role] + " " + EnemyRobotModelSetup.Colors[type]);
                var bones = animator.GetComponentsInChildren<Transform>();
                foreach (string motion in new[] { "Walk", "Run", "Attack", "Damage", "Death" })
                {
                    animator.speed = 1f;
                    animator.Play("Base Layer." + motion, 0, .1f);
                    animator.Update(0f);
                    var positions = bones.Select(b => b.localPosition).ToArray();
                    var rotations = bones.Select(b => b.localRotation).ToArray();
                    animator.Update(.2f);
                    Check(bones.Where((b, i) => Vector3.Distance(positions[i], b.localPosition) > .0001f || Quaternion.Angle(rotations[i], b.localRotation) > .01f).Any(),
                        $"{EnemyRobotModelSetup.Roles[role]} {EnemyRobotModelSetup.Colors[type]} {motion} drives the imported hierarchy");
                }
                animator.Play("Base Layer.Idle", 0, 0f);
                animator.Update(0f);
                animator.speed = 0f;
            }
        var light = new GameObject("RobotPreviewLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        var camera = new GameObject("RobotPreviewCamera").AddComponent<Camera>();
        camera.transform.position = new Vector3(70, 18, 35);
        camera.transform.LookAt(new Vector3(70, 1, 16));
        camera.orthographic = true;
        camera.orthographicSize = 9;
        camera.targetTexture = new RenderTexture(1600, 1000, 24);
        yield return .3f;
        camera.Render();
        var target = camera.targetTexture;
        RenderTexture.active = target;
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(".utmp/enemy-robots-lineup.png", pixels.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(pixels);
    }
    private static void Place(Rigidbody body, Vector3 point)
    {
        if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        body.position = point;
        body.transform.position = point;
        body.rotation = Quaternion.identity;
        Physics.SyncTransforms();
    }
    private static void Check(bool condition, string description) => EnemyAlphaValidation.Check(condition, description);
}
