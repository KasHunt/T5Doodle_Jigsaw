using System;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Code.Scripts
{
    public class JigsawPiece : MonoBehaviour
    {
        public GameObject inNotchObject;
        public GameObject flatNotchObject;
        public GameObject outNotchObject;
        public float apexHeight = 3f;
        public AudioClip impactSound;
        [Min(0f)]
        public float maxAudioVelocity = 10f;
        
        private Material _normalMaterial;
        private Material _selectedMaterial;
        private MeshRenderer _meshRenderer;
        
        [CanBeNull] private Actuator _selectedBy;
        
        private void OnCollisionEnter(Collision collision)
        {
            if (!impactSound)
            {
                return;
            }
            var impactVelocity = collision.relativeVelocity.magnitude;
            var volume = Mathf.Clamp(impactVelocity / maxAudioVelocity, 0f, 1f);
            SoundManager.Instance.PlaySound(impactSound, volume);
        }
        
        public void Setup(Material material, Material selectedMaterial, Piece piece, float columnSize, float rowSize)
        {
            _normalMaterial = material;
            _selectedMaterial = selectedMaterial;
            
            CombineInstance[] combineInstances =
            {
                GetNotchCombineInstance(piece.notches.top, Quaternion.identity),
                GetNotchCombineInstance(piece.notches.left, Quaternion.Euler(0, 90, 0)),
                GetNotchCombineInstance(piece.notches.bottom, Quaternion.Euler(0, 180, 0)),
                GetNotchCombineInstance(piece.notches.right, Quaternion.Euler(0, 270, 0)),
            };
            
            var combinedMesh = new Mesh();
            combinedMesh.CombineMeshes(combineInstances, true, true);
            var meshVerts = combinedMesh.vertices;
            
            // Compute texture coordinates
            var uv = new Vector2[meshVerts.Length];
            for (var i = 0; i < meshVerts.Length; i++)
            {
                var vert = meshVerts[i];
                var x = ((vert.x + 0.3f) / 0.6f) * rowSize + (piece.row * rowSize);
                var y = ((vert.z + 0.3f) / 0.6f) * columnSize - ((1 + piece.column) * columnSize);
                
                uv[i] = new Vector2(x, y);
            }
            combinedMesh.uv = uv;
            
            var combinedMeshFilter = this.AddComponent<MeshFilter>();
            combinedMeshFilter.mesh = combinedMesh;
            _meshRenderer = this.AddComponent<MeshRenderer>();
            _meshRenderer.material = material;

            // Add teh RigidBody (So the pieces start fall...)
            var combinedRigidbody = this.AddComponent<Rigidbody>();
            combinedRigidbody.useGravity = true;

            // Add the BoxCollider (So the pieces stop falling...)
            var boxCollider = this.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(0.55f, 0.075f, 0.55f);
            boxCollider.center = new Vector3(0, 0, 0);

            // Modulate the scale of the piece slightly to avoid z-fighting on overlap
            transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1, Random.Range(0.99f, 1.01f), 1));
        }

        public void SelectBy(Actuator actuator)
        {
            // Don't select if we're already selected
            if (_selectedBy)
            {
                return;
            }
            
            _meshRenderer.material = _selectedMaterial;
            _selectedBy = actuator;
            
            // 'Flip' upside down objects on select
            if (transform.up.y < 0.2f)
            {
                FlipUp();
            }
        }
        
        public void UnselectBy(Actuator actuator)
        {
            // Don't unselect if we're not selected or if we're selected by a different actuator
            if (!_selectedBy || (_selectedBy != actuator))
            {
                return;
            }
            
            _meshRenderer.material = _normalMaterial;
            _selectedBy = null;
        }
        
        private GameObject GetNotchObject(Piece.Notch notch)
        {
            return notch switch
            {
                Piece.Notch.In => inNotchObject,
                Piece.Notch.Flat => flatNotchObject,
                Piece.Notch.Out => outNotchObject,
                _ => throw new ArgumentOutOfRangeException(nameof(notch), notch, null)
            };
        }
        
        private CombineInstance GetNotchCombineInstance(Piece.Notch notch, Quaternion rotation)
        {
            var notchObject = GetNotchObject(notch);
            var meshFilter = notchObject.GetComponentInChildren<MeshFilter>();
            
            // Apply the rotation to the object
            var meshFilterTransform = meshFilter.transform;
            meshFilterTransform.rotation = rotation;

            // Create a CombineInstance object
            return new CombineInstance
            {
                mesh = meshFilter.sharedMesh,
                transform = meshFilterTransform.localToWorldMatrix,
            };
        }

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Quaternion _targetRotation;
        private Vector3 _initialVelocity;
        private Vector3 _targetPosition;
        private Vector3 _apexPosition;
        
        private float _flightTime;
        private float _elapsedTime;

        private GameObject _snapTarget;

        public void FlipUp()
        {
            AnimateTo(transform.position, 
                Quaternion.Euler(0, Random.Range(0, 360), 0),
                Jigsaw.Instance.flipAnimateTime);
        }
        
        public void AnimateTo(Vector3 targetPosition, Quaternion targetRotation, float flightTime)
        {
            var thisTransform = transform;
            _startPosition = thisTransform.position;
            _startRotation = thisTransform.rotation;
            _targetRotation = targetRotation;
            _targetPosition = targetPosition;

            var apexLimit = Mathf.Max(Vector3.Distance(_startPosition, _targetPosition), 1f);
            var apex = Mathf.Min(apexLimit, apexHeight);
            
            _apexPosition = new Vector3(
                (_startPosition.x + _targetPosition.x) / 2,
                ((_startPosition.y + _targetPosition.y) / 2) + apex,
                (_startPosition.z + _targetPosition.z) / 2
            );
            _elapsedTime = 0;
            _flightTime = flightTime;
        }

        private static Vector3 QuadraticBezier(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            var u = 1 - t;
            var point = u * u * p0; // (1-t)^2 * P0
            point += 2 * u * t * p1;       // 2(1-t)t * P1
            point += t * t * p2;           // t^2 * P2
            return point;
        }
        
        private void FixedUpdate()
        {
            if (!(_elapsedTime < _flightTime))
            {
                return;
            }
            
            _elapsedTime += Time.fixedUnscaledDeltaTime;
            var t = _elapsedTime / _flightTime;
            transform.position = QuadraticBezier(t, _startPosition, _apexPosition, _targetPosition);
            transform.rotation = Quaternion.Slerp(_startRotation, _targetRotation, t);
        }

        [CanBeNull]
        private static GameObject FindSnapTarget(Vector3 position, Object selfTransform)
        {
            var snapDistance = Jigsaw.Instance.snapDistance;
            
            // Square the distance, so we can skip the square root in the compare
            snapDistance *= snapDistance;
            
            var closestSnap = float.PositiveInfinity;
            GameObject closestCandidate = null;
            
            var allPieces = Jigsaw.Instance.AllPieces;
            foreach (var candidatePiece in allPieces)
            {
                var candidate = candidatePiece.PieceGameObject;
                
                // Skip ourselves
                if (candidate.transform == selfTransform)
                {
                    continue;
                }
                
                // Determine if we're within snapping distance and a better candidate than our current candidate
                var distance = (position - candidate.transform.position).sqrMagnitude;
                if (distance == 0)
                {
                    continue;
                }
                
                if ((!(distance < snapDistance)) || (!(distance < closestSnap))) continue;
                closestSnap = distance;
                closestCandidate = candidate;
            }

            return closestCandidate;
        }
        
        private void KeepInBounds()
        {
            const float boundsXZ = 6.5f;
            var newPosition = transform.position;

            transform.position = new Vector3(
                newPosition.x = Mathf.Clamp(newPosition.x, -boundsXZ, boundsXZ),
                newPosition.y = Mathf.Max(newPosition.y, 0),
                newPosition.z = Mathf.Clamp(newPosition.z, -boundsXZ, boundsXZ)
            );
        }
        
        private void Update()
        {
            // Clamp the piece position into the visible bounds
            KeepInBounds();
        }

        public bool IsSnapping()
        {
            return _snapTarget;
        }

        public void SetMovedPosition(Vector3 position, bool allowSnap)
        {
            // If snapping is not allowed, simply set the position
            if (!allowSnap)
            {
                transform.position = position;
                _snapTarget = null;
                return;
            }
            
            _snapTarget = FindSnapTarget(position, transform);
            if (_snapTarget)
            {
                SnapToObject(_snapTarget.transform, position.y);
            }
            else
            {
                transform.position = position;
            }
        }
        
        private static float DifferenceFromClosestCardinal(float angle)
        {
            angle = WrapRotation(angle);
            var closestCardinal = RoundTo(angle,90f);
            var difference = angle - closestCardinal;
            return difference switch
            {
                > 45 => difference - 90,
                < -45 => difference + 90,
                _ => difference
            };
        }

        private static float RoundTo(float value, float rounding)
        {
            return Mathf.Round(value / rounding) * rounding;
        }

        private static float WrapRotation(float angle)
        {
            return ((angle % 360) + 360) % 360;
        }
        
        private static float RoundedRotationDifference(Transform from, Transform to, float rounding)
        {
            return RoundTo(Mathf.DeltaAngle(from.eulerAngles.y, to.eulerAngles.y), rounding);
        }
        
        private static float AngleToTarget(Transform from, Transform to)
        {
            var direction = to.position - from.position;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }
        
        private void SnapToObject(Transform target, float snapPositionY)
        {
            // Snap the rotation
            var selfTransform = transform;
            var targetRotation = target.eulerAngles.y;
            selfTransform.rotation = Quaternion.Euler(
                0f, 
                targetRotation - RoundedRotationDifference(selfTransform, target, 90f), 
                0f
            );
            
            var snapDistance = Jigsaw.Instance.gridSize;
            
            // Calculate the offset based on the snapped rotation
            var offset = WrapRotation(RoundTo(AngleToTarget(target, selfTransform), 90)) switch
            {
                0f => new Vector3(0f, snapPositionY, snapDistance),
                90f => new Vector3(snapDistance, snapPositionY, 0f),
                180f => new Vector3(0f, snapPositionY, -snapDistance),
                270f => new Vector3(-snapDistance, snapPositionY, 0f),
                _ => Vector3.zero
            };
            
            var positionalSnapRotation = Quaternion.Euler(
                0, DifferenceFromClosestCardinal(targetRotation), 0);
            

            // Apply the offset to the position of the target
            transform.position = target.position + (positionalSnapRotation * offset);
        }
    }
}