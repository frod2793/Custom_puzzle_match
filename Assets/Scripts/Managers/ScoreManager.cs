using UnityEngine;
using TMPro;
using System;

namespace GravitySpinMatch.Managers
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("점수 설정")]
        [SerializeField] private int m_scorePerBlock = 100;
        [SerializeField] private float m_comboMultiplier = 1.5f;

        [Header("상태")]
        private int m_currentScore;
        private int m_currentCombo;

        public int CurrentScore => m_currentScore;

        public event Action<int> OnScoreChanged;
        public event Action<int> OnComboChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ResetScore();
        }

        public void ResetScore()
        {
            m_currentScore = 0;
            m_currentCombo = 0;
            OnScoreChanged?.Invoke(m_currentScore);
            OnComboChanged?.Invoke(m_currentCombo);
        }

        public void AddScore(int blocksDestroyed)
        {
            // Base score calculation
            int points = blocksDestroyed * m_scorePerBlock;

            // Combo bonus
            if (m_currentCombo > 0)
            {
                points = Mathf.RoundToInt(points * (1f + (m_currentCombo * 0.1f)));
            }

            m_currentScore += points;
            OnScoreChanged?.Invoke(m_currentScore);
        }

        public void IncrementCombo()
        {
            m_currentCombo++;
            OnComboChanged?.Invoke(m_currentCombo);
        }

        public void ResetCombo()
        {
            m_currentCombo = 0;
            OnComboChanged?.Invoke(m_currentCombo);
        }
    }
}
