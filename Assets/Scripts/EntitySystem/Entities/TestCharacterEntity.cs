using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Components.Movement;
using Unity.Mathematics;
using WorldSystem.Data;

public class TestCharacterEntity : Entity
{
    private GridMovementComponent _movement;
    private float _lastY;
    private Vector3 _moveDirection;
    private float _moveSpeed = 5f;
    private float _turnSpeed = 180f;
    private float _currentAngle;
    private bool _isWandering = true;
    private float _wanderTimer;
    private float _wanderInterval = 1f;

    // Debug visualization
    private readonly Color _bodyColor;
    private readonly float _debugHeight = 2f;
    private readonly float _debugRadius = 0.4f;

    public TestCharacterEntity(long id, EntityManager manager) : base(id, manager)
    {
        _movement = AddComponent<GridMovementComponent>();
        _movement.MoveSpeed = _moveSpeed;
        _lastY = float.MinValue;
        _bodyColor = UnityEngine.Random.ColorHSV(0.5f, 0.7f, 0.7f, 0.9f, 0.7f, 0.9f);
        _currentAngle = UnityEngine.Random.Range(0f, 360f);
        
        UpdateMoveDirection();
    }

    private void UpdateMoveDirection()
    {
        _moveDirection = new Vector3(
            Mathf.Cos(_currentAngle * Mathf.Deg2Rad),
            0f,
            Mathf.Sin(_currentAngle * Mathf.Deg2Rad)
        ).normalized;
    }

    public override void OnTick()
    {
        base.OnTick();

        if (Mathf.Abs(_lastY - Position.y) > 0.01f)
        {
            Debug.LogWarning($"Character {Id} Y changed: {_lastY:F2} -> {Position.y:F2}");
            _lastY = Position.y;
        }

        if (_isWandering)
        {
            UpdateWandering();
        }

        UpdateMovement();
        DrawDebugVisualization();
    }

    private void UpdateWandering()
    {
        _wanderTimer += TickManager.TICK_INTERVAL;
        
        if (_wanderTimer >= _wanderInterval)
        {
            float angleChange = UnityEngine.Random.Range(-120f, 120f);
            _currentAngle = (_currentAngle + angleChange) % 360f;
            _wanderTimer = 0f;
            
            UpdateMoveDirection();
            
            if (UnityEngine.Random.value < 0.3f)
            {
                _moveDirection = Vector3.zero;
                Debug.Log($"Entity {Id} pausing at position {Position}");
            }
            else
            {
                Debug.Log($"Entity {Id} changing direction to angle {_currentAngle}, direction {_moveDirection}");
            }
        }
    }

    private void UpdateMovement()
    {
        if (!_movement.IsGrounded)
        {
            Debug.LogWarning($"Entity {Id} not grounded at position {Position}");
            return;
        }

        if (_moveDirection.magnitude > 0.01f)
        {
            Vector3 targetPos = Position + _moveDirection * _moveSpeed * TickManager.TICK_INTERVAL;
            int3 blockPos = new int3(
                Mathf.FloorToInt(targetPos.x),
                Mathf.FloorToInt(Position.y),
                Mathf.FloorToInt(targetPos.z)
            );

            var worldAccess = Manager.GetWorldAccess();
            
            Debug.Log($"Entity {Id} checking move from {Position} to {targetPos}");
            Debug.Log($"Block check at {blockPos}");
            
            bool currentPosValid = worldAccess.CanStandAt(new int3(
                Mathf.FloorToInt(Position.x),
                Mathf.FloorToInt(Position.y),
                Mathf.FloorToInt(Position.z)
            ));
            
            bool targetPosValid = worldAccess.CanStandAt(blockPos);
            
            Debug.Log($"Current pos valid: {currentPosValid}, Target pos valid: {targetPosValid}");

            if (targetPosValid)
            {
                Vector3 velocity = _moveDirection * _moveSpeed;
                _movement.SetVelocity(velocity);
                Debug.Log($"Entity {Id} moving with velocity {velocity}");
            }
            else
            {
                if (!currentPosValid)
                {
                    _currentAngle = (_currentAngle + UnityEngine.Random.Range(90f, 270f)) % 360f;
                    UpdateMoveDirection();
                    Debug.Log($"Entity {Id} hit obstacle, new angle {_currentAngle}");
                }
                _movement.SetVelocity(Vector3.zero);
            }
        }
        else
        {
            _movement.SetVelocity(Vector3.zero);
        }
    }

    private void DrawDebugVisualization()
    {
        Vector3 basePos = Position + Vector3.up * 0.5f;
        Debug.DrawLine(basePos, basePos + Vector3.up * _debugHeight, _bodyColor, TickManager.TICK_INTERVAL);
        
        if (_moveDirection.magnitude > 0.01f)
        {
            Vector3 directionPoint = basePos + _moveDirection * _debugRadius;
            Debug.DrawLine(basePos, directionPoint, Color.blue, TickManager.TICK_INTERVAL);
        }
        
        Color groundColor = _movement.IsGrounded ? Color.green : Color.red;
        Debug.DrawRay(Position, Vector3.down * 0.2f, groundColor, TickManager.TICK_INTERVAL);
        
        Vector3 headPos = basePos + Vector3.up * _debugHeight;
        int segments = 8;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (i / (float)segments) * Mathf.PI * 2;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2;
            
            Vector3 point1 = headPos + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * _debugRadius;
            Vector3 point2 = headPos + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * _debugRadius;
            
            Debug.DrawLine(point1, point2, _bodyColor, TickManager.TICK_INTERVAL);
        }
    }

    public override void Initialize(GameObject gameObject)
    {
        base.Initialize(gameObject);
        
        if (gameObject.TryGetComponent<MeshRenderer>(out var renderer))
        {
            renderer.material.color = _bodyColor;
        }
    }
}