using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class TigerCompanionValidation
{
    public static void ValidateAssets()
    {
        RobotData data = AssetDatabase.LoadAssetAtPath<RobotData>(TigerCompanionSetup.DataPath);
        Check(data != null && data.MaxHP == 200 && data.Attack == 45 && data.MoveSpeed == 4f &&
            data.AttackInterval == 2f && data.AttackRange == 1.5f && data.Type1.name == "Beast" && data.Type2 == null,
            "Tiger: workbook HP, damage, speed, interval, reach and beast type");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TigerCompanionSetup.PrefabPath);
        Check(prefab.GetComponent<TigerAnimation>() != null && prefab.GetComponent<CompanionCombat>() != null &&
            prefab.GetComponent<RobotRuntimeStats>() != null && prefab.GetComponent<ExperienceComponent>() != null,
            "Tiger: prefab includes animation, combat, HP and experience");
        Check(prefab.GetComponentsInChildren<Collider>(true).Length == 1 &&
            prefab.GetComponent<BoxCollider>() != null && prefab.transform.localScale == Vector3.one,
            "Tiger: model has a single stable root collider");
        foreach (string name in new[] { "Idle", "Walk", "Run", "Run_Forward", "Bite", "Roar", "ClawSwipe" })
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TigerCompanionSetup.AnimationFolder + "/" + name + ".anim");
            bool loop = name == "Idle" || name == "Walk" || name == "Run" || name == "Run_Forward";
            Check(clip != null && clip.length > 0f && AnimationUtility.GetAnimationClipSettings(clip).loopTime == loop,
                "Tiger: " + name + " has the correct loop setting");
        }
        foreach (string path in new[] { "Assets/Scenes/System/Boot.unity", BossEncounterSetup.ScenePath })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CompanionManager manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CompanionManager>(true)).Single();
            Check(manager.GetCompanionCount() == 3 && manager.GetCompanion(2).GetComponent<TigerAnimation>() != null &&
                manager.GetCompanion(0).RobotData.RobotName == "Bird" && manager.GetCompanion(1).RobotData.RobotName == "Crab",
                "Tiger: replaces only Mole in " + path);
        }
    }

    public static IEnumerator RunChecks()
    {
        EditorSceneManager.LoadSceneInPlayMode(BossEncounterSetup.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return .15f;
        foreach (StageArea area in Object.FindObjectsByType<StageArea>()) area.enabled = false;
        foreach (EnemySpawnGroup group in Object.FindObjectsByType<EnemySpawnGroup>()) group.gameObject.SetActive(false);
        foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>()) enemy.gameObject.SetActive(false);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<PlayerController>().enabled = false;
        player.GetComponent<PlayerCombat>().enabled = false;
        player.GetComponent<PlayerDodge>().enabled = false;
        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        playerBody.linearVelocity = Vector3.zero;
        playerBody.isKinematic = true;
        Vector3 origin = new Vector3(70, .51f, 0);
        Place(playerBody, origin + new Vector3(0, .5f, 2.3f));
        CompanionManager manager = CompanionManager.Instance;
        manager.enabled = false;
        for (int i = 0; i < 2; i++)
        {
            manager.GetCompanion(i).GetComponent<CompanionCombat>().enabled = false;
            manager.GetCompanion(i).enabled = false;
        }
        manager.SwitchTo(2);
        CompanionController tiger = manager.ActiveCompanion;
        Rigidbody body = tiger.GetComponent<Rigidbody>();
        CompanionCombat combat = tiger.GetComponent<CompanionCombat>();
        TigerAnimation animation = tiger.GetComponent<TigerAnimation>();
        RobotRuntimeStats stats = tiger.GetComponent<RobotRuntimeStats>();
        Animator animator = animation.Animator;
        Place(body, origin);
        yield return .12f;
        Check(tiger.IsDeployed && stats.CurrentHP == 200 && stats.Attack == 45 && animation.CurrentMotion == "Roar",
            $"Tiger: switching to the third slot deploys a healthy tiger and plays Roar (deployed={tiger.IsDeployed}, HP={stats.CurrentHP}, ATK={stats.Attack}, motion={animation.CurrentMotion}, velocity={body.linearVelocity}, target={tiger.GetCurrentTarget()})");
        Check(!animator.applyRootMotion && animator.runtimeAnimatorController != null &&
            tiger.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0,
            "Tiger: imported skinned model uses the gameplay animator without root motion");
        yield return new EnemyAlphaValidation.Until(() => animation.CurrentMotion == "Idle", "Tiger: Roar completes once and returns to Idle");
        Transform jaw = tiger.GetComponentsInChildren<Transform>().First(t => t.name == "Jaw");
        Transform pelvis = tiger.GetComponentsInChildren<Transform>().First(t => t.name == "Pelvis");
        Check(Vector3.Dot(jaw.position - pelvis.position, tiger.transform.forward) > .1f,
            "Tiger: the model's head faces the gameplay forward direction");
        Quaternion jawPose = jaw.localRotation;
        yield return .3f;
        Check(Quaternion.Angle(jawPose, jaw.localRotation) > .001f, "Tiger: Idle animates the imported skeleton");

        playerBody.position = origin + new Vector3(0, .5f, 5f);
        yield return new EnemyAlphaValidation.Until(() => animation.CurrentMotion == "Walk", "Tiger: ordinary player following selects Walk");
        Vector3 walkingStart = body.position;
        yield return .15f;
        Check(body.position.z > walkingStart.z + .3f && Mathf.Abs(body.linearVelocity.z - 4f) < .1f,
            "Tiger: walking animation follows real four-metre-per-second movement");
        Place(body, origin);
        playerBody.position = origin + new Vector3(0, .5f, 12f);
        yield return new EnemyAlphaValidation.Until(() => animation.CurrentMotion == "Run", "Tiger: distant following selects Run");

        Place(body, origin);
        playerBody.position = origin + new Vector3(0, .5f, -2.5f);
        EnemyHealth target = SpawnEnemy(origin + new Vector3(0, .5f, 6));
        yield return new EnemyAlphaValidation.Until(() => animation.CurrentMotion == "Run_Forward", "Tiger: enemy pursuit selects Run_Forward");
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking && animation.CurrentMotion == "Bite", "Tiger: reaching an enemy starts Bite");
        int initialHP = target.CurrentHP;
        float firstStarted = Time.time;
        int firstSequence = combat.AttackSequence;
        Vector3 committedPosition = body.position;
        Check(initialHP == target.MaxHP && combat.AttackProgress < .3f, "Tiger: attack startup has no immediate HP damage");
        yield return .16f;
        Check(target.CurrentHP == initialHP && Vector3.Distance(body.position, committedPosition) < .03f,
            "Tiger: attack wind-up holds position and delays damage");
        yield return new EnemyAlphaValidation.Until(() => target.CurrentHP < initialHP, "Tiger: Bite hits at its impact frame");
        int afterBite = target.CurrentHP;
        Check(initialHP - afterBite == 11, "Tiger: 45 base attack uses the existing neutral ally damage multiplier");
        yield return .15f;
        Check(target.CurrentHP == afterBite && combat.IsAttacking, "Tiger: a Bite deals damage only once during its motion");
        yield return new EnemyAlphaValidation.Until(() => combat.AttackSequence > firstSequence, "Tiger: the next automatic attack starts");
        yield return .02f;
        Check(animation.CurrentMotion == "ClawSwipe" && Mathf.Abs(Time.time - firstStarted - 2f) < .15f,
            "Tiger: ClawSwipe alternates with Bite at the specified two-second attack cadence");
        yield return new EnemyAlphaValidation.Until(() => target.CurrentHP < afterBite, "Tiger: ClawSwipe also deals damage at impact");
        int afterClaw = target.CurrentHP;
        int secondSequence = combat.AttackSequence;
        yield return new EnemyAlphaValidation.Until(() => combat.AttackSequence > secondSequence, "Tiger: a third attack begins");
        Place(target.GetComponent<Rigidbody>(), body.position + Vector3.forward * 9f + Vector3.up * .5f);
        yield return new EnemyAlphaValidation.Until(() => !combat.IsAttacking, "Tiger: a committed attack completes when the target moves away");
        Check(target.CurrentHP == afterClaw, "Tiger: an enemy that leaves reach before impact takes no phantom damage");
        Object.Destroy(target.gameObject);
        yield return .06f;
        tiger.ClearLockedTarget();
        tiger.ClearTarget();
        Place(body, origin);
        target = SpawnEnemy(origin + new Vector3(0, .5f, 2.35f));
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking, "Tiger: a new enemy starts another attack");
        int switchHP = target.CurrentHP;
        manager.SwitchTo(1);
        Check(!combat.IsAttacking && tiger.IsFrozenPrevious && body.isKinematic,
            "Tiger: switching away immediately cancels the pending hit and fixes the tiger");
        yield return .1f;
        Quaternion frozenJaw = jaw.localRotation;
        Check(animation.IsHoldingPose && animator.speed == 0f, "Tiger: fixed companion holds its current pose");
        yield return 1.15f;
        Check(target.CurrentHP == switchHP && Quaternion.Angle(frozenJaw, jaw.localRotation) < .001f,
            "Tiger: frozen tiger neither animates nor delivers a delayed attack");
        FrozenCompanionKnockback reaction = tiger.GetComponent<FrozenCompanionKnockback>();
        int tigerHP = stats.CurrentHP;
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Normal);
        yield return .12f;
        Check(reaction.IsActive && animation.IsHoldingPose && stats.CurrentHP == tigerHP,
            "Tiger: friendly knockback changes physics without HP damage or attacking animation");
        yield return new EnemyAlphaValidation.Until(() => !reaction.IsActive, "Tiger: fixed tiger lands after friendly knockback");
        Check(body.isKinematic && !body.useGravity && tiger.IsFrozenPrevious, "Tiger: launched fixed tiger is fixed again on landing");
        Object.Destroy(target.gameObject);
        yield return .04f;
        manager.SwitchTo(2);
        Place(body, origin);
        target = SpawnEnemy(origin + new Vector3(0, .5f, 2.35f));
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking, "Tiger: switching back resumes animated combat");
        int beforeInterruption = target.CurrentHP;
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Normal);
        yield return .06f;
        Check(!combat.IsAttacking && animation.IsHoldingPose && animator.speed == 0f,
            "Tiger: friendly knockback cancels an attack before impact");
        yield return .55f;
        Check(target.CurrentHP == beforeInterruption && stats.CurrentHP == tigerHP,
            "Tiger: interrupted attack never deals delayed damage, and friendly hits leave HP intact");
        yield return new EnemyAlphaValidation.Until(() => !reaction.IsActive, "Tiger: deployed tiger finishes its reaction");
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking, "Tiger: deployed tiger resumes attacking after landing");
        int beforeDefeat = target.CurrentHP;
        stats.TakeDamage(stats.MaxHP);
        yield return .06f;
        Check(!combat.IsAttacking && animation.IsHoldingPose && animator.speed == 0f &&
            Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).sqrMagnitude < .001f,
            "Tiger: defeat stops movement, animation and pending damage");
        yield return .6f;
        Check(target.CurrentHP == beforeDefeat, "Tiger: a defeated tiger cannot finish a pending strike");
        Object.Destroy(target.gameObject);

        // Verify every imported clip actually drives this hierarchy, independently of state selection.
        tiger.enabled = false;
        combat.enabled = false;
        animation.enabled = false;
        body.isKinematic = true;
        Transform[] bones = tiger.GetComponentsInChildren<Transform>();
        animator.speed = 1f;
        foreach (string motion in new[] { "Idle", "Walk", "Run", "Run_Forward", "Bite", "Roar", "ClawSwipe" })
        {
            animator.Play("Base Layer." + motion, 0, .1f);
            animator.Update(0f);
            Quaternion[] poses = bones.Select(t => t.localRotation).ToArray();
            animator.Update(.27f);
            Check(bones.Where((t, i) => Quaternion.Angle(t.localRotation, poses[i]) > .01f).Any(),
                "Tiger: " + motion + " changes real bone rotations");
            if (motion == "Run_Forward")
            {
                Transform rootBone = bones.First(t => t.name == "Root");
                Check(Mathf.Abs(rootBone.localPosition.x) < .001f && Mathf.Abs(rootBone.localPosition.z) < .001f,
                    "Tiger: forward run stays aligned with the collider without cumulative visual travel");
            }
        }
        Place(body, origin);
        animator.Play("Base Layer.Roar", 0, .35f);
        animator.Update(0f);
        animator.speed = 0f;
        Camera preview = CreatePreview(tiger);
        yield return .25f;
        Capture(preview);
        Check(File.Exists(".utmp/tiger-animation.png"), "Tiger: rendered animated model in the authored scene");
    }

    private static EnemyHealth SpawnEnemy(Vector3 position)
    {
        // Beast versus beast is neutral. Existing ally disadvantage is deliberately zero damage.
        GameObject enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyD.prefab"), position, Quaternion.identity);
        enemy.GetComponent<EnemyController>().enabled = false;
        enemy.GetComponent<EnemyCombat>().enabled = false;
        Rigidbody body = enemy.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        return enemy.GetComponent<EnemyHealth>();
    }

    private static void Place(Rigidbody body, Vector3 position)
    {
        if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        body.position = position;
        body.rotation = Quaternion.identity;
        body.transform.SetPositionAndRotation(position, Quaternion.identity);
        Physics.SyncTransforms();
    }

    private static Camera CreatePreview(CompanionController tiger)
    {
        var light = new GameObject("TigerPreviewLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);
        var camera = new GameObject("TigerPreviewCamera").AddComponent<Camera>();
        camera.transform.position = tiger.transform.position + new Vector3(3, 1.4f, 3.2f);
        camera.transform.LookAt(tiger.transform.position + Vector3.up * .15f);
        camera.targetTexture = new RenderTexture(1024, 768, 24);
        return camera;
    }

    private static void Capture(Camera camera)
    {
        RenderTexture target = camera.targetTexture;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1024, 768, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
        pixels.Apply();
        Directory.CreateDirectory(".utmp");
        File.WriteAllBytes(".utmp/tiger-animation.png", pixels.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(pixels);
    }

    private static void Check(bool condition, string description) => EnemyAlphaValidation.Check(condition, description);
}
