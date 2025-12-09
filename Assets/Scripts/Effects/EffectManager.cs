using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Match3.Effects
{
    /// <summary>
    /// 게임 내 모든 이펙트의 생성과 재사용(Pooling)을 관리하는 싱글톤 매니저입니다.
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance { get; private set; }

        [System.Serializable]
        public struct EffectEntry
        {
            public EffectType type;
            public PooledEffect prefab;
            public int defaultCapacity;
            public int maxCapacity;
        }

        [Header("Effect Configuration")]
        [SerializeField] private List<EffectEntry> m_effectEntries;

        // 이펙트 타입별 오브젝트 풀 딕셔너리
        private Dictionary<EffectType, IObjectPool<PooledEffect>> m_pools;
        // 딕셔너리 조회를 위한 룩업 (최적화)
        private Dictionary<EffectType, PooledEffect> m_prefabs;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializePools();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializePools()
        {
            m_pools = new Dictionary<EffectType, IObjectPool<PooledEffect>>();
            m_prefabs = new Dictionary<EffectType, PooledEffect>();

            foreach (var entry in m_effectEntries)
            {
                if (entry.prefab == null)
                {
                    Debug.LogWarning($"[EffectManager] Prefab missing for type: {entry.type}");
                    continue;
                }

                m_prefabs[entry.type] = entry.prefab;

                // Unity 2021+ Built-in ObjectPool 사용
                var pool = new ObjectPool<PooledEffect>(
                    createFunc: () => CreateEffect(entry.type),
                    actionOnGet: effect => effect.gameObject.SetActive(true),
                    actionOnRelease: effect => effect.gameObject.SetActive(false),
                    actionOnDestroy: effect => Destroy(effect.gameObject),
                    collectionCheck: false, // 성능을 위해 false (릴리즈된거 또 릴리즈하는지 체크 안함)
                    defaultCapacity: entry.defaultCapacity > 0 ? entry.defaultCapacity : 10,
                    maxSize: entry.maxCapacity > 0 ? entry.maxCapacity : 50
                );

                m_pools.Add(entry.type, pool);
            }
        }

        private PooledEffect CreateEffect(EffectType type)
        {
            if (!m_prefabs.TryGetValue(type, out var prefab))
                return null;

            var instance = Instantiate(prefab, transform);
            // 인스턴스에 자신이 속한 풀 정보를 주입하여 스스로 반환할 수 있게 함
            instance.Initialize(type, m_pools[type]);
            return instance;
        }

        /// <summary>
        /// 지정된 위치에서 이펙트를 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position)
        {
            PlayEffect(type, position, Color.white);
        }

        /// <summary>
        /// 지정된 위치에서 지정된 색상으로 이펙트를 재생합니다.
        /// </summary>
        public void PlayEffect(EffectType type, Vector3 position, Color color)
        {
            if (m_pools.TryGetValue(type, out var pool))
            {
                PooledEffect effect = pool.Get();
                effect.SetColor(color);
                effect.Play(position);
            }
            else
            {
                Debug.LogWarning($"[EffectManager] Pool not found for type: {type}");
            }
        }
    }
}
