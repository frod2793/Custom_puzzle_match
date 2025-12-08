using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
using System.Text;
using DG.Tweening;
using Match3.UI;

namespace Match3
{
    public class GameBoard : MonoBehaviour
    {
        public enum BoardRotation { Up = 0, Right = 1, Down = 2, Left = 3 }
        private BoardRotation m_currentRotation = BoardRotation.Up;

        // 요구사항: 중력 방향을 월드 좌표 기준 '아래'로 항상 고정합니다.
        private Vector2Int GravityDirection => Vector2Int.down;
        
        private bool m_isWaitingForRefill = false;

        [Header("Theme & Data")]
        [SerializeField] private LevelData m_levelData;
        [SerializeField] private TileThemeData m_tileTheme;

        [Header("Scene Objects")]
        [SerializeField] private GameObject m_tilePrefab;
        [SerializeField] private Transform m_gridTransform;
        [SerializeField] private Transform m_tileContainer;

        private Tile m_selectedTile;
        private Vector2 m_swipeStartPosition;
        private bool m_isProcessingMove = false;
        private const float k_MinSwipeDistance = 30f;
        
        private IGridManager m_gridManager;
        private TileFactory m_tileFactory;
        private Dictionary<Vector2Int, Tile> m_tileObjects = new Dictionary<Vector2Int, Tile>();
        
        private Camera m_mainCamera;

        private void Awake() { m_mainCamera = Camera.main; }

        private void Start()
        {
            if (m_levelData == null || m_tileTheme == null || m_tilePrefab == null) { return; }
            if (m_tileContainer == null) { Debug.LogError("<b>[Critical Error]</b> 'Tile Container' is not assigned!", this); return; }
            
            InitializeGrid();
            if (m_gridManager == null) { Debug.LogError("GridManager failed to initialize."); return; }
            
            m_tileFactory = new TileFactory(m_tilePrefab, m_tileContainer, m_gridManager.CellSize, m_tileTheme, m_currentRotation);
            CreateTiles();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnRotateButtonPressed += () => RotateBoard().Forget();
                UIManager.Instance.OnRefillButtonPressed += OnRefillButtonPressed;
            }
        }

        private void OnDestroy()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OnRotateButtonPressed -= () => RotateBoard().Forget();
                UIManager.Instance.OnRefillButtonPressed -= OnRefillButtonPressed;
            }
        }

        private void Update()
        {
            if (Keyboard.current.dKey.wasPressedThisFrame) PrintBoardState();
            if (m_isProcessingMove || m_mainCamera == null || Pointer.current == null) return;

            if (Pointer.current.press.wasPressedThisFrame)
            {
                m_selectedTile = GetTileUnderPointer();
                if (m_selectedTile != null)
                {
                    m_swipeStartPosition = Pointer.current.position.ReadValue();
                    m_selectedTile.Select();
                }
            }

            if (Pointer.current.press.wasReleasedThisFrame)
            {
                if (m_selectedTile == null) return;
                m_selectedTile.Deselect();
                Vector2 swipeEndPosition = Pointer.current.position.ReadValue();
                Vector2 swipeVector = swipeEndPosition - m_swipeStartPosition;

                if (swipeVector.magnitude > k_MinSwipeDistance)
                {
                    Vector2Int screenSwipeDir = GetSwipeDirection(swipeVector);
                    Vector2Int logicalSwipeDir = GetRotatedDirection(screenSwipeDir);
                    Tile neighborTile = GetTileAt(m_selectedTile.GridPosition + logicalSwipeDir);
                    
                    if (neighborTile != null)
                    {
                        SwapAndProcessMatchesAsync(m_selectedTile, neighborTile).Forget();
                    }
                }
                m_selectedTile = null;
            }
        }

        private async UniTaskVoid RotateBoard()
        {
            if (m_isProcessingMove) return;

            m_isProcessingMove = true;
            m_currentRotation = (BoardRotation)(((int)m_currentRotation + 1) % 4);
            
            Vector3 rotationVector = new Vector3(0, 0, -90 * (int)m_currentRotation);
            if (m_gridTransform != null)
            {
                await m_gridTransform.DORotate(rotationVector, 0.4f).SetEase(Ease.OutBack)
                                 .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
            }

            // 역할 분리: 회전 전용 재배치 메서드 호출
            await RearrangeAfterRotationAsync();

            m_isWaitingForRefill = true;
            m_isProcessingMove = false;
        }
        
        /// <summary>
        /// [회전 전용] 보드 회전 직후, 모든 타일을 새 위치로 재배치합니다.
        /// </summary>
        private async UniTask RearrangeAfterRotationAsync()
        {
            Debug.Log($"--- RearrangeAfterRotationAsync (Rotation: {m_currentRotation}) ---");
            Vector2Int Rotate(Vector2Int v) => new Vector2Int(v.y, -v.x);
            
            var allTiles = m_tileObjects.Values.ToList();
            foreach (var tile in allTiles)
            {
                tile.SetGridPosition(Rotate(tile.GridPosition));
            }

            var newTileObjects = new Dictionary<Vector2Int, Tile>();
            var moveTasks = new List<UniTask>();

            var columns = m_levelData.TilePositions.GroupBy(p => p.x);

            foreach (var column in columns)
            {
                var sortedValidPositions = column.OrderBy(p => p.y).ToList();

                var tilesInColumn = allTiles
                    .Where(t => t.GridPosition.x == column.Key)
                    .OrderBy(t => t.GridPosition.y)
                    .ToList();

                for (int i = 0; i < tilesInColumn.Count; i++)
                {
                    if (i < sortedValidPositions.Count)
                    {
                        Tile tileToPlace = tilesInColumn[i];
                        Vector2Int finalPos = sortedValidPositions[i];
                        
                        // [로그 추가] 회전 후 타일의 최종 위치 할당 기록
                        Debug.Log($"[Rearrange] Tile (ID:{tileToPlace.GetInstanceID()}) at rotated-pos {tileToPlace.GridPosition} assigned to final-pos {finalPos}");

                        tileToPlace.SetGridPosition(finalPos);
                        newTileObjects[finalPos] = tileToPlace;
                    }
                }
            }

            m_tileObjects = newTileObjects;

            foreach (var tile in m_tileObjects.Values)
            {
                Vector3 targetWorldPos = m_gridManager.GetWorldPosition(tile.GridPosition.x, tile.GridPosition.y);
                if (Vector3.Distance(tile.transform.position, targetWorldPos) > 0.01f)
                {
                    moveTasks.Add(tile.MoveToAsync(targetWorldPos));
                }
            }
            await UniTask.WhenAll(moveTasks);
        }

        /// <summary>
        /// [매치 전용] 타일 매치 후 생긴 빈 공간을 채우기 위해 타일들을 낙하시킵니다.
        /// </summary>
        private async UniTask CollapseGapsAsync()
        {
            Debug.Log($"--- CollapseGapsAsync (Rotation: {m_currentRotation}) ---");
            var moveTasks = new List<UniTask>();
            var columns = m_levelData.TilePositions.GroupBy(p => p.x);

            foreach (var column in columns)
            {
                var sortedValidPositions = column.OrderBy(p => p.y).ToList();

                var tilesInColumn = new List<Tile>();
                foreach (var pos in sortedValidPositions)
                {
                    if (m_tileObjects.TryGetValue(pos, out var tile))
                    {
                        tilesInColumn.Add(tile);
                    }
                }

                for (int i = 0; i < tilesInColumn.Count; i++)
                {
                    Tile tileToPlace = tilesInColumn[i];
                    Vector2Int newPos = sortedValidPositions[i];

                    if (tileToPlace.GridPosition != newPos)
                    {
                        // [로그 추가] 타일 낙하 정보 기록
                        Debug.Log($"[Collapse] Tile (ID:{tileToPlace.GetInstanceID()}) at {tileToPlace.GridPosition} is falling to {newPos}");
                        
                        m_tileObjects.Remove(tileToPlace.GridPosition);
                        tileToPlace.SetGridPosition(newPos);
                        m_tileObjects[newPos] = tileToPlace;
                    }
                }
            }

            foreach (var tile in m_tileObjects.Values)
            {
                Vector3 targetWorldPos = m_gridManager.GetWorldPosition(tile.GridPosition.x, tile.GridPosition.y);
                if (Vector3.Distance(tile.transform.position, targetWorldPos) > 0.01f)
                {
                    moveTasks.Add(tile.MoveToAsync(targetWorldPos));
                }
            }
            await UniTask.WhenAll(moveTasks);
        }

        private Vector2Int GetRotatedDirection(Vector2Int screenDirection)
        {
            switch (m_currentRotation)
            {
                case BoardRotation.Up:    return screenDirection;
                case BoardRotation.Right: return new Vector2Int(screenDirection.y, -screenDirection.x);
                case BoardRotation.Down:  return new Vector2Int(-screenDirection.x, -screenDirection.y);
                case BoardRotation.Left:  return new Vector2Int(-screenDirection.y, screenDirection.x);
                default:                  return screenDirection;
            }
        }
        
        private async UniTask ProcessMatchesLoopAsync(List<Tile> initialMatches)
        {
            var matchesToProcess = new List<Tile>(initialMatches);
            while (matchesToProcess.Count > 0)
            {
                await ClearTilesAsync(matchesToProcess);
                // 역할 분리: 매치 후에는 갭을 채우는 전용 메서드 호출
                await CollapseGapsAsync(); 
                matchesToProcess = FindAllMatches();
            }
            m_isWaitingForRefill = true;
        }

        private async UniTask RefillBoardAsync()
        {
            var refillTasks = new List<UniTask>();
            Vector2Int antiGravity = -GravityDirection;
            int boardSize = Mathf.Max(m_levelData.GridSize.x, m_levelData.GridSize.y);

            foreach (var pos in m_levelData.TilePositions)
            {
                if (!m_tileObjects.ContainsKey(pos))
                {
                    // 리필 로직은 월드 기준 '위'에서 생성되어야 하므로,
                    // antiGravity가 (0, 1)이 되어야 합니다.
                    var creationPos = pos + antiGravity * boardSize;
                    var worldPos = m_gridManager.GetWorldPosition(creationPos.x, creationPos.y);
                    
                    var newType = GetRandomTileTypeAvoidingInitialMatch(pos);
                    var newTile = m_tileFactory.Create(worldPos, pos, newType);
                    m_tileObjects[pos] = newTile;

                    var targetWorldPos = m_gridManager.GetWorldPosition(pos.x, pos.y);
                    
                    float distance = Vector2Int.Distance(pos, creationPos);
                    var delay = UniTask.Delay(System.TimeSpan.FromSeconds(distance * 0.03f));
                    
                    refillTasks.Add(delay.ContinueWith(() => newTile.MoveToAsync(targetWorldPos)));
                }
            }
            await UniTask.WhenAll(refillTasks);
        }

        // --- 이하 다른 메서드들은 변경 없음 ---
        private void OnRefillButtonPressed() { if (m_isProcessingMove || !m_isWaitingForRefill) return; RefillAndCheckCascadesAsync().Forget(); }
        private async UniTaskVoid SwapAndProcessMatchesAsync(Tile tileA, Tile tileB) { m_isProcessingMove = true; await SwapTilesAsync(tileA, tileB); var matches = FindAllMatches(); if (matches.Count > 0) { await ProcessMatchesLoopAsync(matches); } else { await UniTask.Delay(50); await SwapTilesAsync(tileB, tileA); } m_isProcessingMove = false; }
        private async UniTaskVoid RefillAndCheckCascadesAsync() { m_isProcessingMove = true; m_isWaitingForRefill = false; await RefillBoardAsync(); var newMatches = FindAllMatches(); if (newMatches.Count > 0) { await ProcessMatchesLoopAsync(newMatches); } m_isProcessingMove = false; }
        private List<Tile> FindAllMatches() { var allMatchedTiles = new HashSet<Tile>(); foreach (var tile in m_tileObjects.Values.ToList()) { if (tile == null) continue; var matches = FindMatches(tile.GridPosition); if (matches.Count > 0) { allMatchedTiles.UnionWith(matches); } } return allMatchedTiles.ToList(); }
        private List<Tile> FindMatches(Vector2Int startPos) { Tile startTile = GetTileAt(startPos); if (startTile == null) return new List<Tile>(); var matchedTiles = new HashSet<Tile>(); var rightMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.right); var leftMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.left); if (rightMatches.Count + leftMatches.Count >= 2) { matchedTiles.UnionWith(rightMatches); matchedTiles.UnionWith(leftMatches); } var upMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.up); var downMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.down); if (upMatches.Count + downMatches.Count >= 2) { matchedTiles.UnionWith(upMatches); matchedTiles.UnionWith(downMatches); } if (matchedTiles.Count > 0) { matchedTiles.Add(startTile); } return matchedTiles.ToList(); }
        private List<Tile> FindMatchesInDirection(Vector2Int startPos, TileType typeToMatch, Vector2Int direction) { var matches = new List<Tile>(); for (int i = 1; i < 10; i++) { var nextPos = startPos + direction * i; Tile nextTile = GetTileAt(nextPos); if (nextTile != null && nextTile.Type == typeToMatch) { matches.Add(nextTile); } else { break; } } return matches; }
        private async UniTask ClearTilesAsync(List<Tile> tilesToClear) { var clearTasks = new List<UniTask>(); foreach (var tile in tilesToClear) { if (tile == null) continue; m_tileObjects.Remove(tile.GridPosition); clearTasks.Add(tile.ClearAsync()); } await UniTask.WhenAll(clearTasks); }
        private void CreateTiles() { foreach (var pos in m_levelData.TilePositions) { Vector3 worldPos = m_gridManager.GetWorldPosition(pos.x, pos.y); TileType newType = GetRandomTileTypeAvoidingInitialMatch(pos); Tile newTile = m_tileFactory.Create(worldPos, pos, newType); if (newTile != null) { m_tileObjects.Add(pos, newTile); } } }
        private TileType GetRandomTileTypeAvoidingInitialMatch(Vector2Int pos) { var possibleTypes = System.Enum.GetValues(typeof(TileType)).Cast<TileType>().Where(t => t < TileType.Bomb).OrderBy(t => Random.value).ToList(); foreach (var type in possibleTypes) { if (!CreatesInitialMatch(pos, type)) { return type; } } return possibleTypes.FirstOrDefault(); }
        private bool CreatesInitialMatch(Vector2Int pos, TileType type) { Tile r1 = GetTileAt(pos + Vector2Int.right); Tile l1 = GetTileAt(pos + Vector2Int.left); if (r1 != null && l1 != null && r1.Type == type && l1.Type == type) return true; Tile r2 = GetTileAt(pos + new Vector2Int(2, 0)); if (r1 != null && r2 != null && r1.Type == type && r2.Type == type) return true; Tile l2 = GetTileAt(pos + new Vector2Int(-2, 0)); if (l1 != null && l2 != null && l1.Type == type && l2.Type == type) return true; Tile u1 = GetTileAt(pos + Vector2Int.up); Tile d1 = GetTileAt(pos + Vector2Int.down); if (u1 != null && d1 != null && u1.Type == type && d1.Type == type) return true; Tile u2 = GetTileAt(pos + new Vector2Int(0, 2)); if (u1 != null && u2 != null && u1.Type == type && u2.Type == type) return true; Tile d2 = GetTileAt(pos + new Vector2Int(0, -2)); if (d1 != null && d2 != null && d1.Type == type && d2.Type == type) return true; return false; }
        private void PrintBoardState() { var sb = new StringBuilder(); sb.AppendLine("\n<b>--- Current Board State ---</b>"); if (m_tileObjects.Count == 0) { sb.AppendLine("Board is empty."); Debug.Log(sb.ToString()); return; } int minX = m_levelData.TilePositions.Min(p => p.x); int maxX = m_levelData.TilePositions.Max(p => p.x); int minY = m_levelData.TilePositions.Min(p => p.y); int maxY = m_levelData.TilePositions.Max(p => p.y); for (int y = maxY; y >= minY; y--) { sb.Append($"Row {y,2}: "); for (int x = minX; x <= maxX; x++) { var pos = new Vector2Int(x, y); if (m_levelData.TilePositions.Contains(pos)) { Tile tile = GetTileAt(pos); if (tile != null) { sb.Append($"[{tile.Type.ToString()[7]}]"); } else { sb.Append("[ ]"); } } else { sb.Append("   "); } } sb.AppendLine(); } Debug.Log(sb.ToString()); }
        private Vector2Int GetSwipeDirection(Vector2 swipeVector) { if (Mathf.Abs(swipeVector.x) > Mathf.Abs(swipeVector.y)) { return swipeVector.x > 0 ? Vector2Int.right : Vector2Int.left; } else { return swipeVector.y > 0 ? Vector2Int.up : Vector2Int.down; } }
        private Tile GetTileUnderPointer() { Vector2 worldPoint = m_mainCamera.ScreenToWorldPoint(Pointer.current.position.ReadValue()); RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero); return hit.collider?.GetComponent<Tile>(); }
        private async UniTask SwapTilesAsync(Tile tileA, Tile tileB) { Vector3 posA = tileA.transform.position; Vector3 posB = tileB.transform.position; Vector2Int gridPosA = tileA.GridPosition; Vector2Int gridPosB = tileB.GridPosition; var taskA = tileA.MoveToAsync(posB); var taskB = tileB.MoveToAsync(posA); await UniTask.WhenAll(taskA, taskB); m_tileObjects[gridPosA] = tileB; m_tileObjects[gridPosB] = tileA; tileA.SetGridPosition(gridPosB); tileB.SetGridPosition(gridPosA); }
        private void InitializeGrid() { switch (m_levelData.GridType) { case GridType.Square: m_gridManager = new SquareGridManager(); break; case GridType.Hexagon: m_gridManager = new HexGridManager(); break; default: Debug.LogError($"Unsupported GridType: {m_levelData.GridType} in {m_levelData.name}"); return; } m_gridManager.Initialize(m_levelData); }
        public Tile GetTileAt(Vector2Int pos) { m_tileObjects.TryGetValue(pos, out Tile tile); return tile; }
    }
}
