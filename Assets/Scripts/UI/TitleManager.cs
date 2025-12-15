using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // TextMeshProUGUI 사용을 위해 추가

namespace Match3
{
    /// <summary>
    /// 타이틀 씬에서 LevelDatabase를 기반으로 레벨 선택 UI를 생성하고 씬 전환을 관리합니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("데이터베이스 및 UI 프리팹")]
        [SerializeField] private LevelDatabase m_levelDatabase;
        [SerializeField] private GameObject m_levelButtonPrefab;
        [SerializeField] private Transform m_levelButtonContainer;

        [Header("UI 패널 (계층 구조)")]
        [SerializeField] private GameObject m_boardModePanel;   // 1단계: Square / Hexagon
        [SerializeField] private GameObject m_playModePanel;    // 2단계: Stage / Infinite
        [SerializeField] private GameObject m_stageSelectPanel; // 3단계: Level List

        private const string k_GameSceneName = "Game";

        private void Start()
        {
            if (m_levelDatabase == null || m_levelButtonPrefab == null || m_levelButtonContainer == null)
            {
                Debug.LogError("<b>[치명적 오류]</b> TitleManager의 필수 데이터가 할당되지 않았습니다!", this);
                return;
            }

            // 초기 상태: 보드 모드 선택 화면 표시
            ShowPanel(m_boardModePanel);
        }

        #region 1단계: 보드 모드 선택

        public void OnSquareModeSelected()
        {
            GameSettings.CurrentBoardMode = BoardMode.Square;
            ShowPanel(m_playModePanel);
        }

        public void OnHexagonModeSelected()
        {
            GameSettings.CurrentBoardMode = BoardMode.Hexagon;
            ShowPanel(m_playModePanel);
        }

        #endregion

        #region 2단계: 플레이 모드 선택

        public void OnStageModeSelected()
        {
            GameSettings.CurrentPlayMode = PlayMode.Stage;
            PopulateLevelButtons(); // 현재 보드 모드에 맞는 레벨만 로드
            ShowPanel(m_stageSelectPanel);
        }

        public void OnInfiniteModeSelected()
        {
            GameSettings.CurrentPlayMode = PlayMode.Infinite;
            // 무한 모드는 레벨 선택 없이 바로 게임 시작 (추후 구현에 따라 로직 변경 가능)
            // 현재는 임시로 -1 또는 특정 무한 모드 전용 ID를 넘길 수도 있음
            SceneManager.LoadScene(k_GameSceneName);
        }

        #endregion

        #region 공통 UI 로직

        public void OnBackButtonPressed()
        {
            // 간단한 뒤로가기 로직 (현재 패널에 따라 상위로 이동)
            if (m_stageSelectPanel.activeSelf)
            {
                ShowPanel(m_playModePanel);
            }
            else if (m_playModePanel.activeSelf)
            {
                ShowPanel(m_boardModePanel);
            }
        }

        private void ShowPanel(GameObject panelToShow)
        {
            if (m_boardModePanel) m_boardModePanel.SetActive(panelToShow == m_boardModePanel);
            if (m_playModePanel) m_playModePanel.SetActive(panelToShow == m_playModePanel);
            if (m_stageSelectPanel) m_stageSelectPanel.SetActive(panelToShow == m_stageSelectPanel);
        }

        /// <summary>
        /// LevelDatabase의 정보를 바탕으로 레벨 선택 버튼들을 동적으로 생성합니다.
        /// 현재 선택된 BoardMode에 맞는 레벨만 필터링합니다.
        /// </summary>
        private void PopulateLevelButtons()
        {
            // 기존에 생성된 버튼이 있다면 모두 삭제
            foreach (Transform child in m_levelButtonContainer)
            {
                Destroy(child.gameObject);
            }

            GridType targetGridType = GameSettings.CurrentBoardMode == BoardMode.Hexagon 
                ? GridType.Hexagon 
                : GridType.Square;

            for (int i = 0; i < m_levelDatabase.Levels.Count; i++)
            {
                LevelData data = m_levelDatabase.Levels[i];
                
                // 필터링: 선택한 보드 타입과 일치하는 레벨만 생성
                if (data.GridType != targetGridType) continue;

                GameObject buttonGO = Instantiate(m_levelButtonPrefab, m_levelButtonContainer);
                
                // LevelButton 컴포넌트 가져오기 및 설정
                LevelButton levelButton = buttonGO.GetComponent<LevelButton>();
                if (levelButton != null)
                {
                    // 클릭 이벤트 콜백 연결
                    levelButton.Setup(i, i + 1, OnLevelButtonPressed);
                }
                else
                {
                    Debug.LogError("[TitleManager] LevelButton 프리팹에 LevelButton 스크립트가 없습니다!", m_levelButtonPrefab);
                }
            }
        }

        /// <summary>
        /// 레벨 선택 버튼이 눌렸을 때 호출될 함수입니다.
        /// </summary>
        private void OnLevelButtonPressed(int levelID)
        {
            GameSettings.SelectedLevelID = levelID;
            SceneManager.LoadScene(k_GameSceneName);
        }

        #endregion
    }
}
