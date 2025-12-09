using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;

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
        
        /// <summary>
        /// 타일의 타입에 따른 대표 색상을 반환합니다.
        /// </summary>
        public Color CurrentColor
        {
            get
            {
                switch (m_type)
                {
                    case TileType.Normal_Red: return new Color(1f, 0.3f, 0.3f); // Red
                    case TileType.Normal_Green: return new Color(0.3f, 1f, 0.3f); // Green
                    case TileType.Normal_Blue: return new Color(0.3f, 0.6f, 1f); // Blue
                    case TileType.Normal_Yellow: return new Color(1f, 0.9f, 0.2f); // Yellow
                    case TileType.Normal_Purple: return new Color(0.8f, 0.4f, 1f); // Purple
                    case TileType.Bomb: return Color.black;
                    case TileType.Rocket: return Color.white;
                    default: return Color.white;
                }
            }
        }

        private Vector3 m_originalScale;
        private Color m_originalColor;

        private bool m_isInitialized = false;

        private const float k_MoveDuration = 0.2f;
        private const float k_SelectAnimDuration = 0.15f;
        private const float k_ClearAnimDuration = 0.25f;

        private void Awake()
        {
            // Awake에서는 컴포넌트 참조만 가져옵니다.
            m_spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Vector2Int gridPosition, TileType type)
        {
            // m_spriteRenderer가 null이면 Awake가 아직 호출되지 않은 것이므로, 여기서 다시 참조를 가져옵니다.
            if (m_spriteRenderer == null)
            {
                m_spriteRenderer = GetComponent<SpriteRenderer>();
            }

            // 최초 초기화 시에만 원본 색상을 저장합니다.
            if (!m_isInitialized)
            {
                m_originalColor = m_spriteRenderer.color;
                m_isInitialized = true;
            }

            m_gridPosition = gridPosition;
            m_type = type;

            // 재사용될 때를 대비해 색상과 알파 값을 복원합니다.
            m_spriteRenderer.color = m_originalColor; 
            
            // 투명도(alpha)도 원래대로 복원합니다.
            var color = m_spriteRenderer.color;
            color.a = 1f;
            m_spriteRenderer.color = color;
        }
        
        public void SetOriginalScale(Vector3 scale)
        {
            m_originalScale = scale;
            transform.localScale = m_originalScale;
        }

        public void ApplySprite(Sprite sprite)
        {
            if (m_spriteRenderer != null)
            {
                m_spriteRenderer.sprite = sprite;
            }
        }

        public void SetGridPosition(Vector2Int newPosition)
        {
            m_gridPosition = newPosition;
            gameObject.name = $"Tile_{newPosition.x}_{newPosition.y}";
        }

        public void SetVisualRotation(Quaternion rotation)
        {
            transform.localRotation = rotation;
        }

        public async UniTask MoveToAsync(Vector3 targetWorldPosition)
        {
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            await transform.DOMove(targetWorldPosition, k_MoveDuration)
                           .SetEase(Ease.OutQuad)
                           .ToUniTask(cancellationToken: cancellationToken);
        }

        public void Select()
        {
            if (m_originalScale == Vector3.zero) m_originalScale = transform.localScale;
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
        }
    }
}
