using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Registries;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Items.Figurines;
using StrangerThings.Behaviours.Scripts.Projectiles;
using StrangerThings.Managers;
using StrangerThings.Registries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Enemies;

public class HenryAI : EnemyAI
{
    public Transform TurnCompass;
    public Transform DoorGrabPoint;
    public HenrySoundSerializableEntry[] HenrySoundsEntry;
    public HenryVoiceSerializableEntry[] HenryVoicesEntry;
    public Dictionary<Sound, AudioClip[]> HenrySounds;
    public Dictionary<Voice, AudioClip[]> HenryVoices;

    private float moveTimer = 0f;
    private float speakTimer = 0f;
    private float doorGrabTimer = 0f;
    private float treeGrabTimer = 0f;
    private float rockGrabTimer = 0f;
    private float pebbleGrabTimer = 0f;
    private float doorThrowTimer = 0f;

    public float speakCooldown = 3f;
    public float doorGrabCooldown = 20f;
    public float treeGrabCooldown = 20f;
    public float rockGrabCooldown = 10f;
    public float pebbleGrabCooldown = 10f;
    public float doorThrowCooldown = 30f;

    protected bool hasDoor = false;
    protected bool canSpeak = false;
    protected bool canDoorGrab = false;
    protected bool canTreeGrab = false;
    protected bool canRockGrab = false;
    protected bool canPebbleGrab = false;
    protected bool canDoorThrow = false;

    public Coroutine openShipDoorCoroutine;
    public Coroutine grabCoroutine;
    public Coroutine throwCoroutine;
    public Coroutine swingCoroutine;
    public Coroutine stopShipCoroutine;
    public Coroutine killCoroutine;

    protected DoorProjectile doorProjectile;
    protected PebbleProjectile[] pebbleProjectiles = new PebbleProjectile[6];

    protected HangarShipDoor shipDoor;
    protected ElevenPop elevenPop;

    private readonly int interactableObjectsMask = 1073742656;
    private readonly Collider[] overlapBuffer = new Collider[64];

    public enum State { WANDERING, CHASING }
    public enum Sound { MOVE, GRAB, THROW, SWING }
    public enum Voice { DETECT, ANGRY, FOCUS }

    [Serializable] public class HenrySoundSerializableEntry : LFCUtilities.SerializableEntry<Sound, AudioClip[]> { }
    [Serializable] public class HenryVoiceSerializableEntry : LFCUtilities.SerializableEntry<Voice, AudioClip[]> { }

    public override void Start()
    {
        base.Start();

        HenrySounds = HenrySoundsEntry.ToDictionary();
        HenryVoices = HenryVoicesEntry.ToDictionary();

        shipDoor = FindObjectOfType<HangarShipDoor>();
        currentBehaviourStateIndex = (int)State.WANDERING;
        StartSearch(transform.position);

        if (LFCUtilities.IsServer)
        {
            elevenPop = LFCObjectsManager.SpawnNewObject(StrangerThings.ElevenPopItem, minValue: 200, maxValue: 300) as ElevenPop;
            MapObjectsManager.SpawnPortalsForServer();
        }
    }

    public override void Update()
    {
        base.Update();
        if (isEnemyDead || stopShipCoroutine != null || killCoroutine != null) return;

        PlayMoveSound();
        int state = currentBehaviourStateIndex;
        if (targetPlayer != null && state == (int)State.CHASING)
        {
            TurnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 4f * Time.deltaTime);
        }
        LFCUtilities.UpdateTimer(ref speakTimer, speakCooldown, !canSpeak, () => canSpeak = true);
        LFCUtilities.UpdateTimer(ref doorGrabTimer, doorGrabCooldown, !canDoorGrab && !hasDoor, () => canDoorGrab = true);
        LFCUtilities.UpdateTimer(ref rockGrabTimer, rockGrabCooldown, !canRockGrab && !hasDoor, () => canRockGrab = true);
        LFCUtilities.UpdateTimer(ref pebbleGrabTimer, pebbleGrabCooldown, !canPebbleGrab && !hasDoor, () => canPebbleGrab = true);
        LFCUtilities.UpdateTimer(ref treeGrabTimer, treeGrabCooldown, !canTreeGrab, () => canTreeGrab = true);
        LFCUtilities.UpdateTimer(ref doorThrowTimer, doorThrowTimer, !canDoorThrow && hasDoor, () => canDoorThrow = true);
    }

    public void PlayMoveSound()
    {
        AnimatorClipInfo[] currentAnimatorClipInfo = creatureAnimator.GetCurrentAnimatorClipInfo(0);
        if (currentAnimatorClipInfo.Length != 0 && currentAnimatorClipInfo[0].clip.name.Contains("move"))
        {
            moveTimer -= Time.deltaTime;
            if (HenrySounds.TryGetValue((int)Sound.MOVE, out AudioClip[] moveSounds) && moveSounds.Length > 0 && moveTimer <= 0)
            {
                creatureSFX.PlayOneShot(moveSounds[UnityEngine.Random.Range(0, moveSounds.Length)]);
                moveTimer = 1.25f;
            }
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (isEnemyDead || stopShipCoroutine != null || killCoroutine != null || StartOfRound.Instance.allPlayersDead)
            return;

        switch ((State)currentBehaviourStateIndex)
        {
            case State.WANDERING: DoWandering(); break;
            case State.CHASING: DoChasing(); break;
        }
    }

    public void DoWandering()
    {
        agent.speed = 4f;
        if (this.FoundClosestPlayerInRange(35, 10))
        {
            StopSearch(currentSearch);
            PlayVoiceServerRpc((int)Voice.DETECT);
            SwitchToBehaviourClientRpc((int)State.CHASING);
            return;
        }
    }

    public void DoChasing()
    {
        if (swingCoroutine != null || openShipDoorCoroutine != null || grabCoroutine != null || throwCoroutine != null)
            return;

        agent.speed = 6f;
        if (this.TargetOutsideChasedPlayer()) return;
        if (StartOfRound.Instance.hangarDoorsClosed && StartOfRound.Instance.shipStrictInnerRoomBounds.bounds.Contains(destination))
        {
            OpenShipDoorEveryoneRpc();
            return;
        }

        bool hasTarget = this.TargetClosestPlayerInAnyCase(out float distanceWithPlayer);
        bool hasLOS = hasTarget && CheckLineOfSightForPosition(targetPlayer.transform.position);
        if (!hasTarget || (distanceWithPlayer > 50f && !hasLOS))
        {
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.WANDERING);
            return;
        }

        if (hasLOS)
        {
            if (canDoorGrab && !hasDoor)
            {
                DoorLock targetDoor = FindDoorForGrab();
                if (targetDoor != null)
                {
                    canDoorGrab = false;
                    grabCoroutine ??= StartCoroutine(GrabDoorCoroutine(targetDoor.transform.root.gameObject));
                    return;
                }
            }
            if (canDoorThrow && hasDoor && distanceWithPlayer < 10f)
            {
                canDoorThrow = false;
                throwCoroutine ??= StartCoroutine(ThrowDoorCoroutine());

                IEnumerator ThrowDoorCoroutine()
                {
                    for (int i = 0; i < 2; i++)
                    {
                        yield return ThrowCoroutine(onThrow: () => { ThrowDoorEveryoneRpc(isLastThrow: i == 1); });
                        if (i != 1)
                            yield return new WaitForSeconds(0.3f);
                    }
                }

                return;
            }
        }
        if (canTreeGrab)
        {
            GameObject targetTree = FindTreeForGrab();
            if (targetTree != null)
            {
                canTreeGrab = false;
                grabCoroutine ??= StartCoroutine(GrabTreeCoroutine(targetTree));
                return;
            }
        }
        if (canPebbleGrab && !isOutside)
        {
            GameObject[] pebbleObjects = SpawnPebblesForGrab();
            if (pebbleObjects != null && pebbleObjects.Any(p => p != null))
            {
                canPebbleGrab = false;
                NetworkObjectReference[] pebbleObjs = [.. pebbleObjects.Select(p => p != null ? (NetworkObjectReference)p.GetComponent<NetworkObject>() : default)];
                grabCoroutine ??= StartCoroutine(GrabPebblesCoroutine(pebbleObjs));
                return;
            }
        }
        if (canRockGrab && isOutside && !isInsidePlayerShip)
        {
            GameObject[] rockObjects = SpawnRocksForGrab();
            if (rockObjects != null && rockObjects.Any(r => r != null))
            {
                canRockGrab = false;
                RockProjectile[] rockProjectiles = [.. rockObjects.Where(r => r != null).Select(r => r.GetComponent<RockProjectile>())];
                grabCoroutine ??= StartCoroutine(GrabRocksCoroutine(rockProjectiles));
                return;
            }
        }

        SetMovingTowardsTargetPlayer(targetPlayer);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void OpenShipDoorEveryoneRpc() => openShipDoorCoroutine ??= StartCoroutine(OpenShipDoorCoroutine());

    public IEnumerator OpenShipDoorCoroutine()
    {
        agent.speed = 0f;
        creatureAnimator.SetTrigger("startGrab");
        if (HenrySounds.TryGetValue(Sound.GRAB, out AudioClip[] grabSounds) && grabSounds.Length > 0)
            creatureSFX.PlayOneShot(grabSounds[UnityEngine.Random.Range(0, grabSounds.Length)]);

        shipDoor.shipDoorsAnimator.SetBool("PryingOpenDoor", value: true);
        shipDoor.shipDoorsAnimator.SetFloat("pryOpenDoor", 0f);
        if (LFCUtilities.LocalPlayer.isInElevator || LFCUtilities.LocalPlayer.isInHangarShipRoom)
            HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
        StartOfRound.Instance.shipDoorAudioSource.PlayOneShot(StartOfRound.Instance.alarmSFX);
        yield return this.WaitForFullAnimation("grab", onProgress: progress => shipDoor.shipDoorsAnimator.SetFloat("pryOpenDoor", progress));

        shipDoor.shipDoorsAnimator.SetBool("Closed", value: false);
        StartOfRound.Instance.SetShipDoorsClosed(closed: false);
        StartOfRound.Instance.SetShipDoorsOverheatLocalClient();
        shipDoor.doorPower = 0f;
        shipDoor.shipDoorsAnimator.SetBool("PryingOpenDoor", value: false);

        DoAnimationEveryoneRpc("startMove");
        PlayVoiceServerRpc((int)Voice.ANGRY);
        openShipDoorCoroutine = null;
    }

    public DoorLock FindDoorForGrab()
    {
        DoorLock bestDoor = null;
        float bestDistance = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(transform.position, 20f, overlapBuffer, interactableObjectsMask);
        for (int i = 0; i < count; i++)
        {
            if (overlapBuffer[i].gameObject.TryGetComponentInParent(out DoorLock door) && CheckLineOfSightForPosition(door.transform.position, 120f, 20, 5f))
            {
                float distance = Vector3.Distance(transform.position, door.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDoor = door;
                }
            }
        }

        return bestDoor;
    }

    public GameObject FindTreeForGrab()
    {
        GameObject bestTree = null;
        float bestDistance = float.MaxValue;

        foreach (GameObject treeObject in LFCTreesRegistry.GetTrees())
        {
            if (treeObject.activeSelf)
            {
                float distance = Vector3.Distance(targetPlayer.transform.position, treeObject.transform.position);
                if (distance < 15f && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTree = treeObject;
                }
            }
        }

        return bestTree;
    }

    public GameObject[] SpawnPebblesForGrab()
    {
        GameObject[] pebbleObjects = new GameObject[3];

        float baseAngle = UnityEngine.Random.Range(0f, 120f);
        for (int i = 0; i < 3; i++)
        {
            float angle = baseAngle + (i * 120f) + UnityEngine.Random.Range(-20f, 20f);
            float radius = UnityEngine.Random.Range(6f, 12f);
            if (TryGetPositionAroundPlayer(angle, radius, out Vector3 position))
            {
                pebbleObjects[i] = Instantiate(StrangerThings.PebbleProjectileObj, position, Quaternion.identity);
                pebbleObjects[i].GetComponent<NetworkObject>().Spawn();
            }
        }

        return pebbleObjects;
    }

    public GameObject[] SpawnRocksForGrab()
    {
        GameObject[] rockObjects = new GameObject[2];

        float baseAngle = UnityEngine.Random.Range(0f, 120f);
        for (int i = 0; i < 2; i++)
        {
            float angle = baseAngle + (i * 180f) + UnityEngine.Random.Range(-20f, 20f);
            float radius = UnityEngine.Random.Range(10f, 12f);
            if (TryGetPositionAroundPlayer(angle, radius, out Vector3 position))
            {
                rockObjects[i] = Instantiate(StrangerThings.RockProjectileObj, position, Quaternion.identity);
                rockObjects[i].GetComponent<NetworkObject>().Spawn();
            }
        }

        return rockObjects;
    }

    public bool TryGetPositionAroundPlayer(float angle, float radius, out Vector3 position)
    {
        position = targetPlayer.transform.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 0f, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
        Vector3 direction = (position + Vector3.up - targetPlayer.playerEye.position).normalized;

        // Si un obstacle détecté, reculer juste avant le point d'impact
        if (Physics.Raycast(targetPlayer.playerEye.position, direction, out RaycastHit oHit, radius, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
            position = oHit.point - (direction * 0.4f);
        // Sol
        return Physics.Raycast(position + (Vector3.up * 2f), Vector3.down, out RaycastHit fHit, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault) && (position = fHit.point) != null;
    }

    public IEnumerator GrabDoorCoroutine(GameObject doorObject)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startGrab");
        PlaySFXEveryoneRpc((int)Sound.GRAB);
        GrabDoorEveryoneRpc(doorObject.GetComponent<NetworkObject>());
        yield return this.WaitForFullAnimation("grab");

        hasDoor = true;
        DoAnimationEveryoneRpc("startMove");
        PlayVoiceServerRpc((int)Voice.FOCUS);
        grabCoroutine = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void GrabDoorEveryoneRpc(NetworkObjectReference doorObj)
    {
        if (doorObj.TryGet(out NetworkObject networkObject))
        {
            GameObject doorObject = networkObject.gameObject;
            doorProjectile = doorObject.GetComponent<DoorProjectile>() ?? doorObject.AddComponent<DoorProjectile>();
            doorProjectile.Initialize();
            _ = StartCoroutine(doorProjectile.GrabCoroutine(DoorGrabPoint));
        }
    }

    public IEnumerator GrabTreeCoroutine(GameObject targetTree)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startGrab");
        PlaySFXEveryoneRpc((int)Sound.GRAB);
        yield return this.WaitForFullAnimation("grab");

        Collider treeCollider = targetTree.GetComponentsInChildren<Collider>().FirstOrDefault(c => c.isTrigger);
        if (treeCollider != null)
        {
            Vector3 treePosition = targetTree.transform.position + (Vector3.up * 2f);
            _ = treeCollider != null ? treeCollider.bounds.size.y : 8f;
            Vector3 direction = targetPlayer.transform.position - treePosition;
            direction.y = 0f;
            direction = direction.normalized;

            PlayVoiceServerRpc((int)Voice.FOCUS);
            GameObject logObject = Instantiate(StrangerThings.LogProjectileObj, treePosition, Quaternion.identity);
            logObject.GetComponent<NetworkObject>().Spawn();
            logObject.GetComponent<LogProjectile>().FallEveryoneRpc(treeCollider.bounds.size, direction);
            RoundManager.Instance.DestroyTreeOnLocalClient(treePosition);
        }

        DoAnimationEveryoneRpc("startMove");
        grabCoroutine = null;
    }

    public IEnumerator GrabPebblesCoroutine(NetworkObjectReference[] pebblesObjs)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startGrab");
        PlaySFXEveryoneRpc((int)Sound.GRAB);
        GrabPebblesEveryoneRpc(pebblesObjs);
        yield return this.WaitForFullAnimation("grab");

        PlayVoiceServerRpc((int)Voice.ANGRY);
        yield return ThrowCoroutine(ThrowPebblesCoroutine);
        grabCoroutine = null;

        IEnumerator ThrowPebblesCoroutine()
        {
            for (int i = 0; i < 3; i++)
            {
                if (pebbleProjectiles[i] != null)
                {
                    Vector3 direction = (targetPlayer.transform.position + (Vector3.up * 1.2f) - pebbleProjectiles[i].transform.position).normalized;
                    ThrowPebbleAtIndexEveryoneRpc(i, direction);
                }
                if (i < 2)
                    yield return new WaitForSeconds(0.25f);
            }
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void GrabPebblesEveryoneRpc(NetworkObjectReference[] pebbleObjs)
    {
        for (int i = 0; i < pebbleObjs.Length; i++)
        {
            if (pebbleObjs[i].TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out PebbleProjectile pebbleProjectile))
            {
                pebbleProjectiles[i] = pebbleProjectile;
                _ = StartCoroutine(pebbleProjectile.GrabCoroutine());
            }
        }
    }

    public IEnumerator GrabRocksCoroutine(RockProjectile[] rockProjectiles)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startGrab");
        PlaySFXEveryoneRpc((int)Sound.GRAB);
        foreach (RockProjectile rockProjectile in rockProjectiles)
            rockProjectile.GrabEveryoneRpc();
        yield return new WaitForSeconds(0.25f);

        PlayVoiceServerRpc((int)Voice.FOCUS);
        foreach (RockProjectile rockProjectile in rockProjectiles)
            rockProjectile.ShakeEveryoneRpc();
        yield return this.WaitForFullAnimation("grab");

        // Spawn 6 pierres au niveau des rochers
        List<NetworkObjectReference> pebbleObjs = [];
        foreach (RockProjectile rockProjectile in rockProjectiles)
        {
            GameObject[] pebbleObjects = SpawnRockPebbles(rockProjectile.transform.position);
            pebbleObjs.AddRange([.. pebbleObjects.Select(p => p != null ? (NetworkObjectReference)p.GetComponent<NetworkObject>() : default)]);
        }
        NetworkObjectReference[] rockObjs = [.. rockProjectiles.Select(r => r != null ? (NetworkObjectReference)r.GetComponent<NetworkObject>() : default)];
        ExplodeRocksEveryoneRpc(rockObjs, [.. pebbleObjs]);
        yield return ThrowCoroutine(ThrowPebblesCoroutine);
        grabCoroutine = null;

        IEnumerator ThrowPebblesCoroutine()
        {
            for (int i = 0; i < 6; i++)
            {
                if (pebbleProjectiles[i] != null)
                {
                    Vector3 direction = (targetPlayer.transform.position + (Vector3.up * 1.2f) - pebbleProjectiles[i].transform.position).normalized;
                    ThrowPebbleAtIndexEveryoneRpc(i, direction);
                }
                if (i < 5)
                    yield return new WaitForSeconds(0.25f);
            }
        }
    }

    public GameObject[] SpawnRockPebbles(Vector3 position)
    {
        GameObject[] pebbles = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120f) + UnityEngine.Random.Range(-15f, 15f);
            float radius = UnityEngine.Random.Range(0.4f, 1f);
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, UnityEngine.Random.Range(1.5f, 2.5f), Mathf.Sin(angle * Mathf.Deg2Rad) * radius);

            pebbles[i] = Instantiate(StrangerThings.PebbleProjectileObj, position + offset, Quaternion.identity);
            pebbles[i].GetComponent<NetworkObject>().Spawn();
        }
        return pebbles;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ExplodeRocksEveryoneRpc(NetworkObjectReference[] rockObjs, NetworkObjectReference[] pebbleObjs)
    {
        foreach (NetworkObjectReference rockObject in rockObjs)
        {
            if (rockObject.TryGet(out NetworkObject networkObject))
            {
                RockProjectile rockProjectile = networkObject.GetComponent<RockProjectile>();

                LFCGlobalManager.PlayParticle(tag: $"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.brownExplosionParticle.name}",
                position: rockProjectile.transform.position,
                rotation: Quaternion.identity,
                scaleFactor: 1.5f,
                active: DimensionRegistry.AreInSameDimension(gameObject, LFCUtilities.LocalPlayer?.gameObject));
                LFCGlobalManager.PlayAudio(prefab: StrangerThings.StoneImpactAudioObj,
                    position: rockProjectile.transform.position,
                    active: DimensionRegistry.AreInSameDimension(gameObject, LFCUtilities.LocalPlayer?.gameObject));

                rockProjectile.GetComponent<NetworkObject>().Despawn();
            }
        }
        for (int i = 0; i < pebbleObjs.Length; i++)
        {
            if (pebbleObjs[i].TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out PebbleProjectile pebbleProjectile))
                pebbleProjectiles[i] = pebbleProjectile;
        }
    }

    public IEnumerator ThrowCoroutine(Action onThrow)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startThrow");
        PlaySFXEveryoneRpc((int)Sound.THROW);
        yield return new WaitForSeconds(0.8f);

        onThrow();
        yield return this.WaitForFullAnimation("throw");

        DoAnimationEveryoneRpc("startMove");
        throwCoroutine = null;
    }

    public IEnumerator ThrowCoroutine(Func<IEnumerator> onThrow)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startThrow");
        PlaySFXEveryoneRpc((int)Sound.THROW);
        yield return new WaitForSeconds(0.8f);

        yield return onThrow();
        yield return this.WaitForFullAnimation("throw");

        DoAnimationEveryoneRpc("startMove");
        throwCoroutine = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ThrowDoorEveryoneRpc(bool isLastThrow)
    {
        hasDoor = false;
        if (doorProjectile != null)
        {
            doorProjectile.Throw((targetPlayer.transform.position + (Vector3.up * 1.2f) - doorProjectile.transform.position).normalized, 25f, isLastThrow);
            if (isLastThrow)
                doorProjectile = null;
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ThrowPebbleAtIndexEveryoneRpc(int index, Vector3 direction)
    {
        if (index < pebbleProjectiles.Length && pebbleProjectiles[index] != null)
        {
            pebbleProjectiles[index].Throw(direction, 40f);
            pebbleProjectiles[index] = null;
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        if (isEnemyDead || killCoroutine != null || currentBehaviourStateIndex != (int)State.CHASING || swingCoroutine != null) return;
        PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
        if (!LFCUtilities.ShouldBeLocalPlayer(player)) return;

        SwingServerRpc((int)player.playerClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void SwingServerRpc(int playerId) => swingCoroutine ??= StartCoroutine(SwingCoroutine(playerId));

    public IEnumerator SwingCoroutine(int playerId)
    {
        agent.speed = 0f;
        DoAnimationEveryoneRpc("startSwing");
        PlaySFXEveryoneRpc((int)Sound.SWING);
        yield return new WaitForSeconds(0.5f);

        LFCNetworkManager.Instance.DamagePlayerEveryoneRpc(playerId, 20, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
        PlayVoiceServerRpc((int)Voice.ANGRY);
        yield return this.WaitForFullAnimation("recover");

        DoAnimationEveryoneRpc("startMove");
        if (StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>().isPlayerDead)
        {
            StartSearch(transform.position);
            SwitchToBehaviourClientRpc((int)State.WANDERING);
        }

        swingCoroutine = null;
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        if (!isEnemyDead || enemyHP <= 1 || (killCoroutine != null && !hasDoor))
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

            enemyHP = System.Math.Max(enemyHP - force, 1);
            if (LFCUtilities.IsServer && enemyHP <= 1)
            {
                if (ConfigManager.globalTips.Value)
                    HUDManager.Instance.DisplayTip("Information", "Find a way to defeat Henry in the dimension.");
                SetupElevenPopAuraEveryoneRpc(elevenPop.GetComponent<NetworkObject>());
            }
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SetupElevenPopAuraEveryoneRpc(NetworkObjectReference obj)
    {
        if (obj.TryGet(out NetworkObject networkObject))
            LFCCustomPassManager.SetupAuraForObject(networkObject.gameObject, LegaFusionCore.LegaFusionCore.wallhackShader, $"{StrangerThings.modName}ElevenPop", Color.yellow);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void StopShipServerRpc() => stopShipCoroutine = StartCoroutine(StopShipCoroutine());

    public IEnumerator StopShipCoroutine()
    {
        yield return new WaitUntil(() => grabCoroutine == null && throwCoroutine == null && swingCoroutine == null);

        agent.speed = 0f;
        DoAnimationEveryoneRpc("startHold");
        PlaySFXEveryoneRpc((int)Sound.GRAB);
        StopShipEveryoneRpc();
        yield return this.WaitForFullAnimation("hold");

        DoAnimationEveryoneRpc("startMove");
        PlayVoiceServerRpc((int)Voice.FOCUS);
        stopShipCoroutine = null;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void StopShipEveryoneRpc()
    {
        if (LFCUtilities.LocalPlayer.isInElevator || LFCUtilities.LocalPlayer.isInHangarShipRoom)
            HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
        StartOfRound.Instance.shipDoorAudioSource.PlayOneShot(StartOfRound.Instance.alarmSFX);
    }

    // Détruire après animation
    public override void KillEnemy(bool destroy = false) => killCoroutine = StartCoroutine(KillCoroutine(destroy));

    public IEnumerator KillCoroutine(bool destroy)
    {
        agent.speed = 0f;
        if (LFCUtilities.IsServer)
            _ = MapObjectsManager.SpawnPortalForServer(transform.position, isOutside);
        creatureAnimator.SetTrigger("KillEnemy");
        creatureVoice.PlayOneShot(dieSFX);
        yield return this.WaitForFullAnimation("kill");

        base.KillEnemy(destroy);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlaySFXEveryoneRpc(int enemySound)
    {
        if (HenrySounds.TryGetValue((Sound)enemySound, out AudioClip[] enemySounds) && enemySounds.Length > 0)
            creatureSFX.PlayOneShot(enemySounds[UnityEngine.Random.Range(0, enemySounds.Length)]);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void PlayVoiceServerRpc(int enemyVoice)
    {
        if (canSpeak)
        {
            canSpeak = false;
            PlayVoiceEveryoneRpc(enemyVoice);
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayVoiceEveryoneRpc(int enemyVoice)
    {
        if (HenryVoices.TryGetValue((Voice)enemyVoice, out AudioClip[] enemyVoices) && enemyVoices.Length > 0)
            creatureVoice.PlayOneShot(enemyVoices[UnityEngine.Random.Range(0, enemyVoices.Length)]);
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void DoAnimationEveryoneRpc(string animationState) => creatureAnimator.SetTrigger(animationState);
}
