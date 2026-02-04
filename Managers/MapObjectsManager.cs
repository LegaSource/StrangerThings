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
    public static HashSet<UpsideDownPortal> upsideDownPortals = [];
    public static HashSet<AntennaHazard> antennaHazards = [];

    public static void AddUpsideDownPortal(UpsideDownPortal upsideDownPortal) => _ = upsideDownPortals.Add(upsideDownPortal);
    public static UpsideDownPortal[] GetUpsideDownPortals()
    {
        _ = upsideDownPortals.RemoveWhere(p => p == null);
        return upsideDownPortals.Where(p => !p.isFake).ToArray();
    }

    public static void SpawnPortalsForServer()
    {
        if (GetUpsideDownPortals().Length < 8)
        {
            LFCMapObjectsManager.SpawnScatteredMapObjectsForServer(mapObjectsAmount: 8,
                minInside: 2,
                minOutside: 2,
                spawnAction: (position, isOutside) => { _ = SpawnUpsideDownPortalForServer(position, isOutside); });
        }
    }

    public static UpsideDownPortal SpawnUpsideDownPortalForServer(Vector3 position, bool isOutside, bool isFake = false)
    {
        GameObject gameObject = Object.Instantiate(StrangerThings.upsideDownPortal, position + (Vector3.down * 0.1f), Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
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

    public static void AddAntennaHazards(AntennaHazard antennaHazard) => _ = antennaHazards.Add(antennaHazard);
    public static HashSet<AntennaHazard> GetAntennaHazards()
    {
        _ = antennaHazards.RemoveWhere(p => p == null);
        return antennaHazards;
    }

    public static bool IsNearAntennaHazard(PlayerControllerB player)
    {
        foreach (AntennaHazard antennaHazard in GetAntennaHazards())
        {
            if (antennaHazard != null && antennaHazard.antennaItem != null && antennaHazard.antennaItem.insertedBattery != null && !antennaHazard.antennaItem.insertedBattery.empty)
            {
                float distance = Vector3.SqrMagnitude(antennaHazard.transform.position - player.transform.position);
                if (distance < 2500f) return true;
            }
        }
        return false;
    }
}
