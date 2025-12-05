using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System.Threading;
using GravitySpinMatch.Core;

namespace GravitySpinMatch.Game
{
    public class Block : MonoBehaviour, IMovable
    {
        [SerializeField]
        private SpriteRenderer m_renderer;
        
        private int m_typeId; // 블록 타입 식별자
        public int TypeId => m_typeId;

        public void Initialize(int typeId, Sprite sprite)
        {
            m_typeId = typeId;
            if(m_renderer == null)
            {
                m_renderer = GetComponent<SpriteRenderer>();
            }
            
            if(m_renderer != null)
            {
                m_renderer.sprite = sprite;
            }
        }

        public async UniTask MoveToAsync(Vector3 targetPosition, float duration, CancellationToken token)
        {
            // DOTween을 사용한 이동
            // 트윈 중 오브젝트가 파괴되면, ToUniTask가 토큰을 통해 취소 처리를 수행합니다.
            await transform.DOMove(targetPosition, duration)
                .SetEase(Ease.OutQuad)
                .ToUniTask(cancellationToken: token);
        }

        // 파괴 및 매칭 이펙트 처리 메서드
        public async UniTask DestroyAsync(CancellationToken token)
        {
            await transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .ToUniTask(cancellationToken: token);
            
            Destroy(gameObject);
        }
    }
}
