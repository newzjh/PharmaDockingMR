using System;
using System.Collections.Generic;
using UnifiedInput;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FirstInit : MonoBehaviour
{
    public void Start()
    {
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Additive);
    }
}
