using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerView : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button serverButton;
    [SerializeField] private Button clientButton;

    private void Awake()
    {
        hostButton.onClick.AddListener(() => NetworkManager.Singleton.StartHost());
        serverButton.onClick.AddListener(() => NetworkManager.Singleton.StartServer());
        clientButton.onClick.AddListener(() => NetworkManager.Singleton.StartClient());
    }
}