using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class TileFactory
    {
        private readonly GameObject m_tilePrefab;
        private readonly Transform m_parent;
        private readonly TileThemeData m_theme;
        
        private readonly Vector3 m_tileScale;
        private int m_currentRotationAngle;

        private readonly Queue<Tile> m_tilePool = new Queue<Tile>();

        public TileFactory(GameObject tilePrefab, Transform parent, float cellSize, TileThemeData theme, GameBoard.BoardRotation initialRotation)
        {
            m_tilePrefab = tilePrefab;
            m_parent = parent;
            m_theme = theme;
            
            m_tileScale = CalculateScale(cellSize);
            UpdateRotation(initialRotation);
        }

        public void UpdateRotation(GameBoard.BoardRotation newRotation)
        {
            m_currentRotationAngle = (int)newRotation * 90;
        }

        public Tile Get(Vector3 position, Vector2Int gridPosition, TileType type)
        {
            if (m_tilePrefab == null) { Debug.LogError("Tile Prefab is not provided!"); return null; }
            if (m_theme == null) { Debug.LogError("TileThemeData is missing!"); return null; }

            Tile tileComponent;
            if (m_tilePool.Count > 0)
            {
                tileComponent = m_tilePool.Dequeue();
                tileComponent.transform.SetPositionAndRotation(position, Quaternion.identity);
                tileComponent.gameObject.SetActive(true);
            }
            else
            {
                tileComponent = CreateNewTile(position);
                if (tileComponent == null) return null;
            }

            tileComponent.SetOriginalScale(m_tileScale);
            tileComponent.Initialize(gridPosition, type);
            
            tileComponent.name = $"Tile_{gridPosition.x}_{gridPosition.y}";
            
            Sprite sprite = m_theme.GetSprite(type);
            tileComponent.ApplySprite(sprite);

            Quaternion inverseRotation = Quaternion.Euler(0, 0, m_currentRotationAngle);
            tileComponent.SetVisualRotation(inverseRotation);

            return tileComponent;
        }

        public void Release(Tile tile)
        {
            if (tile == null) return;
            
            tile.gameObject.SetActive(false);
            m_tilePool.Enqueue(tile);
        }

        private Tile CreateNewTile(Vector3 position)
        {
            GameObject tileInstance = Object.Instantiate(m_tilePrefab, position, Quaternion.identity, m_parent);
            
            Tile tileComponent = tileInstance.GetComponent<Tile>();
            if (tileComponent != null)
            {
                return tileComponent;
            }

            Debug.LogError($"Tile prefab is missing the 'Tile' component.", tileInstance);
            Object.Destroy(tileInstance);
            return null;
        }

        private Vector3 CalculateScale(float cellSize)
        {
            if (m_tilePrefab == null) return Vector3.one;

            SpriteRenderer prefabSpriteRenderer = m_tilePrefab.GetComponentInChildren<SpriteRenderer>();
            if (prefabSpriteRenderer == null || prefabSpriteRenderer.sprite == null) 
            {
                Debug.LogWarning("Prefab does not have a SpriteRenderer with a sprite to calculate scale from. Defaulting to scale one.");
                return Vector3.one;
            }

            float spriteWidth = prefabSpriteRenderer.sprite.bounds.size.x;
            if (spriteWidth <= 0) 
            {
                Debug.LogWarning("Prefab sprite width is zero or negative. Defaulting to scale one.");
                return Vector3.one;
            }

            float requiredScale = cellSize / spriteWidth;
            return new Vector3(requiredScale, requiredScale, 1f);
        }
    }
}
