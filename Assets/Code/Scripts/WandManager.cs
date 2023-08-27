using System;
using TiltFive;
using UnityEngine;
using System.Collections.Generic;
using JetBrains.Annotations;
using TiltFive.Logging;

namespace Code.Scripts
{
    public interface IWandActuator
    {
        public void SetPlayerIndex(PlayerIndex playerIndex);
    }
    
    public class WandManager : MonoBehaviour
    {
        [Header("Arc")]
        [Range(0.05f, 1f)]
        public float arcWidth = 0.1f;
        [Range(0.05f, 1f)]
        public float arcTimeStep = 0.1f;
        [Min(0f)]
        public float arcLaunchVelocity = 10;
        public Material arcMaterial;
        [Layer] public bool arcVisibleToAll;
        
        [Header("Actuators")]
        public bool enableLeftWand;
        [ConditionalShow("enableLeftWand")] public GameObject leftActuatorObject;
        [ConditionalShow("enableLeftWand")] public bool leftActuatorVisibleToAll;
        public bool enableRightWand;
        [ConditionalShow("enableRightWand")] public GameObject rightActuatorObject;
        [ConditionalShow("enableRightWand")] public bool rightActuatorVisibleToAll;
        
        [Header("Canvas")]
        public GameObject canvasCursorObject;
        public GameObject canvasOtherCursorObject;
        
        [Layer] public int playerOneLayer;
        [Layer] public int playerTwoLayer;
        [Layer] public int playerThreeLayer;
        [Layer] public int playerFourLayer;
        
        public static WandManager Instance { get; private set; }
        
        private static void SetWandObjectsForPlayer(PlayerIndex playerIndex, 
            ControllerIndex hand,
            [CanBeNull] GameObject aimObject = null, 
            [CanBeNull] GameObject gripObject = null, 
            [CanBeNull] GameObject fingertipObject = null)
        {
            var t5 = TiltFiveManager2.Instance;
            var settings = (playerIndex, hand) switch
            {
                (PlayerIndex.One, ControllerIndex.Left) => t5.playerOneSettings.leftWandSettings,
                (PlayerIndex.One, ControllerIndex.Right) => t5.playerOneSettings.rightWandSettings,
                (PlayerIndex.Two, ControllerIndex.Left) => t5.playerTwoSettings.leftWandSettings,
                (PlayerIndex.Two, ControllerIndex.Right) => t5.playerTwoSettings.rightWandSettings,
                (PlayerIndex.Three, ControllerIndex.Left) => t5.playerThreeSettings.leftWandSettings,
                (PlayerIndex.Three, ControllerIndex.Right) => t5.playerThreeSettings.rightWandSettings,
                (PlayerIndex.Four, ControllerIndex.Left) => t5.playerFourSettings.leftWandSettings,
                (PlayerIndex.Four, ControllerIndex.Right) => t5.playerFourSettings.rightWandSettings,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            settings.AimPoint = aimObject;
            settings.GripPoint = gripObject;
            settings.FingertipPoint = fingertipObject;
        }

        private static void CheckActuator(GameObject actuatorObject)
        {
            if (!actuatorObject)
            {
                Log.Warn("Wand Actuator GameObject not specified");
            }

            var wandActuator = actuatorObject.GetComponent<IWandActuator>();
            if (wandActuator == null)
            {
                Log.Warn("Wand Actuator GameObject does not include a component that " +
                         "implements IWandActuator - Actuator won't be attached");
            }
        }

        private void CreateWand(PlayerIndex playerIndex, 
                                ControllerIndex controllerIndex)
        {
            var namePrefix = "Wand_" + playerIndex + "_" + controllerIndex;
            
            var wand = new GameObject(namePrefix);
            wand.transform.SetParent(transform);
            
            var aimObject = new GameObject(namePrefix + "_Aim");
            SetWandObjectsForPlayer(playerIndex, controllerIndex, aimObject: aimObject);
            aimObject.transform.SetParent(wand.transform);
                
            var wandBehaviour = aimObject.AddComponent<Wand>();
            wandBehaviour.playerIndex = playerIndex;
            wandBehaviour.controllerIndex = controllerIndex;
        }
        
        private void Awake()
        {
            // Check for existing instances and destroy this instance if we've already got one
            if (Instance != null && Instance != this)
            {
                Log.Warn("Destroying duplicate WandManager");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            PlayerIndex[] players =
            {
                PlayerIndex.One, 
                PlayerIndex.Two,
                PlayerIndex.Three,
                PlayerIndex.Four
            };

            // Check the actuator is valid
            if (enableLeftWand) CheckActuator(leftActuatorObject);
            if (enableRightWand) CheckActuator(rightActuatorObject);

            // Create the wands
            foreach (var playerIndex in players)
            {
                if (enableLeftWand) CreateWand(playerIndex, ControllerIndex.Left);
                if (enableRightWand) CreateWand(playerIndex, ControllerIndex.Right);
            }
        }
    }
    
    public class Wand : MonoBehaviour, IGameboardCanvasPointer
    {
        public PlayerIndex playerIndex;
        public ControllerIndex controllerIndex;
        
        private Stack<GameObject> _canvasOtherCursorObjects = new();
        private LineRenderer _lineRenderer;
        private List<Vector3> _points = new();
        private bool _canvasCursorActive;
        private int? _canvasArcLimit;
        private Vector3? _canvasArcImpact;
        private bool _wandObserved;
        private int _otherCursorInstanceCount;
        private int _playerLayer;

        private GameObject _actuator;
        private GameObject _canvasCursor;
        private GameObject _canvasOtherCursorTemplate;

        private void Awake()
        {

        }

        private void Start()
        {
            var wandManager = WandManager.Instance;
            var namePrefix = "Wand_" + playerIndex + "_" + controllerIndex;
            
            // Get the layer for the player
            _playerLayer = playerIndex switch
            {
                PlayerIndex.One => wandManager.playerOneLayer,
                PlayerIndex.Two => wandManager.playerTwoLayer,
                PlayerIndex.Three => wandManager.playerThreeLayer,
                PlayerIndex.Four => wandManager.playerFourLayer,
                PlayerIndex.None => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null),
                _ => throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null)
            };
            
            // Create the arc
            var arc = new GameObject("WandArc")
            {
                layer = (wandManager.arcVisibleToAll) ? 0 : _playerLayer
            };

            // Add and configure the LineRenderer
            _lineRenderer = arc.AddComponent<LineRenderer>();
            _lineRenderer.transform.SetParent(transform);
            _lineRenderer.material = wandManager.arcMaterial;
            _lineRenderer.widthCurve = AnimationCurve.Linear(0, 0, 1, wandManager.arcWidth);
            
            // Create and attach an actuator
            var actuatorObject = controllerIndex == ControllerIndex.Left
                ? wandManager.leftActuatorObject : wandManager.rightActuatorObject;
            if (actuatorObject)
            {
                _actuator = Instantiate(actuatorObject, transform, true);
                _actuator.name = namePrefix + "_Actuator";

                // Set the actuator GameObject layer to the player layer if it's not 'visible to all'
                var actuatorVisibleToAll = (controllerIndex == ControllerIndex.Left)
                    ? wandManager.leftActuatorVisibleToAll
                    : wandManager.rightActuatorVisibleToAll;
                if (!actuatorVisibleToAll) _actuator.layer = _playerLayer; 
                
                // Assign the playerIndex to the actuator (via its interface)
                var wandActuator = _actuator.GetComponent<IWandActuator>();
                wandActuator.SetPlayerIndex(playerIndex);
                
                // Actuator is initially disabled - we'll enable it when we first detect the wand
                _actuator.SetActive(false);
            }

            // Create and attach a canvas cursor
            if (wandManager.canvasCursorObject)
            {
                _canvasCursor = Instantiate(wandManager.canvasCursorObject, transform, true);
                _canvasCursor.name = namePrefix + "_CanvasCursor";
                _canvasCursor.layer = _playerLayer;
            }

            // Create and attach a canvas cursor for other players
            if (wandManager.canvasOtherCursorObject)
            {
                _canvasOtherCursorTemplate = Instantiate(wandManager.canvasOtherCursorObject, transform, true);
                _canvasOtherCursorTemplate.name = namePrefix + "_CanvasOtherCursor";
                _canvasOtherCursorTemplate.layer = _playerLayer;
            }
            
            // Register the pointer with the gameboard manager
            GameboardCanvas.AddGameboardCanvasPointer(this, playerIndex, controllerIndex);
        }

        private void OnDestroy()
        {
            GameboardCanvas.RemoveGameboardCanvasPointer(this);
        }

        private void Update()
        {
            DrawArc();

            // If we've not observed the wand yet, try to detect it
            if (!_wandObserved)
            {
                TiltFive.Wand.TryCheckConnected(out var connected, playerIndex, controllerIndex);
                _wandObserved = connected;

                if (!_wandObserved)
                {
                    return;
                }
            }

            // Ensure the actuator is active if it should be, or inactive if it shouldn't be
            var isActive = _actuator.activeSelf;
            var shouldBeActive = !_canvasArcLimit.HasValue;
            if (isActive != shouldBeActive)
            {
                _actuator.SetActive(shouldBeActive);
            }
        }

        private Quaternion ComputeWandRotation()
        {
            var forward = transform.forward;
            var horizontalDirection = new Vector3(forward.x, 0, forward.z).normalized;
            var azimuth = Mathf.Atan2(horizontalDirection.z, horizontalDirection.x) * Mathf.Rad2Deg - 90;
            return Quaternion.Euler(0, -azimuth, 0);
        }
        
        private float ComputeWandElevation()
        {
            var forward = transform.forward;
            return Mathf.Atan2(forward.y, new Vector3(forward.x, 0, forward.z).magnitude) * Mathf.Rad2Deg;
        }
        
        private static Vector3 ComputeInitialVelocity(Quaternion wandRotation, float elevation)
        {
            // Compute the initial velocity, rotated into the azimuth of the wand
            var radianAngle = Mathf.Deg2Rad * elevation;
            return wandRotation * new Vector3(
                0, 
                WandManager.Instance.arcLaunchVelocity * Mathf.Sin(radianAngle), 
                WandManager.Instance.arcLaunchVelocity * Mathf.Cos(radianAngle)
            );
        }

        private List<Vector3> ComputeArc(float gravity, Vector3 initialVelocity)
        {
            var wandManager = WandManager.Instance;
            var arcTimeStep = wandManager.arcTimeStep;
            
            // Prepare the points list
            var currentPosition = transform.position;
            var points = new List<Vector3> { currentPosition };

            // Add points to the list until we cross the (global) Y=0 plane,
            // adjusting velocity to account for gravity
            var velocity = initialVelocity;
            while (currentPosition.y >= 0)
            {
                velocity.y -= gravity * arcTimeStep;
                currentPosition += velocity * arcTimeStep;
                points.Add(currentPosition);
                
                // Continue, or Abort if we're producing an unreasonably long arc
                if (points.Count <= 200) continue;
                break;
            }

            return points;
        }
        
        private static Vector3? ComputeImpactPoint(List<Vector3> points)
        {
            if (points.Count < 2)
            {
                return null;
            }
            var pointAbove = points[^2];
            var pointBelow = points[^1];
            
            // The points are the same, so just take either one
            if (Math.Abs(pointAbove.y - pointBelow.y) < 0.001)
            {
                return pointAbove;
            }
            
            var t = (0 - pointBelow.y) / (pointAbove.y - pointBelow.y);
            return Vector3.Lerp(pointBelow, pointAbove, t);
        }
        
        private void DrawArc()
        {
            // Get (current) gravity and compute velocity
            var gravity = Mathf.Abs(Physics.gravity.y);
            var wandRotation = ComputeWandRotation();
            var wandElevation = ComputeWandElevation();
            var velocity = ComputeInitialVelocity(wandRotation, wandElevation);
            _points = ComputeArc(gravity, velocity);

            // Set the line renderer points
            var points = _points.ToArray();
            if (_canvasArcLimit.HasValue && _canvasArcLimit.Value < _points.Count)
            {
                _lineRenderer.positionCount = _canvasArcLimit.Value + 1;
                points[_canvasArcLimit.Value] = _canvasArcImpact ?? points[_canvasArcLimit.Value];
            }
            else
            {
                _lineRenderer.positionCount = _points.Count;
            }
            _lineRenderer.SetPositions(points);
            
            // Return if there's no actuator
            if (!_actuator) return;
            
            // Compute the impact point, and move the actuator there
            var impactPoint = ComputeImpactPoint(_points);
            if (!impactPoint.HasValue) return;
            _actuator.transform.position = impactPoint.Value;
            _actuator.transform.rotation = wandRotation;
        }
        
        public bool ProcessGameboardCanvasPointer(
            Plane canvasPlane, 
            out Vector3 intersection, 
            out IGameboardCanvasPointer.ButtonState buttonState,
            out object data) {
            // Set the trivial `out` arguments
            buttonState = new IGameboardCanvasPointer.ButtonState()
            {
                TriggerDown = TiltFive.Input.GetTrigger(playerIndex: playerIndex) > 0.5
            };
            
            for (var i = 0; i < _points.Count - 1; i++)
            {
                var lineStart = _points[i];
                var lineEnd = _points[i + 1];
                var lineLengthSquared = (lineEnd - lineStart).sqrMagnitude;
                
                var segmentRay = new Ray(lineStart, lineEnd - lineStart);
                if (!canvasPlane.Raycast(segmentRay, out var enter)) continue;
                
                // Raycast assumes an infinite line, be we only want to return the
                // intersection if it's within the line segment, so ensure the `enter` value
                // (the distance along the ray of the intersection) is less than the
                // length of the ray
                var enterSquared = enter * enter;
                if (!(enterSquared <= lineLengthSquared)) continue;
                
                intersection = segmentRay.GetPoint(enter); 
                data = i;
                return true;
            }

            intersection = Vector3.zero;
            data = null;
            return false;
        }

        private void SetSelfCanvasCursor([CanBeNull] IGameboardCanvasPointer.PointerImpact pointerImpact)
        {
            if (pointerImpact != null)
            {
                _canvasArcLimit = (int?) pointerImpact.Data;
                _canvasArcImpact = pointerImpact.Position;
                if (!_canvasCursor) return;
                _canvasCursor.transform.position = pointerImpact.Position;
                
                if (_canvasCursorActive) return;
                _canvasCursorActive = true;
                _canvasCursor.SetActive(true);
            }
            else
            {
                _canvasArcLimit = null;
                _canvasArcImpact = null;
                if (!_canvasCursor || !_canvasCursorActive) return;
                _canvasCursorActive = false;
                _canvasCursor.SetActive(false);
            }
        }

        private void SetOtherCanvasCursors(List<IGameboardCanvasPointer.PointerImpact> impacts)
        {
            if (!_canvasOtherCursorTemplate)
            {
                return;
            }
            
            var newActiveCursors = new Stack<GameObject>();
            
            // Recycle existing 'other' canvas cursors if possible, or instantiate if needed
            foreach (var impact in impacts)
            {
                var recycled = _canvasOtherCursorObjects.TryPop(out var cursor);
                if (!recycled)
                {
                    cursor = Instantiate(_canvasOtherCursorTemplate, transform, true);
                    cursor.name = _canvasOtherCursorTemplate.name + "(" + _otherCursorInstanceCount++ + ")";
                }
                
                cursor.transform.position = impact.Position;
                if (!cursor.activeSelf) cursor.SetActive(true);
                newActiveCursors.Push(cursor);
            }

            // Deactivate and cache any unused cursors this frame
            while (_canvasOtherCursorObjects.TryPop(out var cursor))
            {
                if (cursor.activeSelf) cursor.SetActive(false);
                newActiveCursors.Push(cursor);
            }

            _canvasOtherCursorObjects = newActiveCursors;
        }
        
        public void SetCanvasPointerImpacts(IGameboardCanvasPointer.PointerImpact selfImpact,
            List<IGameboardCanvasPointer.PointerImpact> otherImpacts)
        {
            SetSelfCanvasCursor(selfImpact);
            SetOtherCanvasCursors(otherImpacts);
        }
    }
}
