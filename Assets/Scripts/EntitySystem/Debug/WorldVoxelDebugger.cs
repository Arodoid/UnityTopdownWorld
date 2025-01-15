using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.World;
using Unity.Mathematics;

namespace EntitySystem.Debugging
{
    public class WorldVoxelDebugger : MonoBehaviour
    {
        [SerializeField] private int radius = 16;
        [SerializeField] private int heightRange = 32;
        [SerializeField] [Range(0f, 1f)] private float gizmoAlpha = 0.3f;
        [SerializeField] private GameManager gameManager;
        
        private DirectWorldAccess _worldAccess;
        private Color _solidColor;
        private Color _airColor;

        private void Start()
        {
            _worldAccess = Object.FindFirstObjectByType<GameManager>().WorldAccess;
            _solidColor = new Color(1f, 0f, 0f, gizmoAlpha);
            _airColor = new Color(0f, 1f, 0f, gizmoAlpha);
        }

        private void Update()
        {
            if (_worldAccess == null && gameManager != null && gameManager.IsInitialized)
            {
                _worldAccess = gameManager.WorldAccess;
            }

            if (_worldAccess == null) return;

            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    int highestBlock = _worldAccess.GetHighestSolidBlock(x, z);
                    if (highestBlock < 0) continue;

                    Vector3 position = new Vector3(x, highestBlock, z);
                    bool isSolid = _worldAccess.IsBlockSolid(new int3(x, highestBlock, z));
                    Color color = isSolid ? _solidColor : _airColor;
                    
                    // Draw a small vertical line at each point
                    Debug.DrawLine(
                        position,
                        position + Vector3.up * 0.5f,
                        color
                    );
                }
            }
        }
    }
} 