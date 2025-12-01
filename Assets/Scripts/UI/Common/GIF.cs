using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class GIF : MonoBehaviour {

    public List<Texture> textures = new List<Texture>();
    public float speed = 1.5f;
    public int frameindex = 0;

    private RawImage rawimage;
    private float lasttime=-1.0f;

    void Start()
    {
        rawimage = this.GetComponent<RawImage>();
    }

    void Update()
    {
        int framecount = textures.Count;
        if (framecount <= 0)
            return;
     
        float curtime = Time.time;
        float advancetime = speed / framecount;
        if (curtime - lasttime > advancetime)
        {
            rawimage.texture = textures[frameindex];
            frameindex = (frameindex+1)%framecount;
            lasttime = curtime;
        }


    }
}
