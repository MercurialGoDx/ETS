using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    private Dictionary<GameObject, object> pools = new Dictionary<GameObject, object>();

    private void Awake()
    {
        Instance = this;
    }

    // ������������� Spawn
    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (!pools.TryGetValue(prefab, out var poolObj))
        {
            PooledObject pooledPrefab = prefab.GetComponent<PooledObject>();

            var newPool = new ObjectPool<PooledObject>(
                pooledPrefab,
                0,
                transform
            );

            pools[prefab] = newPool;
            poolObj = newPool;
        }

        var typedPool = (ObjectPool<PooledObject>)poolObj;

        var obj = typedPool.Get();
        obj.transform.SetPositionAndRotation(pos, rot);

        obj.SetReleaseAction(o => typedPool.Release(o));

        obj.gameObject.transform.localScale = prefab.transform.localScale;

        return obj.gameObject;
    }

    public GameObject Spawn(GameObject prefab, Transform parent, Quaternion rot)
    {
        if (!pools.TryGetValue(prefab, out var poolObj))
        {
            var pooled = prefab.GetComponent<PooledObject>();
            if (pooled == null)
            {
                Debug.LogError($"Prefab {prefab.name} has no PooledObject!");
                return null;
            }

            var newPool = new ObjectPool<PooledObject>(pooled, 0, transform);
            pools[prefab] = newPool;
            poolObj = newPool;
        }

        var pool = (ObjectPool<PooledObject>)poolObj;
        var obj = pool.Get();

        obj.transform.SetParent(parent, false);
        obj.transform.localRotation = rot;

        obj.SetReleaseAction(o => pool.Release(o));

        return obj.gameObject;
    }
}