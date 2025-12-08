using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;

namespace Match3
{
    public enum TileType { Normal_Red, Normal_Green, Normal_Blue, Normal_Yellow, Normal_Purple, Bomb, Rocket }

    [RequireComponent(typeof(SpriteRenderer))]
    public class Tile : MonoBehaviour
    {
        [SerializeField] private TileType m_type;
        public TileType Type => m_type;

        private Vector2Int m_gridPosition;
        public Vector2Int GridPosition => m_gridPosition;

        private SpriteRenderer m_spriteRenderer;
        private Vector3 m_originalScale;

        private const float k_MoveDuration = 0.2f;
        private const float k_SelectAnimDuration = 0.15f;
        private const float k_ClearAnimDuration = 0.25f;

        private void Awake()
        {
            m_spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            m_originalScale = transform.localScale;
        }

        public void Initialize(Vector2Int gridPosition, TileType type)
        {
            m_gridPosition = gridPosition;
            m_type = type;
        }

        public void ApplySprite(Sprite sprite)
        {
            if (m_spriteRenderer != null) m_spriteRenderer.sprite = sprite;
        }

        public void SetGridPosition(Vector2Int newPosition)
        {
            m_gridPosition = newPosition;
            gameObject.name = $"Tile_{newPosition.x}_{newPosition.y}";
        }

        // 타일의 시각적 회전을 설정하는 메서드
        public void SetVisualRotation(Quaternion rotation)
        {
            transform.localRotation = rotation;
        }

        public async UniTask MoveToAsync(Vector3 targetPosition)
        {
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            await transform.DOMove(targetPosition, k_MoveDuration)
                           .SetEase(Ease.OutQuad)
                           .ToUniTask(cancellationToken: cancellationToken);
        }

        public void Select()
        {
            transform.DOScale(m_originalScale * 1.15f, k_SelectAnimDuration).SetEase(Ease.OutBack);
        }

        public void Deselect()
        {
            transform.DOScale(m_originalScale, k_SelectAnimDuration);
        }

        public async UniTask ClearAsync()
        {
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            await DOTween.Sequence()
                .Append(transform.DOScale(Vector3.zero, k_ClearAnimDuration).SetEase(Ease.InBack))
                .Join(m_spriteRenderer.DOFade(0f, k_ClearAnimDuration))
                .ToUniTask(cancellationToken: cancellationToken);
            Destroy(gameObject);
        }
    }
}
