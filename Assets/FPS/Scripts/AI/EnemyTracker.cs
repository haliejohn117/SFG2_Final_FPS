using System.Collections;
using System.Collections.Generic;
using Unity.FPS.AI;
using UnityEngine;

public class EnemyTracker : MonoBehaviour
{
    public static EnemyTracker Instance { get; private set; }

    public List<EnemyController> ActiveEnemies { get; private set; } = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    public void Register(EnemyController enemy)
    {
        if (!ActiveEnemies.Contains(enemy))
            ActiveEnemies.Add(enemy);
    }

    public void Unregister(EnemyController enemy)
    {
        ActiveEnemies.Remove(enemy);
    }
}

