using System;
using UnityEngine;

public class PooledObject : MonoBehaviour
{
    private Action<PooledObject> releaseAction;

    public void SetReleaseAction(Action<PooledObject> action)
    {
        releaseAction = action;
    }

    public void Release()
    {
        releaseAction?.Invoke(this);
    }
}