using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Exercises the real prefabs, physics, damage pipeline and AI in Play Mode.
[InitializeOnLoad]
public static class EnemyAlphaValidation
{
    private const string RunKey = "EnemyAlphaValidation.Pending";
    private const string BossRunKey = "EnemyAlphaValidation.IncludeBoss";
    private const string ComboRunKey = "EnemyAlphaValidation.IncludeCombo";
    private const string ComboOnlyKey = "EnemyAlphaValidation.ComboOnly";
    private static IEnumerator checks;
    private static Until pending;
    private static float nextTime;
    private static float pendingDeadline;
    private static double wallDeadline;
    private static int assertions;
    private static bool hasFailed;
    private static readonly List<string> results = new List<string>();

    internal sealed class Until
    {
        public readonly Func<bool> Predicate;
        public readonly string Description;
        public Until(Func<bool> predicate, string description)
        {
            Predicate = predicate;
            Description = description;
        }
    }

    static EnemyAlphaValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(ComboOnlyKey, false);
        SessionState.SetBool(ComboRunKey, false);
        SessionState.SetBool(BossRunKey, false);
        try
        {
            EnemyAlphaSetup.Apply();
            ValidateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(RunKey, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    public static void RunBossBatch()
    {
        SessionState.SetBool(ComboOnlyKey, false);
        SessionState.SetBool(ComboRunKey, false);
        SessionState.SetBool(BossRunKey, true);
        try
        {
            BossEncounterSetup.Apply();
            BossEncounterValidation.ValidateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(RunKey, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    [MenuItem("Tools/Player/Validate Combo And Combat")]
    public static void RunComboBatch()
    {
        SessionState.SetBool(ComboOnlyKey, false);
        SessionState.SetBool(ComboRunKey, true);
        SessionState.SetBool(BossRunKey, true);
        try
        {
            // Validate authored assets without regenerating existing scene content.
            ValidateAssets();
            BossEncounterValidation.ValidateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(RunKey, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    public static void RunComboOnlyBatch()
    {
        SessionState.SetBool(ComboOnlyKey, true);
        SessionState.SetBool(ComboRunKey, true);
        SessionState.SetBool(BossRunKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(RunKey, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Enemies/Validate Alpha Assets")]
    public static void ValidateAssets()
    {
        int[] hp = { 100, 100, 200, 100, 50 };
        int[] attack = { 10, 10, 10, 20, 5 };
        float[] speed = { 3f, 3f, 1.5f, 3f, 6f };
        float[] duration = { .5f, .5f, 1f, .5f, .5f };
        float[] interval = { 2f, 2f, 3f, 2f, 1f };
        float[] area = { 2f, 1.5f, 1.5f, 1.5f, 1f };
        string[] types = { "Flying", "Aquatic", "Crustacean", "Beast", "Insect" };
        for (int i = 0; i < 5; i++)
        {
            char id = (char)('A' + i);
            GameObject prefab = Prefab(id);
            EnemyData data = Data(id);
            Check(data.MaxHP == hp[i] && data.Attack == attack[i] &&
                Mathf.Approximately(data.MoveSpeed, speed[i]), $"{id}: alpha HP/attack/speed");
            Check(Mathf.Approximately(data.AttackDuration, duration[i]) &&
                Mathf.Approximately(data.AttackInterval, interval[i]), $"{id}: independent action/cooldown times");
            Check(data.AttackArea == Vector2.one * area[i] && data.Experience == 100,
                $"{id}: rectangular attack area and EXP");
            Check(data.Type1 == AssetDatabase.LoadAssetAtPath<TypeData>(
                $"Assets/ScriptableObjects/Types/{types[i]}.asset"), $"{id}: type retained");
            Check(prefab.GetComponent<BoxCollider>().size == Vector3.one * 2f &&
                prefab.transform.localScale == Vector3.one &&
                prefab.GetComponentsInChildren<Collider>(true).Length == 1, $"{id}: single 2x2x2 body collider");
            Check(prefab.transform.Find("AlphaVisual").childCount >= 6 &&
                !prefab.GetComponent<MeshRenderer>().enabled, $"{id}: model and original renderer");
            Check(prefab.GetComponent<EnemyController>().IsFlying == (i == 0 || i == 1),
                $"{id}: crow/floating fish flight setting");
        }
        Debug.Log("Enemy alpha asset validation passed.");
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunKey, false)) return;
        assertions = 0;
        hasFailed = false;
        results.Clear();
        checks = SessionState.GetBool(ComboOnlyKey, false) ? PlayerComboValidation.RunChecks() :
            SessionState.GetBool(BossRunKey, false) ? RunAllChecks() : RunChecks();
        nextTime = 0f;
        pending = null;
        wallDeadline = EditorApplication.timeSinceStartup + 240;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        // This pre-existing editor extension error is unrelated to the test scene.
        // Record the exact known failure, while all other errors still fail validation.
        if (type == LogType.Error && message.EndsWith(" is not valid") &&
            trace.Contains("Utage.ExtensionPackageAutoImporter") &&
            trace.Contains("Utage.AssetDataBaseEx:ImportPackageByGuid"))
        {
            results.Add("EXTERNAL Utage extension package is missing: " + message);
            return;
        }
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Fail(new Exception(message + "\n" + trace));
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > wallDeadline)
                throw new TimeoutException("Play Mode validation exceeded 240 seconds.");
            if (!EditorApplication.isPlaying || Time.time < nextTime) return;
            if (pending != null)
            {
                if (!pending.Predicate())
                {
                    if (Time.time > pendingDeadline) throw new TimeoutException(pending.Description);
                    return;
                }
                Check(true, pending.Description);
                pending = null;
            }
            if (!checks.MoveNext())
            {
                Finish();
                return;
            }
            if (checks.Current is float delay) nextTime = Time.time + delay;
            else if (checks.Current is Until condition)
            {
                pending = condition;
                pendingDeadline = Time.time + 8f;
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static IEnumerator RunChecks()
    {
        ValidateAssets();
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "ValidationGround";
        ground.transform.position = new Vector3(0, -.5f, 0);
        ground.transform.localScale = new Vector3(100, 1, 100);
        var battle = new GameObject("BattleSystem").AddComponent<BattleSystem>();
        Set(battle, "damageMultiplierSettings", AssetDatabase.LoadAssetAtPath<DamageMultiplierSettings>(
            "Assets/ScriptableObjects/Battle/DamageMultiplierSettings.asset"));
        new GameObject("TypeChart").AddComponent<TypeChart>();
        PlayerHealth player = CreatePlayer(new Vector3(0, 1, 2.2f));
        EnemyController enemy = Spawn('C');
        EnemyCombat combat = enemy.GetComponent<EnemyCombat>();
        enemy.DetectPlayer();
        yield return new Until(() => combat.IsAttacking, "C begins its one-second attack");
        int playerHP = player.CurrentHP;
        float started = Time.time;
        yield return .35f;
        Check(player.CurrentHP == playerHP, "Attack does not deal immediate damage");
        yield return new Until(() => player.CurrentHP < playerHP, "Attack lands after its wind-up");
        Check(Time.time - started >= .95f && player.CurrentHP == playerHP - 10,
            "C action lasts one second and hits once for 10");
        Check(combat.CooldownRemaining > 2.8f, "C enters its separate three-second cooldown");
        yield return .65f;
        Check(player.CurrentHP == playerHP - 10, "No repeat damage during cooldown");
        Vector3 beforeMove = enemy.transform.position;
        player.transform.position = new Vector3(0, 1, 3.8f);
        Physics.SyncTransforms();
        yield return .35f;
        Check(enemy.transform.position.z > beforeMove.z + .2f,
            "Enemy chases a retreating target during cooldown");
        Object.Destroy(enemy.gameObject);
        yield return .05f;

        player.transform.position = new Vector3(0, 1, 2.2f);
        Physics.SyncTransforms();
        enemy = Spawn('E');
        combat = enemy.GetComponent<EnemyCombat>();
        EnemyHealth enemyHP = enemy.GetComponent<EnemyHealth>();
        Rigidbody body = enemy.GetComponent<Rigidbody>();
        enemy.DetectPlayer();
        yield return new Until(() => combat.IsAttacking, "E begins its attack");
        playerHP = player.CurrentHP;
        Vector3 hitPosition = body.position;
        battle.Attack(player.gameObject, enemy.gameObject, 4, null);
        Check(enemyHP.CurrentHP == 49 && !combat.IsAttacking && enemy.IsStaggered,
            "BattleSystem passes attacker position and damage interrupts the attack");
        yield return new Until(() => enemy.CurrentState == EnemyController.EnemyState.AirPause,
            "Knockback enters the aerial pause");
        Vector3 pausedPosition = body.position;
        Vector3 horizontal = pausedPosition - hitPosition;
        horizontal.y = 0f;
        Check(Mathf.Abs(horizontal.magnitude - .3f) < .035f && horizontal.z < 0f,
            "Knockback travels 0.3m away from the attacker");
        Check(!body.useGravity, "Gravity is disabled during the aerial pause");
        yield return .08f;
        Check(Vector3.Distance(pausedPosition, body.position) < .015f,
            "Enemy remains stationary in the air");
        enemyHP.TakeDamage(1, body.position + Vector3.left);
        Check(enemy.CurrentState == EnemyController.EnemyState.Knockback,
            "A follow-up during the aerial pause restarts knockback");
        yield return new Until(() => enemy.CurrentState == EnemyController.EnemyState.AirPause,
            "Follow-up reaches a fresh aerial pause");
        float pauseStarted = Time.time;
        yield return new Until(() => enemy.CurrentState == EnemyController.EnemyState.Falling,
            "Aerial pause ends in a gravity-driven fall");
        Check(Time.time - pauseStarted >= .17f && body.useGravity, "Aerial pause lasts 0.2 seconds");
        yield return new Until(() => enemy.CurrentState == EnemyController.EnemyState.Recovering,
            "Landing begins recovery");
        float landed = Time.time;
        yield return .65f;
        Check(enemy.IsStaggered && player.CurrentHP == playerHP,
            "No movement or attacks during landing recovery");
        yield return new Until(() => !enemy.IsStaggered, "Enemy resumes after landing recovery");
        Check(Time.time - landed >= .95f, "Landing recovery lasts one second");
        Object.Destroy(enemy.gameObject);
        yield return .05f;

        enemy = Spawn('B');
        enemyHP = enemy.GetComponent<EnemyHealth>();
        body = enemy.GetComponent<Rigidbody>();
        yield return new Until(() => body.position.y > 2.2f, "Aquatic enemy hovers");
        enemyHP.TakeDamage(1, player.transform.position);
        yield return new Until(() => enemy.CurrentState == EnemyController.EnemyState.Recovering,
            "Floating enemy falls to the ground when struck");
        Check(body.position.y < 1.1f, "Floating enemy lands on the ground");
        yield return new Until(() => !enemy.IsStaggered, "Floating enemy completes its recovery");
        Check(!body.useGravity, "Hover gravity setting is restored after recovery");
        yield return new Until(() => body.position.y > 1.2f, "Floating enemy regains altitude");
        Object.Destroy(enemy.gameObject);
        yield return .05f;

        // Rectangular range and a moving target: a locked attack must be dodgeable.
        player.transform.position = new Vector3(0, 1, 2.2f);
        Physics.SyncTransforms();
        enemy = Spawn('D');
        enemyHP = enemy.GetComponent<EnemyHealth>();
        combat = enemy.GetComponent<EnemyCombat>();
        Check(combat.IsTargetInRange(player.transform), "Forward rectangle includes a reachable target");
        player.transform.position = new Vector3(2, 1, 0);
        Physics.SyncTransforms();
        Check(!combat.IsTargetInRange(player.transform), "Forward rectangle excludes a target at the side");
        player.transform.position = new Vector3(0, 1, 2.2f);
        Physics.SyncTransforms();
        enemy.DetectPlayer();
        yield return new Until(() => combat.IsAttacking, "D commits to an attack");
        playerHP = player.CurrentHP;
        player.transform.position = new Vector3(3.5f, 1, 0);
        Physics.SyncTransforms();
        yield return .65f;
        Check(player.CurrentHP == playerHP, "Moving out of the attack box dodges the committed attack");
        Object.Destroy(enemy.gameObject);
        yield return .05f;

        // A frozen/defeated companion is no longer a valid target even while active.
        var managerRoot = new GameObject("CompanionManager");
        managerRoot.SetActive(false);
        var manager = managerRoot.AddComponent<CompanionManager>();
        GameObject allyRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/Crab.prefab"));
        var ally = allyRoot.GetComponent<CompanionController>();
        allyRoot.GetComponent<CompanionCombat>().enabled = false;
        Set(manager, "player", player.transform);
        Set(manager, "companions", new Object[] { ally });
        managerRoot.SetActive(true);
        yield return .05f;
        ally.enabled = false;
        allyRoot.transform.position = new Vector3(-1.5f, 1, 1.5f);
        player.transform.position = new Vector3(0, 1, 2.2f);
        var group = new GameObject("Group").AddComponent<EnemySpawnGroup>();
        group.SetPlayer(player.transform);
        enemy = Spawn('A');
        enemy.SetSpawnGroup(group);
        combat = enemy.GetComponent<EnemyCombat>();
        combat.enabled = false;
        Physics.SyncTransforms();
        UnityEngine.Random.State oldRandom = UnityEngine.Random.state;
        UnityEngine.Random.InitState(173);
        int chosePlayer = 0, choseAlly = 0;
        for (int i = 0; i < 24; i++)
        {
            enemy.ClearTarget();
            enemy.DetectPlayer();
            yield return .04f;
            if (enemy.CurrentTarget == player.transform) chosePlayer++;
            if (enemy.CurrentTarget == ally.transform) choseAlly++;
        }
        UnityEngine.Random.state = oldRandom;
        Check(chosePlayer > 0 && choseAlly > 0, "Random targeting selects both player and deployed companion");
        ally.FreezeAsPreviousCompanion();
        Check(!EnemyController.IsLivingTarget(ally.transform), "Frozen previous companion is excluded");
        enemy.ClearTarget();
        yield return .06f;
        Check(enemy.CurrentTarget == player.transform, "Enemy retargets after a companion is withdrawn");
        player.TakeDamage(player.MaxHP);
        yield return .06f;
        Check(enemy.CurrentTarget == null, "A defeated player is excluded even when still active");
        Check(!group.ContainsTarget(player.transform), "Defeated characters do not keep a group alerted");
        enemy.LosePlayerDetection();
        yield return new Until(() => Vector2.Distance(
            new Vector2(enemy.transform.position.x, enemy.transform.position.z), Vector2.zero) < .15f,
            "Enemy returns to its waiting position when no valid targets remain");
        enemyHP = enemy.GetComponent<EnemyHealth>();
        enemyHP.TakeDamage(enemyHP.MaxHP, player.transform.position);
        Check(!enemy.gameObject.activeSelf && !combat.IsAttacking,
            "Defeat removes the character and cancels its attack");

        Object.Destroy(enemy.gameObject);
        player.Heal(player.MaxHP);
        player.transform.position = new Vector3(0, 1, -5);
        ally.ReleaseFrozenState();
        ally.Deploy();
        ally.enabled = true;
        allyRoot.GetComponent<CompanionCombat>().enabled = true;
        allyRoot.GetComponent<Rigidbody>().position = new Vector3(0, 1, -3.3f);
        enemy = Spawn('C');
        enemyHP = enemy.GetComponent<EnemyHealth>();
        Physics.SyncTransforms();
        yield return new Until(() => enemyHP.CurrentHP < enemyHP.MaxHP,
            "A real companion can approach and attack the enlarged two-metre body");

        // Player attacks must not hit a single actor twice through two colliders.
        ally.enabled = false;
        allyRoot.GetComponent<CompanionCombat>().enabled = false;
        enemy.GetComponent<Rigidbody>().position = new Vector3(0, 1, 0);
        var extraCollider = new GameObject("ExtraHitCollider");
        extraCollider.layer = LayerMask.NameToLayer("Enemy");
        extraCollider.transform.SetParent(enemy.transform, false);
        extraCollider.AddComponent<BoxCollider>().size = Vector3.one;
        player.transform.position = new Vector3(0, 1, -1.6f);
        player.transform.rotation = Quaternion.identity;
        var playerCombat = player.gameObject.AddComponent<PlayerCombat>();
        playerCombat.enabled = false;
        Set(playerCombat, "playerData", AssetDatabase.LoadAssetAtPath<PlayerData>(
            "Assets/ScriptableObjects/Players/Player.asset"));
        var playerCombatSettings = new SerializedObject(playerCombat);
        playerCombatSettings.FindProperty("targetLayer").intValue = LayerMask.GetMask("Enemy");
        playerCombatSettings.ApplyModifiedPropertiesWithoutUndo();
        Physics.SyncTransforms();
        int beforeHP = enemyHP.CurrentHP;
        playerCombat.SendMessage("Attack", SendMessageOptions.RequireReceiver);
        Check(beforeHP - enemyHP.CurrentHP == 4,
            "Player attack deals damage once per enemy with multiple colliders");
        Object.Destroy(enemy.gameObject);
        yield return .05f;

        // Check the actual group monitor and replacement lifecycle with a shorter test-only delay.
        var respawnGroup = new GameObject("RespawnGroup").AddComponent<EnemySpawnGroup>();
        respawnGroup.transform.position = new Vector3(15, 0, 0);
        Transform spawnPoint = new GameObject("SpawnPoint").transform;
        spawnPoint.position = new Vector3(15, 1, 0);
        player.transform.position = new Vector3(15, 1, 2.2f);
        respawnGroup.SetPlayer(player.transform);
        Set(respawnGroup, "enemyPrefabs", new Object[] { Prefab('E') });
        Set(respawnGroup, "spawnPoints", new Object[] { spawnPoint });
        var respawnSettings = new SerializedObject(respawnGroup);
        respawnSettings.FindProperty("minEnemyCount").intValue = 1;
        respawnSettings.FindProperty("maxEnemyCount").intValue = 1;
        respawnSettings.FindProperty("respawnSeconds").floatValue = .2f;
        respawnSettings.ApplyModifiedPropertiesWithoutUndo();
        respawnGroup.BeginSpawn();
        yield return .04f;
        EnemyController previousSpawn = FindRespawnEnemy();
        Check(previousSpawn != null, "Group spawns the configured alpha prefab");
        previousSpawn.GetComponent<EnemyHealth>().TakeDamage(1000);
        yield return new Until(() => previousSpawn == null,
            "Respawn disposes of the old defeated group");
        Check(FindRespawnEnemy() != null && FindRespawnEnemy().GetComponent<EnemyHealth>().CurrentHP == 50,
            "Replacement group starts with the correct alpha HP");

        Camera preview = CreateLineup();
        // Let the normal render loop initialize all material constant buffers.
        yield return .25f;
        CaptureLineup(preview);
    }

    private static IEnumerator RunAllChecks()
    {
        IEnumerator normal = RunChecks();
        while (normal.MoveNext()) yield return normal.Current;
        IEnumerator boss = BossEncounterValidation.RunChecks();
        while (boss.MoveNext()) yield return boss.Current;
        if (SessionState.GetBool(ComboRunKey, false))
        {
            IEnumerator combo = PlayerComboValidation.RunChecks();
            while (combo.MoveNext()) yield return combo.Current;
        }
    }

    private static EnemyController FindRespawnEnemy()
    {
        foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>())
            if (enemy.transform.position.x > 10f) return enemy;
        return null;
    }

    private static PlayerHealth CreatePlayer(Vector3 position)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        root.name = "ValidationPlayer";
        root.SetActive(false);
        root.tag = "Player";
        root.layer = LayerMask.NameToLayer("Player");
        root.transform.position = position;
        PlayerHealth health = root.AddComponent<PlayerHealth>();
        Set(health, "playerData", AssetDatabase.LoadAssetAtPath<PlayerData>(
            "Assets/ScriptableObjects/Players/Player.asset"));
        root.SetActive(true);
        return health;
    }

    private static EnemyController Spawn(char id)
    {
        return Object.Instantiate(Prefab(id), new Vector3(0, 1.01f, 0), Quaternion.identity)
            .GetComponent<EnemyController>();
    }

    private static GameObject Prefab(char id) => AssetDatabase.LoadAssetAtPath<GameObject>(
        $"Assets/Prefabs/Enemy/Enemy{id}.prefab");
    private static EnemyData Data(char id) => AssetDatabase.LoadAssetAtPath<EnemyData>(
        $"Assets/ScriptableObjects/Enemies/Enemy {id}.asset");

    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string field, Object[] values)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty array = serialized.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        assertions++;
        results.Add("PASS " + description);
    }

    private static Camera CreateLineup()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.55f, .6f, .7f);
        for (int i = 0; i < 5; i++)
        {
            GameObject enemy = Object.Instantiate(Prefab((char)('A' + i)),
                new Vector3((2 - i) * 3f, 1f, 30), Quaternion.Euler(0, 20, 0));
            enemy.GetComponent<EnemyController>().enabled = false;
            enemy.GetComponent<EnemyCombat>().enabled = false;
            enemy.GetComponent<Rigidbody>().isKinematic = true;
            foreach (Canvas canvas in enemy.GetComponentsInChildren<Canvas>())
                canvas.gameObject.SetActive(false);
        }
        var light = new GameObject("KeyLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(45, -25, 0);
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 5, 42);
        camera.transform.LookAt(new Vector3(0, .7f, 30));
        camera.orthographic = true;
        camera.orthographicSize = 3.4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.13f, .17f, .23f);
        var texture = new RenderTexture(1920, 720, 24);
        camera.targetTexture = texture;
        return camera;
    }

    private static void CaptureLineup(Camera camera)
    {
        RenderTexture texture = camera.targetTexture;
        RenderTexture.active = texture;
        var pixels = new Texture2D(1920, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1920, 720), 0, 0);
        pixels.Apply();
        Directory.CreateDirectory(".utmp");
        File.WriteAllBytes(".utmp/enemy-alpha-lineup.png", pixels.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        texture.Release();
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(pixels);
    }

    private static void Finish()
    {
        bool bossRun = SessionState.GetBool(BossRunKey, false);
        Cleanup();
        Directory.CreateDirectory(".utmp");
        bool comboRun = SessionState.GetBool(ComboRunKey, false);
        File.WriteAllLines(comboRun ? ".utmp/combo-validation.txt" : bossRun ? ".utmp/boss-validation.txt" : ".utmp/enemy-alpha-validation.txt", results);
        Debug.Log($"{(comboRun ? "COMBO" : bossRun ? "BOSS" : "ENEMY_ALPHA")}_VALIDATION_PASSED: {assertions} checks");
        if (Application.isBatchMode) EditorApplication.Exit(0);
        else EditorApplication.isPlaying = false;
    }

    private static void Fail(Exception error)
    {
        if (hasFailed) return;
        hasFailed = true;
        bool bossRun = SessionState.GetBool(BossRunKey, false);
        Cleanup();
        Directory.CreateDirectory(".utmp");
        results.Add("FAIL " + error);
        bool comboRun = SessionState.GetBool(ComboRunKey, false);
        File.WriteAllLines(comboRun ? ".utmp/combo-validation.txt" : bossRun ? ".utmp/boss-validation.txt" : ".utmp/enemy-alpha-validation.txt", results);
        Debug.LogError((comboRun ? "COMBO" : bossRun ? "BOSS" : "ENEMY_ALPHA") + "_VALIDATION_FAILED: " + error);
        // Do not shut Unity down reentrantly from an asset-loading/log callback.
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(1);
        else EditorApplication.isPlaying = false;
    }

    private static void Cleanup()
    {
        SessionState.SetBool(RunKey, false);
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= Tick;
    }
}
