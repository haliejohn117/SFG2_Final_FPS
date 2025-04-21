using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.FPS.Gameplay;
using Unity.FPS.AI;

public class GameMusicManager : MonoBehaviour
{
    public AK.Wwise.State PlayerStateCombat;
    public AK.Wwise.State PlayerStateExploration;
    string m_CurrentState = "";

    PlayerCharacterController m_Player;

    void Start()
    {
        m_Player = FindObjectOfType<PlayerCharacterController>();
        m_CurrentState = "Exploration";
        PlayerStateExploration?.SetValue();

        if (m_Player == null)
        {
            Debug.LogError("[GameMusicManager] Could not find PlayerCharacterController in the scene.");
            enabled = false;
        }
    }

    void Update()
    {
        HandleCombatMusic();
    }

    void HandleCombatMusic()
    {
        int alertedCount = EnemyTracker.Instance?.ActiveEnemies
            .Count(ec => ec != null && ec.enabled && ec.CurrentState == EnemyController.EnemyState.Alerted) ?? 0;

        string newState = alertedCount > 0 ? "Combat" : "Exploration";

        if (newState != m_CurrentState)
        {
            if (newState == "Combat")
                PlayerStateCombat?.SetValue();
            else
                PlayerStateExploration?.SetValue();

            m_CurrentState = newState;
        }
    }
}
