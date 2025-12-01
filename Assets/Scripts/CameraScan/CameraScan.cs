using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraScan : Singleton<CameraScan>
{
    private Camera cam;
	private WebCamTexture webCameraTexture;
    private Material cameraSkyboxMaterial;
    //private Material sunSkyboxMaterial;
    public bool enableCamera = true;

    private const float lowPassFilterFactor = 0.2f;
    private Quaternion cameraBase = Quaternion.identity;
    private Quaternion calibration = Quaternion.identity;
    private Quaternion referanceRotation = Quaternion.identity;

    private GameObject goScene;

	// Use this for initialization
	void Awake ()
	{
        cam = Camera.main;

        if (Application.platform == RuntimePlatform.WSAPlayerX64 ||
                   Application.platform == RuntimePlatform.WSAPlayerX86 ||
                   Application.platform == RuntimePlatform.WSAPlayerARM)
        {
            enableCamera = false;
        }
#if UNITY_WSA
        enableCamera = false;
#endif

        if (Application.platform != RuntimePlatform.Android && Application.platform!= RuntimePlatform.IPhonePlayer)
        {
            if (this.gameObject.GetComponent<ImageEffects.GlowOutline>() == null)
                this.gameObject.AddComponent<ImageEffects.GlowOutline>();
        }

        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            if (this.gameObject.GetComponent<ImageEffects.SSAOPro>() == null)
                this.gameObject.AddComponent<ImageEffects.SSAOPro>();
        }

        if (!enableCamera)
        {
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.clearFlags = CameraClearFlags.SolidColor;
            return;
        }

        int sw = 1920;
        int sh = 1280;
        //Camera.main.clearFlags = CameraClearFlags.Skybox;
		//  bool success = CameraDevice.Instance.SetFocusMode(CameraDevice.FocusMode.FOCUS_MODE_CONTINUOUSAUTO);
		// Checks how many and which cameras are available on the device
		for (int cameraIndex = 0; cameraIndex < WebCamTexture.devices.Length; cameraIndex++) {
			if (!WebCamTexture.devices [cameraIndex].isFrontFacing) {
                webCameraTexture = new WebCamTexture(sw, sh);
                if (webCameraTexture) //break if successs to avoid dual camera problem
                    break;
			}
		}

		if (webCameraTexture == null) {
			for (int cameraIndex = 0; cameraIndex < WebCamTexture.devices.Length; cameraIndex++) {
                webCameraTexture = new WebCamTexture(sw, sh);
                if (webCameraTexture) //break if successs to avoid dual camera problem
                    break;
			}
		}          

        //设置设备陀螺仪的开启/关闭状态，使用陀螺仪功能必须设置为 true  
        Input.gyro.enabled = true;
        //设置陀螺仪的更新检索时间，即隔 0.1秒更新一次  
        Input.gyro.updateInterval = 0.1f;

        cameraSkyboxMaterial = Resources.Load("Material/CameraSkybox") as Material;
        //sunSkyboxMaterial = Resources.Load("Material/SunSkybox") as Material;
        //goScene = GameObject.Find("OFFICE");



		Stop ();
	}

	public void Play ()
	{
        if (!enableCamera)
            return;

        cameraSkyboxMaterial.mainTexture = webCameraTexture;
        if (webCameraTexture)
            webCameraTexture.Play();
        cam.clearFlags = CameraClearFlags.Skybox;
        RenderSettings.skybox = cameraSkyboxMaterial;

        //if (goScene)
        //    goScene.SetActive(false);

        if (!webCameraTexture)
            return;

        UpdateCalibration(true);
        UpdateCameraBaseRotation(false);
        RecalculateReferenceRotation();
        //AttachGyro();

	}

	public void Stop ()
	{
        if (!enableCamera)
            return;

        if(cameraSkyboxMaterial)
            cameraSkyboxMaterial.mainTexture = null;
        if (webCameraTexture)
            webCameraTexture.Stop();
        cam.clearFlags = CameraClearFlags.SolidColor;
        //cam.clearFlags = CameraClearFlags.Skybox;
        //RenderSettings.skybox = sunSkyboxMaterial;

        //if (goScene)
        //    goScene.SetActive(true);

        if (bCameraMove)
            transform.localRotation = Quaternion.identity;

        if (!webCameraTexture)
            return;

        //DetachGyro();
	}

	void OnDestroy ()
	{
		Stop ();
        webCameraTexture = null;
        Input.gyro.enabled = false;
	}

    public bool IsPlaying()
    {
        if (!enableCamera)
            return false;

        if (!webCameraTexture)
            return false;

        return webCameraTexture.isPlaying;
    }

    Vector3 curacc = Vector3.zero;

    void OnGUI()
    {
        GUI.skin.box.fontSize = 42;
        GUI.skin.button.fontSize = 36;
        //Vector3 pureacc = Input.acceleration - Input.gyro.gravity;
        //if (pureacc.magnitude > 0.1f)
        //    curacc = pureacc;
        ////GUI.Box(new Rect(10, 100, 300, 50), curacc.x.ToString());
        ////GUI.Box(new Rect(10, 150, 300, 50), curacc.y.ToString());
        ////GUI.Box(new Rect(10, 200, 300, 50), curacc.z.ToString());


        //GUI.Box(new Rect(10, 100, 300, 50), Input.acceleration.ToString());
        //GUI.Box(new Rect(10, 150, 300, 50), Input.gyro.gravity.ToString());
        //GUI.Box(new Rect(10, 200, 300, 50), transform.forward.ToString());


        //Vector3 acc=Input.gyro.userAcceleration;
        //GUI.Box(new Rect(10, 250, 300, 50), acc.z.ToString("f8"));
        //GUI.Box(new Rect(10, 300, 300, 50), cameraAcc.ToString("f8"));
        ////GUI.Box(new Rect(10, 350, 300, 50), tspeed.ToString());

        //GUI.Box(new Rect(10, 400, 300, 50), transform.position.x.ToString());
        //GUI.Box(new Rect(10, 450, 300, 50), transform.position.y.ToString());
        //GUI.Box(new Rect(10, 500, 300, 50), transform.position.z.ToString());

        if (!enableCamera)
            return;

        if (!webCameraTexture)
            return;

        int sw = Screen.width;

        if (webCameraTexture.isPlaying)
        {
            if (bCameraMove)
            {
                if (GUI.Button(new Rect(sw-350-10, 80, 350, 60), "CameraMove OFF"))
                {
                    bCameraMove = false;
                }
            }
            else
            {
                if (GUI.Button(new Rect(sw-350-10, 80, 350, 60), "CameraMove ON"))
                {
                    bCameraMove = true;
                }
            }
        }
    }

    public bool bCameraMove = false;
    float cameraAcc = 0.0f;
    float cameraSpeed = 0.0f;
	private void Update ()
	{
        if (!enableCamera)
            return;

        if (!webCameraTexture)
            return;

        if (!webCameraTexture.isPlaying)
            return;


        int sw = Screen.width;
        int sh = Screen.height;
        if (sw > 0 && sh > 0)
        {
            float scalex = (float)sw / (float)webCameraTexture.width;
            float scaley = (float)sh / (float)webCameraTexture.height;
            float scale = Mathf.Max(scalex, scaley);
            {
                if (scalex > scaley)
                {
                    if (cameraSkyboxMaterial)
                    {
                        cameraSkyboxMaterial.SetFloat("_ScaleX", 1.0f);
                        cameraSkyboxMaterial.SetFloat("_ScaleY", scaley / scalex);
                    }
                }
                else
                {
                    if (cameraSkyboxMaterial)
                    {
                        cameraSkyboxMaterial.SetFloat("_ScaleX", scalex / scaley);
                        cameraSkyboxMaterial.SetFloat("_ScaleY", 1.0f);
                    }
                }
            }
        }

        Quaternion gyroattitude = Input.gyro.attitude;
        Vector3 vecattitude = new Vector3(gyroattitude.x, gyroattitude.y, gyroattitude.z);
        if (vecattitude.magnitude < 0.01f)
            return;

        Quaternion currentRotation = cameraBase * (ConvertRotation(referanceRotation * gyroattitude));
        transform.localRotation = Quaternion.Slerp(transform.localRotation,
            currentRotation, lowPassFilterFactor);

        Vector3 pureacc = Input.acceleration - Input.gyro.gravity;

        //加速度控制移动

        if (bCameraMove)
        {
            //Vector3 projectDir = Vector3.Cross(Input.gyro.gravity, transform.right);
            //projectDir.Normalize();
            //cameraAcc = Vector3.Dot(Input.acceleration, projectDir);
            cameraAcc = Input.gyro.userAcceleration.z;
            float zs = 0.0f;
            if (Mathf.Abs(cameraAcc) > 0.01f)
            {
                if (cameraSpeed * cameraAcc < -0.0002f)
                {
                    cameraSpeed /= 2.0f;
                }
                else
                {
                    cameraSpeed += cameraAcc * Time.deltaTime;
                }
            }
            else
            {
                cameraSpeed /= 2.0f;
                //if (Mathf.Abs(cameraSpeed) < 0.01f)
                //{
                //    cameraSpeed = 0.0f;
                //}
            }
            if (Mathf.Abs(cameraSpeed) > 0.02f)
            {
                zs = Mathf.Sign(cameraSpeed);
                transform.position += transform.forward * zs * Time.deltaTime * 2.0f;
            }
        }
	}

    /// <summary>
    /// Update the gyro calibration.
    /// </summary>
    private void UpdateCalibration(bool onlyHorizontal)
    {
        if (onlyHorizontal)
        {
            var fw = (Input.gyro.attitude) * (-Vector3.forward);
            fw.z = 0;
            if (fw == Vector3.zero)
            {
                calibration = Quaternion.identity;
            }
            else
            {
                calibration = (Quaternion.FromToRotation(Vector3.up, fw));
            }
        }
        else
        {
            calibration = Input.gyro.attitude;
        }
    }

    /// <summary>
    /// Update the camera base rotation.
    /// </summary>
    private void UpdateCameraBaseRotation(bool onlyHorizontal)
    {
        if (onlyHorizontal)
        {
            Vector3 fw = transform.forward;
            fw.y = 0;
            if (fw == Vector3.zero)
            {
                cameraBase = Quaternion.identity;
            }
            else
            {
                cameraBase = Quaternion.FromToRotation(Vector3.forward, fw);
            }
        }

        else
        {
            cameraBase = transform.localRotation;
        }
    }


    /// <summary>
    /// Converts the rotation from right handed to left handed.
    /// </summary>
    private static Quaternion ConvertRotation(Quaternion q)
    {
        return new Quaternion(q.x, q.y, -q.z, -q.w);
    }

    /// <summary>
    /// Recalculates reference rotation.
    /// </summary>
    private void RecalculateReferenceRotation()
    {
        Quaternion baseOrientation = Quaternion.Euler(90, 0, 0);
        referanceRotation = Quaternion.Inverse(baseOrientation) * Quaternion.Inverse(calibration);
    }
}
