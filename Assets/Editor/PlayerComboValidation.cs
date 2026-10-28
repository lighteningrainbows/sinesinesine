using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Collections.Generic;
using System.IO;
using Object = UnityEngine.Object;

public static class PlayerComboValidation
{
    private static readonly List<InputDevice> pausedDevices = new List<InputDevice>();
    private static Keyboard keyboard;
    private static Gamepad gamepad;
    private static InputSettings originalInputSettings, testInputSettings;
    private static bool originalRunInBackground;

    public static IEnumerator RunChecks()
    {
        // Use controlled inputs; a user's live mouse must not steer a batch check.
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        originalInputSettings = InputSystem.settings;
        testInputSettings = Object.Instantiate(originalInputSettings);
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = testInputSettings;
        foreach (InputDevice device in InputSystem.devices)
            if (device.enabled) pausedDevices.Add(device);
        foreach (InputDevice device in pausedDevices) InputSystem.DisableDevice(device);
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
        EditorApplication.playModeStateChanged += RestoreOnExit;
        // Start with the authored player so movement ownership is exercised too.
        EditorSceneManager.LoadSceneInPlayMode(BossEncounterSetup.ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return .1f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerCombat combo = player.GetComponent<PlayerCombat>();
        Rigidbody body = player.GetComponent<Rigidbody>();
        foreach (EnemySpawnGroup group in Object.FindObjectsByType<EnemySpawnGroup>())
            group.gameObject.SetActive(false);
        foreach (EnemyController sceneEnemy in Object.FindObjectsByType<EnemyController>())
            sceneEnemy.gameObject.SetActive(false);
        CompanionManager manager = CompanionManager.Instance;
        manager.enabled = false;
        for (int i = 0; i < manager.GetCompanionCount(); i++)
            manager.GetCompanion(i).gameObject.SetActive(false);
        Vector3 origin = new Vector3(70, 1.02f, 0);
        ResetPlayer(combo, body, origin);
        yield return .1f;
        Check(combo.RequestAttack() && combo.ComboStep == 1, "Actual scene player starts normal attack one");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        float startZ = body.position.z;
        yield return .1f;
        Check(body.position.z > startZ + .15f && body.position.z <= startZ + .51f,
            "Attack moves the player forward before its hit frame, despite movement input controller");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new EnemyAlphaValidation.Until(() => !combo.IsAttacking, "Single press completes one swing");
        Check(combo.ComboStep == 1 && Mathf.Abs(body.position.z - startZ - .5f) < .035f,
            "One press travels at most 0.5m and does not automatically chain");
        yield return .75f;
        Check(combo.ComboStep == 0 && combo.RequestAttack() && combo.ComboStep == 1,
            "Waiting past the chain window resets to normal attack one");
        Check(combo.RequestAttack() && !combo.RequestAttack() && combo.HasBufferedAttack,
            "Only one follow-up press is buffered for the current swing");
        yield return new EnemyAlphaValidation.Until(() => combo.ComboStep == 2, "Buffered press starts normal attack two");
        Check(combo.RequestAttack(), "Second swing accepts a finisher press");
        yield return new EnemyAlphaValidation.Until(() => combo.ComboStep == 3, "Third press starts the finisher");
        float finishZ = body.position.z;
        Check(!combo.RequestAttack(), "Finisher does not buffer an unintended fourth attack");
        yield return .14f;
        CaptureFeedback(player);
        yield return new EnemyAlphaValidation.Until(() => !combo.IsAttacking, "Finisher completes");
        Check(Mathf.Abs(body.position.z - finishZ) < .02f && combo.ComboStep == 0 && !combo.RequestAttack(),
            "Finisher stays in place, ends the combo and respects recovery");
        yield return .5f;
        Check(combo.RequestAttack() && combo.ComboStep == 1, "Recovery ends and the next combo begins at one");

        // A real enemy has an additional collider, and is launched by actual combo hits.
        ResetPlayer(combo, body, origin);
        EnemyController enemy = SpawnEnemy(origin + Vector3.forward * 2.05f);
        EnemyHealth hp = enemy.GetComponent<EnemyHealth>();
        var extra = new GameObject("ComboExtraCollider");
        extra.layer = LayerMask.NameToLayer("Enemy");
        extra.transform.SetParent(enemy.transform, false);
        extra.AddComponent<BoxCollider>();
        yield return .06f;
        Vector3 enemyStart = enemy.transform.position;
        int before = hp.CurrentHP;
        Check(combo.RequestAttack(), "Normal attack is accepted near an enemy");
        yield return .08f;
        Check(hp.CurrentHP == before, "No damage before the combo impact frame");
        yield return new EnemyAlphaValidation.Until(() => hp.CurrentHP < before, "Normal attack lands after stepping");
        Check(before - hp.CurrentHP == 4 && enemy.IsStaggered,
            "Normal hit deals one damage instance across multiple colliders and interrupts AI");
        Check(body.position.z < origin.z + .5f && body.position.z < enemyStart.z - 1.4f,
            "Close enemy stops the lunge at its body surface");
        yield return new EnemyAlphaValidation.Until(() => enemy.CurrentState == EnemyController.EnemyState.AirPause,
            "Normal combo knockback reaches its pause");
        Vector3 normalOffset = enemy.transform.position - enemyStart;
        Check(Mathf.Abs(normalOffset.z - .3f) < .04f && Mathf.Abs(normalOffset.y - .1f) < .035f,
            "Normal launch follows 2.52m/s at 33.7 degrees, travelling 0.3m and rising about 10cm");
        Check(!enemy.GetComponent<Rigidbody>().useGravity && !enemy.CanAct,
            "Enemy cannot act and gravity is disabled during normal pause");
        yield return .1f;
        Check(enemy.CurrentState == EnemyController.EnemyState.AirPause, "Normal pause lasts 0.2 seconds");
        before = hp.CurrentHP;
        Check(combo.RequestAttack() && combo.ComboStep == 2, "Second attack continues within the chain window");
        yield return new EnemyAlphaValidation.Until(() => hp.CurrentHP < before, "Second normal attack hits the staggered enemy");
        Check(before - hp.CurrentHP == 4, "Second normal attack has normal damage");
        yield return new EnemyAlphaValidation.Until(() => !combo.IsAttacking, "Second attack completes without automatic finisher");
        Vector3 finishStart = enemy.transform.position;
        before = hp.CurrentHP;
        Check(combo.RequestAttack() && combo.ComboStep == 3, "Third attack uses finisher parameters");
        yield return new EnemyAlphaValidation.Until(() => hp.CurrentHP < before, "Finisher lands");
        finishStart = enemy.transform.position;
        Check(before - hp.CurrentHP == 8, "Finisher doubles base damage before attribute calculation");
        yield return new EnemyAlphaValidation.Until(() => enemy.CurrentState == EnemyController.EnemyState.AirPause,
            "Finisher completes its launch");
        Check(Mathf.Abs(enemy.transform.position.z - finishStart.z - 2f) < .09f &&
            enemy.transform.position.y > finishStart.y + .2f && !enemy.CanAct,
            "Finisher launches the enemy two metres away at 7.07m/s and 20 degrees");
        Object.Destroy(enemy.gameObject);
        yield return .06f;

        // Airborne strikes suspend falling, then lift by 0.2m over 0.2s.
        ResetPlayer(combo, body, origin + Vector3.up * 4);
        body.linearVelocity = Vector3.down * 4;
        Check(combo.RequestAttack() && !body.useGravity, "Air attack immediately suspends gravity");
        float airY = body.position.y;
        yield return .25f;
        Check(Mathf.Abs(body.position.y - airY) < .025f, "Player does not fall during airborne swing");
        yield return new EnemyAlphaValidation.Until(() => !combo.IsAttacking, "Air swing and lift finish");
        Check(body.useGravity && Mathf.Abs(body.position.y - airY - .2f) < .045f,
            "Air attack lifts 0.2m over 0.2s and restores gravity");
        yield return .15f;
        Check(body.position.y < airY + .16f, "Player falls normally after air attack");
        ResetPlayer(combo, body, origin + Vector3.up * 4);
        combo.RequestAttack();
        combo.enabled = false;
        Check(body.useGravity && !combo.IsAttacking && combo.ComboStep == 0,
            "Disabling combat during an air strike restores physics and clears the combo");
        combo.enabled = true;

        // Walls block stepping and damage.
        ResetPlayer(combo, body, origin);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = origin + new Vector3(0, 1, .7f);
        wall.transform.localScale = new Vector3(4, 5, .2f);
        enemy = SpawnEnemy(origin + Vector3.forward * 2);
        hp = enemy.GetComponent<EnemyHealth>();
        before = hp.CurrentHP;
        Physics.SyncTransforms();
        combo.RequestAttack();
        yield return new EnemyAlphaValidation.Until(() => !combo.IsAttacking, "Attack against wall completes");
        Check(body.position.z < origin.z + .15f && hp.CurrentHP == before,
            "Wall stops both lunge and damage to an enemy behind it");
        Object.Destroy(wall);
        Object.Destroy(enemy.gameObject);
        yield return .06f;

        ResetPlayer(combo, body, origin);
        EnemyController centreTarget = SpawnEnemy(origin + new Vector3(.4f, 0, 1.5f));
        EnemyController nearPlayerTarget = SpawnEnemy(origin + new Vector3(-.7f, 0, .5f));
        foreach (EnemyController candidate in new[] { centreTarget, nearPlayerTarget })
        {
            candidate.enabled = false;
            candidate.GetComponent<Rigidbody>().isKinematic = true;
            candidate.GetComponent<BoxCollider>().size = Vector3.one * .3f;
        }
        Physics.SyncTransforms();
        combo.RequestAttack();
        Vector3 expectedDirection = new Vector3(.4f, 0, 1.5f).normalized;
        Check(Vector3.Dot(body.rotation * Vector3.forward, expectedDirection) > .999f,
            "Aim assist prioritizes the target closest to the follow area's centre");
        combo.CancelCombo();
        Object.Destroy(centreTarget.gameObject);
        Object.Destroy(nearPlayerTarget.gameObject);
        yield return .06f;

        // Use the real companion and switching methods, including an interrupted flight.
        ResetPlayer(combo, body, origin);
        CompanionController ally = manager.GetCompanion(0);
        ally.gameObject.SetActive(true);
        ally.enabled = true;
        ally.GetComponent<CompanionCombat>().enabled = false;
        ally.FreezeAsPreviousCompanion();
        Rigidbody allyBody = ally.GetComponent<Rigidbody>();
        allyBody.position = origin + Vector3.forward * 1.6f;
        Physics.SyncTransforms();
        int allyHP = ally.GetComponent<IDamageable>().CurrentHP;
        Vector3 allyStart = allyBody.position;
        Check(allyHP > 0 && ally.IsFrozenPrevious && combo.RequestAttack(),
            "Living fixed companion is ready and the player accepts the strike");
        yield return new EnemyAlphaValidation.Until(() => ally.GetComponent<FrozenCompanionKnockback>() != null &&
            ally.GetComponent<FrozenCompanionKnockback>().IsActive, "Player strike releases the frozen companion into knockback");
        Check(ally.GetComponent<IDamageable>().CurrentHP == allyHP && ally.IsFrozenPrevious,
            "Frozen companion takes no damage and remains the fixed previous companion");
        yield return new EnemyAlphaValidation.Until(() => !ally.GetComponent<FrozenCompanionKnockback>().IsActive,
            "Frozen companion lands and finishes knockback");
        Check(allyBody.isKinematic && !allyBody.useGravity && allyBody.position.z > allyStart.z + .24f,
            "Companion is fixed again at its landing position");
        Vector3 landed = allyBody.position;
        yield return .2f;
        Check(Vector3.Distance(landed, allyBody.position) < .01f, "Fixed companion stays at its new position");
        var reaction = ally.GetComponent<FrozenCompanionKnockback>();
        Vector3 finishAllyStart = allyBody.position;
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Finish);
        yield return new EnemyAlphaValidation.Until(() => !reaction.IsActive, "Fixed companion completes a finisher launch");
        Check(Mathf.Abs(Vector3.Distance(new Vector3(finishAllyStart.x, 0, finishAllyStart.z),
            new Vector3(allyBody.position.x, 0, allyBody.position.z)) - 2f) < .06f &&
            ally.GetComponent<IDamageable>().CurrentHP == allyHP && allyBody.isKinematic,
            "Finisher pushes a fixed companion two metres without damage and fixes it on landing");
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Finish);
        yield return .08f;
        ally.ReleaseFrozenState();
        Check(!reaction.IsActive && !allyBody.isKinematic && !ally.IsFrozenPrevious,
            "Switching back during knockback cancels the reaction and releases fixed physics");
        ally.Deploy();
        Check(ally.IsDeployed && !reaction.IsActive && !allyBody.isKinematic,
            "Previously launched companion can deploy and resume its normal AI");
        ResetPlayer(combo, body, origin);
        var allySettings = new SerializedObject(ally);
        allySettings.FindProperty("isFlying").boolValue = false;
        allySettings.ApplyModifiedPropertiesWithoutUndo();
        ally.Deploy();
        allyBody.position = origin + Vector3.forward * 1.6f;
        // Bird normally has gravity disabled; configure a grounded fixture for this case.
        allyBody.useGravity = true;
        allyBody.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        combo.RequestAttack();
        yield return new EnemyAlphaValidation.Until(() => reaction.IsActive,
            "Player strike knocks back a deployed companion with its AI enabled");
        Vector3 activeHitPosition = allyBody.position;
        Check(ally.IsKnockedBack && ally.IsDeployed && ally.GetComponent<IDamageable>().CurrentHP == allyHP,
            "Deployed companion is interrupted without HP damage or loss of deployment");
        yield return new EnemyAlphaValidation.Until(() => !reaction.IsActive, "Deployed companion finishes normal knockback");
        Check(!ally.IsKnockedBack && !allyBody.isKinematic && allyBody.useGravity &&
            allyBody.position.z > activeHitPosition.z + .24f && ally.GetComponent<IDamageable>().CurrentHP == allyHP,
            "Deployed companion receives normal displacement despite AI and restores ground physics without damage");
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Finish);
        yield return .08f;
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Finish);
        yield return new EnemyAlphaValidation.Until(() => !reaction.IsActive, "Repeated hits on a deployed companion complete");
        Check(allyBody.useGravity && !allyBody.isKinematic && ally.GetComponent<IDamageable>().CurrentHP == allyHP,
            "Repeated friendly hits preserve original physics and HP");
        yield return new EnemyAlphaValidation.Until(() => allyBody.linearVelocity.z < -.1f,
            "Deployed companion resumes following after the finisher lands");
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Normal);
        ally.FreezeAsPreviousCompanion();
        Check(!reaction.IsActive && ally.IsFrozenPrevious && allyBody.isKinematic && !allyBody.useGravity,
            "Switching a launched deployed companion to fixed state cancels the reaction cleanly");
        ally.ReleaseFrozenState();
        allySettings.Update();
        allySettings.FindProperty("isFlying").boolValue = true;
        allySettings.ApplyModifiedPropertiesWithoutUndo();
        ally.Deploy();
        reaction.ReceiveHit(player.transform.position, CombatKnockback.Normal);
        yield return .08f;
        reaction.enabled = false;
        Check(!reaction.IsActive && !allyBody.isKinematic && !allyBody.useGravity && ally.IsDeployed,
            "Interrupting a flying companion's reaction restores its hovering physics");
        reaction.enabled = true;
        ally.gameObject.SetActive(false);
        ResetPlayer(combo, body, origin);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
        yield return new EnemyAlphaValidation.Until(() => combo.IsAttacking, "J key starts the combo through the input system");
        yield return .5f;
        Check(!combo.IsAttacking && combo.ComboStep == 1 && !combo.HasBufferedAttack,
            "Holding J does not automatically chain another swing");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        ResetPlayer(combo, body, origin);
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North));
        yield return new EnemyAlphaValidation.Until(() => combo.IsAttacking, "Gamepad Y starts the combo through the input system");
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return new EnemyAlphaValidation.Until(() => !combo.IsAttacking, "Gamepad attack completes normally");
        RestoreInputs();
    }

    private static void ResetPlayer(PlayerCombat combo, Rigidbody body, Vector3 position)
    {
        combo.CancelCombo();
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.position = position;
        body.rotation = Quaternion.identity;
        body.transform.SetPositionAndRotation(position, Quaternion.identity);
        Physics.SyncTransforms();
    }

    private static void RestoreOnExit(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) RestoreInputs();
    }

    private static void CaptureFeedback(GameObject player)
    {
        Camera camera = player.GetComponentInChildren<Camera>();
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        Directory.CreateDirectory(".utmp");
        File.WriteAllBytes(".utmp/player-combo.png", pixels.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(pixels);
        Check(player.transform.Find("ComboSwipe").GetComponent<LineRenderer>().enabled,
            "Finisher displays its attack trail in the actual scene");
    }

    private static void RestoreInputs()
    {
        EditorApplication.playModeStateChanged -= RestoreOnExit;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        gamepad = null;
        foreach (InputDevice device in pausedDevices)
            if (device.added) InputSystem.EnableDevice(device);
        pausedDevices.Clear();
        if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
        originalInputSettings = null;
        if (testInputSettings != null) Object.Destroy(testInputSettings);
        testInputSettings = null;
        Application.runInBackground = originalRunInBackground;
    }

    private static EnemyController SpawnEnemy(Vector3 position)
    {
        var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemy/EnemyC.prefab"), position, Quaternion.identity);
        // Waiting AI remains enabled so stagger/fall/recovery are real.
        enemy.GetComponent<EnemyCombat>().enabled = false;
        return enemy.GetComponent<EnemyController>();
    }

    private static void Check(bool condition, string description) => EnemyAlphaValidation.Check(condition, description);
}
