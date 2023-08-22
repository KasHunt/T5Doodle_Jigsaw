using System;
using TiltFive;
using UnityEngine;
using System.Collections.Generic;
using Code.Scripts.Editor;
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
        
        [Header("Actuators")]
        public bool enableLeftWand;
        [ConditionalShow("enableLeftWand")]
        public GameObject leftActuatorObject;
        public bool enableRightWand;
        [ConditionalShow("enableRightWand")]
        public GameObject rightActuatorObject;
        
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
                                ControllerIndex controllerIndex, 
                                [CanBeNull] GameObject actuatorObject)
        {
            var wand = new GameObject("Wand_" + playerIndex + "_" + controllerIndex);
            SetWandObjectsForPlayer(playerIndex, controllerIndex, aimObject: wand);
            wand.transform.SetParent(transform);
                
            var wandBehaviour = wand.AddComponent<Wand>();
            wandBehaviour.playerIndex = playerIndex;
            wandBehaviour.controllerIndex = controllerIndex;
            wandBehaviour.ArcWidth = arcWidth;
            wandBehaviour.arcTimeStep = arcTimeStep;
            wandBehaviour.ArcMaterial = arcMaterial;

            // Loop if we're not attaching an actuator
            if (!actuatorObject) return;
                
            wandBehaviour.actuatorObject = Instantiate(actuatorObject);
            var wandActuator = wandBehaviour.actuatorObject.GetComponent<IWandActuator>();
            wandActuator.SetPlayerIndex(playerIndex);
            wandBehaviour.actuatorObject.SetActive(false);
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
                if (enableLeftWand) CreateWand(playerIndex, ControllerIndex.Left, leftActuatorObject);
                if (enableRightWand) CreateWand(playerIndex, ControllerIndex.Right, rightActuatorObject);
            }
        }
    }
    
    public class Wand : MonoBehaviour
    {
        public Material ArcMaterial
        {
            set => _lineRenderer.material = value;
        }
        
        public float ArcWidth
        {
            set => _lineRenderer.widthCurve = AnimationCurve.Linear(0, 0, 1, value);
        }
        public GameObject actuatorObject;
        public float arcTimeStep;
        public PlayerIndex playerIndex;
        public ControllerIndex controllerIndex;
        
        private LineRenderer _lineRenderer;

        private void Awake()
        {
            _lineRenderer = new GameObject("WandArc").AddComponent<LineRenderer>();
            _lineRenderer.transform.SetParent(transform);
        }

        private void Update()
        {
            DrawArc();
            
            // Enable actuators the first time we see them
            if (!actuatorObject || actuatorObject.activeSelf) return;
            TiltFive.Wand.TryCheckConnected(out var connected, playerIndex, controllerIndex);
            if (connected)
            {
                actuatorObject.SetActive(true);                    
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
            var points = ComputeArc(gravity, velocity);

            // Set the line renderer points
            _lineRenderer.positionCount = points.Count;
            _lineRenderer.SetPositions(points.ToArray());
            
            // Return if there's no actuator
            if (!actuatorObject) return;
            
            // Compute the impact point, and move the actuator there
            var impactPoint = ComputeImpactPoint(points);
            if (!impactPoint.HasValue) return;
            actuatorObject.transform.position = impactPoint.Value;
            actuatorObject.transform.rotation = wandRotation;
        }
    }
}
