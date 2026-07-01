using GameNetcodeStuff;
using LegaFusionCore.Managers.NetworkManagers;
using LegaFusionCore.Utilities;
using StrangerThings.Registries;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Scripts.Projectiles;

public class LogProjectile : NetworkBehaviour
{
    public Rigidbody Rigidbody;
    public BoxCollider BoxCollider;

    private bool isFalling = false;
    private bool hasLanded = false;
    private bool hasHit = false;

    private float lowVelocityTimer;
    private const float LowVelocityThreshold = 0.5f;
    private const float LowVelocityGracePeriod = 0.5f;

    protected Vector3 networkPosition;
    protected Quaternion networkRotation;
    private float syncTimer;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void FallEveryoneRpc(Vector3 treeSize, Vector3 direction)
    {
        transform.localScale = new Vector3(treeSize.x / BoxCollider.size.x, treeSize.y / BoxCollider.size.y, treeSize.z / BoxCollider.size.z);

        if (!isFalling)
        {
            isFalling = true;
            _ = StartCoroutine(FallCoroutine(direction));
        }
    }

    private IEnumerator FallCoroutine(Vector3 direction)
    {
        Vector3 pivotPoint = transform.position;
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction).normalized;
        if (rotationAxis == Vector3.zero)
            rotationAxis = Vector3.right;

        float previousAngle = 0f;
        float timePassed = 0f;
        float duration = 0.1f;

        while (timePassed < duration && !hasHit)
        {
            float currentAngle = Mathf.SmoothStep(0f, 45f, timePassed / duration);
            float deltaAngle = currentAngle - previousAngle;

            transform.RotateAround(pivotPoint, rotationAxis, deltaAngle);
            previousAngle = currentAngle;

            timePassed += Time.deltaTime;
            yield return null;
        }

        if (!hasHit)
            transform.RotateAround(pivotPoint, rotationAxis, 45f - previousAngle);

        // Vélocité de transition pour fluidifier la chute
        Rigidbody.angularVelocity = rotationAxis * 0.8f;
        Rigidbody.useGravity = true;
        Rigidbody.isKinematic = false;
    }

    private void LateUpdate()
    {
        if (!isFalling || hasHit || hasLanded || Rigidbody.isKinematic)
            return;

        if (LFCUtilities.IsServer)
        {
            syncTimer += Time.deltaTime;
            if (syncTimer >= 0.05f)
            {
                syncTimer = 0f;
                SyncPositionNotServerRpc(transform.position, transform.rotation, hasLanded: false);
            }
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 18f);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * 18f);
            return;
        }
        // Détection vitesse et vitesse de rotation faible
        if (Rigidbody.velocity.magnitude <= LowVelocityThreshold && Rigidbody.angularVelocity.magnitude <= LowVelocityThreshold)
        {
            lowVelocityTimer += Time.deltaTime;
            if (lowVelocityTimer >= LowVelocityGracePeriod)
            {
                hasLanded = true;
                Rigidbody.velocity = Vector3.zero;
                Rigidbody.angularVelocity = Vector3.zero;
                Rigidbody.useGravity = false;
                Rigidbody.isKinematic = true;
                SyncPositionNotServerRpc(transform.position, transform.rotation, hasLanded: true);
            }
            return;
        }
        lowVelocityTimer = 0f;
    }

    [Rpc(SendTo.NotServer, RequireOwnership = false)]
    public void SyncPositionNotServerRpc(Vector3 position, Quaternion rotation, bool hasLanded)
    {
        if (hasLanded)
        {
            this.hasLanded = true;
            transform.position = position;
            transform.rotation = rotation;
            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
            Rigidbody.useGravity = false;
            Rigidbody.isKinematic = true;
            return;
        }

        networkPosition = position;
        networkRotation = rotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isFalling && !hasLanded && !hasHit && collision.gameObject.TryGetComponent(out PlayerControllerB player) && DimensionRegistry.AreInSameDimension(player.gameObject, gameObject))
        {
            hasHit = true;
            if (LFCUtilities.IsServer)
                LFCNetworkManager.Instance.KillPlayerEveryoneRpc((int)player.playerClientId, velocity: Vector3.zero, spawnBody: true, (int)CauseOfDeath.Crushing);
        }
    }
}