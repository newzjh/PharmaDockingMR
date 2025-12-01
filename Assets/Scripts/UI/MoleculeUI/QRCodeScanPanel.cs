

using System.Runtime.InteropServices;
using System;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using UnifiedInput;

public class QRCodeScanPanel : BasePanelEx<QRCodeScanPanel>
{
    public delegate void BrowserEventHandler(string path);
    public event BrowserEventHandler Selected;
    public event BrowserEventHandler Canceled;

	public Color32[] data;
	
	//public GUITexture myCameraTexture;
	private WebCamTexture webCameraTexture;
    private Texture2D spriteTexture;
    public bool enableCamera = true;
    private Sprite videoCamSprite;

	// Use this for initialization
	public override void OnShow()
	{
        base.OnShow();

        //if (Application.platform == RuntimePlatform.WSAPlayerX64 ||
        //   Application.platform == RuntimePlatform.WSAPlayerX86 ||
        //   Application.platform == RuntimePlatform.WSAPlayerARM)
        //{
        //    enableCamera = false;
        //}

        if (!enableCamera)
            return;

		//  bool success = CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
		// Checks how many and which cameras are available on the device
		for (int cameraIndex = 0; cameraIndex < WebCamTexture.devices.Length; cameraIndex++) {
			if (!WebCamTexture.devices [cameraIndex].isFrontFacing) {
				webCameraTexture = new WebCamTexture (256, 256, 5);
			}
		}

		if (webCameraTexture == null) {
			for (int cameraIndex = 0; cameraIndex < WebCamTexture.devices.Length; cameraIndex++) {
				webCameraTexture = new WebCamTexture (256, 256, 5);
			}
		}
		
		Play ();
	}

    public override void OnHide()
    {
        base.OnHide();

        Stop();

        videoCamSprite = null;
        spriteTexture = null;
        webCameraTexture = null;

        System.GC.Collect();
    }

    void OnDestroy()
    {
        Stop();
    }

	void Play ()
	{
        if (!enableCamera)
            return;

		if (webCameraTexture) {
			webCameraTexture.Play ();
		}
	}

	void Stop ()
	{
        if (!enableCamera)
            return;

		if (webCameraTexture) {
			webCameraTexture.Stop ();
		}
	}



	void OnRresult(string path)
	{
        this.SetVisible(false);
        if (Selected != null)
        {
            Selected(path);
        }
	}

    void OnCancel()
    {
        this.SetVisible(false);
        if (Canceled != null)
        {
            Canceled(null);
        }
    }

    private float lasttime = 0.0f;
	private void Update ()
	{
        if (UnifiedInputManager.GetKeyDown(KeyCode.Escape) || UnifiedInputManager.GetKeyDown(KeyCode.Backspace))
        {
            OnCancel();
        }

        if (!enableCamera)
            return;

        float curtime = Time.time;
        if (curtime - lasttime < 0.2f)
            return;

        Transform tVideoCam = this.transform.Find("VideoCam");
        if (!tVideoCam)
            return;

        Image img = tVideoCam.GetComponent<Image>();
        if (!img)
            return;

        if (!webCameraTexture)
            return;

        img.sprite = videoCamSprite;

		if (webCameraTexture.isPlaying) 
        {

            if (spriteTexture == null || spriteTexture.width != webCameraTexture.width || spriteTexture.height != webCameraTexture.height)
            {
                spriteTexture = new Texture2D(webCameraTexture.width, webCameraTexture.height);
                videoCamSprite = Sprite.Create(spriteTexture, new Rect(0, 0, spriteTexture.width, spriteTexture.height), new Vector2(0, 0));
            }

            Color32[] pixels = webCameraTexture.GetPixels32();
            spriteTexture.SetPixels32(pixels);
            spriteTexture.Apply();
			DecodeQR (webCameraTexture.width, webCameraTexture.height,pixels);
		}
	}

    public override void OnClick(GameObject sender)
    {
        if (sender.name == "CancelButton")
        {
            OnCancel();
        }
    }

    private void DecodeQR(int W, int H, Color32[] data)
	{
		// create a reader with a custom luminance source
        BarcodeReader barcodeReader = new BarcodeReader { AutoRotate = true, TryHarder = true };

		try 
        {
			// decode the current frame
            Result result = barcodeReader.Decode(data, W, H);
			if (result != null) 
            {
				Stop ();
				OnRresult(result.Text);
			}
				
			data = null;
		}
        catch 
        {
            data = null;
		}
	}
}
