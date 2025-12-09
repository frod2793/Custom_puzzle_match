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
        //todo : 낙하 이펙트에 약간의 랜덤 딜레이를주어 동시에 낙하 하는 것을 약간이나마 방지 
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
        [Tooltip("타일이 낙하하는 속도 (Units per Second). 값이 클수록 빠르게 떨어집니다.")]
        [SerializeField] private float m_fallSpeed = 15.0f; 

        [Tooltip("낙하 시 웨이브 효과를 주기 위한 지연 시간 계수")]
        [SerializeField] private float m_fallStaggerDelay = 0.08f; 

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
            m_tileObjects.Clear(); 

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
            // 스텝별 이동 속도 (짧게 설정하여 끊김 없이 흐르듯이 보이게 함)
            float stepDuration = 0.12f; 

            do
            {
                moved = false;
                
                // 이번 스텝에서 이동할 타일들 예약
                var moveTasks = new List<UniTask>();
                
                // 이동 후의 위치를 임시로 추적하여 중복 이동 방지
                HashSet<Vector2Int> destinationLocked = new HashSet<Vector2Int>();
                
                // 아래쪽 행부터 위쪽으로 검사 (Y 오름차순)
                var sortedSlots = m_currentLevelData.TilePositions
                    .OrderBy(p => GetWorldPositionFromGrid(p.x, p.y).y)
                    .ToList();

                // 이번 턴에 이동할 타일과 목적지 매핑
                Dictionary<Tile, Vector2Int> pendingMoves = new Dictionary<Tile, Vector2Int>();

                foreach (var slotGridPos in sortedSlots)
                {
                    // 1. 이미 채워져 있는 칸은 패스
                    if (m_tileObjects.ContainsKey(slotGridPos)) continue;
                    
                    // 2. 이미 이번 턴에 누군가가 오기로 예약된 칸도 패스
                    if (destinationLocked.Contains(slotGridPos)) continue;

                    // 3. 이 칸을 채워줄 수 있는 '인접한' 공급자 찾기
                    Tile supplier = GetBestSupplierForSlot(slotGridPos, GetWorldPositionFromGrid(slotGridPos.x, slotGridPos.y));

                    if (supplier != null)
                    {
                        // 4. 공급자가 이미 다른 곳으로 가기로 했으면 패스
                        if (pendingMoves.ContainsKey(supplier)) continue;

                        // 이동 예약
                        pendingMoves[supplier] = slotGridPos;
                        destinationLocked.Add(slotGridPos); // 목적지 잠금
                    }
                }

                // 실제 데이터 갱신 및 애니메이션 시작
                if (pendingMoves.Count > 0)
                {
                    moved = true;

                    foreach (var kvp in pendingMoves)
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
                        moveTasks.Add(tile.transform.DOMove(targetWorldPos, stepDuration).SetEase(Ease.Linear).ToUniTask());
                    }

                    // 모든 타일이 한 칸씩 움직일 때까지 대기
                    await UniTask.WhenAll(moveTasks);
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

            var emptyPositions = m_currentLevelData.TilePositions
                .Where(p => !m_tileObjects.ContainsKey(p))
                .ToList();

            if (emptyPositions.Count == 0) return;

            float minY = m_currentLevelData.TilePositions.Min(p => GetWorldPositionFromGrid(p.x, p.y).y);
            float cameraTopY = m_mainCamera.transform.position.y + m_mainCamera.orthographicSize;
            float spawnOffset = m_gridManager.CellSize * 2f;

            foreach (var gridPos in emptyPositions)
            {
                if (m_tileObjects.ContainsKey(gridPos)) continue;

                Vector3 targetWorldPos = GetWorldPositionFromGrid(gridPos.x, gridPos.y);
                Vector3 startPos = new Vector3(targetWorldPos.x, cameraTopY + spawnOffset, 0);

                var newType = GetRandomTileTypeAvoidingInitialMatch(gridPos);
                var newTile = m_tileFactory.Get(startPos, gridPos, newType);
                if (newTile == null) continue;

                m_tileObjects[gridPos] = newTile; 
                
                float delay = (targetWorldPos.y - minY) * m_fallStaggerDelay;
                refillTasks.Add(AnimateTileMove(newTile, targetWorldPos, delay, Ease.OutBounce));
            }
            await UniTask.WhenAll(refillTasks);
        }

        private async UniTask AnimateTileMove(Tile tile, Vector3 targetPos, float delay, Ease easeType)
        {
            var ct = this.GetCancellationTokenOnDestroy();
            if (delay > 0) await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            if (ct.IsCancellationRequested) return;
            
            float distance = Vector3.Distance(tile.transform.position, targetPos);
            float duration = m_fallSpeed > 0 ? distance / m_fallSpeed : 0.1f;

            await tile.transform.DOMove(targetPos, duration)
                .SetEase(easeType)
                .ToUniTask(cancellationToken: ct);
        }

        #endregion

        #region 매치 찾기 (Graph Based - Reinforced)

        private List<Tile> FindAllMatchesGraphBased()
        {
            var allMatches = new HashSet<Tile>();

            // [수정] 매치 검사 방향을 로컬 공간 기준으로 정의하고 월드로 변환
            // 보드가 회전하더라도 "타일 기준의 위/아래/대각선"을 정확히 추적하기 위함
            List<Vector3> matchDirections = new List<Vector3>();

            if (m_currentLevelData.GridType == GridType.Hexagon)
            {
                // Flat Top Hexagon의 Local Axes
                Vector3[] localDirs = new Vector3[] 
                { 
                    Vector3.up,                                  
                    Quaternion.Euler(0,0,-60) * Vector3.up,      
                    Quaternion.Euler(0,0,60) * Vector3.up        
                };
                // 현재 Grid Transform의 회전을 반영하여 월드 방향으로 변환
                foreach(var ld in localDirs) matchDirections.Add(m_gridTransform.TransformDirection(ld));
            }
            else 
            {
                // Square Grid Local Axes
                matchDirections.Add(m_gridTransform.TransformDirection(Vector3.right));
                matchDirections.Add(m_gridTransform.TransformDirection(Vector3.up));
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