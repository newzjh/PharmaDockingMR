using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

public class UnifiedSimulatorController : MonoBehaviour
{
    XRDeviceSimulator simulator;

    // Start is called before the first frame update
    void Start()
    {
        simulator = GetComponent<XRDeviceSimulator>();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
