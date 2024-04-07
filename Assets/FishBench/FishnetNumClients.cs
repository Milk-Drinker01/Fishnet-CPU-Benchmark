using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FishnetNumClients : MonoBehaviour
{
    public Text clientText;
    private void Update()
    {
        clientText.text = $"Num Clients: {FishNet.InstanceFinder.ServerManager.Clients.Count}";
    }
}
