using GameNetcodeStuff;
using LegaFusionCore.Managers;
using StrangerThings.Behaviours.MapObjects;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Managers;

public static class MapObjectsManager
{
    private static readonly HashSet<AntennaHazard> AntennaHazards = [];
    private static readonly HashSet<UpsideDownPortal> UpsideDownPortals = [];

    public static void AddAntenna(AntennaHazard antennaHazard) => _ = AntennaHazards.Add(antennaHazard);
    public static HashSet<AntennaHazard> GetAntennaHazards()
    {
        _ = AntennaHazards.RemoveWhere(p => p == null);
        return AntennaHazards;
    }

    public static bool IsNearAntenna(PlayerControllerB player)
    {
        foreach (AntennaHazard antennaHazard in GetAntennaHazards())
        {
            if (antennaHazard != null && antennaHazard.antennaItem != null)
            {
                float distance = Vector3.SqrMagnitude(antennaHazard.transform.position - player.transform.position);
                if (distance < 5625f)
                    return true;
            }
        }
        return false;
    }

    public static void AddPortal(UpsideDownPortal upsideDownPortal) => _ = UpsideDownPortals.Add(upsideDownPortal);
    public static void ClearPortals() => UpsideDownPortals.Clear();
    public static HashSet<UpsideDownPortal> GetUpsideDownPortals()
    {
        _ = UpsideDownPortals.RemoveWhere(p => p == null);
        return UpsideDownPortals.Where(p => !p.isFake).ToHashSet();
    }

    public static void SpawnPortalsForServer()
    {
        if (GetUpsideDownPortals().Count < 8)
        {
            LFCMapObjectsManager.SpawnScatteredMapObjectsForServer(mapObjectsAmount: 8,
                minInside: 2,
                minOutside: 2,
                spawnAction: (position, isOutside) => { _ = SpawnPortalForServer(position, isOutside); });
        }
    }

    public static UpsideDownPortal SpawnPortalForServer(Vector3 position, bool isOutside, bool isFake = false)
    {
        GameObject gameObject = Object.Instantiate(StrangerThings.UpsideDownPortalObj, position + (Vector3.down * 0.1f), Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
        gameObject.GetComponent<NetworkObject>().Spawn(true);
        UpsideDownPortal upsideDownPortal = gameObject.GetComponent<UpsideDownPortal>();
        upsideDownPortal.InitializeEveryoneRpc(isOutside, isFake);
        return upsideDownPortal;
    }

    public static UpsideDownPortal GetClosestPortal(Vector3 position)
    {
        UpsideDownPortal closest = null;
        float closestDistance = float.MaxValue;
        foreach (UpsideDownPortal portal in GetUpsideDownPortals())
        {
            if (portal != null)
            {
                float distance = Vector3.SqrMagnitude(portal.transform.position - position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = portal;
                }
            }
        }
        return closest;
    }

    public static UpsideDownPortal GetFurthestPortal(Vector3 position)
    {
        UpsideDownPortal furthest = null;
        float furthestDistance = 0f;
        foreach (UpsideDownPortal portal in GetUpsideDownPortals())
        {
            if (portal != null)
            {
                float distance = Vector3.SqrMagnitude(portal.transform.position - position);
                if (distance > furthestDistance)
                {
                    furthestDistance = distance;
                    furthest = portal;
                }
            }
        }
        return furthest;
    }
}
