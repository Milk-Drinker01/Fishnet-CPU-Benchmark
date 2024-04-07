using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTargetFrameRate : MonoBehaviour
{
    public int TargetFrameRate = 33;
    void Start()
    {
        Application.targetFrameRate = TargetFrameRate;
    }
}
