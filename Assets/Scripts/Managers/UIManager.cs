using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GravitySpinMatch.Managers
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private Button m_importBtn;
        [SerializeField] private CustomThemeManager m_customThemeManager;
        
        [Header("점수 UI")]
        [SerializeField] private TextMeshProUGUI m_scoreText;
        [SerializeField] private TextMeshProUGUI m_comboText;
        [SerializeField] private TextMeshProUGUI m_movesText;

        [Header("패널")]
        [SerializeField] private GameObject m_gameOverPanel;
        [SerializeField] private Button m_restartBtn;
        [SerializeField] private TextMeshProUGUI m_finalScoreText;

        private void Start()
        {
            if (m_importBtn != null)
            {
                m_importBtn.onClick.AddListener(OnImportClick);
            }

            if (m_restartBtn != null)
            {
                m_restartBtn.onClick.AddListener(OnRestartClick);
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += UpdateScoreUI;
                ScoreManager.Instance.OnComboChanged += UpdateComboUI;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMovesChanged += UpdateMovesUI;
                GameManager.Instance.OnStateChanged += HandleGameStateChanged;
                
                // Init UI
                UpdateMovesUI(GameManager.Instance.MovesLeft);
                if(m_gameOverPanel != null) m_gameOverPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= UpdateScoreUI;
                ScoreManager.Instance.OnComboChanged -= UpdateComboUI;
            }
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMovesChanged -= UpdateMovesUI;
                GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
            }
        }

        private void UpdateScoreUI(int score)
        {
            if (m_scoreText != null) m_scoreText.text = $"점수: {score}";
        }

        private void UpdateComboUI(int combo)
        {
            if (m_comboText != null)
            {
                m_comboText.text = combo > 1 ? $"콤보 x{combo}!" : "";
            }
        }

        private void UpdateMovesUI(int moves)
        {
            if (m_movesText != null) m_movesText.text = $"이동: {moves}";
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                if (m_gameOverPanel != null)
                {
                    m_gameOverPanel.SetActive(true);
                    if (m_finalScoreText != null && ScoreManager.Instance != null)
                    {
                        m_finalScoreText.text = $"최종 점수: {ScoreManager.Instance.CurrentScore}";
                    }
                }
            }
            else
            {
                if (m_gameOverPanel != null) m_gameOverPanel.SetActive(false);
            }
        }

        private void OnRestartClick()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }

        private void OnImportClick()
        {
            m_customThemeManager.ImportImageAsync();
        }
    }
}
