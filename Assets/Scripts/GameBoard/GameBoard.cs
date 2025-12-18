using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using System.Text;
using DG.Tweening;
using Match3.UI;
using Match3.Effects;
using System;
using System.Threading;

#if UNITY_EDITOR
using UnityEditor; 
#endif

namespace Match3
{
    public class GameBoard : MonoBehaviour
    {
        public enum BoardRotation { Up = 0, Right = 1, Down = 2, Left = 3 }

        [Header("데이터")]
        [SerializeField] private LevelDatabase m_levelDatabase;
        [SerializeField] private TileThemeData m_tileTheme;

        [Header("메인 그리드 (필수)")] 
        [SerializeField] private Grid m_mainGrid;
        
        [Header("그리드 컨테이너")]
        [SerializeField] private GameObject m_squareGridContainer;
        [SerializeField] private GameObject m_hexagonGridContainer;
        
        [Header("타일 프리팹")]
        [SerializeField] private GameObject m_tilePrefab;

        [Header("설정")]
        [Tooltip("리필 시 타일이 낙하하는 속도 (Units per Second).")]
        [SerializeField] private float m_refillMoveSpeed = 15.0f; // Renamed from m_fallSpeed

        [Tooltip("낙하 시 웨이브 효과를 주기 위한 지연 시간 계수")]
        [SerializeField] private float m_fallStaggerDelay = 0.08f; 

        [Header("애니메이션 상세 설정")]
        [Tooltip("일반 낙하(Collapse) 시 타일 이동 속도 (Units per Second).")]
        [SerializeField] private float m_gravityMoveSpeed = 9.0f; // Replaced m_collapseStepDuration

        [Tooltip("리필 시 높이에 따른 지연 가중치. 클수록 아래쪽이 먼저 차오르는 느낌이 강해집니다.")]
        [SerializeField] private float m_refillHeightDelayFactor = 0.2f;

        [Tooltip("리필 시 추가되는 무작위 지연 시간의 최대값 (초)")]
        [SerializeField] private float m_refillRandomFactor = 0.25f; 

        private LevelData m_currentLevelData;
        private Transform m_gridTransform;
        private Transform m_tileContainer;
        
        private BoardRotation m_currentRotation = BoardRotation.Up;
        private bool m_isWaitingForRefill;
        private bool m_isProcessingMove;
        
        private Tile m_selectedTile;
        private Vector2 m_swipeStartPosition;
        private const float k_MinSwipeDistancePixels = 40f;

        private IGridManager m_gridManager;
        private TileFactory m_tileFactory;
        
        // 타일 관리: Grid 좌표 -> 타일 객체
        private readonly Dictionary<Vector2Int, Tile> m_tileObjects = new Dictionary<Vector2Int, Tile>();

        // 인접 타일 사전 검사 저장소 (Adjacency Graph)
        private Dictionary<Vector2Int, List<Vector2Int>> m_adjacencyGraph = new Dictionary<Vector2Int, List<Vector2Int>>();

        private Camera m_mainCamera;
        private Action m_onRotateButtonPressedAction;
        
        // 최적화용 캐시 필드 (Zero Allocation)
        private List<Vector2Int> m_sortedTilePositionsByY; // Collapse용 미리 정렬된 좌표 리스트
        private readonly List<UniTask> m_cachedMoveTasks = new List<UniTask>(64);
        private readonly HashSet<Vector2Int> m_cachedDestinationLocked = new HashSet<Vector2Int>();
        private readonly Dictionary<Tile, Vector2Int> m_cachedPendingMoves = new Dictionary<Tile, Vector2Int>();
        private readonly List<Vector2Int> m_cachedEmptyPositions = new List<Vector2Int>(64);
        private readonly HashSet<Tile> m_cachedMatchSet = new HashSet<Tile>();
        private readonly List<Vector3> m_cachedMatchDirections = new List<Vector3>(6); // Hexagon 고려하여 넉넉히
        
        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;
            InitializeBoardAndLevelData();
            InitializeGrid();
            InitializeOptimizationCache(); // [수정] Grid 생성 후 캐시 초기화 (NRE 방지)
            
            m_onRotateButtonPressedAction = () => RotateBoard().Forget();
        }

        private void Start()
        {
            if (m_currentLevelData == null || m_tileTheme == null || m_tilePrefab == null || m_gridManager == null)
            {
                Debug.LogError("<b>[치명적 오류]</b> GameBoard 데이터 누락.", this);
                this.enabled = false;
                return;
            }

            m_tileFactory = new TileFactory(m_tilePrefab, m_tileContainer, m_gridManager.CellSize, m_tileTheme, m_currentRotation);
            
            CreateTiles();
            AnalyzeBoardStructure();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnRotateButtonPressed += m_onRotateButtonPressedAction;
                UIManager.Instance.OnRefillButtonPressed += OnRefillButtonPressed;
            }
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnRotateButtonPressed -= m_onRotateButtonPressedAction;
                UIManager.Instance.OnRefillButtonPressed -= OnRefillButtonPressed;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                PrintBoardState();
            }
#endif
            HandleInput();
        }

        #endregion

        #region 초기화 및 구조 분석

        private void InitializeBoardAndLevelData()
        {
            if (GameSettings.CurrentPlayMode == PlayMode.Infinite)
            {
                // [무한 모드] 런타임 생성
                m_currentLevelData = GenerateInfiniteLevelData(GameSettings.CurrentBoardMode);
            }
            else
            {
                // [스테이지 모드] DB 로드
                int levelID = GameSettings.SelectedLevelID < 0 ? 0 : GameSettings.SelectedLevelID;
                m_currentLevelData = m_levelDatabase != null ? m_levelDatabase.GetLevel(levelID) : null;
            }

            if (m_currentLevelData == null) return;
            
            
            // [제거] 여기서 GetWorldPositionFromGrid 호출 시 m_gridManager가 없어서 NRE 발생함.
            // InitializeOptimizationCache()로 이동.

            if (m_mainGrid != null)
            {
                if (m_currentLevelData.GridType == GridType.Hexagon)
                {
                    m_mainGrid.cellLayout = GridLayout.CellLayout.Hexagon;
                }
                else
                {
                    m_mainGrid.cellLayout = GridLayout.CellLayout.Rectangle;
                }
            }

            bool isSquare = m_currentLevelData.GridType == GridType.Square;
            if (m_squareGridContainer) m_squareGridContainer.SetActive(isSquare);
            if (m_hexagonGridContainer) m_hexagonGridContainer.SetActive(!isSquare);

            GameObject activeContainer = isSquare ? m_squareGridContainer : m_hexagonGridContainer;
            if (activeContainer)
            {
                m_gridTransform = activeContainer.transform;
                m_tileContainer = activeContainer.transform;
            }
        }

        private LevelData GenerateInfiniteLevelData(BoardMode boardMode)
        {
            LevelData newLevel = ScriptableObject.CreateInstance<LevelData>();
            List<Vector2Int> positions = new List<Vector2Int>();
            GridType gridType;
            Vector2Int gridSize;

            if (boardMode == BoardMode.Hexagon)
            {
                gridType = GridType.Hexagon;
                // 육각형: 반지름 4 (q, r 좌표계)
                int radius = 4;
                for (int q = -radius; q <= radius; q++)
                {
                    int r1 = Mathf.Max(-radius, -q - radius);
                    int r2 = Mathf.Min(radius, -q + radius);
                    for (int r = r1; r <= r2; r++)
                    {
                        positions.Add(new Vector2Int(q, r));
                    }
                }
                gridSize = new Vector2Int(radius * 2 + 1, radius * 2 + 1);
            }
            else
            {
                gridType = GridType.Square;
                // 사각형: 9x9 (-4~4)
                int ext = 4; 
                for (int x = -ext; x <= ext; x++)
                {
                    for (int y = -ext; y <= ext; y++)
                    {
                        positions.Add(new Vector2Int(x, y));
                    }
                }
                gridSize = new Vector2Int(9, 9);
            }

            newLevel.SetupRuntimeLevel(gridType, gridSize, positions);
            newLevel.name = "Infinite_Procedural_Level";
            return newLevel;
        }

        private void InitializeOptimizationCache()
        {
            if (m_currentLevelData == null) return;

            // [최적화] Y축 기준 정렬 리스트 미리 생성 (Bottom-Up)
            // m_gridManager가 초기화된 이후에 호출되어야 함
            m_sortedTilePositionsByY = m_currentLevelData.TilePositions
                .OrderBy(p => GetWorldPositionFromGrid(p.x, p.y).y)
                .ToList();
            
            // [최적화] 매치 방향 캐싱 (초기 1회)
            CacheMatchDirections();
        }

        // [최적화] 매치 방향 미리 계산
        private void CacheMatchDirections()
        {
            m_cachedMatchDirections.Clear();
            if (m_currentLevelData.GridType == GridType.Hexagon)
            {
                Vector3[] localDirs = new Vector3[] 
                { 
                    Vector3.up,                                  
                    Quaternion.Euler(0,0,-60) * Vector3.up,      
                    Quaternion.Euler(0,0,60) * Vector3.up        
                };
                foreach(var ld in localDirs) m_cachedMatchDirections.Add(m_gridTransform.TransformDirection(ld));
            }
            else 
            {
                m_cachedMatchDirections.Add(m_gridTransform.TransformDirection(Vector3.right));
                m_cachedMatchDirections.Add(m_gridTransform.TransformDirection(Vector3.up));
            }
        }

        private void InitializeGrid()
        {
            if (m_currentLevelData == null) return;
            m_gridManager = m_currentLevelData.GridType == GridType.Hexagon ? new HexGridManager() : new SquareGridManager();
            m_gridManager.Initialize(m_currentLevelData, m_mainGrid);
        }

        private void CreateTiles()
        {
            m_tileObjects.Clear(); 
            
            // 기존 타일 초기화 로직 유지
            foreach (var pos in m_currentLevelData.TilePositions)
            {
                if (m_tileObjects.ContainsKey(pos)) continue;

                Vector3 worldPos = GetWorldPositionFromGrid(pos.x, pos.y);
                TileType newType = GetRandomTileTypeAvoidingInitialMatch(pos);
                Tile newTile = m_tileFactory.Get(worldPos, pos, newType);
                if (newTile != null) m_tileObjects[pos] = newTile;
            }
        }

        private void AnalyzeBoardStructure()
        {
            m_adjacencyGraph.Clear();

            var allPositions = m_currentLevelData.TilePositions;
            if (allPositions.Count == 0) return;

            float neighborDistanceThreshold = m_gridManager.CellSize * 1.15f; 

            foreach (var pos in allPositions)
            {
                m_adjacencyGraph[pos] = new List<Vector2Int>();
                Vector3 myWorldPos = GetWorldPositionFromGrid(pos.x, pos.y);

                foreach (var neighborPos in allPositions)
                {
                    if (pos == neighborPos) continue;

                    Vector3 neighborWorldPos = GetWorldPositionFromGrid(neighborPos.x, neighborPos.y);
                    if (Vector3.Distance(myWorldPos, neighborWorldPos) <= neighborDistanceThreshold)
                    {
                        m_adjacencyGraph[pos].Add(neighborPos);
                    }
                }
            }
        }

        #endregion
        
        #region 입력 처리

        private void HandleInput()
        {
            if (m_mainCamera == null || Pointer.current == null) return;

            if (Pointer.current.press.wasPressedThisFrame)
            {
                if (m_isProcessingMove && !m_isWaitingForRefill) return;

                Tile hitTile = GetTileUnderPointer();
                if (hitTile != null)
                {
                    m_selectedTile = hitTile;
                    m_swipeStartPosition = Pointer.current.position.ReadValue();
                    m_selectedTile.Select();
                }
            }

            if (Pointer.current.press.wasReleasedThisFrame)
            {
                if (m_selectedTile != null)
                {
                    m_selectedTile.Deselect();
                    
                    if (m_isProcessingMove && !m_isWaitingForRefill)
                    {
                        m_selectedTile = null;
                        return;
                    }

                    Vector2 currentPos = Pointer.current.position.ReadValue();
                    Vector2 swipeVector = currentPos - m_swipeStartPosition;

                    if (swipeVector.magnitude > k_MinSwipeDistancePixels)
                    {
                        Tile neighborTile = FindPrecalculatedNeighbor(m_selectedTile, swipeVector);

                        if (neighborTile != null)
                        {
                            SwapAndProcessMatchesAsync(m_selectedTile, neighborTile).Forget();
                        }
                    }
                    m_selectedTile = null;
                }
            }
        }

        private Tile FindPrecalculatedNeighbor(Tile startTile, Vector2 swipeVector)
        {
            if (!m_adjacencyGraph.TryGetValue(startTile.GridPosition, out var neighbors))
                return null;

            Vector2 normalizedSwipe = swipeVector.normalized;
            Tile bestMatch = null;
            float maxDot = 0.5f;

            // 스왑 대상 검색도 논리적 위치 기반으로 변경
            Vector3 startPos = GetWorldPositionFromGrid(startTile.GridPosition.x, startTile.GridPosition.y);

            foreach (var neighborPos in neighbors)
            {
                Tile neighborTile = GetTileAt(neighborPos);
                if (neighborTile == null) continue;

                Vector3 neighborPosWorld = GetWorldPositionFromGrid(neighborPos.x, neighborPos.y);
                Vector3 directionToNeighbor = (neighborPosWorld - startPos).normalized;
                
                float dot = Vector2.Dot(normalizedSwipe, new Vector2(directionToNeighbor.x, directionToNeighbor.y).normalized);

                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestMatch = neighborTile;
                }
            }
            return bestMatch;
        }

        private Tile GetTileUnderPointer()
        {
            Vector2 screenPoint = Pointer.current.position.ReadValue();
            Vector3 worldPoint = m_mainCamera.ScreenToWorldPoint(screenPoint);
            
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);
            
            if (hit.collider != null)
            {
                var tile = hit.collider.GetComponent<Tile>();
                if (tile != null && tile.gameObject.activeInHierarchy) return tile;
            }
            return null;
        }

        #endregion

        #region 게임 로직 (매치 & 스왑)

        private async UniTaskVoid SwapAndProcessMatchesAsync(Tile tileA, Tile tileB)
        {
            m_isProcessingMove = true;
            m_isWaitingForRefill = false;

            await SwapTilesAsync(tileA, tileB);

            var matches = FindAllMatchesGraphBased();
            
            if (matches.Count > 0)
            {
                await ProcessMatchesLoopAsync(matches);
            }
            else
            {
                await UniTask.Delay(50);
                await SwapTilesAsync(tileB, tileA); 
            }
            m_isProcessingMove = false;
        }

        private async UniTask SwapTilesAsync(Tile tileA, Tile tileB)
        {
            Vector2Int gridPosA = tileA.GridPosition;
            Vector2Int gridPosB = tileB.GridPosition;

            Vector3 targetWorldPosA = GetWorldPositionFromGrid(gridPosB.x, gridPosB.y);
            Vector3 targetWorldPosB = GetWorldPositionFromGrid(gridPosA.x, gridPosA.y);

            var taskA = tileA.transform.DOMove(targetWorldPosA, 0.25f).SetEase(Ease.OutQuad).ToUniTask();
            var taskB = tileB.transform.DOMove(targetWorldPosB, 0.25f).SetEase(Ease.OutQuad).ToUniTask();
            await UniTask.WhenAll(taskA, taskB);

            if (m_tileObjects.ContainsKey(gridPosA)) m_tileObjects[gridPosA] = tileB;
            if (m_tileObjects.ContainsKey(gridPosB)) m_tileObjects[gridPosB] = tileA;
            
            tileA.SetGridPosition(gridPosB);
            tileB.SetGridPosition(gridPosA);
        }

        private async UniTask ProcessMatchesLoopAsync(List<Tile> initialMatches)
        {
            var matchesToProcess = new List<Tile>(initialMatches);
            
            int safetyCounter = 0;
            const int k_MaxCascades = 20;

            while (matchesToProcess.Count > 0)
            {
                if (safetyCounter++ > k_MaxCascades)
                {
                    Debug.LogWarning("[GameBoard] 무한 연쇄 매치 감지로 인한 강제 중단");
                    break;
                }

                await ClearTilesAsync(matchesToProcess);
                
                await CollapseGapsGraphBasedAsync(); 
                
                matchesToProcess = FindAllMatchesGraphBased();
                
                if (matchesToProcess.Count > 0)
                {
                    Debug.Log($"[ProcessMatches] 낙하 후 {matchesToProcess.Count}개의 타일이 매치됨. 연쇄 반응 시작.");
                }
            }
            m_isWaitingForRefill = true;
        }

        private async UniTask ClearTilesAsync(List<Tile> tilesToClear)
        {
            var clearTasks = new List<UniTask>();
            foreach (var tile in tilesToClear)
            {
                if (tile == null) continue;
                
                if (m_tileObjects.ContainsKey(tile.GridPosition))
                {
                    // 이펙트 재생 (타일 색상 반영)
                    if (EffectManager.Instance != null)
                    {
                        EffectManager.Instance.PlayEffect(EffectType.TileMatch, tile.transform.position, tile.CurrentColor);
                    }

                    m_tileObjects.Remove(tile.GridPosition);
                    clearTasks.Add(tile.ClearAsync().ContinueWith(() => m_tileFactory.Release(tile)));
                }
            }
            await UniTask.WhenAll(clearTasks);
        }

        #endregion

        #region 낙하 로직 (Step-by-Step Cascade)

        private async UniTask CollapseGapsGraphBasedAsync()
        {
            bool moved;
            // 스텝별 이동 시간 계산 (거리 1 / 속도)
            // m_gravityMoveSpeed가 클수록 stepDuration은 작아짐 (빠름)
            float stepDuration = m_gravityMoveSpeed > 0 ? 1.0f / m_gravityMoveSpeed : 0.1f;

            do
            {
                moved = false;
                
                // [최적화] 컬렉션 재사용 (Clear)
                m_cachedMoveTasks.Clear();
                m_cachedDestinationLocked.Clear();
                m_cachedPendingMoves.Clear();
                
                // [최적화] Y축 정렬은 미리 계산된 리스트 사용 (m_sortedTilePositionsByY)
                // 만약 레벨 데이터가 동적으로 바뀐다면 여기서 다시 정렬해야 하지만, 정적이라면 캐시 사용 가능.
                // 안전을 위해 null 체크
                var sortedSlots = m_sortedTilePositionsByY ?? m_currentLevelData.TilePositions;

                foreach (var slotGridPos in sortedSlots)
                {
                    // 1. 이미 채워져 있는 칸은 패스
                    if (m_tileObjects.ContainsKey(slotGridPos)) continue;
                    
                    // 2. 이미 이번 턴에 누군가가 오기로 예약된 칸도 패스
                    if (m_cachedDestinationLocked.Contains(slotGridPos)) continue;

                    // 3. 이 칸을 채워줄 수 있는 '인접한' 공급자 찾기
                    Tile supplier = GetBestSupplierForSlot(slotGridPos, GetWorldPositionFromGrid(slotGridPos.x, slotGridPos.y));

                    if (supplier != null)
                    {
                        // 4. 공급자가 이미 다른 곳으로 가기로 했으면 패스
                        if (m_cachedPendingMoves.ContainsKey(supplier)) continue;

                        // 이동 예약
                        m_cachedPendingMoves[supplier] = slotGridPos;
                        m_cachedDestinationLocked.Add(slotGridPos); // 목적지 잠금
                    }
                }

                // 실제 데이터 갱신 및 애니메이션 시작
                if (m_cachedPendingMoves.Count > 0)
                {
                    moved = true;

                    foreach (var kvp in m_cachedPendingMoves)
                    {
                        Tile tile = kvp.Key;
                        Vector2Int newPos = kvp.Value;
                        Vector2Int oldPos = tile.GridPosition;

                        // 논리적 위치 이동
                        m_tileObjects.Remove(oldPos);
                        m_tileObjects[newPos] = tile;
                        tile.SetGridPosition(newPos);

                        // 시각적 이동 (한 칸 이동)
                        Vector3 targetWorldPos = GetWorldPositionFromGrid(newPos.x, newPos.y);
                        // [수정] Linear 이동 (Gravity Speed 적용)
                        m_cachedMoveTasks.Add(tile.transform.DOMove(targetWorldPos, stepDuration).SetEase(Ease.Linear).ToUniTask());
                    }

                    // 모든 타일이 한 칸씩 움직일 때까지 대기
                    await UniTask.WhenAll(m_cachedMoveTasks);
                }

            } while (moved); // 더 이상 움직일 타일이 없을 때까지 반복
        }

        private Tile GetBestSupplierForSlot(Vector2Int targetGridPos, Vector3 targetWorldPos)
        {
            if (!m_adjacencyGraph.TryGetValue(targetGridPos, out var neighbors)) return null;

            Tile bestCandidate = null;
            float bestScore = float.MinValue;
            Vector3 upDir = Vector3.up; 

            foreach (var neighborPos in neighbors)
            {
                // 빈 칸이나, 이미 이동 중인 칸은 공급자가 될 수 없음
                // (하지만 여기서는 m_tileObjects만 보므로, 루프 내 pendingMoves 체크가 중요)
                if (!m_tileObjects.TryGetValue(neighborPos, out Tile candidateTile)) continue;

                Vector3 candidatePos = GetWorldPositionFromGrid(neighborPos.x, neighborPos.y); 
                Vector3 vectorToCandidate = candidatePos - targetWorldPos;
                Vector3 dirToCandidate = vectorToCandidate.normalized;
                float distance = vectorToCandidate.magnitude;

                float dot = Vector3.Dot(dirToCandidate, upDir);

                // [조건] 낙하 허용 각도 (0.8 이상) - 수직 혹은 그에 준하는 위쪽
                if (dot > 0.8f) 
                {
                    // 점수 = (내적 * 10) - 거리
                    // 거리가 가까울수록(작을수록) 유리. 바로 위(거리1)가 저 위(거리2)보다 우선됨.
                    float score = (dot * 10f) - distance;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCandidate = candidateTile;
                    }
                }
            }

            return bestCandidate;
        }

        #endregion

        #region 리필 로직

        private void OnRefillButtonPressed()
        {
            if (m_isProcessingMove && !m_isWaitingForRefill) return;
            RefillAndCheckCascadesAsync().Forget();
        }

        private async UniTaskVoid RefillAndCheckCascadesAsync()
        {
            m_isProcessingMove = true;
            m_isWaitingForRefill = false;

            await RefillBoardAsync();
            var newMatches = FindAllMatchesGraphBased();
            
            if (newMatches.Count > 0)
            {
                Debug.Log($"[Refill] 리필 후 {newMatches.Count}개의 매치 발견.");
                await ProcessMatchesLoopAsync(newMatches);
            }
            m_isProcessingMove = false;
        }

        private async UniTask RefillBoardAsync()
        {
            var refillTasks = new List<UniTask>();
            var ct = this.GetCancellationTokenOnDestroy();

            // [최적화] LINQ Where 제거 및 cached list 사용
            m_cachedEmptyPositions.Clear();
            foreach(var p in m_currentLevelData.TilePositions)
            {
                if (!m_tileObjects.ContainsKey(p))
                {
                    m_cachedEmptyPositions.Add(p);
                }
            }

            if (m_cachedEmptyPositions.Count == 0) return;

            float minY = m_currentLevelData.TilePositions.Min(p => GetWorldPositionFromGrid(p.x, p.y).y);
            float cameraTopY = m_mainCamera.transform.position.y + m_mainCamera.orthographicSize;
            float spawnOffset = m_gridManager.CellSize * 2f;

            foreach (var gridPos in m_cachedEmptyPositions)
            {
                if (m_tileObjects.ContainsKey(gridPos)) continue;

                Vector3 targetWorldPos = GetWorldPositionFromGrid(gridPos.x, gridPos.y);
                Vector3 startPos = new Vector3(targetWorldPos.x, cameraTopY + spawnOffset, 0);

                var newType = GetRandomTileTypeAvoidingInitialMatch(gridPos);
                var newTile = m_tileFactory.Get(startPos, gridPos, newType);
                if (newTile == null) continue;

                m_tileObjects[gridPos] = newTile; 
                
                // [수정] Bottom-Up 채우기 구현
                float heightFactor = (targetWorldPos.y - minY) * m_refillHeightDelayFactor; 
                float randomNoise = UnityEngine.Random.Range(0.0f, m_refillRandomFactor);
                float finalDelay = heightFactor + randomNoise;

                refillTasks.Add(AnimateTileMove(newTile, targetWorldPos, finalDelay, Ease.OutBounce));
            }
            await UniTask.WhenAll(refillTasks);
        }

        private async UniTask AnimateTileMove(Tile tile, Vector3 targetPos, float delay, Ease easeType)
        {
            var ct = this.GetCancellationTokenOnDestroy();
            if (delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;
            
            float distance = Vector3.Distance(tile.transform.position, targetPos);
            // [수정] m_refillMoveSpeed 사용 (리필 전용 속도)
            float duration = m_refillMoveSpeed > 0 ? distance / m_refillMoveSpeed : 0.1f;

            await tile.transform.DOMove(targetPos, duration)
                .SetEase(easeType)
                .ToUniTask(cancellationToken: ct);
        }

        #endregion

        #region 매치 찾기 (Graph Based - Reinforced)

        private List<Tile> FindAllMatchesGraphBased()
        {
            // [최적화] 캐시된 HashSet 재사용
            m_cachedMatchSet.Clear();

            // [최적화] 매치 방향은 Awake/Rotate 시점에 계산된 m_cachedMatchDirections 사용

            foreach (var kvp in m_tileObjects)
            {
                Tile startTile = kvp.Value;
                if (startTile == null) continue;

                foreach (var dir in m_cachedMatchDirections)
                {
                    List<Tile> line = GetMatchLine(startTile, dir);
                    if (line.Count >= 3)
                    {
                        foreach (var t in line) m_cachedMatchSet.Add(t);
                    }
                }
            }
            // 반환 타입 호환성을 위해 ToList() 사용 (필요시 리턴 타입도 void로 바꾸고 내부 처리가능)
            return m_cachedMatchSet.ToList();
        }

        private List<Tile> GetMatchLine(Tile startTile, Vector3 worldDir)
        {
            var line = new List<Tile> { startTile };
            TileType type = startTile.Type;

            CheckDirection(startTile, worldDir, type, line);
            CheckDirection(startTile, -worldDir, type, line);

            return line;
        }

        private void CheckDirection(Tile startTile, Vector3 worldDir, TileType type, List<Tile> resultList)
        {
            Tile current = startTile;
            Vector3 probeDir = worldDir.normalized;
            
            for(int i=0; i<9; i++)
            {
                Tile next = FindNeighborInDirectionGraph(current, probeDir);
                if (next != null && next.Type == type && !resultList.Contains(next))
                {
                    resultList.Add(next);
                    current = next;
                }
                else break;
            }
        }

        private Tile FindNeighborInDirectionGraph(Tile origin, Vector3 dir)
        {
            if (!m_adjacencyGraph.TryGetValue(origin.GridPosition, out var neighbors)) return null;

            Tile best = null;
            // [수정] 매치 판정 기준 강화: 0.85 -> 0.98
            // 거의 완벽하게 일직선상에 있는 타일만 이웃으로 인정하여 오판정 방지
            float maxDot = 0.98f; 

            Vector3 originPos = GetWorldPositionFromGrid(origin.GridPosition.x, origin.GridPosition.y);

            foreach(var nPos in neighbors)
            {
                Tile t = GetTileAt(nPos);
                if (t == null) continue;

                Vector3 targetPos = GetWorldPositionFromGrid(nPos.x, nPos.y);
                Vector3 toNeighbor = (targetPos - originPos).normalized;
                
                if (Vector3.Dot(toNeighbor, dir) > maxDot)
                {
                    best = t;
                    break; 
                }
            }
            return best;
        }

        #endregion

        #region 초기 생성 유틸

        private TileType GetRandomTileTypeAvoidingInitialMatch(Vector2Int pos)
        {
            var types = Enum.GetValues(typeof(TileType)).Cast<TileType>()
                .Where(t => t < TileType.Bomb)
                .OrderBy(x => UnityEngine.Random.value)
                .ToList();
            return types[0];
        }

        #endregion

        #region 기본 유틸리티

        public Tile GetTileAt(Vector2Int pos)
        {
            m_tileObjects.TryGetValue(pos, out Tile t);
            return t;
        }

        private Vector3 GetWorldPositionFromGrid(int x, int y)
        {
            Vector3 local = m_gridManager.GetLocalPosition(x, y);
            return m_gridTransform.TransformPoint(local);
        }

        private async UniTask RotateBoard()
        {
            if (m_isProcessingMove) return;
            m_isProcessingMove = true;
            m_currentRotation = (BoardRotation)(((int)m_currentRotation + 1) % 4);
            
            await m_gridTransform.DORotate(new Vector3(0, 0, -90 * (int)m_currentRotation), 0.4f)
                .SetEase(Ease.OutBack)
                .ToUniTask();
            
            AnalyzeBoardStructure();
            // [최적화] 회전 후 방향 재계산 (캐시 업데이트)
            CacheMatchDirections();

            await CollapseGapsGraphBasedAsync();
            
            var newMatches = FindAllMatchesGraphBased();
            if (newMatches.Count > 0)
            {
                Debug.Log($"[Rotate] 회전 후 {newMatches.Count}개의 매치 발견.");
                await ProcessMatchesLoopAsync(newMatches);
            }

            m_isWaitingForRefill = true;
            m_isProcessingMove = false;
        }

        private void PrintBoardState()
        {
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (m_currentLevelData == null || m_gridManager == null) return;
            
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            foreach (var pos in m_currentLevelData.TilePositions)
            {
                Vector3 wp = GetWorldPositionFromGrid(pos.x, pos.y);
                Handles.Label(wp, $"{pos.x},{pos.y}", style);
            }
        }
#endif
        #endregion

#if UNITY_EDITOR
        public void LoadLevelForEditor(int levelID)
        {
            if (!Application.isPlaying) return;
            GameSettings.SelectedLevelID = levelID;
            Start(); 
        }
#endif
    }
}