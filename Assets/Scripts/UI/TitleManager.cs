using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System;

using EasyTransition; // EasyTransition 사용

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

        [Header("Title Screen")]
        [SerializeField] private GameObject m_titlePanel;
        [SerializeField] private Button m_boardButton; // Inspector에서 이것만 할당하면 됨
        private RectTransform m_boardButtonRect; // 코드로 가져옴
        [SerializeField] private TransitionSettings m_titleTransitionSettings; // EasyTransition 설정

        [Header("UI 패널 (계층 구조)")]
        [SerializeField] private GameObject m_lobbyPanel;       // 2단계 ~ 4단계의 부모 패널
        [SerializeField] private GameObject m_boardModePanel;   // 1단계: Square / Hexagon
        [SerializeField] private GameObject m_playModePanel;    // 2단계: Stage / Infinite
        [SerializeField] private GameObject m_stageSelectPanel; // 3단계: Level List

        private const string k_GameSceneName = "Game";
        private Sequence m_titleSequence;

        private void Start()
        {
            if (m_levelDatabase == null || m_levelButtonPrefab == null || m_levelButtonContainer == null)
            {
                Debug.LogError("<b>[치명적 오류]</b> TitleManager의 필수 데이터가 할당되지 않았습니다!", this);
                return;
            }

            // 초기 상태: 타이틀 화면 표시
            ShowPanel(m_titlePanel);
            
            // 버튼 이벤트 연결 및 RectTransform 가져오기
            if (m_boardButton != null)
            {
                m_boardButtonRect = m_boardButton.GetComponent<RectTransform>();
                
                m_boardButton.onClick.RemoveAllListeners();
                m_boardButton.onClick.AddListener(OnTitleBoardClicked);
            }

            // 타이틀 애니메이션 시작
            AnimateBoardButton();
        }

        private void OnDestroy()
        {
            // DOTween 시퀀스 정리
            if (m_titleSequence != null)
            {
                m_titleSequence.Kill();
            }
        }

        #region Title Screen

        /// <summary>
        /// 보드 버튼이 3초마다 45도씩 회전하는 애니메이션을 설정합니다.
        /// </summary>
        private void AnimateBoardButton()
        {
            if (m_boardButtonRect == null) return;

            // 기존 시퀀스가 있다면 제거
            if (m_titleSequence != null) m_titleSequence.Kill();

            m_titleSequence = DOTween.Sequence();
            
            // 3초 대기 후 0.5초 동안 -90도 회전 (반복)
            // "3초마다" -> 2.5초 대기 + 0.5초 회전
            m_titleSequence.AppendInterval(2.5f);
            m_titleSequence.Append(m_boardButtonRect.DORotate(new Vector3(0, 0, -90), 0.5f, RotateMode.LocalAxisAdd).SetEase(Ease.OutBack));
            m_titleSequence.SetLoops(-1, LoopType.Incremental);
        }

        /// <summary>
        /// 보드 버튼 클릭 시 호출됩니다. 타이틀에서 로비(모드 선택)로 전환합니다.
        /// </summary>
        public void OnTitleBoardClicked()
        {
            if (m_boardButton != null) m_boardButton.interactable = false;
            
            // 회전 애니메이션 중지
            if (m_titleSequence != null) m_titleSequence.Pause();

            // 1. 버튼 클릭 피드백 (Punch Scale)
            if (m_boardButtonRect != null)
            {
                m_boardButtonRect.DOPunchScale(Vector3.one * 0.1f, 0.2f, 10, 1).OnComplete(() =>
                {
                    // 2. EasyTransition 시작
                    StartTitleTransition();
                });
            }
            else
            {
                StartTitleTransition();
            }
        }

        private void StartTitleTransition()
        {
            if (m_titleTransitionSettings == null)
            {
                Debug.LogError("[TitleManager] Transition Settings가 할당되지 않았습니다! 기본 전환을 수행합니다.");
                ShowPanel(m_boardModePanel);
                return;
            }

            // TransitionManager 인스턴스 확인
            var transitionManager = TransitionManager.Instance();
            if (transitionManager == null)
            {
                Debug.LogError("[TitleManager] 씬에 TransitionManager가 없습니다!");
                ShowPanel(m_boardModePanel);
                return;
            }

            // 이벤트 구독 (컷 포인트 도달 시 패널 교체)
            // 중복 구독 방지를 위해 먼저 제거
            transitionManager.onTransitionCutPointReached -= OnTitleTransitionCutPoint;
            transitionManager.onTransitionCutPointReached += OnTitleTransitionCutPoint;

            // 트랜지션 시작 (Scene 로드 없이 효과만 재생)
            transitionManager.Transition(m_titleTransitionSettings, 0f);
        }

        private void OnTitleTransitionCutPoint()
        {
            // 이벤트 구독 해제 (일회성)
            var transitionManager = TransitionManager.Instance();
            if (transitionManager != null)
            {
                transitionManager.onTransitionCutPointReached -= OnTitleTransitionCutPoint;
            }

            // 패널 교체: Title -> BoardMode
            if (m_titlePanel != null) m_titlePanel.SetActive(false);
            ShowPanel(m_boardModePanel);
        }

        #endregion

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
            else if (m_boardModePanel.activeSelf)
            {
                // 보드 모드에서 뒤로가기 -> 타이틀로
                StartBackToTitleTransition();
            }
        }

        private void StartBackToTitleTransition()
        {
            if (m_titleTransitionSettings == null)
            {
                // 설정 없으면 즉시 이동
                GoBackToTitleImmediate();
                return;
            }

            var transitionManager = TransitionManager.Instance();
            if (transitionManager == null)
            {
                GoBackToTitleImmediate();
                return;
            }

             transitionManager.onTransitionCutPointReached -= OnBackToTitleCutPoint;
             transitionManager.onTransitionCutPointReached += OnBackToTitleCutPoint;
             transitionManager.Transition(m_titleTransitionSettings, 0f);
        }

        private void OnBackToTitleCutPoint()
        {
            var transitionManager = TransitionManager.Instance();
            if (transitionManager != null)
            {
                transitionManager.onTransitionCutPointReached -= OnBackToTitleCutPoint;
            }

            GoBackToTitleImmediate();
        }

        private void GoBackToTitleImmediate()
        {
            ShowPanel(m_titlePanel);
            
            if (m_boardButton != null) m_boardButton.interactable = true;
            AnimateBoardButton(); // 애니메이션 재시작
        }

        private void ShowPanel(GameObject panelToShow)
        {
            if (m_titlePanel) m_titlePanel.SetActive(panelToShow == m_titlePanel);
            
            // 로비 패널 그룹(BoardMode, PlayMode, StageSelect) 중 하나가 활성화되면 로비 패널도 켭니다.
            bool isLobbySubPanel = (panelToShow == m_boardModePanel || panelToShow == m_playModePanel || panelToShow == m_stageSelectPanel);
            if (m_lobbyPanel) m_lobbyPanel.SetActive(isLobbySubPanel);

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
