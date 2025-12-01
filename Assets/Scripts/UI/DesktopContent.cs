using System.Collections.Generic;
using Mirror;
using MixedReality.Toolkit.UX;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using MixedReality.Toolkit.Input.Simulation;
using System;
using System.IO;
using UnityEngine.InputSystem;

using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using TMPro;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;



public class DesktopContent : NetworkBehaviour
{
    public TMP_Dropdown LoadMenu;
    public Button LoadButton;
    public Toggle OverlayToggle;
    public TMPro.TMP_InputField InputField;

    public RectTransform LayoutViewV;


    public RectTransform ImageMenu;
    public GameObject ImageManipulator;
    public Transform MainMenu;
    public RectTransform RenderingPage;


    public Transform indicator;


#if UNITY_EDITOR || UNITY_STANDALONE



    public const int sw = 512;
    public const int sh = 512;
    public const int sd = 16;


    [NonSerialized]
    public InputAction tabInputActionOnDesktop = new InputAction(binding: "<Keyboard>/Tab", expectedControlType: "Button");


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Awake()
    {
        if (NetworkManager.isStandalone)
        {
            Hide();
            return;
        }
    }

    public override void OnStartServer()
    {
        if (NetworkManager.isStandalone)
        {
            Hide();
            return;
        }



        Color[] pixels = new Color[sw * sh];
        for (int j = 0; j < sw * sh; j++)
            pixels[j] = Color.black;





        tabInputActionOnDesktop.Enable();
        tabInputActionOnDesktop.performed += OnSwitchMRTKInput;

        OnSwitchMRTKInput();
        //OnSwitchImageMenu();
        //OnSwitchRenderingPage();

        //var window = GameObject.FindFirstObjectByType<SelectListWindow>(FindObjectsInactive.Include);
        //if (vol != null && window != null)
        //    vol.selectListHandler = window;

        //var attachpanels = GetComponentsInChildren<AttachPanel>(true);
        //foreach (var panel in attachpanels)
        //    panel.Attach();

        //var statuswindow = GameObject.FindFirstObjectByType<StatusWindow>(FindObjectsInactive.Include);
        //if (statuswindow)
        //{
        //    var pos = statuswindow.transform.localPosition;
        //    pos.z = 1;
        //    statuswindow.transform.localPosition = pos;
        //    statuswindow.gameObject.SetActive(false);
        //}
    }


    public override void OnStopServer()
    {
        tabInputActionOnDesktop.Disable();
        tabInputActionOnDesktop.performed -= OnSwitchMRTKInput;

 
    }

    public override void OnStartClient()
    {
        if (!isServer)
            gameObject.SetActive(false);
    }

    public override void OnStopClient()
    {
        if (!isServer)
            gameObject.SetActive(false);
    }

    public void OnSwitchMRTKInput(InputAction.CallbackContext obj)
    {
        var inputsimulator = GameObject.FindFirstObjectByType<InputSimulator>(FindObjectsInactive.Include);
        if (inputsimulator != null)
        {
            inputsimulator.gameObject.SetActive(!inputsimulator.gameObject.activeSelf);
        }

        if (ImageManipulator)
        {
            ImageManipulator.gameObject.SetActive(!ImageManipulator.activeSelf);
        }
    }

    public void OnSwitchMRTKInput()
    {
        var inputsimulator = GameObject.FindFirstObjectByType<InputSimulator>(FindObjectsInactive.Include);
        if (inputsimulator != null)
        {
            inputsimulator.gameObject.SetActive(!inputsimulator.gameObject.activeSelf);
        }

        if (ImageManipulator)
        {
            ImageManipulator.gameObject.SetActive(!ImageManipulator.activeSelf);
        }
    }



    public void OnSwitchMainMenu()
    {
        if (MainMenu)
            MainMenu.gameObject.SetActive(!MainMenu.gameObject.activeSelf);
    }

    public void OnSwitchImageMenu()
    {
        if (ImageMenu)
            ImageMenu.gameObject.SetActive(!ImageMenu.gameObject.activeSelf);
    }


    public void OnSwitchRenderingPage()
    {
        if (RenderingPage)
            RenderingPage.gameObject.SetActive(!RenderingPage.gameObject.activeSelf);
    }




    private async UniTask<string> GetHtml(string url)
    {
        var request = UnityWebRequest.Get(url);
        await request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            request.Dispose();
            return string.Empty;
        }

        if (!request.downloadHandler.isDone)
        {
            request.downloadHandler.Dispose();
            return string.Empty;
        }

        string html = request.downloadHandler.text;
        request.downloadHandler.Dispose();
        request.Dispose();

        return html;
    }

    private async UniTask<byte[]> GetData(string url)
    {
        var request = UnityWebRequest.Get(url);

        {
            await request.SendWebRequest();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            request.Dispose();
            return null;
        }

        if (!request.downloadHandler.isDone)
        {
            request.downloadHandler.Dispose();
            return null;
        }

        byte[] data = request.downloadHandler.data;
        request.downloadHandler.Dispose();
        request.Dispose();

        return data;
    }

    private async UniTask ExtractZipToFolder(byte[] zipbytes, string folder)
    {
        MemoryStream zipms = new MemoryStream(zipbytes);
        ZipInputStream zipis = new ZipInputStream(zipms);

        await UniTask.SwitchToMainThread();
        string rootpath = Application.persistentDataPath;

        while (true)
        {
            var entry = zipis.GetNextEntry();

            if (entry == null)
                break;

            byte[] bytes = new byte[entry.Size];
            //zipis.Read(bytes, 0, (int)entry.Size);
            await zipis.ReadAsync(bytes, 0, (int)entry.Size);

            zipis.CloseEntry();

            string filepath = folder + "/" + entry.Name;
            string subfolder = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(subfolder))
                Directory.CreateDirectory(subfolder);
            await File.WriteAllBytesAsync(filepath, bytes);
        }

        zipis.Dispose();
        zipms.Dispose();
    }

    public void OnLoadFromNetwork()
    {
        if (!isServer)
            return;

        if (InputField != null)
        {
            cmdOnLoadFromNetwork(InputField.text);
        }
    }

    [Command(requiresAuthority = false)]
    public async void cmdOnLoadFromNetwork(string text)
    {
        rpcOnLoadFromNetwork(text);
    }

    [ClientRpc]
    public void rpcOnLoadFromNetwork(string text)
    {
        var panel = GameObject.FindFirstObjectByType<MoleculeUI.BigPanel>(FindObjectsInactive.Include);
        if (panel != null)
        {
            panel.loadPDBFromInternet(text);
        }
    }

    public void OnLoadFromFolder()
    {
        if (!isServer)
            return;

        cmdOnLoadFromFolder();
    }


    [Command(requiresAuthority = false)]
    public async void cmdOnLoadFromFolder()
    {
        rpcOnLoadFromFolder("");
    }

    [ClientRpc]
    public void rpcOnLoadFromFolder(string text)
    {
        var panel = GameObject.FindFirstObjectByType<MoleculeUI.BigPanel>(FindObjectsInactive.Include);
        if (panel != null)
        {
            panel.OpenZipCallback(text);
        }
    }

    public void OnLoadFromFile()
    {
        if (!isServer)
            return;

        //cmdOnLoadFromFile();
    }

    [Command(requiresAuthority = false)]
    public async void cmdOnLoadFromFile(string path)
    {
        rpcOnLoadFromFile(path);
    }

    [ClientRpc]
    public void rpcOnLoadFromFile(string path)
    {
        var panel = GameObject.FindFirstObjectByType<MoleculeUI.BigPanel>(FindObjectsInactive.Include);
        if (panel != null)
        {
            panel.OpenFileCallback(path);
        }
    }

    public void OnLayoutView0()
    {
        var style = GetComponentInChildren<LayoutViewStyle>(true);
        if (style)
            style.style = 0;
        var root = GetComponentInChildren<LayoutViewRoot>(true);
        if (root)
            root.Layout();
    }

    public void OnLayoutView1()
    {
        var style = GetComponentInChildren<LayoutViewStyle>(true);
        if (style)
            style.style = 1;
        var root = GetComponentInChildren<LayoutViewRoot>(true);
        if (root)
            root.Layout();
    }






    private Dictionary<string, int> ClientMap = new();

    public const float f1 = 1.15f;
    public const float f2 = 0.3f;
    public const float f3 = 0.73f;
    public const float f5 = 0.4f;
    public const float f6 = 0.135f;

    private async void UpdateSliceTransforms(Camera cam)
    {
        await UniTask.NextFrame();

        if (!Application.isPlaying)
            return;

        Transform camtrans = cam.transform;

        if (camtrans == null)
            return;
    }


    private int lastsw = 0;
    private int lastsh = 0;

    // Update is called once per frame
    void Update()
    {
        if (!isServer)
            return;

        var cam = Camera.main;
        if (cam)
        {
            var camtrans = cam.transform;
            if (camtrans.hasChanged)
            {
                UpdateSliceTransforms(cam);
            }

            int sw = Screen.width;
            int sh = Screen.height;
            if (sw != lastsw || sh != lastsh)
            {
                lastsw = sw;
                lastsh = sh;

                UpdateSliceTransforms(cam);
            }
        }

        ClientMap.Clear();

        var players = GameObject.FindObjectsByType<VRNetworkPlayerScript>(FindObjectsSortMode.None);
        if (players != null)
        {
            int NonLocalPlayerCount = 0;
            foreach(var player in players)
            {
                if (player.isLocalPlayer)
                    continue;

                if (NonLocalPlayerCount >= 4)
                    break;

                {
                    //ClientRenderRawImages[NonLocalPlayerCount].enabled = true;
                    //ClientCamRawImages[NonLocalPlayerCount].enabled = true;
                    //ClientObjs[NonLocalPlayerCount].gameObject.SetActive(true);
                    //ClientCams[NonLocalPlayerCount].enabled = true;
                    //ClientCams[NonLocalPlayerCount].transform.position = player.headTransform.position;
                    //ClientCams[NonLocalPlayerCount].transform.forward = player.headTransform.forward;

                    if (player.connectionToClient!=null)
                        ClientMap[player.connectionToClient.address] = NonLocalPlayerCount;

                    NonLocalPlayerCount++;

                    break;
                }
            }

            for (int i = NonLocalPlayerCount; i < 4; i++)
            {
                //ClientCamRawImages[i].texture = null;
                //ClientCamRawImages[i].enabled = false;
                //ClientRenderRawImages[i].enabled = false;
                //ClientRenderRawImages[i].enabled = false;
                //ClientObjs[i].gameObject.SetActive(false);
                //ClientCams[i].enabled = false;
            }

        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

#else

    public void Awake()
    {
        Hide();
    }

    public override void OnStartServer()
    {
        Hide();
    }

    public override void OnStartClient()
    {
        Hide();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

#endif
}
