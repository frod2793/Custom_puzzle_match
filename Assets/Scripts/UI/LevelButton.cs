using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Match3
{
    /// <summary>
    /// 개별 레벨 선택 버튼의 UI 및 이벤트를 관리하는 클래스입니다.
    /// TitleManager에서 동적으로 생성 및 설정됩니다.
    /// </summary>
    public class LevelButton : MonoBehaviour
    {
        [Header("UI 구성 요소")]
        [SerializeField] private TextMeshProUGUI m_levelText;
        [SerializeField] private Button m_button;

        /// <summary>
        /// 버튼의 데이터와 클릭 이벤트를 설정합니다.
        /// </summary>
        /// <param name="levelIndex">실제 데이터베이스 인덱스 (0부터 시작)</param>
        /// <param name="displayLevelNumber">화면에 표시될 레벨 번호 (1부터 시작)</param>
        /// <param name="onClick">버튼 클릭 시 호출될 콜백 (levelIndex 전달)</param>
        public void Setup(int levelIndex, int displayLevelNumber, Action<int> onClick)
        {
            if (m_levelText != null)
            {
                m_levelText.text = $"Level {displayLevelNumber}";
            }
            else
            {
                Debug.LogWarning($"[LevelButton] TextMeshProUGUI가 연결되지 않았습니다. (Level {displayLevelNumber})", this);
            }

            if (m_button != null)
            {
                // 기존 리스너 제거 후 새 리스너 등록 (재사용 시 안전장치)
                m_button.onClick.RemoveAllListeners();
                m_button.onClick.AddListener(() => onClick?.Invoke(levelIndex));
            }
            else
            {
                Debug.LogError($"[LevelButton] Button 컴포넌트가 연결되지 않았습니다. (Level {displayLevelNumber})", this);
            }
        }
    }
}
