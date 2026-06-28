using LegaFusionCore.Utilities;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Items.Figurines;

public class FigurinePop : UpsideDownObject
{
    public bool onCooldown = false;
    public int currentTimeLeft;

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void StartChronoEveryoneRpc(int cooldown)
    {
        onCooldown = true;
        currentTimeLeft = cooldown;
        _ = StartCoroutine(StartChronoCoroutine());
    }

    public IEnumerator StartChronoCoroutine()
    {
        while (currentTimeLeft > 0)
        {
            yield return new WaitForSecondsRealtime(1f);

            currentTimeLeft--;
            SetControlTipsForItem();
        }

        onCooldown = false;
        SetControlTipsForItem();
    }

    public override void SetControlTipsForItem()
    {
        if (LFCUtilities.ShouldBeLocalPlayer(playerHeldBy))
        {
            string toolTip = onCooldown ? $"[On Cooldown : {currentTimeLeft}]" : "";
            HUDManager.Instance.ChangeControlTipMultiple(itemProperties.toolTips.Concat([toolTip]).ToArray(), holdingItem: true, itemProperties);
        }
    }
}
