using GameNetcodeStuff;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Behaviours.Enemies;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Scripts;

public class RockProjectile : NetworkBehaviour
{
    public Rigidbody rigidbody;
    public CrustapikanAI throwingEnemy;

    public bool isThrown = false;
    public bool deactivated = false;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void ThrowRockEveryoneRpc(NetworkObjectReference enemyObject, Vector3 targetPosition)
    {
        if (isThrown || !enemyObject.TryGet(out NetworkObject networkObject)) return;

        isThrown = true;
        throwingEnemy = networkObject.gameObject.GetComponentInChildren<CrustapikanAI>();
        transform.SetParent(null);

        float speed = 40f;
        Vector3 toTarget = targetPosition - transform.position;

        // Séparation des composantes horizontales et verticales
        Vector3 horizontal = new Vector3(toTarget.x, 0, toTarget.z);
        float horizontalDistance = horizontal.magnitude;

        // Calcul de l'angle de lancement (en radians) pour créer un arc
        float angle = 15f * Mathf.Deg2Rad;
        float timeToReachTarget = horizontalDistance / (speed * Mathf.Cos(angle));

        // Calcul des vitesses initiales
        float verticalVelocity = (toTarget.y / timeToReachTarget) - (0.5f * Physics.gravity.y * timeToReachTarget);
        Vector3 horizontalVelocity = horizontal.normalized * (speed * Mathf.Cos(angle));

        // Préparation rigidbody pour le lancer
        rigidbody.isKinematic = false;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.velocity = Vector3.zero;
        rigidbody.AddForce(horizontalVelocity + (Vector3.up * verticalVelocity), ForceMode.VelocityChange);

        if (LFCUtilities.IsServer)
            _ = StartCoroutine(DetectGroundAndWalls());
    }

    public IEnumerator DetectGroundAndWalls()
    {
        while (!deactivated)
        {
            if (Physics.SphereCast(transform.position, 0.4f, Vector3.down, out _, 1.5f, 605030721, QueryTriggerInteraction.Collide))
            {
                SpawnRockExplosionEveryoneRpc();
                _ = throwingEnemy?.SpawnEnemyForServer(StrangerThings.crustopikanLarvaeType, transform.position, 2f);
                _ = StartCoroutine(DestroyCoroutine());
                yield break;
            }
            yield return null;
        }
    }

    public IEnumerator DestroyCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (deactivated) yield break;

        deactivated = true;
        if (LFCUtilities.IsServer) Destroy(gameObject);
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!isThrown || other == null || !LFCUtilities.IsServer) return;
        if (HandlePlayerHit(other))
        {
            deactivated = true;
            SpawnRockExplosionEveryoneRpc();
            Destroy(gameObject);
        }
    }

    public bool HandlePlayerHit(Collider other)
    {
        PlayerControllerB player = other.GetComponent<PlayerControllerB>();
        if (player != null)
        {
            LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, 80, hasDamageSFX: true, callRPC: true, (int)CauseOfDeath.Crushing);
            return true;
        }
        return false;
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void SpawnRockExplosionEveryoneRpc()
    {
        GameObject particleObj = Instantiate(StrangerThings.rockExplosionParticle, transform.position, Quaternion.identity);
        ParticleSystem particleSystem = particleObj.GetComponent<ParticleSystem>();
        Destroy(particleObj, particleSystem.main.duration + particleSystem.main.startLifetime.constantMax);

        GameObject audioObject = Instantiate(StrangerThings.rockExplosionAudio, transform.position, Quaternion.identity);
        AudioSource audioSource = audioObject.GetComponent<AudioSource>();
        Destroy(audioObject, audioSource.clip.length);
    }
}
