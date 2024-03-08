using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLayers : MonoBehaviour
{
    [SerializeField] LayerMask solidObjectsLayer;
    [SerializeField] LayerMask grassLayer;
    [SerializeField] LayerMask interactablesLayer;

    public static GameLayers instance { get; set; }

    private void Awake()
    {
        instance = this;
    }

    public LayerMask SolidLayer { get => solidObjectsLayer; }
    public LayerMask GrassLayer { get => grassLayer; }
    public LayerMask InteractableLayer { get => interactablesLayer; }
}
