using UnityEngine;
using System.Collections.Generic;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    public class OverheatBehavior : MonoBehaviour
    {
        [System.Serializable]
        public struct RendererIndexData
        {
            public Renderer Renderer;
            public int MaterialIndex;

            public RendererIndexData(Renderer renderer, int index)
            {
                this.Renderer = renderer;
                this.MaterialIndex = index;
            }
        }

        [Header("Visual")] [Tooltip("The VFX to scale the spawn rate based on the ammo ratio")]
        public ParticleSystem SteamVfx;

        [Tooltip("The emission rate for the effect when fully overheated")]
        public float SteamVfxEmissionRateMax = 8f;

        //Set gradient field to HDR
        [GradientUsage(true)] [Tooltip("Overheat color based on ammo ratio")]
        public Gradient OverheatGradient;

        [Tooltip("The material for overheating color animation")]
        public Material OverheatingMaterial;


        WeaponController m_Weapon;
        List<RendererIndexData> m_OverheatingRenderersData;
        MaterialPropertyBlock m_OverheatMaterialPropertyBlock;
        float m_LastAmmoRatio;
        ParticleSystem.EmissionModule m_SteamVfxEmissionModule;

        void Awake()
        {
            var emissionModule = SteamVfx.emission;
            emissionModule.rateOverTimeMultiplier = 0f;

            m_OverheatingRenderersData = new List<RendererIndexData>();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] == OverheatingMaterial)
                        m_OverheatingRenderersData.Add(new RendererIndexData(renderer, i));
                }
            }

            m_OverheatMaterialPropertyBlock = new MaterialPropertyBlock();
            m_SteamVfxEmissionModule = SteamVfx.emission;

            m_Weapon = GetComponent<WeaponController>();
            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, OverheatBehavior>(m_Weapon, this, gameObject);

        }

        void Update()
        {
            // visual smoke shooting out of the gun
            float currentAmmoRatio = m_Weapon.CurrentAmmoRatio;
            if (currentAmmoRatio != m_LastAmmoRatio)
            {
                m_OverheatMaterialPropertyBlock.SetColor("_EmissionColor",
                    OverheatGradient.Evaluate(1f - currentAmmoRatio));

                foreach (var data in m_OverheatingRenderersData)
                {
                    data.Renderer.SetPropertyBlock(m_OverheatMaterialPropertyBlock, data.MaterialIndex);
                }

                m_SteamVfxEmissionModule.rateOverTimeMultiplier = SteamVfxEmissionRateMax * (1f - currentAmmoRatio);
            }

            m_LastAmmoRatio = currentAmmoRatio;
        }
    }
}