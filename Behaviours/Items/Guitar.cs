using Unity.Netcode;
using UnityEngine;

namespace StrangerThings.Behaviours.Items;

public class Guitar : UpsideDownObject
{
    public AudioSource GuitarAudio;

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (buttonDown && playerHeldBy != null)
            PlayGuitarEveryoneRpc();
    }

    [Rpc(SendTo.Everyone, RequireOwnership = false)]
    public void PlayGuitarEveryoneRpc()
    {
        if (GuitarAudio.isPlaying)
        {
            GuitarAudio.Stop();
            return;
        }
        GuitarAudio.Play();
    }
}
