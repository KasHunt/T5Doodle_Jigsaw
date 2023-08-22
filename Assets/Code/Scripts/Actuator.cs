using System.Collections.Generic;
using System.Linq;
using TiltFive;
using UnityEngine;

namespace Code.Scripts
{
    public class Actuator : MonoBehaviour, IWandActuator
    {
        private PlayerIndex _playerIndex;
    
        private float _selectorScale = 1f;
        private bool _moving;
    
        private readonly Dictionary<Piece, Vector3> _movingPieces = new();
    
        private static Vector3 ComputeCentroid(IReadOnlyCollection<Piece> pieces)
        {
            var centroid = pieces.Aggregate(Vector3.zero, (current, piece) =>
                current + piece.PieceGameObject.transform.position);
            return centroid / pieces.Count;
        }
    
        private Vector3 _selectionStartPosition;

        public void SetPlayerIndex(PlayerIndex playerIndex)
        {
            _playerIndex = playerIndex;
        }
        
        private static void GetPiecesInSelectionRange(Vector3 actuatorPosition, 
            float range, out List<Piece> inRange, out List<Piece> notInRange)
        {
            var squaredSelectionRadius = range * range;

            inRange = new List<Piece>();
            notInRange = new List<Piece>();
            var allPieces = Jigsaw.Instance.AllPieces;
            foreach (var piece in allPieces)
            {
                var closestPoint = piece.PieceGameObjectCollider.ClosestPoint(actuatorPosition);
                var squaredDistance = (actuatorPosition - closestPoint).sqrMagnitude;
                (squaredDistance < squaredSelectionRadius ? inRange : notInRange).Add(piece);
            }
        }
    
        private Vector2 _previousStickTilt = Vector2.zero;

        private void UpdateSelectedPieces(List<Piece> inRange, List<Piece> notInRange)
        {
            // Deselect pieces that are no longer in range and select pieces that are now in range 
            foreach (var piece in notInRange) piece.JigsawPieceBehavior.UnselectBy(this);
            foreach (var piece in inRange) piece.JigsawPieceBehavior.SelectBy(this);
        }

        private void UpdateActuatorScale()
        {
            transform.localScale = new Vector3(_selectorScale, Jigsaw.Instance.selectorHeight, _selectorScale);
        }
    
        private void BeginMove(List<Piece> inRange)
        {
            // If we've just started moving, flag it as such and get the relative positions of moving pieces
            _moving = true;
            var position = transform.position;
            _selectionStartPosition = new Vector3(position.x, -Jigsaw.Instance.pieceMoveHeight, position.z);
            foreach (var piece in inRange)
            {
                _movingPieces.Add(piece, piece.PieceGameObject.transform.position);
            }
        }

        private void Move()
        {
            // If we're already moving, compute the total move position,
            // and set the moving pieces to that plus their original positions
            var allowSnapping = _movingPieces.Count == 1;
            var movementDelta = transform.position - _selectionStartPosition;
            foreach (var (piece, initialPosition) in _movingPieces)
            {
                piece.JigsawPieceBehavior.SetMovedPosition(initialPosition + movementDelta, allowSnapping);
            }
        }

        private void EndMove()
        {
            _moving = false;
            _movingPieces.Clear();
        }

        private void HandleWandStickWhileNotMoving(Vector2 stickTilt, List<Piece> inRange)
        {
            // Stick X: Rotates the selected pieces
            if (stickTilt.x < -0.5)
            {
                var centroid = ComputeCentroid(inRange);
                foreach (var piece in inRange)
                {
                    piece.PieceGameObject.transform.RotateAround(centroid, Vector3.up, -90 * Time.deltaTime);
                }
            }
            else if (stickTilt.x > 0.5)
            {
                var centroid = ComputeCentroid(inRange);
                foreach (var piece in inRange)
                {
                    piece.PieceGameObject.transform.RotateAround(centroid, Vector3.up, 90 * Time.deltaTime);
                }
            }
        
            // Stick Y: Scale the selection area
            var jigsaw = Jigsaw.Instance;
            if (stickTilt.y < -0.5)
            {
                _selectorScale = Mathf.Max(_selectorScale - jigsaw.selectorScaleStep, jigsaw.selectorMinScale);
            }
            else if (stickTilt.y > 0.5)
            {
                _selectorScale = Mathf.Min(_selectorScale + jigsaw.selectorScaleStep, jigsaw.selectorMaxScale);
            }
        }
    
        private void HandleWandStickForSingleMovingPiece(Vector2 stickTilt)
        {
            var piece = _movingPieces.First().Key;
            switch (StickX: stickTilt.x, Snapping: piece.JigsawPieceBehavior.IsSnapping())
            {
                case (< -0.5f, true) when _previousStickTilt.x >= -0.5f:
                    piece.PieceGameObject.transform.Rotate(Vector3.up, -90);
                    break;
            
                case (> 0.5f, true) when _previousStickTilt.x <= 0.5f:
                    piece.PieceGameObject.transform.Rotate(Vector3.up, 90);
                    break;
            
                case (< -0.5f, false):
                    piece.PieceGameObject.transform.Rotate(Vector3.up, -90 * Time.deltaTime);
                    break;
            
                case (> 0.5f, false):
                    piece.PieceGameObject.transform.Rotate(Vector3.up, 90 * Time.deltaTime);
                    break;
            }
        
            _previousStickTilt = stickTilt;
        }
    
        private void HandleWandStick(List<Piece> inRange)
        {
            var stickTilt = TiltFive.Input.GetStickTilt(playerIndex: _playerIndex);
            if (!_moving)
            {
                HandleWandStickWhileNotMoving(stickTilt, inRange);
            } 
            else if (_movingPieces.Count == 1)
            {
                HandleWandStickForSingleMovingPiece(stickTilt);
            }
        }

        private void HandleWandTrigger(List<Piece> inRange)
        {
            // Trigger grabs the intersected pieces
            switch (TiltFive.Input.GetTrigger(playerIndex: _playerIndex))
            {
                case > 0.5f when !_moving:
                    BeginMove(inRange);
                    break;
                case > 0.5f when _moving:
                    Move();
                    break;
                case < 0.5f when _moving:
                    EndMove();
                    break;
            }
        }

        private void HandleWandButtons(List<Piece> inRange)
        {
            // Button 1: Flip pieces (But only if we're not moving)
            if (!_moving && TiltFive.Input.GetButtonUp(TiltFive.Input.WandButton.One))
            {
                foreach (var piece in inRange)
                {
                    piece.JigsawPieceBehavior.FlipUp();
                }
            }
        
            // Button 2: Toggle hint
            if (TiltFive.Input.GetButtonUp(TiltFive.Input.WandButton.Two))
            {
                Jigsaw.Instance.SetHintVisible(!Jigsaw.Instance.IsHintVisible());
            }
        }
    
        private void Update()
        {
            UpdateActuatorScale();
        
            GetPiecesInSelectionRange(transform.position, _selectorScale / 2,
                out var inRange, out var notInRange);
        
            // Only update the selected pieces if we're not moving, so we don't hoover up all pieces as we pass them
            if (!_moving) UpdateSelectedPieces(inRange, notInRange);

            HandleWandTrigger(inRange);
            HandleWandStick(inRange);
            HandleWandButtons(inRange);
        }
    }
}
