using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using EasyTransition; // EasyTransition 사용
using EPOOutline;

namespace Match3
{
    /// <summary>
    /// 타이틀 씬에서 LevelDatabase를 기반으로 레벨 선택 UI를 생성하고 씬 전환을 관리합니다.
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("1. 필수 설정 (Database & Prefabs)")] [SerializeField]
        private LevelDatabase m_LevelDatabase;

        [SerializeField] private GameObject m_levelButtonPrefab;
        [SerializeField] private Transform m_levelButtonContainer;

        [Header("2. 타이틀 화면 (Title Screen)")] [SerializeField]
        private GameObject m_titlePanel;

        [SerializeField] private Button m_boardButton;
        private RectTransform m_boardButtonRect;
        [SerializeField] private Outlinable m_arrowOutline;
        [SerializeField] private Outlinable m_boardButtonOutline;

        [Header("공통 UI (Common)")] [SerializeField]
        private Button m_backButton;

        [SerializeField] private TransitionSettings m_titleTransitionSettings;

        [Header("3. 로비 화면 - 보드 모드 선택 (Board Mode)")] [SerializeField]
        private Button m_squareModeButton;

        [SerializeField] private Outlinable m_squareModeOutline;
        [SerializeField] private Button m_hexagonModeButton;
        [SerializeField] private Outlinable m_hexagonModeOutline;

        [Header("4. 플레이 모드 화면 - 방식 선택 (Play Mode)")] [SerializeField]
        private Button m_stageModeButton;
        [SerializeField] private Outlinable m_stageModeOutline;
        [SerializeField] private Button m_infiniteModeButton;
        [SerializeField] private Outlinable m_infiniteModeOutline;

        [Header("5. UI 패널 관리 (Panels)")] [SerializeField]
        private GameObject m_lobbyPanel;

        [SerializeField] private GameObject m_boardModePanel;
        [SerializeField] private GameObject m_playModePanel;
        [SerializeField] private GameObject m_stageSelectPanel;

        private const string k_GameSceneName = "Match3_Game";
        private Sequence m_rotationSequence;
        private Sequence m_outlineSequence;

        // 메모리 최적화: 오브젝트 풀링을 위한 리스트
        private List<LevelButton> m_pooledLevelButtons = new List<LevelButton>();

        // 캐싱 데이터
        private Dictionary<GridType, List<int>> m_cachedLevelIndices;
        private void Start()
        {
            if (m_LevelDatabase == null|| m_levelButtonPrefab == null || m_levelButtonContainer == null)
            {
                Debug.LogError("<b>[치명적 오류]</b> TitleManager의 필수 데이터가 할당되지 않았습니다!", this);
                return;
            }

            // 레벨 데이터 캐싱 (Start 시 1회 수행)
            CacheLevelData();

            // 초기 상태: 타이틀 화면 표시
            ShowPanel(m_titlePanel);

            // 버튼 이벤트 연결 및 RectTransform 가져오기
            if (m_boardButton != null)
            {
                m_boardButtonRect = m_boardButton.GetComponent<RectTransform>();

                m_boardButton.onClick.RemoveAllListeners();
                m_boardButton.onClick.AddListener(() => OnTitleBoardClicked().Forget());
            }

            // 뒤로가기 버튼 설정
            if (m_backButton != null)
            {
                m_backButton.onClick.RemoveAllListeners();
                m_backButton.onClick.AddListener(OnBackButtonPressed);
            }

            // 타이틀 애니메이션 시작
            AnimateBoardButton();

            // 모드 버튼 설정
            SetupModeButtons();
        }

        private void OnDestroy()
        {
            // DOTween 시퀀스 정리
            if (m_rotationSequence != null) m_rotationSequence.Kill();
            if (m_outlineSequence != null) m_outlineSequence.Kill();
        }

        #region Title Screen

        /// <summary>
        /// 보드 버튼이 3초마다 45도씩 회전하는 애니메이션을 설정합니다.
        /// 화살표의 아웃라인도 함께 일렁이도록(Pulse) 연출합니다.
        /// </summary>
        private void AnimateBoardButton()
        {
            if (m_boardButtonRect == null) return;

            // 기존 시퀀스 정리
            if (m_rotationSequence != null) m_rotationSequence.Kill();
            if (m_outlineSequence != null) m_outlineSequence.Kill();

            // 1. 회전 애니메이션
            m_rotationSequence = DOTween.Sequence();
            m_rotationSequence.AppendInterval(2.5f);
            m_rotationSequence.Append(m_boardButtonRect.DORotate(new Vector3(0, 0, -90), 0.5f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutBack));
            m_rotationSequence.SetLoops(-1, LoopType.Incremental);

            // 2. 아웃라인 애니메이션
            m_outlineSequence = DOTween.Sequence();

            float pulseDuration = 1.0f;

            if (m_arrowOutline != null)
            {
                SetupPulseAnimation(m_arrowOutline.OutlineParameters, pulseDuration);
            }

            if (m_boardButtonOutline != null)
            {
                SetupPulseAnimation(m_boardButtonOutline.OutlineParameters, pulseDuration);
            }

            if (m_arrowOutline != null || m_boardButtonOutline != null)
            {
                m_outlineSequence.SetLoops(-1, LoopType.Yoyo);
            }

            // 모드 버튼 아웃라인도 있으면 같이 실행
            if (m_squareModeOutline != null) SetupPulseAnimation(m_squareModeOutline.OutlineParameters, pulseDuration);
            if (m_hexagonModeOutline != null)
                SetupPulseAnimation(m_hexagonModeOutline.OutlineParameters, pulseDuration);
            if (m_stageModeOutline != null) SetupPulseAnimation(m_stageModeOutline.OutlineParameters, pulseDuration);
            if (m_infiniteModeOutline != null)
                SetupPulseAnimation(m_infiniteModeOutline.OutlineParameters, pulseDuration);
        }

        private void SetupPulseAnimation(Outlinable.OutlineProperties targetParams, float duration)
        {
            targetParams.DilateShift = 0f;
            var c = targetParams.Color;
            c.a = 0f;
            targetParams.Color = c;

            m_outlineSequence.Join(targetParams.DODilateShift(1.0f, duration).SetEase(Ease.InOutSine));
            m_outlineSequence.Join(targetParams.DOFade(1.0f, duration).SetEase(Ease.InOutSine));
        }

        /// <summary>
        /// 보드 버튼 클릭 시 호출됩니다. 타이틀에서 로비(모드 선택)로 전환합니다.
        /// </summary>
        public async UniTaskVoid OnTitleBoardClicked()
        {
            if (m_boardButton != null) m_boardButton.interactable = false;

            // 회전 애니메이션 중지
            if (m_rotationSequence != null) m_rotationSequence.Pause();
            if (m_outlineSequence != null) m_outlineSequence.Pause();

            // 1. 버튼 클릭 피드백 (Punch Scale)
            if (m_boardButtonRect != null)
            {
                // UniTask 비동기 대기 (콜백 제거)
                await m_boardButtonRect.DOPunchScale(Vector3.one * 0.1f, 0.2f, 10, 1)
                    .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

                // 2. EasyTransition 시작
                StartTitleTransition();
            }
            else
            {
                StartTitleTransition();
            }
        }

        private void PlayTransition(TransitionSettings settings, Action onCutPointReached)
        {
            if (settings == null)
            {
                onCutPointReached?.Invoke();
                return;
            }

            var transitionManager = TransitionManager.Instance();
            if (transitionManager == null)
            {
                onCutPointReached?.Invoke();
                return;
            }

            // 이벤트 핸들러 래핑 (일회성 구독)
            void HandleCutPoint()
            {
                transitionManager.onTransitionCutPointReached -= HandleCutPoint;
                onCutPointReached?.Invoke();
            }

            transitionManager.onTransitionCutPointReached += HandleCutPoint;
            transitionManager.Transition(settings, 0f);
        }

        private void StartTitleTransition()
        {
            // 공통 헬퍼 메서드 사용 (중복 제거)
            PlayTransition(m_titleTransitionSettings, () =>
            {
                if (m_titlePanel != null) m_titlePanel.SetActive(false);
                ShowPanel(m_boardModePanel);
            });
        }

        private void StartBackToTitleTransition()
        {
            // 공통 헬퍼 메서드 사용 (중복 제거)
            PlayTransition(m_titleTransitionSettings, GoBackToTitleImmediate);
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

        private void SetupModeButtons()
        {
            if (m_squareModeButton != null)
            {
                m_squareModeButton.onClick.RemoveAllListeners();
                m_squareModeButton.onClick.AddListener(OnSquareModeSelected);
            }

            if (m_hexagonModeButton != null)
            {
                m_hexagonModeButton.onClick.RemoveAllListeners();
                m_hexagonModeButton.onClick.AddListener(OnHexagonModeSelected);
            }

            if (m_stageModeButton != null)
            {
                m_stageModeButton.onClick.RemoveAllListeners();
                m_stageModeButton.onClick.AddListener(OnStageModeSelected);
            }

            if (m_infiniteModeButton != null)
            {
                m_infiniteModeButton.onClick.RemoveAllListeners();
                m_infiniteModeButton.onClick.AddListener(OnInfiniteModeSelected);
            }
        }

        #endregion

        #region 2단계: 플레이 모드 선택

        public void OnStageModeSelected()
        {
            GameSettings.CurrentPlayMode = PlayMode.Stage;

            PopulateLevelButtons(); // 현재 보드 모드에 맞는 레벨만 로드
            ShowPanel(m_stageSelectPanel);
        }

        /// <summary>
        /// 레벨 데이터를 GridType별로 분류하여 캐싱합니다.
        /// </summary>
        private void CacheLevelData()
        {

            m_cachedLevelIndices = new Dictionary<GridType, List<int>>();

            // 1. Square Levels 캐싱
            List<int> squareIndices = new List<int>();
            for (int i = 0; i < m_LevelDatabase.Squarelevels.Count; i++)
            {
                squareIndices.Add(i);
            }
            m_cachedLevelIndices[GridType.Square] = squareIndices;

            // 2. Hexagon Levels 캐싱
            List<int> hexagonIndices = new List<int>();
            for (int i = 0; i < m_LevelDatabase.HaxagonLevels.Count; i++)
            {
                hexagonIndices.Add(i);
            }
            m_cachedLevelIndices[GridType.Hexagon] = hexagonIndices;
        }

        public void OnInfiniteModeSelected()
        {
            GameSettings.CurrentPlayMode = PlayMode.Infinite;
            // 무한 모드는 레벨 선택 없이 바로 게임 시작
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


        private void GoBackToTitleImmediate()
        {
            ShowPanel(m_titlePanel);

            if (m_boardButton != null) m_boardButton.interactable = true;
            AnimateBoardButton(); // 애니메이션 재시작
        }

        private void ShowPanel(GameObject panelToShow)
        {
            if (m_titlePanel)
            {
                m_titlePanel.SetActive(panelToShow == m_titlePanel);
            }

            if (m_backButton != null)
            {
                // 타이틀 패널이 켜져있으면 뒤로가기 버튼 숨김, 그 외에는 표시
                m_backButton.gameObject.SetActive(panelToShow != m_titlePanel);
            }

            // 로비(BoardMode)만 로비 패널로 간주하고, 플레이 모드/스테이지 선택은 별도로 처리
            bool isLobbySubPanel = (panelToShow == m_boardModePanel);
            if (m_lobbyPanel) m_lobbyPanel.SetActive(isLobbySubPanel);

            if (m_boardModePanel) m_boardModePanel.SetActive(panelToShow == m_boardModePanel);
            if (m_playModePanel) m_playModePanel.SetActive(panelToShow == m_playModePanel);
            if (m_stageSelectPanel) m_stageSelectPanel.SetActive(panelToShow == m_stageSelectPanel);
        }

        /// <summary>
        /// LevelDatabase의 정보를 바탕으로 레벨 선택 버튼들을 동적으로 생성합니다.
        /// (최적화) 오브젝트 풀링을 적용하여 Destroy/Instantiate 반복을 방지합니다.
        /// </summary>
        private void PopulateLevelButtons()
        {
            // 1. 기존 활성 버튼 모두 비활성화 (Pool 반환 효과)
            foreach (var btn in m_pooledLevelButtons)
            {
                if (btn != null) btn.gameObject.SetActive(false);
            }

            GridType targetGridType = GameSettings.CurrentBoardMode == BoardMode.Hexagon 
                ? GridType.Hexagon 
                : GridType.Square;

            // 캐싱된 데이터 사용 (검색 속도 O(1))
            if (m_cachedLevelIndices == null) CacheLevelData(); // 안전장치

            if (m_cachedLevelIndices.TryGetValue(targetGridType, out List<int> levelIndices))
            {
                int activeCount = 0;

                // 해당 타입의 레벨만 순회
                foreach (int levelIndex in levelIndices)
                {
                    LevelButton levelButton;

                    if (activeCount < m_pooledLevelButtons.Count)
                    {
                        levelButton = m_pooledLevelButtons[activeCount];
                        levelButton.gameObject.SetActive(true);
                    }
                    else
                    {
                        GameObject buttonGO = Instantiate(m_levelButtonPrefab, m_levelButtonContainer);
                        levelButton = buttonGO.GetComponent<LevelButton>();

                        if (levelButton != null)
                        {
                            m_pooledLevelButtons.Add(levelButton);
                        }
                        else
                        {
                            activeCount++; 
                            continue;
                        }
                    }

                    levelButton.Setup(levelIndex, levelIndex + 1, OnLevelButtonPressed);
                    activeCount++;
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