using GameNetcodeStuff;
using LegaFusionCore.Managers;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Scripts.Projectiles;

public class PebbleProjectile : NetworkBehaviour
{
    public Rigidbody Rigidbody;
    private bool isThrown = false;

    public IEnumerator GrabCoroutine()
    {
        yield return ShakeCoroutine(1f, 0.1f);
        yield return MoveToPositionCoroutine(transform.position + Vector3.up, 0.1f);
    }

    public IEnumerator ShakeCoroutine(float duration, float intensity)
    {
        Vector3 startPosition = transform.position;
        float timePassed = 0f;

        while (timePassed < duration)
        {
            SpawnRockExplosion(transform.position);
            transform.position = startPosition + (Random.insideUnitSphere * intensity);

            yield return new WaitForSeconds(0.025f);
            timePassed += 0.025f;
        }

        transform.position = startPosition;
    }

    private IEnumerator MoveToPositionCoroutine(Vector3 targetPosition, float duration)
    {
        float timePassed = 0f;
        while (timePassed < duration && transform != null)
        {
            timePassed += Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, targetPosition, Mathf.SmoothStep(0f, 1f, timePassed / duration));
            yield return null;
        }
        if (transform != null)
            transform.position = targetPosition;
    }

    public void Throw(Vector3 direction, float force)
    {
        isThrown = true;
        Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Rigidbody.angularVelocity = Vector3.zero;
        Rigidbody.AddForce(direction.normalized * force, ForceMode.VelocityChange);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!LFCUtilities.IsServer || collision == null || !isThrown || !NetworkObject.IsSpawned) return;
        if (collision.collider != null && (collision.collider.gameObject.TryGetComponentInParent(out PlayerControllerB _) || collision.collider.gameObject.TryGetComponentInParent(out EnemyAI _)))
            return;

        SpawnRockExplosionEveryoneRpc(transform.position);
        NetworkObject.Despawn(gameObject);
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (LFCUtilities.IsServer && isThrown && NetworkObject.IsSpawned && collider != null && collider.TryGetComponent(out PlayerControllerB player) && DimensionRegistry.AreInSameDimension(player.gameObject, gameObject))
        {
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 10, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
            SpawnRockExplosionEveryoneRpc(transform.position);
            NetworkObject.Despawn(gameObject);
        }
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SpawnRockExplosionEveryoneRpc(Vector3 position) => SpawnRockExplosion(position);

    public void SpawnRockExplosion(Vector3 position)
    {
        LFCGlobalManager.PlayParticle(tag: $"{LegaFusionCore.LegaFusionCore.modName}{LegaFusionCore.LegaFusionCore.brownExplosionParticle.name}",
            position: position,
            rotation: Quaternion.identity,
            scaleFactor: 0.5f,
            active: !DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer?.gameObject));

        LFCGlobalManager.PlayAudio(prefab: StrangerThings.StoneImpactAudioObj,
            position: position,
            active: !DimensionRegistry.IsInUpsideDown(LFCUtilities.LocalPlayer?.gameObject));
    }
}
