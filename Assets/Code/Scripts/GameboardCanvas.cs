using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using TiltFive;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.Scripts
{
    public interface IGameboardCanvasPointer
    {
        public class ButtonState
        {
            public bool TriggerDown;
        }

        public class PointerImpact
        {
            public IGameboardCanvasPointer Origin;
            public PlayerIndex PlayerIndex;
            public ControllerIndex ControllerIndex;
            public Vector3 Position;
            public object Data;
        }
        
        public bool ProcessGameboardCanvasPointer(Plane canvasPlane,
            out Vector3 intersection,
            out ButtonState buttonState,
            out object data);

        public void SetCanvasPointerImpacts([CanBeNull] PointerImpact impact, List<PointerImpact> otherImpacts);
    }
    
    public class GameboardCanvas : MonoBehaviour
    {
        private class GameboardPointerEventData : PointerEventData
        {
            public readonly PlayerIndex PlayerIndex;
            public readonly ControllerIndex ControllerIndex;
            public IGameboardCanvasPointer.ButtonState CurrentButtonState = new();
            public IGameboardCanvasPointer.ButtonState PreviousButtonState;
            
            public GameboardPointerEventData(EventSystem eventSystem, 
                PlayerIndex playerIndex, ControllerIndex controllerIndex) : base(eventSystem)
            {
                PlayerIndex = playerIndex;
                ControllerIndex = controllerIndex;
            }
        }

        private enum CardinalDirection
        {
            North,
            East,
            South,
            West
        }
        
        private static Quaternion CardinalDirectionToRotation(CardinalDirection direction)
        {
            return (direction) switch
            {
                CardinalDirection.South => Quaternion.Euler(0, 0, 0),
                CardinalDirection.West => Quaternion.Euler(0, 90, 0),
                CardinalDirection.North => Quaternion.Euler(0, 180, 0),
                CardinalDirection.East => Quaternion.Euler(0, 270, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
            };
        }
        
        private readonly Dictionary<PlayerIndex, CardinalDirection> _currentPlayerRotation = new();
        private readonly Dictionary<PlayerIndex, GameObject> _surfaces = new();
        private readonly Dictionary<CardinalDirection, Plane> _uiPlanes = new();

        [Header("Initial State")]
        public Canvas initialCanvas;
        
        [Header("Materials")]
        public Material canvasMaterial;
        
        [Header("Layers")]
        [Layer] public int playerOneLayer;
        [Layer] public int playerTwoLayer;
        [Layer] public int playerThreeLayer;
        [Layer] public int playerFourLayer;
        
        [Header("Thresholds")]
        [Range(0.1f, 1f)] 
        public float reClickTimeLimit = 0.5f;
        [Range(0.2f, 10f)]
        public float dragThresholdMultiplier = 5.0f;
        
        private const int TextureSize = 1024;
        
        private Camera _renderCamera;

        private static readonly Dictionary<IGameboardCanvasPointer, GameboardPointerEventData> Pointers = new();
        private static readonly int Mode = Shader.PropertyToID("_Mode");
        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        public static void AddGameboardCanvasPointer(IGameboardCanvasPointer pointer, PlayerIndex playerIndex, ControllerIndex controllerIndex)
        {
            Pointers.Add(pointer, new GameboardPointerEventData(EventSystem.current, playerIndex ,controllerIndex));
        }
        
        public static void RemoveGameboardCanvasPointer(IGameboardCanvasPointer pointer)
        {
            Pointers.Remove(pointer);
        }

        private GameObject CreateSurface(PlayerIndex player, Texture texture)
        {
            // Create the display surface
            var displaySurface = new GameObject("Gameboard UI Canvas (Player " + player + ")")
            {
                transform =
                {
                    position = new Vector3(5.37f, 1.75f, 0f),
                    rotation = Quaternion.Euler(45f, 90f, 0),
                    localScale = new Vector3(7f, 1f, 4f)
                }
            };
            var meshFilter = displaySurface.AddComponent<MeshFilter>();
            meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx");
            
            // Set the basic material properties
            var meshRenderer = displaySurface.AddComponent<MeshRenderer>();
            meshRenderer.material = canvasMaterial;
            meshRenderer.material.mainTexture = texture;

            return displaySurface;
        }

        private void MatchCameraToSurface(GameObject surface)
        {
            // Get the aspect ratio of the plane
            var gameboardCanvasPosition = surface.transform.position;
            var gameboardCanvasScale = surface.transform.localScale;
            var planeAspectRatio = gameboardCanvasScale.x / gameboardCanvasScale.z;
            
            // Set the aspect ratio of the camera to match the plane
            _renderCamera.aspect = planeAspectRatio;
            _renderCamera.orthographicSize = gameboardCanvasScale.z / 2;

            _renderCamera.transform.position = gameboardCanvasPosition + surface.transform.up * 10;
            _renderCamera.transform.LookAt(gameboardCanvasPosition);
        }

        public static GameboardCanvas Instance;
        
        private void Awake()
        {
            // Check for existing instances and destroy this instance if we've already got one
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Destroying duplicate GameboardCanvas");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            var root = new GameObject("Gameboard UI");

            EventSystem.current.pixelDragThreshold =
                (int)(EventSystem.current.pixelDragThreshold * dragThresholdMultiplier);
            
            // Create the texture
            var newTexture = new RenderTexture(TextureSize, TextureSize, 24);
            
            // Create the camera
            _renderCamera = new GameObject("Gameboard UI Canvas Renderer").AddComponent<Camera>();
            _renderCamera.orthographic = true; // Set to Orthographic projection
            _renderCamera.cullingMask = LayerMask.GetMask("UI");
            _renderCamera.clearFlags = CameraClearFlags.SolidColor;
            _renderCamera.backgroundColor = new Color(0, 0, 0, 0);
            _renderCamera.depth = 1;
            _renderCamera.targetTexture = newTexture;
            
            // Create surfaces in the cardinal directions
            var players = Enum.GetValues(typeof(PlayerIndex))
                .Cast<PlayerIndex>()
                .Where(index => index != PlayerIndex.None);
            foreach (var player in players)
            {
                // Create the pivot
                var surfacePivot = new GameObject("Gameboard UI (Player " + player + ")");
                surfacePivot.transform.SetParent(root.transform);
                
                // Create the surface
                var surface = CreateSurface(player, newTexture);
                surface.transform.SetParent(surfacePivot.transform);

                surface.layer = (player) switch
                {
                    PlayerIndex.One => playerOneLayer,
                    PlayerIndex.Two => playerTwoLayer,
                    PlayerIndex.Three => playerThreeLayer,
                    PlayerIndex.Four => playerFourLayer,
                    PlayerIndex.None => throw new ArgumentOutOfRangeException(),
                    _ => throw new ArgumentOutOfRangeException()
                };
                
                // Use the first player surface to create the intersection planes, and attach
                // the camera to first 'camera' surface - pointer event raycasts are rotated
                // before rendering for all players back, and we only want one 'copy' of the UI
                if (player == PlayerIndex.One)
                {
                    MatchCameraToSurface(surface);
                    foreach (CardinalDirection direction in Enum.GetValues(typeof(CardinalDirection)))
                    {
                        surfacePivot.transform.rotation = CardinalDirectionToRotation(direction);
                        _uiPlanes[direction] = new Plane(surface.transform.up, surface.transform.position);
                    }
                }
                
                _surfaces[player] = surfacePivot;
            }
        }

        private Vector2 WorldToScreenPoint(PlayerIndex playerIndex, Vector3 position)
        {
            var rotation = Quaternion.Inverse(CardinalDirectionToRotation(_currentPlayerRotation[playerIndex]));
            return _renderCamera.WorldToScreenPoint(rotation * position);
        }

        private readonly Stack<Canvas> _canvas = new();
        
        private void Start()
        {
            if (initialCanvas) PushCanvas(initialCanvas);
        }

        public void PushCanvas(Canvas canvas)
        {
            // Disable the current top canvas
            if (_canvas.TryPeek(out var currentTopCanvas))
            {
                currentTopCanvas.enabled = false;
            }

            // Reconfigure the new canvas, enable it and push it ont the tack
            canvas.worldCamera = _renderCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.enabled = true;
            _canvas.Push(canvas);
        }

        public void PopCanvas()
        {
            // Try to pop the top stack entry
            if (!_canvas.TryPop(out var poppedCanvas))
            {
                Debug.LogWarning("Discarding attempt to pop empty canvas stack");
            }

            // Disable the popped canvas
            poppedCanvas.enabled = false;
            
            // If we've revealed another canvas, enable it
            if (_canvas.TryPeek(out var newTop))
            {
                newTop.enabled = true;
            }
        }
        
        private static CardinalDirection GetPlayerQuadrant(PlayerIndex playerIndex)
        {
            Glasses.TryGetPose(playerIndex, out var pose);

            var closerToX = Mathf.Abs(pose.position.x) > Mathf.Abs(pose.position.z);
            return (pose.position.x, pose.position.z, closerToX) switch
            {
                (>=0, <0, true) => CardinalDirection.South,
                (>=0, >=0, true) => CardinalDirection.South,
                (>=0, <0, false) => CardinalDirection.West,
                (<0, <0, false) => CardinalDirection.West,
                (<0, >=0, true) => CardinalDirection.North,
                (<0, <0, true) => CardinalDirection.North,
                (<0, >=0, false) => CardinalDirection.East,
                (>=0, >=0, false) => CardinalDirection.East,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private void Update()
        {
            RotateCanvases();
            ProcessPointers();
        }

        private void RotateCanvases()
        {
            var players = Enum.GetValues(typeof(PlayerIndex)).Cast<PlayerIndex>().Where(index => index != PlayerIndex.None);
            foreach (var player in players) RotateCanvas(player);
        }

        private void RotateCanvas(PlayerIndex playerIndex)
        {
            var canvasRotation = GetPlayerQuadrant(playerIndex);
            
            // Only apply rotations if they've changed
            if (_currentPlayerRotation.TryGetValue(playerIndex, out var lastRotation) && lastRotation == canvasRotation)
            {
                return;
            }
            _currentPlayerRotation[playerIndex] = canvasRotation;

            _surfaces[playerIndex].transform.rotation = CardinalDirectionToRotation(canvasRotation);
        }
        
        private bool GetPointerIntersection(PlayerIndex playerIndex, IGameboardCanvasPointer pointer, Plane plane,
            out Vector2 uiPosition, out Vector3 canvasPosition, out IGameboardCanvasPointer.ButtonState buttonState,
            out object data)
        {
            var intersects = pointer.ProcessGameboardCanvasPointer(plane, out var pointerIntersection, out var pointerButtonState, out var pointerData);
            buttonState = pointerButtonState;
            data = pointerData;
            
            uiPosition = intersects ? WorldToScreenPoint(playerIndex, pointerIntersection) : new Vector2();
            canvasPosition = intersects ? pointerIntersection : new Vector2();
            return intersects;
        }

        private static RaycastResult GetPointerRaycast(PointerEventData pointerEventData)
        {
            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);
            
            return raycastResults.FirstOrDefault(raycastResult => raycastResult.gameObject);
        }
        
        [CanBeNull]
        private static GameObject GetCommonAncestor(GameObject a, GameObject b)
        {
            if (!a || !b)
            {
                return null;
            }

            if (a == b)
            {
                return a;
            }
            
            // Collect the hashset of all ancestors of A by traversing up the parents of A
            var ancestorsOfA = new HashSet<Transform>();
            var currenTransform = a.transform;
            while (currenTransform)
            {
                ancestorsOfA.Add(currenTransform);
                currenTransform = currenTransform.parent;
            }

            // Traverse up parents of B until we find an element that is an ancestor of A
            currenTransform = b.transform;
            while (currenTransform)
            {
                if (ancestorsOfA.Contains(currenTransform))
                {
                    return currenTransform.gameObject;
                }
                currenTransform = currenTransform.parent;
            }
            
            return null;
        }

        private static void ProcessExits(PointerEventData pointerEventData, 
            GameObject previousEnter, [CanBeNull] GameObject commonAncestor)
        {
            // Walk up the tree from the exited object until we reach the
            // common ancestor, exiting objects as we go (E.g. A2 -> A1)
            var currentTransform = previousEnter.transform;
            var commonAncestorTransform = commonAncestor ? commonAncestor.transform : null;
            while (currentTransform && currentTransform != commonAncestorTransform)
            {
                var obj = currentTransform.gameObject;
                ExecuteEvents.Execute(obj, pointerEventData, ExecuteEvents.pointerExitHandler);
                pointerEventData.hovered.Remove(obj);
                currentTransform = currentTransform.parent;
            }
        }

        private static void ProcessEnters(PointerEventData pointerEventData,
            GameObject currentEnter, [CanBeNull] GameObject commonAncestor)
        {
            // Walk up the tree from the entered object until we
            // reach the common ancestor, collecting references. 
            var enteredParents = new List<Transform>();                
            var currentTransform = currentEnter.transform;
            var commonAncestorTransform = commonAncestor ? commonAncestor.transform : null;
            while (currentTransform && currentTransform != commonAncestorTransform)
            {
                enteredParents.Add(currentTransform);
                currentTransform = currentTransform.parent;
            }

            // After walking the tree, reverse the entered list, and
            // iterate it entering objects as we go (E.g. B1 -> B2)
            enteredParents.Reverse();
            foreach (var obj in enteredParents.Select(entered => entered.gameObject))
            {
                ExecuteEvents.Execute(obj, pointerEventData, ExecuteEvents.pointerEnterHandler);
                pointerEventData.hovered.Add(obj);
            }
        }

        private static void ProcessExitEnters(PointerEventData pointerEventData)
        {
            // Exit/Enter tracking
            // ═══════════════════
            //
            //       ╭┄┄┄┄┄┄┄┄┄┄┄┄╮
            //       ┆            ▼
            //     ┌────┐       ┌────┐   
            //     │ A2 │       │ B2 │
            //   ┌─┴────┴─┐   ┌─┴────┴─┐
            //   │   A1   │   │   B1   │
            // ┌─┴────────┴───┴────────┴─┐
            // │            C            │
            // └─────────────────────────┘
            // ┆                         ┆ 
            // ┌─────────────────────────┐
            // │ ┌────────┐   ┌────────┐ │
            // │ │ ┌────┐ │   │ ┌────┐ │ │
            // │ │ │ A2 │ │   │ │ B2 │ │ │
            // │ │ └────┘ │   │ └────┘ │ │ 
            // │ └────────┘   └────────┘ │
            // └─────────────────────────┘
            // 
            // Consider entering UI element B2, when we were last recorded as entering A2.
            // We need to exit elements A2, and A1, and enter elements B1 and B2.
            // Element C doesn't change since it's common to both A2 and B2.
            var previousEnter = pointerEventData.pointerEnter;
            var currentEnter = pointerEventData.pointerCurrentRaycast.gameObject;
            var commonAncestor = GetCommonAncestor(previousEnter, currentEnter);
            
            if (previousEnter) ProcessExits(pointerEventData, previousEnter, commonAncestor);
            pointerEventData.pointerEnter = currentEnter;
            if (currentEnter) ProcessEnters(pointerEventData, currentEnter, commonAncestor);
        }
        
        private static void ProcessPointerMove(PointerEventData pointerEventData)
        {
            // If our raycast didn't hit anything, we can't be hovering over anything any more,
            // so send exit events to all hovered GameObjects and return
            if (!pointerEventData.pointerCurrentRaycast.gameObject)
            {
                // Exit and clear all 'hovered' objects
                foreach (var obj in pointerEventData.hovered)
                {
                    ExecuteEvents.Execute(obj, pointerEventData, ExecuteEvents.pointerExitHandler);
                }
                pointerEventData.hovered.Clear();
                
                // Clear the 'last entered' object
                pointerEventData.pointerEnter = null;
                return;
            }
            
            // If we've moved, send move events to all objects we're currently hovering over
            var isMoving = pointerEventData.IsPointerMoving();
            if (isMoving)
            {
                foreach (var obj in pointerEventData.hovered)
                {
                    ExecuteEvents.Execute(obj, pointerEventData, ExecuteEvents.pointerMoveHandler);
                }
            }
            
            // Process exiting objects we're no longer over, and entering objects we've now entered
            ProcessExitEnters(pointerEventData);
        }

        private void ProcessPointerButtonDown(GameObject obj, PointerEventData pointerEventData)
        {
            // Inform the event system of the new selection (if it's changed)
            var selectHandlerObj = ExecuteEvents.GetEventHandler<ISelectHandler>(obj);
            if (selectHandlerObj && selectHandlerObj != EventSystem.current.currentSelectedGameObject)
            {
                EventSystem.current.SetSelectedGameObject(selectHandlerObj);                
            }
         
            // Send a 'down' event to the hit GameObject. If no GameObject received the 'down' event,
            // check to see if any of the objects would have accepted a 'Click' instead
            var downObj = ExecuteEvents.ExecuteHierarchy(obj, pointerEventData, ExecuteEvents.pointerDownHandler);
            if (!downObj)
            {
                downObj = ExecuteEvents.GetEventHandler<IPointerClickHandler>(obj);
            }

            // If the object receiving the 'down' (or fallback 'click') event is different from
            // the last clicked object, reset the click trackers and counters.
            // Also reset the click counters if it's been more than the re-click time threshold.
            var clickTargetChanged = downObj != pointerEventData.pointerPress;
            var clickExpired = (Time.unscaledTime - pointerEventData.clickTime) > reClickTimeLimit;
            if (clickTargetChanged || clickExpired)
            {
                pointerEventData.clickTime = default;
                pointerEventData.clickCount = 0;
            }

            pointerEventData.dragging = false; 
            pointerEventData.useDragThreshold = true;
            pointerEventData.eligibleForClick = true;
            pointerEventData.pressPosition = pointerEventData.position; 
            pointerEventData.pointerPress = downObj;
            pointerEventData.rawPointerPress = obj;
        }
        
        private static void ProcessPointerButtonUp(GameObject obj, PointerEventData pointerEventData)
        {
            // Send a 'up' event to the hit GameObject. If no GameObject received the 'down' event,
            // check to see if any of the objects would have accepted a 'Click' instead
            var upObj = ExecuteEvents.ExecuteHierarchy(obj, pointerEventData, ExecuteEvents.pointerUpHandler);
            if (!upObj)
            {
                upObj = ExecuteEvents.GetEventHandler<IPointerClickHandler>(obj);
            }

            // If we're dragging, 'up' events are treated as 'drop' instead of 'click' 
            if (pointerEventData.dragging)
            {
                ExecuteEvents.ExecuteHierarchy(obj, pointerEventData, ExecuteEvents.dropHandler);
            }
            else
            {
                // Check if this is an 'up' event for a button we didn't press 'down' on 
                var clickTargetChanged = upObj != pointerEventData.pointerPress;
                
                // Check if the event is eligible for a click and that the hit GameObject has a click handler.
                if (pointerEventData.eligibleForClick && !clickTargetChanged)
                {
                    pointerEventData.pointerClick = obj;
                    pointerEventData.clickTime = Time.unscaledTime;
                    pointerEventData.clickCount++;
                
                    ExecuteEvents.Execute(upObj, pointerEventData, ExecuteEvents.pointerClickHandler);
                }
            }
            
            // Clear the 'pressed' state 
            pointerEventData.eligibleForClick = false;
            pointerEventData.pointerPress = null;
            pointerEventData.rawPointerPress = null;
        }
        
        private void ProcessPointerButton(GameboardPointerEventData pointerEventData)
        {
            var previousState = pointerEventData.PreviousButtonState.TriggerDown;
            var currentState = pointerEventData.CurrentButtonState.TriggerDown;
            var buttonChanged = previousState != currentState;
            if (!buttonChanged) return;
            
            // Set up event for press/click handling
            pointerEventData.pointerPressRaycast = pointerEventData.pointerCurrentRaycast;
            
            if (currentState)
            {
                // Get the target of button events, and exit if there isn't one
                var obj = pointerEventData.pointerCurrentRaycast.gameObject;
                if (!obj) return;
                
                ProcessPointerButtonDown(obj, pointerEventData);
            }
            else
            {
                // Get the target of button events (or the dragged object if we're dragging),
                // and exit if both are unset
                var obj = pointerEventData.pointerCurrentRaycast.gameObject;
                if (!obj)
                {
                    obj = pointerEventData.pointerDrag;
                    if (!obj) return;
                }
                
                ProcessPointerButtonUp(obj, pointerEventData);
            }
        }
        
        private static void ProcessPointerDrag(PointerEventData pointerEventData)
        {
            if (pointerEventData.pointerPress)
            {
                if (!pointerEventData.dragging)
                {   // Pointer is down, but we're not dragging - start a drag
                    
                    // Get the target of button events, and exit if there isn't one
                    var obj = pointerEventData.pointerCurrentRaycast.gameObject;
                    if (!obj) return;
                    
                    // Regardless of if we've moved far enough to start a drag,
                    // we should send an initializePotentialDrag
                    pointerEventData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(obj);
                    if (pointerEventData.pointerDrag)
                    {
                        ExecuteEvents.Execute(pointerEventData.pointerDrag, 
                            pointerEventData, ExecuteEvents.initializePotentialDrag);                        
                    }
                    
                    var shouldStartDrag = !pointerEventData.useDragThreshold;
                    if (!shouldStartDrag)
                    {
                        var sqDragThreshold = EventSystem.current.pixelDragThreshold * 
                                              EventSystem.current.pixelDragThreshold;
                        var sqDistanceFromDown =
                            (pointerEventData.pressPosition - pointerEventData.position).sqrMagnitude;
                        shouldStartDrag = sqDistanceFromDown > sqDragThreshold;
                    }

                    if (shouldStartDrag)
                    {
                        if (pointerEventData.pointerDrag)
                        {
                            ExecuteEvents.Execute(pointerEventData.pointerDrag, 
                                pointerEventData, ExecuteEvents.beginDragHandler);                        
                        }
                        pointerEventData.dragging = true;
                    }
                }
                
                if (pointerEventData.dragging && pointerEventData.IsPointerMoving())
                {
                    // Pointer is down, and we're dragging - handle a drag move
                    ExecuteEvents.Execute(pointerEventData.pointerDrag, pointerEventData, ExecuteEvents.dragHandler);
                }
            }
            else if (pointerEventData.dragging)
            {
                // Pointer is up, but we're dragging - end the drag event
                ExecuteEvents.Execute(pointerEventData.pointerDrag, pointerEventData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(pointerEventData.pointerDrag, pointerEventData, ExecuteEvents.endDragHandler);
                pointerEventData.dragging = false;
                pointerEventData.pointerDrag = null;
            }
        }
        
        private void ProcessPointers()
        {
            var impacts = new List<IGameboardCanvasPointer.PointerImpact>();
            
            foreach (var (pointer, pointerEventData) in Pointers)
            {
                var playerIndex = pointerEventData.PlayerIndex;
                
                // Get the rotation and intersection plane of the current player
                if (!_currentPlayerRotation.TryGetValue(playerIndex, out var playerRotation))
                {
                    continue;
                }
                var plane = _uiPlanes[playerRotation];
                
                // Get the intersection of the pointer with the canvas, looping if no intersect is found
                var intersects = GetPointerIntersection(playerIndex, pointer, plane, 
                    out var uiPosition, out var canvasPosition, out var buttonState, out var data);
                if (!intersects)
                {
                    continue;
                }

                // We've intersected the canvas - raycast to find which GameObject (if any) we're intersecting with
                var raycastResult = GetPointerRaycast(pointerEventData);
                
                // Prepare the pointer data for this frame
                pointerEventData.Reset();
                pointerEventData.PreviousButtonState = pointerEventData.CurrentButtonState;
                pointerEventData.CurrentButtonState = buttonState;
                pointerEventData.button = PointerEventData.InputButton.Left;
                pointerEventData.delta = uiPosition - pointerEventData.position; 
                pointerEventData.position = uiPosition;
                pointerEventData.pointerCurrentRaycast = raycastResult;

                ProcessPointerMove(pointerEventData);
                ProcessPointerButton(pointerEventData);
                ProcessPointerDrag(pointerEventData);
                
                // If we impacted, add to the impact list
                if (raycastResult.gameObject)
                {
                    impacts.Add(new IGameboardCanvasPointer.PointerImpact
                    {
                        Origin = pointer,
                        PlayerIndex = playerIndex,
                        ControllerIndex = pointerEventData.ControllerIndex,
                        Position = canvasPosition,
                        Data = data
                    });
                }
            }

            // Notify the pointers of the canvas impacts
            foreach (var (pointer, lastPointerEvent) in Pointers)
            {
                IGameboardCanvasPointer.PointerImpact pointerImpact = null;
                var otherImpacts = new List<IGameboardCanvasPointer.PointerImpact>();
                
                foreach (var impact in impacts)
                {
                    if ((impact.PlayerIndex == lastPointerEvent.PlayerIndex) && 
                        (impact.ControllerIndex == lastPointerEvent.ControllerIndex))
                    {
                        pointerImpact = impact;
                    } else
                    {
                        var rotationForCursor =
                            Quaternion.Inverse(_surfaces[impact.PlayerIndex].transform.rotation) *
                            _surfaces[lastPointerEvent.PlayerIndex].transform.rotation;
                        
                        // Rotate the pointer into the necessary plane for the pointer
                        otherImpacts.Add(new IGameboardCanvasPointer.PointerImpact()
                        {
                            PlayerIndex = impact.PlayerIndex,
                            ControllerIndex = impact.ControllerIndex,
                            Position = rotationForCursor * impact.Position,
                            Data = impact.Data,
                            Origin = impact.Origin,
                        });
                    }
                }
                
                pointer.SetCanvasPointerImpacts(pointerImpact, otherImpacts); 
            }
        }

        private static void DrawPlane(Vector3 inNormal, Vector3 inPoint) {
            var planeColor = Color.yellow;
            var rayColor = Color.magenta;
            
            var offset = Vector3.Cross(inNormal, 
                (inNormal.normalized != Vector3.forward) ? Vector3.forward : Vector3.up)
                .normalized * inNormal.magnitude;

            for (var i = 0; i <= 180; i += 20)
            {
                var rotatedOffset = Quaternion.AngleAxis(i, inNormal) * offset;
                Debug.DrawLine(inPoint + rotatedOffset, inPoint - rotatedOffset, planeColor);
            }
            Debug.DrawLine(inPoint, inPoint + inNormal, rayColor);
        }
    }
}
