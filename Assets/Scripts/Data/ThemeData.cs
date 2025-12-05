using UnityEngine;
using UnityEngine.Serialization;

namespace GravitySpinMatch.Data
{
    [CreateAssetMenu(fileName = "NewThemeData", menuName = "GravitySpinMatch/Theme Data")]
    public class ThemeData : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField]
        private string m_themeName;
        public string ThemeName => m_themeName;

        [Header("비주얼")]
        [SerializeField]
        private Sprite m_backgroundSprite;
        public Sprite BackgroundSprite => m_backgroundSprite;

        [Tooltip("블록 타입별 스프라이트. 인덱스는 BlockType 열거형과 일치합니다.")]
        [SerializeField]
        private Sprite[] m_blockSprites;
        public Sprite[] BlockSprites => m_blockSprites;

        /// <summary>
        /// 런타임에 블록 스프라이트를 업데이트합니다.
        /// </summary>
        /// <param name="newSprites">새로운 스프라이트 배열.</param>
        public void UpdateBlockSprites(Sprite[] newSprites)
        {
            m_blockSprites = newSprites;
        }
    }
}
