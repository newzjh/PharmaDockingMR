using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class Audios : Singleton<Audios>
{
    public static void Play(int id)
    {
        Audios audioset = Audios.Instance;
        if (!audioset)
            return;

        for(int i=0;i<audioset.transform.childCount;i++)
        {
            Transform tChild = audioset.transform.GetChild(i);
            if (tChild)
            {
                AudioSource audio = tChild.GetComponent<AudioSource>();
                if (audio)
                    audio.Stop();
            }
        }
        
        if (id<audioset.transform.childCount)
        {
            Transform tChild = audioset.transform.GetChild(id);
            if (tChild)
            {
                AudioSource audio = tChild.GetComponent<AudioSource>();
                if (audio)
                    audio.Play();
            }
        }
    }

    public static void Play(string name)
    {
        Audios audioset = Audios.Instance;
        if (!audioset)
            return;

        for (int i = 0; i < audioset.transform.childCount; i++)
        {
            Transform tChild = audioset.transform.GetChild(i);
            if (tChild)
            {
                AudioSource audio = tChild.GetComponent<AudioSource>();
                if (audio)
                    audio.Stop();
            }
        }

        {
            Transform tChild = audioset.transform.Find(name);
            if (tChild)
            {
                AudioSource audio = tChild.GetComponent<AudioSource>();
                if (audio)
                    audio.Play();
            }
        }
    }
}