using UnityEngine;
using EntitySystem.Core;
using EntitySystem.Core.Interfaces;

namespace EntitySystem.Components.Visual
{
    public class VisualComponent : GameComponent, IPositionAwareComponent
    {
        private SpriteRenderer _spriteRenderer;
        private float _yOffset = 0.2f;  // Higher above ground
        
        private static readonly Color[] DEBUG_COLORS = new[] {
            Color.blue,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };

        protected override void OnInitialize(Entity entity)
        {
            var visualGO = new GameObject("Visual");
            visualGO.transform.SetParent(Transform);
            
            _spriteRenderer = visualGO.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = CreateCircleSprite();
            _spriteRenderer.color = GetDebugColor(Entity.Id);
            
            visualGO.transform.localPosition = new Vector3(0, _yOffset, 0);
            visualGO.transform.localScale = Vector3.one * 2f;
            
            // Rotate 90 degrees around X to face Y+ (billboard up)
            visualGO.transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        private Sprite CreateCircleSprite()
        {
            var texture = new Texture2D(64, 64);
            var center = new Vector2(32, 32);
            var radius = 28f;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    var pos = new Vector2(x, y);
                    var dist = Vector2.Distance(pos, center);
                    var alpha = dist <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        private Color GetDebugColor(long entityId)
        {
            return DEBUG_COLORS[entityId % DEBUG_COLORS.Length];
        }

        public void OnPositionChanged(Vector3 oldPosition, Vector3 newPosition)
        {
            // Keep facing Y+ always
        }
    }
} 