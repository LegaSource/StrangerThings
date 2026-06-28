using GameNetcodeStuff;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Managers;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace StrangerThings.Behaviours.Scripts.Projectiles;

public class DoorProjectile : MonoBehaviour
{
    public Rigidbody Rigidbody { get; protected set; }
    public BoxCollider BoxCollider { get; private set; }

    protected bool isCarried;
    protected bool isThrown;
    protected bool isLastThrow;
    protected bool isLanding;
    protected bool hasLanded;
    protected bool hasHit;

    protected Transform carryTarget;
    protected Vector3 carryPositionOffset;

    private Vector3 originalColliderSize;
    private const float LowVelocityThreshold = 1f;
    private const float LowVelocityGracePeriod = 0.15f;
    private float lowVelocityTimer;

    protected Vector3 networkPosition;
    protected Quaternion networkRotation;
    private float syncTimer;

    public void Initialize()
    {
        isCarried = false;
        isThrown = false;
        isLastThrow = false;
        hasLanded = false;
        hasHit = false;
        lowVelocityTimer = 0f;

        Rigidbody ??= GetComponent<Rigidbody>();
        if (Rigidbody == null)
            Rigidbody = gameObject.AddComponent<Rigidbody>();
        Rigidbody.useGravity = false;
        Rigidbody.isKinematic = false;
        Rigidbody.velocity = Vector3.zero;
        Rigidbody.angularVelocity = Vector3.zero;
        Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        BoxCollider ??= GetComponentsInChildren<BoxCollider>().FirstOrDefault(c => !c.isTrigger);
        if (BoxCollider != null)
            originalColliderSize = BoxCollider.size;

        foreach (NavMeshObstacle obstacle in GetComponentsInChildren<NavMeshObstacle>())
            obstacle.enabled = false;
    }

    public IEnumerator GrabCoroutine(Transform target)
    {
        yield return ShakeCoroutine(1f, 0.1f);
        yield return MoveToTargetCoroutine(target, 0.2f);
    }

    public IEnumerator ShakeCoroutine(float duration, float intensity)
    {
        Vector3 startPosition = transform.position;
        float timePassed = 0f;

        while (timePassed < duration)
        {
            PlayImpactAudio();
            transform.position = startPosition + (Random.insideUnitSphere * intensity);

            yield return new WaitForSeconds(0.025f);
            timePassed += 0.025f;
        }

        transform.position = startPosition;
    }

    public IEnumerator MoveToTargetCoroutine(Transform target, float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float timePassed = 0f;

        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            float t = Mathf.Clamp01(timePassed / duration);
            transform.position = Vector3.Lerp(startPosition, target.position, t);
            transform.rotation = Quaternion.Slerp(startRotation, target.rotation, t);
            yield return null;
        }

        // Fermeture de la porte si elle était ouverte
        if (gameObject.TryGetComponentInChildren(out DoorLock doorLock) && doorLock.isDoorOpened && doorLock.gameObject.TryGetComponent(out AnimatedObjectTrigger objectTrigger))
        {
            if (LFCUtilities.IsServer)
                objectTrigger.TriggerAnimationNonPlayer();
            doorLock.isDoorOpened = false;
            doorLock.navMeshObstacle.enabled = false;
            doorLock.enabled = false;
        }

        isCarried = true;
        carryTarget = target;
        carryPositionOffset = target.InverseTransformPoint(transform.position);

        // Collider désactivé en bouclier pour éviter de bloquer les passages
        if (BoxCollider != null)
            BoxCollider.enabled = false;
        Rigidbody.isKinematic = true;
        Rigidbody.detectCollisions = false;
    }

    public void Throw(Vector3 direction, float force, bool isLastThrow)
    {
        isThrown = true;
        this.isLastThrow = isLastThrow;
        isLanding = false;
        isCarried = false;
        carryTarget = null;
        // Trigger de détection joueur pendant le throw
        if (BoxCollider != null)
            BoxCollider.enabled = false;

        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(originalColliderSize.x, originalColliderSize.y, originalColliderSize.z) * 0.4f;

        Rigidbody.isKinematic = false;
        Rigidbody.detectCollisions = true;
        Rigidbody.angularVelocity = Vector3.zero;
        Rigidbody.excludeLayers = LayerMask.GetMask("Railing", "RoomLight");
        Rigidbody.AddForce(direction.normalized * force, ForceMode.VelocityChange);
    }

    public void LateUpdate()
    {
        if (hasLanded)
            return;

        if (isCarried && carryTarget != null)
        {
            float swayAngle = Mathf.Sin(Time.time * 3f) * 4f;
            Quaternion sway = Quaternion.AngleAxis(swayAngle, carryTarget.forward);
            transform.position = carryTarget.TransformPoint(carryPositionOffset);
            transform.rotation = sway * Quaternion.Euler(0f, carryTarget.eulerAngles.y, 0f);
            return;
        }
        if (isThrown && !hasHit)
        {
            if (isLanding && isLastThrow)
            {
                if (Rigidbody.velocity.magnitude <= LowVelocityThreshold && Rigidbody.angularVelocity.magnitude <= LowVelocityThreshold)
                {
                    hasLanded = true;
                    StrangerThingsNetworkManager.Instance.SyncDoorPositionNotServerRpc(GetComponent<NetworkObject>(), transform.position, transform.rotation, hasLanded: true);
                    return;
                }
                if (LFCUtilities.IsServer)
                {
                    syncTimer += Time.deltaTime;
                    if (syncTimer >= 0.05f)
                    {
                        syncTimer = 0f;
                        StrangerThingsNetworkManager.Instance.SyncDoorPositionNotServerRpc(GetComponent<NetworkObject>(), transform.position, transform.rotation, hasLanded: false);
                    }
                }
                else
                {
                    transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 18f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * 18f);
                    return;
                }
            }
            // Détection vitesse faible - tombée au sol
            if (Rigidbody.velocity.magnitude <= LowVelocityThreshold)
            {
                lowVelocityTimer += Time.deltaTime;
                if (lowVelocityTimer >= LowVelocityGracePeriod)
                    Land();
                return;
            }
            lowVelocityTimer = 0f;
        }
    }

    public void OnTriggerEnter(Collider collider)
    {
        if (LFCUtilities.IsServer && isThrown && !isLanding && !hasHit && collider.gameObject.TryGetComponentInParent(out PlayerControllerB player))
        {
            hasHit = true;
            PlayImpactAudio();
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 80);
            Land();
        }
    }

    private void Land()
    {
        isLanding = true;

        // Réactiver le vrai collider et supprimer le trigger de vol
        if (BoxCollider != null)
        {
            BoxCollider.size = originalColliderSize;
            BoxCollider.enabled = true;
        }

        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger != null)
            Destroy(trigger);

        Rigidbody.velocity *= 0.2f;
        Rigidbody.angularVelocity = Vector3.zero;
        Rigidbody.useGravity = true;
        Rigidbody.AddTorque(transform.right * 3f, ForceMode.Impulse);
    }

    public void SyncPosition(Vector3 position, Quaternion rotation, bool hasLanded)
    {
        networkPosition = position;
        networkRotation = rotation;
        this.hasLanded = hasLanded;
    }

    public void PlayImpactAudio()
    {
        GameObject[] audios = [StrangerThings.DoorImpact1AudioObj, StrangerThings.DoorImpact2AudioObj, StrangerThings.DoorImpact3AudioObj];
        LFCNetworkManager.Instance.PlayAudioEveryoneRpc(
            audios[Random.Range(0, audios.Length)].name, transform.position);
    }
}