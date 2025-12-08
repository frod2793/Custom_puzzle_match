using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Match3
{
    [CreateAssetMenu(fileName = "TileTheme_", menuName = "Match3/Tile Theme Data")]
    public class TileThemeData : ScriptableObject
    {
        [System.Serializable]
        public struct TileSprite
        {
            public TileType Type;
            public Sprite Sprite;
        }

        [SerializeField]
        private List<TileSprite> m_tileSprites;

        // 빠른 조회를 위한 딕셔너리
        private Dictionary<TileType, Sprite> m_spriteLookup;

        private void OnEnable()
        {
            // ScriptableObject가 활성화될 때 리스트를 딕셔너리로 변환하여 조회 성능을 높입니다.
            m_spriteLookup = new Dictionary<TileType, Sprite>();
            foreach (var tileSprite in m_tileSprites)
            {
                if (!m_spriteLookup.ContainsKey(tileSprite.Type))
                {
                    m_spriteLookup.Add(tileSprite.Type, tileSprite.Sprite);
                }
            }
        }

        public Sprite GetSprite(TileType type)
        {
            if (m_spriteLookup != null && m_spriteLookup.TryGetValue(type, out Sprite sprite))
            {
                return sprite;
            }
            
            Debug.LogWarning($"Sprite for TileType '{type}' not found in theme '{this.name}'.");
            return null;
        }
    }
}
