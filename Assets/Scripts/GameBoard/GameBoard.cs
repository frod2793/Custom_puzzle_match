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

namespace Match3
{
    public class GameBoard : MonoBehaviour
    {
        public enum BoardRotation { Up = 0, Right = 1, Down = 2, Left = 3 }

        [Header("데이터")]
        [SerializeField] private LevelDatabase m_levelDatabase;
        [SerializeField] private TileThemeData m_tileTheme;

        [Header("그리드 컨테이너")]
        [SerializeField] private GameObject m_squareGridContainer;
        [SerializeField] private GameObject m_hexagonGridContainer;
        
        [Header("타일 프리팹")]
        [SerializeField] private GameObject m_tilePrefab;

        [Header("리필 애니메이션 속도")]
        [SerializeField] private float m_refillMoveDuration = 0.6f;
        [SerializeField] private float m_refillMaxStaggerDelay = 0.4f;

        private LevelData m_currentLevelData;
        private Transform m_gridTransform;
        private Transform m_tileContainer;
        
        private BoardRotation m_currentRotation = BoardRotation.Up;
        private bool m_isWaitingForRefill;
        private bool m_isProcessingMove;
        
        private Tile m_selectedTile;
        private Vector2 m_swipeStartPosition;
        private const float k_MinSwipeDistancePixels = 50f;

        private IGridManager m_gridManager;
        private TileFactory m_tileFactory;
        private readonly Dictionary<Vector2Int, Tile> m_tileObjects = new Dictionary<Vector2Int, Tile>();

        private Camera m_mainCamera;
        private Action m_onRotateButtonPressedAction;
        private readonly RaycastHit2D[] m_raycastHits = new RaycastHit2D[10];

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
                Debug.LogError("<b>[치명적 오류]</b> GameBoard의 필수 데이터가 할당되지 않았거나 초기화에 실패했습니다.", this);
                this.enabled = false;
                return;
            }

            m_tileFactory = new TileFactory(m_tilePrefab, m_tileContainer, m_gridManager.CellSize, m_tileTheme, m_currentRotation);
            CreateTiles();

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
            if (Keyboard.current.dKey.wasPressedThisFrame)
            {
                PrintBoardState();
            }
            HandleInput();
        }

        #endregion

        #region 초기화

        private void InitializeBoardAndLevelData()
        {
            if (m_levelDatabase == null || m_squareGridContainer == null || m_hexagonGridContainer == null)
            {
                Debug.LogError("<b>[치명적 오류]</b> LevelDatabase 또는 그리드 컨테이너가 할당되지 않았습니다!", this);
                return;
            }

            int levelID = GameSettings.SelectedLevelID;

            // 타이틀 씬을 거치지 않고 테스트하는 경우, 첫 번째 레벨을 기본값으로 사용
            if (levelID < 0)
            {
                Debug.LogWarning("<b>[경고]</b> 선택된 레벨 ID가 없습니다. 데이터베이스의 첫 번째 레벨로 시작합니다.");
                levelID = 0;
            }

            m_currentLevelData = m_levelDatabase.GetLevel(levelID);

            if (m_currentLevelData == null)
            {
                Debug.LogError($"<b>[치명적 오류]</b> LevelDatabase에서 ID {levelID}에 해당하는 레벨 데이터를 찾을 수 없습니다!", this);
                return;
            }

            bool isSquare = m_currentLevelData.GridType == GridType.Square;
            
            m_squareGridContainer.SetActive(isSquare);
            m_hexagonGridContainer.SetActive(!isSquare);

            GameObject activeContainer = isSquare ? m_squareGridContainer : m_hexagonGridContainer;
            m_gridTransform = activeContainer.transform;
            m_tileContainer = activeContainer.transform;
        }

        private void InitializeGrid()
        {
            if (m_currentLevelData == null) return;

            switch (m_currentLevelData.GridType)
            {
                case GridType.Square:
                    m_gridManager = new SquareGridManager();
                    break;
                case GridType.Hexagon:
                    m_gridManager = new HexGridManager();
                    break;
            }
            m_gridManager.Initialize(m_currentLevelData);
        }

        private void CreateTiles()
        {
            foreach (var pos in m_currentLevelData.TilePositions)
            {
                Vector3 worldPos = GetWorldPositionFromGrid(pos.x, pos.y);
                TileType newType = GetRandomTileTypeAvoidingInitialMatch(pos);
                Tile newTile = m_tileFactory.Get(worldPos, pos, newType);
                if (newTile != null)
                {
                    m_tileObjects.Add(pos, newTile);
                }
            }
        }

        #endregion
        
        #region 입력 처리

        private void HandleInput()
        {
            if (m_isProcessingMove || m_mainCamera == null || Pointer.current == null)
            {
                return;
            }

            if (Pointer.current.press.wasPressedThisFrame)
            {
                m_selectedTile = GetTileUnderPointer();
                if (m_selectedTile != null)
                {
                    m_swipeStartPosition = Pointer.current.position.ReadValue();
                    m_selectedTile.Select();
                }
            }

            if (Pointer.current.press.wasReleasedThisFrame && m_selectedTile != null)
            {
                m_selectedTile.Deselect();
                Vector2 swipeEndPosition = Pointer.current.position.ReadValue();
                Vector2 swipeVector = swipeEndPosition - m_swipeStartPosition;

                if (swipeVector.magnitude > k_MinSwipeDistancePixels)
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

        private Tile GetTileUnderPointer()
        {
            Vector2 screenPoint = Pointer.current.position.ReadValue();
            int hitCount = Physics2D.RaycastNonAlloc(m_mainCamera.ScreenToWorldPoint(screenPoint), Vector2.zero, m_raycastHits);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = m_raycastHits[i];
                if (hit.collider != null)
                {
                    var tile = hit.collider.GetComponent<Tile>();
                    if (tile != null && tile.gameObject.activeInHierarchy)
                    {
                        return tile;
                    }
                }
            }
            return null;
        }

        private Vector2Int GetSwipeDirection(Vector2 swipeVector)
        {
            if (Mathf.Abs(swipeVector.x) > Mathf.Abs(swipeVector.y))
            {
                return swipeVector.x > 0 ? Vector2Int.right : Vector2Int.left;
            }
            else
            {
                return swipeVector.y > 0 ? Vector2Int.up : Vector2Int.down;
            }
        }

        #endregion

        #region 게임 로직 (매치, 스왑, 클리어)

        private async UniTaskVoid SwapAndProcessMatchesAsync(Tile tileA, Tile tileB)
        {
            m_isProcessingMove = true;
            await SwapTilesAsync(tileA, tileB);

            var matches = FindAllMatches();
            if (matches.Count > 0)
            {
                await ProcessMatchesLoopAsync(matches);
            }
            else
            {
                await UniTask.Delay(50);
                await SwapTilesAsync(tileB, tileA); // 매치 실패 시 원위치
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

            m_tileObjects[gridPosA] = tileB;
            m_tileObjects[gridPosB] = tileA;
            tileA.SetGridPosition(gridPosB);
            tileB.SetGridPosition(gridPosA);
        }

        private async UniTask ProcessMatchesLoopAsync(List<Tile> initialMatches)
        {
            var matchesToProcess = new List<Tile>(initialMatches);
            while (matchesToProcess.Count > 0)
            {
                await ClearTilesAsync(matchesToProcess);
                await CollapseGapsAsync();
                matchesToProcess = FindAllMatches();
            }
            m_isWaitingForRefill = true;
        }

        private async UniTask ClearTilesAsync(List<Tile> tilesToClear)
        {
            var clearTasks = new List<UniTask>();
            foreach (var tile in tilesToClear)
            {
                if (tile == null) continue;

                if (m_tileObjects.Remove(tile.GridPosition))
                {
                    clearTasks.Add(tile.ClearAsync().ContinueWith(() => m_tileFactory.Release(tile)));
                }
            }
            await UniTask.WhenAll(clearTasks);
        }

        #endregion

        #region 보드 제어 (회전, 재정렬, 리필)

        private async UniTaskVoid RotateBoard()
        {
            if (m_isProcessingMove) return;

            m_isProcessingMove = true;
            m_currentRotation = (BoardRotation)(((int)m_currentRotation + 1) % 4);

            Vector3 rotationVector = new Vector3(0, 0, -90 * (int)m_currentRotation);
            await m_gridTransform.DORotate(rotationVector, 0.4f).SetEase(Ease.OutBack)
                                 .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

            await RearrangeAfterRotationAsync();

            m_isWaitingForRefill = true;
            m_isProcessingMove = false;
        }

        private async UniTask RearrangeAfterRotationAsync()
        {
            await CollapseGapsAsync();
        }

        private async UniTask CollapseGapsAsync()
        {
            var moveTasks = new List<UniTask>();
            Vector2Int gravityDir = GetGravityDirection();
            bool isVerticalGravity = (gravityDir.x == 0);

            var columns = new Dictionary<int, List<Vector2Int>>();
            foreach (var pos in m_currentLevelData.TilePositions)
            {
                int colKey = isVerticalGravity ? pos.x : pos.y;
                if (!columns.ContainsKey(colKey))
                {
                    columns[colKey] = new List<Vector2Int>();
                }
                columns[colKey].Add(pos);
            }

            foreach (var kvp in columns)
            {
                var slots = kvp.Value;

                slots.Sort((a, b) =>
                {
                    int dotA = a.x * gravityDir.x + a.y * gravityDir.y;
                    int dotB = b.x * gravityDir.x + b.y * gravityDir.y;
                    return dotB.CompareTo(dotA);
                });

                var tiles = new List<Tile>();
                var tilesToRemove = new List<Vector2Int>();

                foreach (var slot in slots)
                {
                    if (m_tileObjects.TryGetValue(slot, out Tile t))
                    {
                        tiles.Add(t);
                        tilesToRemove.Add(slot);
                    }
                }

                foreach (var pos in tilesToRemove)
                {
                    m_tileObjects.Remove(pos);
                }

                for (int i = 0; i < tiles.Count; i++)
                {
                    Tile tile = tiles[i];
                    Vector2Int targetSlot = slots[i];

                    tile.SetGridPosition(targetSlot);
                    m_tileObjects[targetSlot] = tile;

                    Vector3 targetWorldPos = GetWorldPositionFromGrid(targetSlot.x, targetSlot.y);
                    if (Vector3.Distance(tile.transform.position, targetWorldPos) > 0.01f)
                    {
                        moveTasks.Add(tile.MoveToAsync(targetWorldPos));
                    }
                }
            }

            await UniTask.WhenAll(moveTasks);
        }

        private void OnRefillButtonPressed()
        {
            if (m_isProcessingMove || !m_isWaitingForRefill) return;
            RefillAndCheckCascadesAsync().Forget();
        }

        private async UniTaskVoid RefillAndCheckCascadesAsync()
        {
            m_isProcessingMove = true;
            m_isWaitingForRefill = false;

            await RefillBoardAsync();
            var newMatches = FindAllMatches();
            if (newMatches.Count > 0)
            {
                await ProcessMatchesLoopAsync(newMatches);
            }
            m_isProcessingMove = false;
        }

        /// <summary>
        /// 비어있는 모든 타일 슬롯을 채웁니다. 타일은 월드 좌표 기준 위에서 생성되며,
        /// 보드의 가장 아래쪽에 채워질 타일부터 애니메이션이 시작됩니다.
        /// </summary>
        private async UniTask RefillBoardAsync()
        {
            var refillTasks = new List<UniTask>();
            var cancellationToken = this.GetCancellationTokenOnDestroy();

            var emptyGridPositions = m_currentLevelData.TilePositions
                .Where(pos => !m_tileObjects.ContainsKey(pos))
                .ToList();

            if (emptyGridPositions.Count == 0)
            {
                return;
            }

            var newTileData = emptyGridPositions.Select(gridPos => new
            {
                GridPos = gridPos,
                TargetWorldPos = GetWorldPositionFromGrid(gridPos.x, gridPos.y)
            }).ToList();

            float minY = newTileData.Min(d => d.TargetWorldPos.y);
            float maxY = newTileData.Max(d => d.TargetWorldPos.y);
            float verticalRange = maxY - minY;

            float screenEdgeOffset = m_gridManager.CellSize;
            float cameraTopY = m_mainCamera.transform.position.y + m_mainCamera.orthographicSize;

            foreach (var data in newTileData)
            {
                var startWorldPos = new Vector3(data.TargetWorldPos.x, cameraTopY + screenEdgeOffset, data.TargetWorldPos.z);
                var newType = GetRandomTileTypeAvoidingInitialMatch(data.GridPos);
                var newTile = m_tileFactory.Get(startWorldPos, data.GridPos, newType);
                if (newTile == null) continue;

                m_tileObjects[data.GridPos] = newTile;

                float delayFactor = (verticalRange > 0.01f) ? (data.TargetWorldPos.y - minY) / verticalRange : 0;
                float delaySeconds = delayFactor * m_refillMaxStaggerDelay;

                refillTasks.Add(AnimateTileFall(newTile, data.TargetWorldPos, delaySeconds, cancellationToken));
            }

            await UniTask.WhenAll(refillTasks);
        }

        /// <summary>
        /// 타일 하나를 지정된 위치로 이동시키는 애니메이션을 실행합니다.
        /// </summary>
        private async UniTask AnimateTileFall(Tile tile, Vector3 targetPosition, float delay, CancellationToken cancellationToken)
        {
            if (delay > 0)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) return;

            await tile.transform.DOMove(targetPosition, m_refillMoveDuration)
                .SetEase(Ease.OutBack)
                .ToUniTask(cancellationToken: cancellationToken);
        }

        #endregion

        #region 매치 찾기

        private List<Tile> FindAllMatches()
        {
            var allMatchedTiles = new HashSet<Tile>();
            foreach (var tile in m_tileObjects.Values.ToList())
            {
                if (tile == null) continue;

                var matches = FindMatches(tile.GridPosition);
                if (matches.Count > 0)
                {
                    allMatchedTiles.UnionWith(matches);
                }
            }
            return allMatchedTiles.ToList();
        }

        private List<Tile> FindMatches(Vector2Int startPos)
        {
            Tile startTile = GetTileAt(startPos);
            if (startTile == null)
            {
                return new List<Tile>();
            }

            var matchedTiles = new HashSet<Tile>();
            var rightMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.right);
            var leftMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.left);

            if (rightMatches.Count + leftMatches.Count >= 2)
            {
                matchedTiles.UnionWith(rightMatches);
                matchedTiles.UnionWith(leftMatches);
            }

            var upMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.up);
            var downMatches = FindMatchesInDirection(startPos, startTile.Type, Vector2Int.down);

            if (upMatches.Count + downMatches.Count >= 2)
            {
                matchedTiles.UnionWith(upMatches);
                matchedTiles.UnionWith(downMatches);
            }

            if (matchedTiles.Count > 0)
            {
                matchedTiles.Add(startTile);
            }
            return matchedTiles.ToList();
        }

        private List<Tile> FindMatchesInDirection(Vector2Int startPos, TileType typeToMatch, Vector2Int direction)
        {
            var matches = new List<Tile>();
            for (int i = 1; i < 10; i++)
            {
                var nextPos = startPos + direction * i;
                Tile nextTile = GetTileAt(nextPos);
                if (nextTile != null && nextTile.Type == typeToMatch)
                {
                    matches.Add(nextTile);
                }
                else
                {
                    break;
                }
            }
            return matches;
        }

        private TileType GetRandomTileTypeAvoidingInitialMatch(Vector2Int pos)
        {
            var possibleTypes = System.Enum.GetValues(typeof(TileType))
                                      .Cast<TileType>()
                                      .Where(t => t < TileType.Bomb)
                                      .OrderBy(t => UnityEngine.Random.value)
                                      .ToList();

            foreach (var type in possibleTypes)
            {
                if (!CreatesInitialMatch(pos, type))
                {
                    return type;
                }
            }
            return possibleTypes.FirstOrDefault();
        }

        private bool CreatesInitialMatch(Vector2Int pos, TileType type)
        {
            // 수평 체크
            Tile r1 = GetTileAt(pos + Vector2Int.right);
            Tile l1 = GetTileAt(pos + Vector2Int.left);
            if (r1 != null && l1 != null && r1.Type == type && l1.Type == type) return true;

            Tile r2 = GetTileAt(pos + new Vector2Int(2, 0));
            if (r1 != null && r2 != null && r1.Type == type && r2.Type == type) return true;

            Tile l2 = GetTileAt(pos + new Vector2Int(-2, 0));
            if (l1 != null && l2 != null && l1.Type == type && l2.Type == type) return true;

            // 수직 체크
            Tile u1 = GetTileAt(pos + Vector2Int.up);
            Tile d1 = GetTileAt(pos + Vector2Int.down);
            if (u1 != null && d1 != null && u1.Type == type && d1.Type == type) return true;

            Tile u2 = GetTileAt(pos + new Vector2Int(0, 2));
            if (u1 != null && u2 != null && u1.Type == type && u2.Type == type) return true;

            Tile d2 = GetTileAt(pos + new Vector2Int(0, -2));
            if (d1 != null && d2 != null && d1.Type == type && d2.Type == type) return true;

            return false;
        }

        #endregion

        #region 유틸리티 및 디버그

        public Tile GetTileAt(Vector2Int pos)
        {
            m_tileObjects.TryGetValue(pos, out Tile tile);
            return tile;
        }

        private Vector3 GetWorldPositionFromGrid(int x, int y)
        {
            Vector3 localPos = m_gridManager.GetLocalPosition(x, y);
            return m_gridTransform.TransformPoint(localPos);
        }

        private Vector2Int GetGravityDirection()
        {
            switch (m_currentRotation)
            {
                case BoardRotation.Up: return Vector2Int.down;
                case BoardRotation.Right: return Vector2Int.right;
                case BoardRotation.Down: return Vector2Int.up;
                case BoardRotation.Left: return Vector2Int.left;
                default: return Vector2Int.down;
            }
        }

        private Vector2Int GetRotatedDirection(Vector2Int screenDirection)
        {
            switch (m_currentRotation)
            {
                case BoardRotation.Up: return screenDirection;
                case BoardRotation.Right: return new Vector2Int(-screenDirection.y, screenDirection.x);
                case BoardRotation.Down: return new Vector2Int(-screenDirection.x, -screenDirection.y);
                case BoardRotation.Left: return new Vector2Int(screenDirection.y, -screenDirection.x);
                default: return screenDirection;
            }
        }

        private void PrintBoardState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n<b>--- 현재 보드 상태 ---</b>");

            if (m_tileObjects == null || m_tileObjects.Count == 0)
            {
                sb.AppendLine("보드가 비어있습니다.");
                Debug.Log(sb.ToString());
                return;
            }

            int minX = m_currentLevelData.TilePositions.Min(p => p.x);
            int maxX = m_currentLevelData.TilePositions.Max(p => p.x);
            int minY = m_currentLevelData.TilePositions.Min(p => p.y);
            int maxY = m_currentLevelData.TilePositions.Max(p => p.y);

            for (int y = maxY; y >= minY; y--)
            {
                sb.Append($"Row {y,2}: ");
                for (int x = minX; x <= maxX; x++)
                {
                    var pos = new Vector2Int(x, y);
                    if (m_currentLevelData.TilePositions.Contains(pos))
                    {
                        Tile tile = GetTileAt(pos);
                        if (tile != null)
                        {
                            sb.Append($"[{tile.Type.ToString()[7]}]");
                        }
                        else
                        {
                            sb.Append("[ ]");
                        }
                    }
                    else
                    {
                        sb.Append("   ");
                    }
                }
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
        }

        #endregion
    }
}
