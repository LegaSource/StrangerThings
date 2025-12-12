using GameNetcodeStuff;
using LegaFusionCore.Managers.NetworkManagers;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace StrangerThings.Behaviours.Scripts;

public class DoorProjectile : MonoBehaviour
{
    private Rigidbody rigidbody;
    private bool hasHit = false;

    private void Start() => StartCoroutine(SelfDestructAfterDelay(2f));

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        PlayerControllerB player = collision.gameObject.GetComponentInParent<PlayerControllerB>();
        if (player != null)
        {
            hasHit = true;
            rigidbody = GetComponent<Rigidbody>();

            int damage = (int)(rigidbody.velocity.magnitude * 2);
            if (damage > 0) LFCNetworkManager.Instance.DamagePlayerEveryoneRpc((int)player.playerClientId, damage);

            rigidbody.velocity *= 0.3f;
            rigidbody.useGravity = true;
            rigidbody.constraints = RigidbodyConstraints.None;

            if (collision.contactCount > 0)
            {
                Vector3 normal = collision.contacts[0].normal;
                Vector3 tiltAxis = Vector3.Cross(normal, Vector3.up);
                rigidbody.AddTorque(tiltAxis * 15f, ForceMode.Impulse);
            }

            _ = StartCoroutine(SelfDestructAfterDelay(1f));
        }
    }

    private IEnumerator SelfDestructAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        rigidbody ??= GetComponent<Rigidbody>();
        Destroy(rigidbody);
        foreach (Collider collider in GetComponentsInChildren<Collider>()) Destroy(collider);
        foreach (NavMeshObstacle obstacle in GetComponentsInChildren<NavMeshObstacle>())
        {
            obstacle.carving = false;
            Destroy(obstacle);
        }
        Destroy(GetComponentInChildren<DoorLock>());
        Destroy(this, 1f);
    }
}
