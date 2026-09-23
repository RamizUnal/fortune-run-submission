using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using Vertigo.Fortune.Domain;

namespace Vertigo.Fortune.Content
{
    [Serializable]
    public sealed class NamedSprite
    {
        public string Name;
        public Sprite Sprite;
    }

    [CreateAssetMenu(fileName = "GameContent", menuName = "Fortune/Game Content")]
    public sealed class GameContent : ScriptableObject
    {
        public RewardCatalog Catalog;
        public WheelDefinition Bronze;
        public WheelDefinition Silver;
        public WheelDefinition Golden;
        public SpriteAtlas Atlas;
        [Range(1.5f, 7f)] public float SpinDuration = 4.2f;
        [SerializeField] private List<NamedSprite> sprites = new List<NamedSprite>();
        public IReadOnlyList<NamedSprite> Sprites => sprites;

        public Sprite GetSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (var i = 0; i < sprites.Count; i++)
                if (string.Equals(sprites[i].Name, name, StringComparison.Ordinal))
                    return sprites[i].Sprite;
            return null;
        }

        public IWheelProvider CreateProvider() => new ScriptableWheelProvider(this);

        public WheelDefinition GetWheel(int zone)
        {
            switch (ZoneRules.KindFor(zone))
            {
                case ZoneKind.Golden: return Golden;
                case ZoneKind.Silver: return Silver;
                default: return Bronze;
            }
        }

        public void SetSprites(IEnumerable<NamedSprite> values)
        {
            sprites = new List<NamedSprite>(values);
        }
    }
}
