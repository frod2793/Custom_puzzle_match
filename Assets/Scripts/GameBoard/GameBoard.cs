using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using System.Text;
using DG.Tweening;
using Match3.UI;
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

        //todo : 낙하 로직 의 속도를 조절 할수있는 인스펙터 변수 추가 , 낙하후에도 매치 로직 작동 
        
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
        [SerializeField] private float m_refillMoveDuration = 0.4f;
        [SerializeField] private float m_refillMaxStaggerDelay = 0.2f;

        // [수정] 순차 낙하 지연 시간 계수 조정
        private const float k_FallStaggerDelay = 0.08f; 

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
        // Key: 기준 좌표, Value: 해당 좌표의 면(Face)이 맞닿은 이웃 좌표 리스트
        private Dictionary<Vector2Int, List<Vector2Int>> m_adjacencyGraph = new Dictionary<Vector2Int, List<Vector2Int>>();

        private Camera m_mainCamera;
        private Action m_onRotateButtonPressedAction;
        
        #region Unity 라이프사이클

        private void Awake()
        {
            m_mainCamera = Camera.main;
            InitializeBoardAndLevelData();
            InitializeGrid();
            
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
            
            // 1. 타일 초기 생성
            CreateTiles();
            
            // 2. [핵심] 보드 구조 분석 (인접 그래프 구축)
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
            int levelID = GameSettings.SelectedLevelID < 0 ? 0 : GameSettings.SelectedLevelID;
            m_currentLevelData = m_levelDatabase != null ? m_levelDatabase.GetLevel(levelID) : null;

            if (m_currentLevelData == null) return;
            
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

        private void InitializeGrid()
        {
            if (m_currentLevelData == null) return;
            m_gridManager = m_currentLevelData.GridType == GridType.Hexagon ? new HexGridManager() : new SquareGridManager();
            m_gridManager.Initialize(m_currentLevelData, m_mainGrid);
        }

        private void CreateTiles()
        {
            m_tileObjects.Clear(); // 딕셔너리 초기화 (중복 생성 방지)

            foreach (var pos in m_currentLevelData.TilePositions)
            {
                // 이미 해당 위치에 타일이 있다면 스킵 (안전 장치)
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

            foreach (var neighborPos in neighbors)
            {
                Tile neighborTile = GetTileAt(neighborPos);
                if (neighborTile == null) continue;

                Vector2 directionToNeighbor = (neighborTile.transform.position - startTile.transform.position).normalized;
                float dot = Vector2.Dot(normalizedSwipe, directionToNeighbor);

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
            
            // [수정] Deprecated된 RaycastNonAlloc 대신 Raycast 사용
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

            var taskA = tileA.MoveToAsync(targetWorldPosA);
            var taskB = tileB.MoveToAsync(targetWorldPosB);
            await UniTask.WhenAll(taskA, taskB);

            // 딕셔너리 안전 갱신
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
            }
            m_isWaitingForRefill = true;
        }

        private async UniTask ClearTilesAsync(List<Tile> tilesToClear)
        {
            var clearTasks = new List<UniTask>();
            foreach (var tile in tilesToClear)
            {
                if (tile == null) continue;
                
                // 확실하게 딕셔너리에서 제거되었는지 확인
                if (m_tileObjects.ContainsKey(tile.GridPosition))
                {
                    m_tileObjects.Remove(tile.GridPosition);
                    clearTasks.Add(tile.ClearAsync().ContinueWith(() => m_tileFactory.Release(tile)));
                }
            }
            await UniTask.WhenAll(clearTasks);
        }

        #endregion

        #region 낙하 로직 (핵심 수정 사항)

        private async UniTask CollapseGapsGraphBasedAsync()
        {
            var moveTasks = new List<UniTask>();
            bool moved;
            int iterationCount = 0;
            const int k_MaxIterations = 100;

            var allPositions = m_currentLevelData.TilePositions;
            
            do
            {
                if (iterationCount++ > k_MaxIterations)
                {
                    Debug.LogWarning("[GameBoard] Collapse Loop Limit Exceeded");
                    break;
                }

                moved = false;
                moveTasks.Clear(); // [수정] 태스크 리스트 초기화
                
                // 바닥부터 위로 스캔 (월드 Y 기준 정렬)
                var sortedSlots = allPositions
                    .Select(p => new { GridPos = p, WorldPos = GetWorldPositionFromGrid(p.x, p.y) })
                    .OrderBy(item => item.WorldPos.y) 
                    .ToList();

                // 이번 패스에서 이동할 타일들의 목표 위치를 기록하여 중복 방지
                HashSet<Vector2Int> targetedPositions = new HashSet<Vector2Int>();

                foreach (var slot in sortedSlots)
                {
                    // 1. 이미 타일이 있는 곳은 패스
                    if (m_tileObjects.ContainsKey(slot.GridPos)) continue;
                    
                    // 2. 이번 패스에서 이미 누군가 오기로 한 곳이면 패스
                    if (targetedPositions.Contains(slot.GridPos)) continue;

                    Tile supplier = GetBestSupplierForSlot(slot.GridPos, slot.WorldPos);

                    if (supplier != null)
                    {
                        Vector2Int oldPos = supplier.GridPosition;
                        
                        // [핵심] 딕셔너리 즉시 갱신 (논리적 위치 이동 완료)
                        m_tileObjects.Remove(oldPos);
                        m_tileObjects[slot.GridPos] = supplier;
                        supplier.SetGridPosition(slot.GridPos);
                        
                        targetedPositions.Add(slot.GridPos); // 이 슬롯은 채워짐

                        // 시각적 애니메이션 태스크 추가
                        moveTasks.Add(AnimateTileMove(supplier, slot.WorldPos, 0f, Ease.OutQuad));
                        
                        moved = true;
                    }
                }

                // [핵심 수정] 이번 패스의 모든 애니메이션이 끝날 때까지 대기
                // 이를 통해 물리적 위치와 논리적 위치의 싱크를 맞춤 (겹침 방지)
                if (moveTasks.Count > 0)
                {
                    await UniTask.WhenAll(moveTasks);
                }

            } while (moved); 
        }

        private Tile GetBestSupplierForSlot(Vector2Int targetGridPos, Vector3 targetWorldPos)
        {
            if (!m_adjacencyGraph.TryGetValue(targetGridPos, out var neighbors)) return null;

            Tile bestCandidate = null;
            float bestScore = -1f;
            Vector3 upDir = Vector3.up; // World Up

            foreach (var neighborPos in neighbors)
            {
                if (!m_tileObjects.TryGetValue(neighborPos, out Tile candidateTile)) continue;

                Vector3 candidatePos = GetWorldPositionFromGrid(neighborPos.x, neighborPos.y); 
                Vector3 dirToCandidate = (candidatePos - targetWorldPos).normalized;

                float score = Vector3.Dot(dirToCandidate, upDir);

                if (score > 0.45f) 
                {
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
                await ProcessMatchesLoopAsync(newMatches);
            }
            m_isProcessingMove = false;
        }

        private async UniTask RefillBoardAsync()
        {
            var refillTasks = new List<UniTask>();
            var ct = this.GetCancellationTokenOnDestroy();

            // m_tileObjects 키 검사로 빈 공간 확인
            var emptyPositions = m_currentLevelData.TilePositions
                .Where(p => !m_tileObjects.ContainsKey(p))
                .ToList();

            if (emptyPositions.Count == 0) return;

            // 딜레이 계산용
            float minY = m_currentLevelData.TilePositions.Min(p => GetWorldPositionFromGrid(p.x, p.y).y);
            float cameraTopY = m_mainCamera.transform.position.y + m_mainCamera.orthographicSize;
            float spawnOffset = m_gridManager.CellSize * 2f;

            foreach (var gridPos in emptyPositions)
            {
                // 혹시 모를 중복 생성 방지
                if (m_tileObjects.ContainsKey(gridPos)) continue;

                Vector3 targetWorldPos = GetWorldPositionFromGrid(gridPos.x, gridPos.y);
                Vector3 startPos = new Vector3(targetWorldPos.x, cameraTopY + spawnOffset, 0);

                var newType = GetRandomTileTypeAvoidingInitialMatch(gridPos);
                var newTile = m_tileFactory.Get(startPos, gridPos, newType);
                if (newTile == null) continue;

                m_tileObjects[gridPos] = newTile; // 즉시 등록하여 점유 표시
                
                // 월드 Y 기준 딜레이
                float delay = (targetWorldPos.y - minY) * k_FallStaggerDelay;
                refillTasks.Add(AnimateTileMove(newTile, targetWorldPos, delay, Ease.OutBounce));
            }
            await UniTask.WhenAll(refillTasks);
        }

        private async UniTask AnimateTileMove(Tile tile, Vector3 targetPos, float delay, Ease easeType)
        {
            var ct = this.GetCancellationTokenOnDestroy();
            if (delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;
            
            await tile.transform.DOMove(targetPos, m_refillMoveDuration)
                .SetEase(easeType)
                .ToUniTask(cancellationToken: ct);
        }

        #endregion

        #region 매치 찾기 (Graph Based)

        private List<Tile> FindAllMatchesGraphBased()
        {
            var allMatches = new HashSet<Tile>();

            Vector3[] matchDirections;
            if (m_currentLevelData.GridType == GridType.Hexagon)
            {
                matchDirections = new Vector3[] 
                { 
                    Vector3.up,                                  
                    Quaternion.Euler(0,0,-60) * Vector3.up,      
                    Quaternion.Euler(0,0,60) * Vector3.up        
                };
            }
            else 
            {
                matchDirections = new Vector3[] { Vector3.right, Vector3.up };
            }

            foreach (var kvp in m_tileObjects)
            {
                Tile startTile = kvp.Value;
                if (startTile == null) continue;

                foreach (var dir in matchDirections)
                {
                    List<Tile> line = GetMatchLine(startTile, dir);
                    if (line.Count >= 3)
                    {
                        foreach (var t in line) allMatches.Add(t);
                    }
                }
            }
            return allMatches.ToList();
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
            float maxDot = 0.85f; 

            foreach(var nPos in neighbors)
            {
                Tile t = GetTileAt(nPos);
                if (t == null) continue;

                Vector3 toNeighbor = (t.transform.position - origin.transform.position).normalized;
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
            
            // 회전 후 구조 재분석 (필요시)
            AnalyzeBoardStructure();
            
            // 낙하 시도 
            await CollapseGapsGraphBasedAsync();
            
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