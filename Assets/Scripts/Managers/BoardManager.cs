using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using GravitySpinMatch.Game;
using GravitySpinMatch.Data;

namespace GravitySpinMatch.Managers
{
    public class BoardManager : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private int m_width = 7;
        [SerializeField] private int m_height = 7;
        [SerializeField] private float m_cellSize = 1.0f;
        [SerializeField] private Block m_blockPrefab;
        [SerializeField] private ThemeData m_currentTheme;

        public void SetTheme(ThemeData newTheme)
        {
            m_currentTheme = newTheme;
            RefreshBoardVisuals();
        }

        private void RefreshBoardVisuals()
        {
            if (m_grid == null) return;

            for (int x = 0; x < m_width; x++)
            {
                for (int y = 0; y < m_height; y++)
                {
                    Block block = m_grid[x, y];
                    if (block != null)
                    {
                        // Re-initialize visual based on current TypeId
                        if (m_currentTheme != null && block.TypeId >= 0 && block.TypeId < m_currentTheme.BlockSprites.Length)
                        {
                            // We need a public way to update sprite on block, or just re-init
                            block.Initialize(block.TypeId, m_currentTheme.BlockSprites[block.TypeId]);
                        }
                    }
                }
            }
        }

        private Block[,] m_grid;
        private Transform m_boardContainer;
        private bool m_isRotating = false;

        private void OnEnable()
        {
            InputManager.OnRotateCommand += HandleRotateCommand;
        }

        private void OnDisable()
        {
            InputManager.OnRotateCommand -= HandleRotateCommand;
        }

        private void HandleRotateCommand(bool clockwise)
        {
            if (m_isRotating) return;
            
            // Check GameManager for state and moves
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentState != GameState.Playing) return;
                if (!GameManager.Instance.TryUseMove()) return;
            }
            
            var token = this.GetCancellationTokenOnDestroy();
            RotateBoardWrapper(clockwise, token).Forget();
        }

        private async UniTaskVoid RotateBoardWrapper(bool clockwise, System.Threading.CancellationToken token)
        {
            m_isRotating = true;
            await RotateBoardAsync(clockwise, token);
            m_isRotating = false;
            
            // 모든 애니메이션(매칭/중력) 종료 후 게임 오버 체크
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CheckGameOverCondition();
            }
        }

        private void Start()
        {
            // For testing, initialize on start. 
            // In a full game, GameManager would call this.
            var token = this.GetCancellationTokenOnDestroy();
            InitializeBoardAsync(token).Forget();
        }

        public async UniTask InitializeBoardAsync(System.Threading.CancellationToken token)
        {
            Debug.Log("[BoardManager] 보드 초기화 중...");

            if (m_boardContainer != null) Destroy(m_boardContainer.gameObject);
            m_boardContainer = new GameObject("BoardContainer").transform;
            m_boardContainer.SetParent(transform);

            m_grid = new Block[m_width, m_height];
            
            // 보드 중심 로직
            // (0,0)을 화면 중앙으로 가정하고 컨테이너를 오프셋합니다.
            float boardWidth = m_width * m_cellSize;
            float boardHeight = m_height * m_cellSize;
            
            // grid[0,0]이 컨테이너 기준 좌하단이 되도록 하고, 컨테이너는 중앙에 위치시킵니다.
            // 블록의 로컬 위치는 (x, y) * cellSize 입니다.
            // 따라서 그리드의 중심은 (w-1)/2, (h-1)/2 입니다.
            
            m_boardContainer.position = new Vector3(
                -(boardWidth - m_cellSize) / 2f, 
                -(boardHeight - m_cellSize) / 2f, 
                0
            );

            // 블록 생성
            for (int x = 0; x < m_width; x++)
            {
                for (int y = 0; y < m_height; y++)
                {
                    CreateBlockAt(x, y);
                }
            }
            
            Debug.Log("[BoardManager] 보드 초기화 완료.");
        }

        private void CreateBlockAt(int x, int y)
        {
            if (m_currentTheme == null || m_currentTheme.BlockSprites == null || m_currentTheme.BlockSprites.Length == 0)
            {
                Debug.LogWarning("ThemeData가 없거나 비어있습니다! 디버그용 빈 블록을 생성합니다.");
                // In real prod, handle error. For prototype, continue.
            }

            Block block = Instantiate(m_blockPrefab, m_boardContainer);
            block.transform.localPosition = new Vector3(x * m_cellSize, y * m_cellSize, 0);
            
            if (m_currentTheme != null && m_currentTheme.BlockSprites.Length > 0)
            {
                int randomType = Random.Range(0, m_currentTheme.BlockSprites.Length);
                Sprite sprite = m_currentTheme.BlockSprites[randomType];
                block.Initialize(randomType, sprite);
            }
            else
            {
                block.Initialize(-1, null);
            }

            block.name = $"Block_{x}_{y}";
            m_grid[x, y] = block;
        }

        // 보드를 90도 회전시킵니다.
        public async UniTask RotateBoardAsync(bool clockwise, System.Threading.CancellationToken token)
        {
            if (ScoreManager.Instance != null) ScoreManager.Instance.ResetCombo();

            float angle = clockwise ? -90f : 90f;

            // 1. 시각적 회전 (컨테이너)
            // 참고: 이것은 시각적인 컨테이너만 회전시킵니다.
            // 논리적 그리드(m_grid)는 x,y 그대로입니다.
            // 중력 로직은 별도로 처리해야 합니다.
            await m_boardContainer.DORotate(new Vector3(0, 0, angle), 0.5f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutBack)
                .ToUniTask(cancellationToken: token);

            // 2. 논리적 회전 (그리드 재매핑)
            RemapGrid(clockwise);

            // 3. 컨테이너 리셋 및 블록 트랜스폼 수정
            // 블록들을 분리하고 컨테이너를 리셋한 뒤, 다시 붙여서 월드 위치는 유지하되 로컬 계층을 정리합니다.
            ResetContainerTransform();

            // 4. 중력 적용 (빈 공간 채우기)
            // 참고: 꽉 찬 보드 회전 시에는 이동이 없을 수 있으나,
            // 비정형 보드나 빈 타일 시나리오를 위해 견고성을 보장합니다.
            await ApplyGravityAsync(token);

            // 5. 매칭 처리
            await ProcessMatchesAsync(token);
        }

        private void RemapGrid(bool clockwise)
        {
            Block[,] newGrid = new Block[m_width, m_height];

            for (int x = 0; x < m_width; x++)
            {
                for (int y = 0; y < m_height; y++)
                {
                    // 새로운 좌표 계산
                    // 시계: (x, y) -> (y, H - 1 - x)
                    // 반시계: (x, y) -> (W - 1 - y, x)
                    // 정사각형 그리드 가정. 직사각형일 경우 W와 H가 바뀝니다.
                    
                    int newX, newY;
                    if (clockwise)
                    {
                        newX = y;
                        newY = m_height - 1 - x;
                    }
                    else
                    {
                        newX = m_width - 1 - y;
                        newY = x;
                    }

                    // Move reference
                    newGrid[newX, newY] = m_grid[x, y];
                    
                    // Rename for debug clarity
                    if (newGrid[newX, newY] != null)
                    {
                        newGrid[newX, newY].name = $"Block_{newX}_{newY}";
                    }
                }
            }

            m_grid = newGrid;
        }

        private void ResetContainerTransform()
        {
            // 모든 블록 저장
            List<Block> blocks = new List<Block>();
            foreach (var block in m_grid)
            {
                if (block != null) blocks.Add(block);
            }

            // 월드로 분리 (World)
            foreach (var block in blocks)
            {
                block.transform.SetParent(null, true); // true = worldPositionStays
            }

            // 컨테이너 회전 리셋
            m_boardContainer.rotation = Quaternion.identity;

            // 컨테이너로 다시 붙이기
            foreach (var block in blocks)
            {
                block.transform.SetParent(m_boardContainer, true);
                
                // 부동 소수점 오차 수정을 위해 가장 가까운 셀 로컬 위치로 스냅
                Vector3 localPos = block.transform.localPosition;
                localPos.x = Mathf.Round(localPos.x / m_cellSize) * m_cellSize;
                localPos.y = Mathf.Round(localPos.y / m_cellSize) * m_cellSize;
                localPos.z = 0;
                block.transform.localPosition = localPos;
            }
        }

        public async UniTask ApplyGravityAsync(System.Threading.CancellationToken token)
        {
            bool moved = false;

            // 열(Column) 단위 처리
            for (int x = 0; x < m_width; x++)
            {
                int emptySlots = 0;
                
                // 아래에서 위로 체크
                for (int y = 0; y < m_height; y++)
                {
                    if (m_grid[x, y] == null)
                    {
                        emptySlots++;
                    }
                    else if (emptySlots > 0)
                    {
                        // 블록을 아래로 이동
                        Block block = m_grid[x, y];
                        int targetY = y - emptySlots;

                        // 그리드 업데이트
                        m_grid[x, targetY] = block;
                        m_grid[x, y] = null;

                        // 시각적 이동
                        Vector3 targetPos = new Vector3(x * m_cellSize, targetY * m_cellSize, 0);
                        // 병렬 실행을 위해 개별적으로 await하지 않고 Forget() 사용
                        block.MoveToAsync(targetPos, 0.3f, token).Forget(); 
                        
                        block.name = $"Block_{x}_{targetY}";
                        moved = true;
                    }
                }
            }

            // 블록 이동이 있었다면 애니메이션 시간만큼 대기
            if (moved)
            {
                await UniTask.Delay(300, cancellationToken: token);
            }

            // 상단 빈 공간 채우기
            await FillBoardAsync(token);
        }

        private async UniTask FillBoardAsync(System.Threading.CancellationToken token)
        {
            bool generated = false;

            for (int x = 0; x < m_width; x++)
            {
                for (int y = 0; y < m_height; y++)
                {
                    if (m_grid[x, y] == null)
                    {
                        // 상단에 새 블록 생성 (화면 밖에서 생성하는 것이 이상적이나 지금은 그냥 생성)
                        CreateBlockAt(x, y);
                        Block block = m_grid[x, y];

                        // 시작 위치 (보드 위)
                        Vector3 finalPos = block.transform.localPosition;
                        block.transform.localPosition = new Vector3(finalPos.x, m_height * m_cellSize, 0);
                        
                        block.MoveToAsync(finalPos, 0.3f, token).Forget();
                        generated = true;
                    }
                }
            }

            if (generated)
            {
                await UniTask.Delay(300, cancellationToken: token);
            }
        }
        private async UniTask ProcessMatchesAsync(System.Threading.CancellationToken token)
        {
            var matches = FindMatches();

            if (matches.Count > 0)
            {
                // 점수 업데이트
                if (ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.IncrementCombo();
                    ScoreManager.Instance.AddScore(matches.Count);
                }

                // 매칭 파괴
                List<UniTask> destroyTasks = new List<UniTask>();
                foreach (var block in matches)
                {
                    if (block != null)
                    {
                        // 그리드에서 제거
                        // 좌표 찾기 - O(N^2)이지만 N이 작음(7x7)
                        for (int x = 0; x < m_width; x++)
                        {
                            for (int y = 0; y < m_height; y++)
                            {
                                if (m_grid[x, y] == block)
                                {
                                    m_grid[x, y] = null;
                                }
                            }
                        }
                        
                        destroyTasks.Add(block.DestroyAsync(token));
                    }
                }

                await UniTask.WhenAll(destroyTasks);

                // 빈 공간을 채우기 위해 다시 중력 적용
                await ApplyGravityAsync(token);

                // 연쇄 매칭 확인
                await ProcessMatchesAsync(token);
            }
            else
            {
                // 매칭 없음. 콤보 리셋 로직이 필요할 수 있음.
                // 하지만 ProcessMatchesAsync는 재귀적입니다.
                // 초기 체크인지 재귀 체크인지 확인이 필요합니다.
                // 단순화를 위해 RotateBoardAsync 시작 부분에서 콤보를 리셋합니다.
            }
        }

        private List<Block> FindMatches()
        {
            List<Block> matchedBlocks = new List<Block>();

            // 가로 체크
            for (int y = 0; y < m_height; y++)
            {
                for (int x = 0; x < m_width - 2; x++)
                {
                    Block b1 = m_grid[x, y];
                    Block b2 = m_grid[x + 1, y];
                    Block b3 = m_grid[x + 2, y];

                    if (b1 != null && b2 != null && b3 != null)
                    {
                        if (b1.TypeId == b2.TypeId && b1.TypeId == b3.TypeId && b1.TypeId != -1)
                        {
                            if (!matchedBlocks.Contains(b1)) matchedBlocks.Add(b1);
                            if (!matchedBlocks.Contains(b2)) matchedBlocks.Add(b2);
                            if (!matchedBlocks.Contains(b3)) matchedBlocks.Add(b3);
                        }
                    }
                }
            }

            // 세로 체크
            for (int x = 0; x < m_width; x++)
            {
                for (int y = 0; y < m_height - 2; y++)
                {
                    Block b1 = m_grid[x, y];
                    Block b2 = m_grid[x, y + 1];
                    Block b3 = m_grid[x, y + 2];

                    if (b1 != null && b2 != null && b3 != null)
                    {
                        if (b1.TypeId == b2.TypeId && b1.TypeId == b3.TypeId && b1.TypeId != -1)
                        {
                            if (!matchedBlocks.Contains(b1)) matchedBlocks.Add(b1);
                            if (!matchedBlocks.Contains(b2)) matchedBlocks.Add(b2);
                            if (!matchedBlocks.Contains(b3)) matchedBlocks.Add(b3);
                        }
                    }
                }
            }

            return matchedBlocks;
        }
    }
}
