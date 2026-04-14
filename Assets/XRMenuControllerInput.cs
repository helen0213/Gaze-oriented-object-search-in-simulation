using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public static class XRMenuControllerInput
{
    private static readonly string[] LeftControllerNames =
    {
        "LeftControllerAnchor",
        "LeftHandOnControllerAnchor"
    };

    private static readonly string[] RightControllerNames =
    {
        "RightControllerAnchor",
        "RightHandOnControllerAnchor"
    };

    private static readonly List<InputDevice> Devices = new List<InputDevice>();

    public static bool TryGetPressedButton(
        GameObject primaryButton,
        GameObject secondaryButton,
        ref bool leftPressedLastFrame,
        ref bool rightPressedLastFrame,
        out GameObject pressedButton)
    {
        pressedButton = null;

        if (TryPressFromController(
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
                LeftControllerNames,
                primaryButton,
                secondaryButton,
                ref leftPressedLastFrame,
                out pressedButton))
        {
            return true;
        }

        return TryPressFromController(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            RightControllerNames,
            primaryButton,
            secondaryButton,
            ref rightPressedLastFrame,
            out pressedButton);
    }

    private static bool TryPressFromController(
        InputDeviceCharacteristics characteristics,
        string[] controllerNames,
        GameObject primaryButton,
        GameObject secondaryButton,
        ref bool pressedLastFrame,
        out GameObject pressedButton)
    {
        pressedButton = null;

        Transform controllerTransform = FindControllerTransform(controllerNames);
        if (controllerTransform == null)
        {
            pressedLastFrame = false;
            return false;
        }

        bool isPressed = IsControllerPressed(characteristics);
        bool wasPressedThisFrame = isPressed && !pressedLastFrame;
        pressedLastFrame = isPressed;

        if (!wasPressedThisFrame)
        {
            return false;
        }

        Ray ray = new Ray(controllerTransform.position, controllerTransform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        GameObject bestMatch = null;

        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;
            if (hitObject != primaryButton && hitObject != secondaryButton)
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestMatch = hitObject;
        }

        pressedButton = bestMatch;
        return bestMatch != null;
    }

    private static Transform FindControllerTransform(string[] controllerNames)
    {
        for (int i = 0; i < controllerNames.Length; i++)
        {
            GameObject controllerObject = GameObject.Find(controllerNames[i]);
            if (controllerObject != null)
            {
                return controllerObject.transform;
            }
        }

        return null;
    }

    private static bool IsControllerPressed(InputDeviceCharacteristics characteristics)
    {
        Devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(characteristics, Devices);

        for (int i = 0; i < Devices.Count; i++)
        {
            InputDevice device = Devices[i];

            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed)
            {
                return true;
            }

            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed) && primaryPressed)
            {
                return true;
            }
        }

        return false;
    }
}
