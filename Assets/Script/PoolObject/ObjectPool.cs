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
            pool.Push(Create());
    }

    // Только создаёт неактивный объект. В свободный стек его кладёт вызывающий:
    // предзаполнение — сразу, Get — никогда, потому что выдаёт его активным.
    // Раньше Create сам делал Push, и объект, выданный из Get при пустом пуле,
    // оставался в стеке свободных — следующий Get отдавал тот же живой объект второй раз.
    private T Create()
    {
        T obj = Object.Instantiate(prefab, parent);
        obj.gameObject.SetActive(false);
        return obj;
    }

    public T Get()
    {
        T obj = null;

        // Пропускаем уничтоженные объекты в стеке (например, снесённые при выгрузке
        // сцены или ошибочно уничтоженные вместо Release) — иначе обращение к их
        // .gameObject бросает MissingReferenceException.
        while (pool.Count > 0)
        {
            obj = pool.Pop();
            if (obj != null) break; // Unity-перегрузка ==: уничтоженный объект == null
            obj = null;
        }

        if (obj == null)
            obj = Create();

        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Release(T obj)
    {
        obj.gameObject.SetActive(false);
        pool.Push(obj);
    }
}