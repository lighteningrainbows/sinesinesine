using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BossEncounterValidation
{
    [MenuItem("Tools/Enemies/Validate Spider Boss Assets")]
    public static void ValidateAssets()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(BossEncounterSetup.DataPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossEncounterSetup.PrefabPath);
        Check(data != null && data.IsBoss && data.MaxHP == 400 && data.MoveSpeed == 3 &&
            data.Experience == 500, "Boss HP, movement speed and reward match the specification");
        Check(data.Type1 == AssetDatabase.LoadAssetAtPath<TypeData>("Assets/ScriptableObjects/Types/Insect.asset"),
            "Boss is insect type");
        Check(data.Attack == 20 && data.AttackDuration == .5f && data.AttackInterval == 2 &&
            data.AttackArea == Vector2.one * 1.5f, "Weak attack parameters match the specification");
        BossAttackSettings attacks = data.BossAttacks;
        Check(attacks.StrongDamage == 40 && attacks.StrongDuration == 2 &&
            attacks.StrongInterval == 4 && attacks.StrongArea == Vector2.one * 2 &&
            Mathf.Approximately(attacks.StrongProbability, .3f), "Strong attack parameters match the specification");
        Check(prefab.GetComponent<BoxCollider>().size == Vector3.one * 3 &&
            prefab.GetComponentsInChildren<Collider>(true).Length == 1,
            "Spider boss has one 3x3x3 body collider");
        Check(prefab.transform.Find("SpiderVisual") != null && prefab.GetComponent<BossAttackTelegraph>() != null,
            "Spider model and attack warning are present");
    }

    public static IEnumerator RunChecks()
    {
        ValidateAssets();
        EditorSceneManager.LoadSceneInPlayMode(BossEncounterSetup.ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return .12f;
        BossEncounter encounter = Object.FindAnyObjectByType<BossEncounter>();
        Check(encounter != null && !encounter.HasStarted && encounter.ActiveBoss == null,
            "Actual exploration scene starts without spawning the boss outside the arena");
        Check(encounter.GetComponentsInChildren<EnemySpawnGroup>().Length == 0,
            "Boss arena is independent of ordinary random-spawn groups");
        var player = GameObject.FindGameObjectWithTag("Player");
        Check(player.layer == LayerMask.NameToLayer("Player"),
            "Scene player uses the combat target layer");
        var playerHealth = player.GetComponent<PlayerHealth>();
        player.GetComponent<PlayerController>().enabled = false;
        player.GetComponent<PlayerCombat>().enabled = false;
        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        playerBody.linearVelocity = Vector3.zero;
        playerBody.isKinematic = true;
        CompanionManager companions = CompanionManager.Instance;
        CompanionController active = companions.ActiveCompanion;
        for (int i = 0; i < companions.GetCompanionCount(); i++)
        {
            CompanionController companion = companions.GetCompanion(i);
            companion.enabled = false;
            companion.GetComponent<CompanionCombat>().enabled = false;
        }
        Rigidbody allyBody = active.GetComponent<Rigidbody>();
        allyBody.linearVelocity = Vector3.zero;
        allyBody.isKinematic = true;
        CanvasGroup panel = encounter.transform.Find("BossHUD/BossPanel").GetComponent<CanvasGroup>();
        Check(panel.alpha == 0, "Boss HP panel is hidden before the encounter");

        // Verify a walkable connection to the start-side platform, including the doorway.
        for (int z = 40; z <= 56; z += 2)
            Check(Physics.Raycast(new Vector3(67.6f, .5f, z), Vector3.down, out RaycastHit hit, 2,
                ~LayerMask.GetMask("Player", "Enemy", "Companion")) && Mathf.Abs(hit.point.y) < .05f,
                $"Northern route has ground at z={z}");

        Move(playerBody, encounter.transform.position + new Vector3(0, 1, -14.5f));
        yield return new EnemyAlphaValidation.Until(() => encounter.ActiveBoss != null,
            "Entering the dedicated area spawns the boss");
        EnemyController boss = encounter.ActiveBoss;
        EnemyHealth bossHP = boss.GetComponent<EnemyHealth>();
        EnemyCombat combat = boss.GetComponent<EnemyCombat>();
        encounter.BeginEncounter();
        encounter.BeginEncounter();
        Check(encounter.ActiveBoss == boss && bossHP.CurrentHP == 400,
            "Repeated encounter triggers do not duplicate the boss");
        yield return .06f;
        Check(boss.IsTargetAvailable(player.transform) &&
            Vector3.Distance(player.transform.position, boss.transform.position) > 4,
            "Boss can pursue targets across its arena rather than a four-metre group radius");
        Check(panel.alpha == 1 && encounter.transform.Find("BossHUD/BossPanel/BossHP").GetComponent<Slider>().maxValue == 400,
            "Boss HP display activates with a 400 HP maximum");
        Text statusLabel = encounter.transform.Find("BossHUD/BossPanel/BossStatus").GetComponent<Text>();
        Check(statusLabel.rectTransform.rect.height >= statusLabel.preferredHeight,
            "Boss title has enough vertical space to render without clipping");

        Camera preview = CreateArenaCamera(encounter);
        yield return .25f;
        Capture(preview);
        Canvas bossCanvas = encounter.transform.Find("BossHUD").GetComponent<Canvas>();
        bossCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        bossCanvas.worldCamera = null;
        Object.Destroy(preview.gameObject);

        BossAttackSettings attacks = bossHP.EnemyData.BossAttacks;
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(791);
        int strongRolls = 0;
        for (int i = 0; i < 10000; i++) if (attacks.SelectStrongAttack(false)) strongRolls++;
        Check(strongRolls > 2700 && strongRolls < 3300, "Eligible attacks follow the specified 70/30 random weighting");
        bool alwaysWeakAfterStrong = true;
        for (int i = 0; i < 100; i++)
            alwaysWeakAfterStrong &= !attacks.SelectStrongAttack(true);
        Check(alwaysWeakAfterStrong, "A strong attack always forces a subsequent weak selection");
        UnityEngine.Random.state = randomState;

        Move(playerBody, new Vector3(boss.transform.position.x, 1, boss.transform.position.z - 2.2f));
        UnityEngine.Random.state = RandomForRoll(false);
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking, "Boss starts a weak attack");
        Check(!combat.IsStrongAttack && combat.CurrentAttackDamage == 20 &&
            combat.CurrentAttackArea == Vector2.one * 1.5f, "Weak attack commits its own damage and range");
        int health = playerHealth.CurrentHP;
        float began = Time.time;
        yield return .2f;
        Check(playerHealth.CurrentHP == health, "Weak attack respects its wind-up");
        int bossHealthBefore = bossHP.CurrentHP;
        bossHP.TakeDamage(1, player.transform.position);
        Check(bossHP.CurrentHP == bossHealthBefore - 1 && combat.IsAttacking &&
            !boss.IsStaggered && !boss.CanReceiveKnockback,
            "October 23: weak wind-up takes damage without knockback or cancellation");
        yield return new EnemyAlphaValidation.Until(() => playerHealth.CurrentHP < health,
            "Weak attack lands");
        Check(health - playerHealth.CurrentHP == 20 && Time.time - began >= .45f &&
            combat.CooldownRemaining > 1.8f, "Weak attack deals 20 after 0.5s and rests for 2s");

        UnityEngine.Random.state = RandomForRoll(true);
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking, "Boss starts its strong attack");
        Check(combat.IsStrongAttack && combat.CurrentAttackDamage == 40 &&
            combat.CurrentAttackDuration == 2 && combat.CurrentAttackArea == Vector2.one * 2,
            "Strong attack commits its own damage, duration and larger area");
        yield return .04f;
        Check(boss.transform.Find("AttackWarning").gameObject.activeSelf,
            "Attack wind-up shows the ground warning");
        health = playerHealth.CurrentHP;
        began = Time.time;
        yield return .8f;
        Check(playerHealth.CurrentHP == health, "Strong attack does not deal damage early");
        bossHealthBefore = bossHP.CurrentHP;
        bossHP.TakeDamage(1, player.transform.position, CombatKnockback.Finish);
        Check(bossHP.CurrentHP == bossHealthBefore - 1 && combat.IsAttacking &&
            combat.IsStrongAttack && !boss.IsStaggered,
            "October 23: even a finisher cannot interrupt the heavy wind-up");
        yield return new EnemyAlphaValidation.Until(() => playerHealth.CurrentHP < health,
            "Strong attack lands");
        Check(health - playerHealth.CurrentHP == 40 && Time.time - began >= 1.9f &&
            combat.CooldownRemaining > 3.8f, "Strong attack deals 40 after 2s and rests for 4s");
        UnityEngine.Random.state = RandomForRoll(true);
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking,
            "Boss attacks again after the strong cooldown");
        Check(!combat.IsStrongAttack, "Even a strong random roll cannot produce consecutive heavy attacks");
        yield return new EnemyAlphaValidation.Until(() => combat.IsInPostAttackInterval,
            "Boss completes its attack before the knockback window opens");
        bossHP.TakeDamage(1, player.transform.position);
        Check(!combat.IsAttacking && boss.IsStaggered && boss.CanReceiveKnockback,
            "October 23: a hit staggers the stationary boss during its post-attack interval");
        yield return new EnemyAlphaValidation.Until(() => boss.CurrentState == EnemyController.EnemyState.AirPause,
            "Boss knockback reaches the aerial pause");
        bossHP.TakeDamage(1, player.transform.position);
        Check(boss.CurrentState == EnemyController.EnemyState.Knockback, "Boss follow-up restarts its hit reaction");
        yield return new EnemyAlphaValidation.Until(() => boss.CurrentState == EnemyController.EnemyState.Recovering,
            "Boss lands and begins recovery");
        began = Time.time;
        yield return new EnemyAlphaValidation.Until(() => !boss.IsStaggered, "Boss completes landing recovery");
        Check(Time.time - began >= .95f, "Boss landing recovery lasts one second");
        combat.enabled = false;

        Move(allyBody, boss.transform.position + new Vector3(2.2f, 0, 0));
        Move(playerBody, boss.transform.position + new Vector3(0, -.5f, -2.2f));
        randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(521);
        int heroTargets = 0, allyTargets = 0;
        for (int i = 0; i < 20; i++)
        {
            boss.ClearTarget();
            boss.DetectPlayer();
            yield return .04f;
            if (boss.CurrentTarget == player.transform) heroTargets++;
            if (boss.CurrentTarget == active.transform) allyTargets++;
        }
        UnityEngine.Random.state = randomState;
        Check(heroTargets > 0 && allyTargets > 0, "Boss randomly selects both living player and companion");
        playerHealth.TakeDamage(playerHealth.MaxHP);
        yield return .06f;
        Check(boss.CurrentTarget == active.transform, "Boss retargets the companion when the player is defeated");
        int remainingBossHP = bossHP.CurrentHP;
        Move(allyBody, encounter.transform.position + new Vector3(20, 2, 0));
        yield return .06f;
        Check(boss.CurrentTarget == null, "Boss stops targeting characters outside its dedicated area");
        playerHealth.Heal(playerHealth.MaxHP);
        Move(playerBody, encounter.transform.position + new Vector3(20, 1, 0));
        yield return .04f;
        Move(playerBody, encounter.transform.position + new Vector3(0, 1, -12));
        yield return .06f;
        Check(encounter.ActiveBoss == boss && bossHP.CurrentHP == remainingBossHP,
            "Leaving and re-entering preserves the same boss and remaining HP");

        ExperienceComponent playerExp = player.GetComponent<ExperienceComponent>();
        int priorPlayerExp = playerExp.Experience;
        int[] prior = new int[companions.GetCompanionCount()];
        for (int i = 0; i < prior.Length; i++)
            prior[i] = companions.GetCompanion(i).GetComponent<ExperienceComponent>().Experience;
        bossHP.TakeDamage(10000, player.transform.position);
        yield return .08f;
        Check(encounter.IsCleared && !boss.gameObject.activeSelf, "Defeating the boss clears the encounter and removes it");
        Check(playerExp.Experience == priorPlayerExp + 500, "Player receives the boss's 500 EXP");
        for (int i = 0; i < prior.Length; i++)
            Check(companions.GetCompanion(i).GetComponent<ExperienceComponent>().Experience == prior[i] + 500,
                $"Owned robot {i} receives 500 EXP irrespective of type or deployment");
        Check(encounter.transform.Find("BossHUD/BossPanel/BossStatus").GetComponent<Text>().text == "AREA CLEAR",
            "Boss UI displays encounter completion");
        Check(encounter.transform.Find("BossHUD/BossPanel/BossReward").GetComponent<Text>().text.Contains("500"),
            "Boss UI displays the all-robot reward");
        bossHP.TakeDamage(10000);
        encounter.BeginEncounter();
        Move(playerBody, encounter.transform.position + new Vector3(20, 1, 0));
        yield return .04f;
        Move(playerBody, encounter.transform.position + new Vector3(0, 1, -12));
        yield return .06f;
        Check(encounter.IsCleared && encounter.ActiveBoss == boss && !boss.gameObject.activeSelf,
            "Cleared encounter cannot respawn from repeated entry or event triggers");
        Check(playerExp.Experience == priorPlayerExp + 500, "Repeated damage never duplicates the boss reward");
        IEnumerator knockback = RunOctober23Checks(encounter, playerBody);
        while (knockback.MoveNext()) yield return knockback.Current;
    }

    private static IEnumerator RunOctober23Checks(BossEncounter encounter, Rigidbody player)
    {
        Vector3 home = encounter.transform.position + Vector3.up * 1.51f;
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            BossEncounterSetup.PrefabPath), home, Quaternion.identity);
        EnemyController boss = root.GetComponent<EnemyController>();
        EnemyCombat combat = root.GetComponent<EnemyCombat>();
        EnemyHealth hp = root.GetComponent<EnemyHealth>();
        Rigidbody body = root.GetComponent<Rigidbody>();
        // Keep the real timings and physics, making only the attack selection deterministic.
        EnemyData data = Object.Instantiate(hp.EnemyData);
        BossAttackSettings attacks = Object.Instantiate(data.BossAttacks);
        Set(attacks, "strongProbability", 0f);
        Set(data, "bossAttacks", attacks);
        Set(boss, "enemyData", data);
        Set(combat, "enemyData", data);
        Set(hp, "enemyData", data);
        boss.SetBossEncounter(encounter);
        BattleSystem battle = BattleSystem.Instance;
        Move(player, home + new Vector3(0, -.5f, -7));
        int before = hp.CurrentHP;
        battle.Attack(player.gameObject, root, 15, null, CombatKnockback.Normal);
        Check(hp.CurrentHP == before - 4 && !boss.IsStaggered && !boss.CanReceiveKnockback,
            "October 23: an idle boss before its first attack takes damage but resists knockback");
        boss.DetectPlayer();
        yield return .12f;
        Check(boss.CurrentState == EnemyController.EnemyState.Chasing && !boss.CanReceiveKnockback,
            "October 23: a moving boss is immune to knockback");
        before = hp.CurrentHP;
        battle.Attack(player.gameObject, root, 30, null, CombatKnockback.Finish);
        Check(hp.CurrentHP == before - 8 && !boss.IsStaggered,
            "October 23: finisher damage applies during pursuit without interrupting movement");
        Move(player, body.position + new Vector3(0, -.5f, -2.2f));
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking,
            "October 23: the boss starts its first attack normally");
        yield return new EnemyAlphaValidation.Until(() => combat.IsInPostAttackInterval,
            "October 23: a completed attack opens the real two-second interval");
        Check(boss.CanReceiveKnockback, "October 23: the stationary post-attack boss is vulnerable");

        Move(player, body.position + new Vector3(0, -.5f, -7));
        yield return .06f;
        Check(combat.CooldownRemaining > 0f && boss.CurrentState == EnemyController.EnemyState.Chasing &&
            !boss.CanReceiveKnockback, "October 23: moving during an interval still prevents knockback");
        before = hp.CurrentHP;
        battle.Attack(player.gameObject, root, 15, null, CombatKnockback.Normal);
        Check(hp.CurrentHP == before - 4 && !boss.IsStaggered,
            "October 23: a moving boss keeps chasing when hit during cooldown");
        Move(player, body.position + new Vector3(0, -.5f, -2.2f));
        yield return new EnemyAlphaValidation.Until(() => boss.CanReceiveKnockback,
            "October 23: stopping in attack range reopens the remaining interval");
        battle.Attack(player.gameObject, root, 15, null, CombatKnockback.Normal);
        yield return new EnemyAlphaValidation.Until(() => boss.CurrentState == EnemyController.EnemyState.AirPause,
            "October 23: a normal player hit still launches and pauses the vulnerable boss");
        float intervalBefore = combat.CooldownRemaining;
        hp.TakeDamage(1, player.position, CombatKnockback.Normal);
        Check(boss.CurrentState == EnemyController.EnemyState.Knockback &&
            combat.CooldownRemaining <= intervalBefore && combat.IsInPostAttackInterval,
            "October 23: follow-up hits restart knockback without resetting or closing the interval");
        yield return new EnemyAlphaValidation.Until(() => combat.CooldownRemaining > 0f &&
            combat.CooldownRemaining < .14f, "October 23: the original interval approaches its deadline");
        battle.Attack(player.gameObject, root, 30, null, CombatKnockback.Finish);
        Check(boss.IsStaggered, "October 23: a finisher just before interval expiry starts a launch");
        float deadline = Time.time + combat.CooldownRemaining;
        yield return new EnemyAlphaValidation.Until(() => combat.CooldownRemaining <= 0f,
            "October 23: cooldown continues to expire while the boss is airborne");
        yield return .04f;
        Check(!boss.CanReceiveKnockback && boss.CurrentState == EnemyController.EnemyState.Falling &&
            body.useGravity && body.linearVelocity.y <= 0f &&
            new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude < .01f,
            "October 23: interval expiry cancels the aerial launch and drops the boss vertically");
        for (int i = 0; i < 3; i++)
        {
            before = hp.CurrentHP;
            battle.Attack(player.gameObject, root, 30, null, CombatKnockback.Finish);
            Check(hp.CurrentHP == before - 8 && !boss.CanReceiveKnockback &&
                boss.CurrentState != EnemyController.EnemyState.Knockback &&
                boss.CurrentState != EnemyController.EnemyState.AirPause,
                $"October 23: airborne follow-up {i + 1} deals damage without renewing the launch");
            yield return .025f;
        }
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking,
            "October 23: the landed boss breaks out of the combo and attacks again");
        Check(Time.time - deadline < .8f && !boss.CanReceiveKnockback,
            "October 23: expired knockback adds no extra one-second recovery before attacking");

        yield return new EnemyAlphaValidation.Until(() => combat.IsInPostAttackInterval,
            "October 23: the next completed attack opens a fresh interval");
        Move(player, body.position + new Vector3(0, -.5f, -2.2f));
        yield return new EnemyAlphaValidation.Until(() => combat.CooldownRemaining < .9f,
            "October 23: the next interval reaches its final second");
        battle.Attack(player.gameObject, root, 15, null, CombatKnockback.Normal);
        yield return new EnemyAlphaValidation.Until(() => boss.CurrentState == EnemyController.EnemyState.Recovering,
            "October 23: a vulnerable boss still enters normal landing recovery");
        deadline = Time.time + combat.CooldownRemaining;
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking,
            "October 23: interval expiry also releases a grounded recovery");
        Check(Time.time - deadline < .15f && !boss.IsStaggered,
            "October 23: grounded recovery cannot extend the post-attack vulnerability deadline");
        combat.CancelAttack();
        Check(combat.CooldownRemaining > 0f && !combat.IsInPostAttackInterval && !boss.CanReceiveKnockback,
            "October 23: a cancelled wind-up is not a completed post-attack interval");
        before = hp.CurrentHP;
        battle.Attack(player.gameObject, root, 15, null, CombatKnockback.Normal);
        Check(hp.CurrentHP == before - 4 && !boss.IsStaggered,
            "October 23: cancellation cooldown does not accidentally make the boss vulnerable");
        root.SetActive(false);
        root.SetActive(true);
        Check(!combat.IsInPostAttackInterval && !boss.CanReceiveKnockback,
            "October 23: reactivation resets interval and knockback eligibility");
        boss.DetectPlayer();
        Move(player, body.position + new Vector3(0, -.5f, -2.2f));
        yield return new EnemyAlphaValidation.Until(() => combat.IsAttacking,
            "October 23: reactivated boss starts an immune attack");
        hp.TakeDamage(10000, player.position);
        Check(hp.CurrentHP == 0 && !root.activeSelf && !combat.IsAttacking,
            "October 23: knockback immunity never prevents lethal damage or defeat");
        Object.Destroy(root);
        Object.Destroy(data);
        Object.Destroy(attacks);
        yield return .04f;
    }

    private static void Set(Object target, string field, Object value)
    {
        var settings = new SerializedObject(target);
        settings.FindProperty(field).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string field, float value)
    {
        var settings = new SerializedObject(target);
        settings.FindProperty(field).floatValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static UnityEngine.Random.State RandomForRoll(bool strong)
    {
        for (int seed = 0; seed < 100; seed++)
        {
            UnityEngine.Random.InitState(seed);
            UnityEngine.Random.State state = UnityEngine.Random.state;
            if ((UnityEngine.Random.value < .3f) == strong) return state;
        }
        throw new InvalidOperationException("Unable to select a deterministic random roll.");
    }

    private static void Move(Rigidbody body, Vector3 position)
    {
        body.position = position;
        Physics.SyncTransforms();
    }

    private static Camera CreateArenaCamera(BossEncounter encounter)
    {
        var camera = new GameObject("ArenaPreviewCamera").AddComponent<Camera>();
        camera.transform.position = encounter.transform.position + new Vector3(24, 23, -30);
        camera.transform.LookAt(encounter.transform.position + new Vector3(0, .5f, 0));
        camera.fieldOfView = 53;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.13f, .17f, .23f);
        camera.targetTexture = new RenderTexture(1600, 1000, 24);
        // Include the real HP panel in the verification image.
        Canvas canvas = encounter.transform.Find("BossHUD").GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        return camera;
    }

    private static void Capture(Camera camera)
    {
        RenderTexture target = camera.targetTexture;
        RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();
        Directory.CreateDirectory(".utmp");
        File.WriteAllBytes(".utmp/boss-arena.png", texture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(texture);
    }

    private static void Check(bool condition, string description) => EnemyAlphaValidation.Check(condition, description);
}
