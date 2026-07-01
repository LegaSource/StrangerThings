using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using StrangerThings.Registries;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Scripts.Projectiles;

public class RockProjectile : NetworkBehaviour
{
    public Rigidbody Rigidbody;
    private EnemyAI throwingEnemy;

    private bool isThrown = false;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void GrabEveryoneRpc() => _ = StartCoroutine(MoveToPositionCoroutine(transform.position + (Vector3.up * 7f), 0.2f));

    private IEnumerator MoveToPositionCoroutine(Vector3 targetPosition, float duration)
    {
        SpawnRockExplosion(transform.position, withParticle: true, withAudio: true);

        float timePassed = 0f;
        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, targetPosition, Mathf.SmoothStep(0f, 1f, timePassed / duration));
            yield return null;
        }
        transform.position = targetPosition;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ShakeEveryoneRpc() => _ = StartCoroutine(ShakeCoroutine(duration: 1f, intensity: 0.1f));

    private IEnumerator ShakeCoroutine(float duration, float intensity)
    {
        Vector3 startPosition = transform.position;
        float timePassed = 0f;

        while (timePassed < duration)
        {
            SpawnRockExplosion(transform.position, withParticle: false, withAudio: true);
            transform.position = startPosition + (Random.insideUnitSphere * intensity);

            yield return new WaitForSeconds(0.025f);
            timePassed += 0.025f;
        }

        transform.position = startPosition;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ThrowFromPositionEveryoneRpc(NetworkObjectReference enemyObject, Vector3 startPosition, Vector3 direction)
    {
        if (!isThrown && enemyObject.TryGet(out NetworkObject networkObject))
        {
            isThrown = true;
            throwingEnemy = networkObject.gameObject.GetComponentInChildren<EnemyAI>();

            transform.position = startPosition;
            Rigidbody.position = startPosition;
            Rigidbody.isKinematic = false;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Rigidbody.velocity = Vector3.zero;

            float speed = 40f;
            Rigidbody.AddForce(ComputeArcVelocity(direction, speed, angleDeg: 15f), ForceMode.VelocityChange);
        }
    }

    private static Vector3 ComputeArcVelocity(Vector3 direction, float speed, float angleDeg)
    {
        // Séparation des composantes horizontales et verticales
        Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
        float horizontalDistance = horizontal.magnitude;
        if (horizontalDistance <= 0.0001f)
            return Vector3.up * speed;

        // Calcul de l'angle de lancement (en radians) pour créer un arc
        float angle = angleDeg * Mathf.Deg2Rad;
        float time = horizontalDistance / (speed * Mathf.Cos(angle));

        // Calcul des vitesses initiales
        float verticalVelocity = (direction.y / time) - (0.5f * Physics.gravity.y * time);
        Vector3 horizontalVelocity = horizontal.normalized * (speed * Mathf.Cos(angle));
        return horizontalVelocity + (Vector3.up * verticalVelocity);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!LFCUtilities.IsServer || collision == null || !isThrown || !NetworkObject.IsSpawned) return;
        if (collision.collider != null && (collision.collider.gameObject.TryGetComponentInParent(out PlayerControllerB _) || collision.collider.gameObject.TryGetComponentInParent(out EnemyAI _)))
            return;

        SpawnRockExplosionEveryoneRpc(transform.position, withParticle: true, withAudio: true);
        if (throwingEnemy is CrustapikanAI crustapikan)
        {
            _ = crustapikan.SpawnEnemyForServer(StrangerThings.CrustopikanLarvaeType, transform.position, 2f);
            NetworkObject.Despawn(gameObject);
        }
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (LFCUtilities.IsServer && isThrown && NetworkObject.IsSpawned && collider != null && collider.TryGetComponent(out PlayerControllerB player) && DimensionRegistry.AreInSameDimension(player.gameObject, gameObject))
        {
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 50, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
            SpawnRockExplosionEveryoneRpc(transform.position, withParticle: true, withAudio: true);
            NetworkObject.Despawn(gameObject);
        }
    }

    public void SpawnRockExplosion(Vector3 position, bool withParticle, bool withAudio) => SpawnRockExplosionEveryoneRpc(position, withParticle, withAudio);

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SpawnRockExplosionEveryoneRpc(Vector3 position, bool withParticle, bool withAudio)
    {
        if (withParticle)
        {
            LFCGlobalManager.PlayParticle(tag: $"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.brownExplosionParticle.name}",
                position: position,
                rotation: Quaternion.identity,
                scaleFactor: 1.5f,
                active: DimensionRegistry.AreInSameDimension(gameObject, LFCUtilities.LocalPlayer?.gameObject));
        }
        if (withAudio)
        {
            LFCGlobalManager.PlayAudio(prefab: StrangerThings.StoneImpactAudioObj,
                position: position,
                active: DimensionRegistry.AreInSameDimension(gameObject, LFCUtilities.LocalPlayer?.gameObject));
        }
    }
}
