using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Wwise3DEmitter
{
    public static void PlayOneShotAt(AK.Wwise.Event wwiseEvent, Vector3 position, string parentName = null, float lifetime = 2f)
    {
        if (wwiseEvent == null) return;

        GameObject emitter = new GameObject("Wwise3DEmitter_" + wwiseEvent.Name);
        emitter.transform.position = position;

        if (!string.IsNullOrEmpty(parentName))
        {
            GameObject parent = GameObject.Find(parentName);
            if (parent != null)
            {
                emitter.transform.parent = parent.transform;
            }
        }

        emitter.AddComponent<AkGameObj>();
        wwiseEvent.Post(emitter);

        Object.Destroy(emitter, lifetime);
    }

    public static void PlayOnGameObject(AK.Wwise.Event wwiseEvent, GameObject target, float lifetime = 0f)
    {
        if (wwiseEvent == null || target == null) return;

        if (!target.TryGetComponent(out AkGameObj akGameObj))
        {
            akGameObj = target.AddComponent<AkGameObj>();
        }

        wwiseEvent.Post(target);

        if (lifetime > 0f)
        {
            Object.Destroy(akGameObj, lifetime);
        }
    }
}