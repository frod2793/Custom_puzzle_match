using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace Match3.Effects
{
    /// <summary>
    /// 오브젝트 풀링을 지원하는 이펙트의 기본 클래스입니다.
    /// 파티클 시스템이 종료되면 자동으로 풀로 반환됩니다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledEffect : MonoBehaviour
    {
        private ParticleSystem m_particleSystem;
        private IObjectPool<PooledEffect> m_managedPool;

        public EffectType Type { get; private set; }

        private void Awake()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
            
            // 파티클 시스템의 Stop Action을 Callback으로 설정해야 OnParticleSystemStopped가 호출됨
            var main = m_particleSystem.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        /// <summary>
        /// 이펙트가 생성(초기화)될 때 풀 정보를 주입받습니다.
        /// </summary>
        public void Initialize(EffectType type, IObjectPool<PooledEffect> pool)
        {
            Type = type;
            m_managedPool = pool;
        }

        /// <summary>
        /// 파티클의 시작 색상을 변경합니다.
        /// </summary>
        public void SetColor(Color color)
        {
            if (m_particleSystem != null)
            {
                var main = m_particleSystem.main;
                main.startColor = color;
            }
        }

        /// <summary>
        /// 이펙트를 재생합니다.
        /// </summary>
        public void Play(Vector3 position)
        {
            transform.position = position;
            gameObject.SetActive(true);
            m_particleSystem.Play(true); // 자식 파티클까지 모두 재생
        }

        /// <summary>
        /// 파티클 재생이 끝나면 Unity 엔진에 의해 자동으로 호출됩니다.
        /// (ParticleSystem 설정에서 Stop Action이 Callback이어야 함)
        /// </summary>
        private void OnParticleSystemStopped()
        {
            // 풀로 반환
            m_managedPool?.Release(this);
        }
    }
}
