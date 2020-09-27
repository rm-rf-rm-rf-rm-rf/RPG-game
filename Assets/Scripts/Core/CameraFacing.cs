﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace RPG.Core
{
    public class CameraFacing : MonoBehaviour
    {
        // Update is called once per frame
        void LateUpdate()
        {
            transform.forward = Camera.main.transform.forward;
        }
    }
}
