using UnityEngine;
using System;

[Serializable]
public class DrawingData
{
    public string lineId;

    // pos
    public float pX;
    public float pY;
    public float pZ;

    // Color 
    public float r;
    public float g;
    public float b;
    public float a;


    public bool isStart;

    // uid of pubb
    public string publisherId;

    public DrawingData(string lineId, Vector3 position, Color color, bool isStart, string publisherId)
    {
        this.lineId = lineId;
        pX = position.x;
        pY = position.y;
        pZ = position.z;
        r = color.r;
        g = color.g;
        b = color.b;
        a = color.a;
        this.isStart = isStart;
        this.publisherId = publisherId;
    }

    public Vector3 GetPosition() => new Vector3(pX, pY, pZ);
    public Color GetColor() => new Color(r, g, b, a);
}