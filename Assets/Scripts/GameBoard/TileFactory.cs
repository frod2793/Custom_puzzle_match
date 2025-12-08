using UnityEngine;

namespace Match3
{
    public class TileFactory
    {
        private readonly GameObject m_tilePrefab;
        private readonly Transform m_parent;
        private readonly float m_cellSize;
        private readonly TileThemeData m_theme;
        private readonly GameBoard.BoardRotation m_initialRotation;

        public TileFactory(GameObject tilePrefab, Transform parent, float cellSize, TileThemeData theme, GameBoard.BoardRotation initialRotation)
        {
            m_tilePrefab = tilePrefab;
            m_parent = parent;
            m_cellSize = cellSize;
            m_theme = theme;
            m_initialRotation = initialRotation;
        }

        public Tile Create(Vector3 position, Vector2Int gridPosition, TileType type)
        {
            if (m_tilePrefab == null) { Debug.LogError("Tile Prefab is not provided!"); return null; }
            if (m_theme == null) { Debug.LogError("TileThemeData is missing!"); return null; }

            GameObject tileInstance = Object.Instantiate(m_tilePrefab, position, Quaternion.identity, m_parent);
            tileInstance.name = $"Tile_{gridPosition.x}_{gridPosition.y}";

            AdjustScale(tileInstance);
            
            Tile tileComponent = tileInstance.GetComponent<Tile>();
            if (tileComponent != null)
            {
                tileComponent.Initialize(gridPosition, type);
                
                Sprite sprite = m_theme.GetSprite(type);
                tileComponent.ApplySprite(sprite);

                // 타일의 초기 회전값 설정
                Quaternion inverseRotation = Quaternion.Euler(0, 0, 90 * (int)m_initialRotation);
                tileComponent.SetVisualRotation(inverseRotation);

                return tileComponent;
            }
            else
            {
                Debug.LogError($"Tile prefab is missing the 'Tile' component.", tileInstance);
                Object.Destroy(tileInstance);
                return null;
            }
        }

        private void AdjustScale(GameObject tileInstance)
        {
            SpriteRenderer spriteRenderer = tileInstance.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) { return; }

            Sprite originalSprite = m_tilePrefab.GetComponent<SpriteRenderer>()?.sprite;
            if (originalSprite == null) { return; }

            float spriteWidth = originalSprite.bounds.size.x;
            if (spriteWidth <= 0) return;

            float requiredScale = m_cellSize / spriteWidth;
            tileInstance.transform.localScale = new Vector3(requiredScale, requiredScale, 1f);
        }
    }
}
