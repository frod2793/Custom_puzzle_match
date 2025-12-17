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
        private int m_levelIndex;
        private Action<int> m_onClickAction;

        private void Awake()
        {
            if (m_button != null)
            {
                // 한 번만 리스너 등록 (람다 캡처 제거)
                m_button.onClick.AddListener(OnButtonClicked);
            }
        }

        /// <summary>
        /// 버튼의 데이터와 클릭 이벤트를 설정합니다.
        /// (메모리 최적화) 람다 대신 캐싱된 액션 사용
        /// </summary>
        /// <param name="levelIndex">실제 데이터베이스 인덱스</param>
        /// <param name="displayLevelNumber">화면에 표시될 레벨 번호</param>
        /// <param name="onClick">버튼 클릭 시 호출될 콜백</param>
        public void Setup(int levelIndex, int displayLevelNumber, Action<int> onClick)
        {
            m_levelIndex = levelIndex;
            m_onClickAction = onClick;

            if (m_levelText != null)
            {
                 // (선택) 스트링 할당 최적화 필요시 StringBuilder 사용 가능하나, 숫자는 매번 바뀌므로 포맷팅 불가피
                m_levelText.text = $"Level {displayLevelNumber}";
            }
        }

        private void OnButtonClicked()
        {
            m_onClickAction?.Invoke(m_levelIndex);
        }
    }
}
