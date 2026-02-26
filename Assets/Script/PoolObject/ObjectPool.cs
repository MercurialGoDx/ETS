using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Stack<T> pool = new Stack<T>();

    public ObjectPool(T prefab, int initialSize, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < initialSize; i++)
            Create();
    }

    private T Create()
    {
        T obj = Object.Instantiate(prefab, parent);
        obj.gameObject.SetActive(false);
        pool.Push(obj);
        return obj;
    }

    public T Get()
    {
        T obj = pool.Count > 0 ? pool.Pop() : Create();
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Release(T obj)
    {
        obj.gameObject.SetActive(false);
        pool.Push(obj);
    }
}