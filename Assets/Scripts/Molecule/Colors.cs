using UnityEngine;
using System.Collections;

public class Colors 
{
    public static Color FromRgb(int r, int g, int b)
    {
        return new Color(r / 255.0f, g / 255.0f, b / 255.0f);
    }
}
