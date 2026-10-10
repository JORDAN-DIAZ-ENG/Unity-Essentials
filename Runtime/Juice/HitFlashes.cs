using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Essential.Juice
{
    /// <summary>
    /// Flashes renderers a solid color by swapping their materials for a flat
    /// "silhouette" material and swapping the originals back when the flash ends.
    ///
    /// Swapping works with any shader the game uses. Tinting through a property
    /// would only work on shaders that expose one, and a white tint on a textured
    /// material does not read as white. Flash materials are shared per color,
    /// so a flash never instantiates per-renderer material copies.
    /// </summary>
    internal sealed class HitFlashes
    {
        const string ShaderResourcePath = "Essential/JuiceFlash";
        const int OpaqueRenderQueue = 2000;
        const int TransparentRenderQueue = 3000;

        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        sealed class Flash
        {
            public Renderer Renderer;
            public Material[] OriginalMaterials;
            public float RemainingSeconds;
        }

        readonly List<Flash> _active = new List<Flash>();
        readonly Stack<Flash> _pool = new Stack<Flash>();
        readonly Dictionary<Color, Material> _opaqueMaterials = new Dictionary<Color, Material>();
        readonly Dictionary<Color, Material> _spriteMaterials = new Dictionary<Color, Material>();

        Shader _shader;
        bool _shaderResolved;

        /// <summary>
        /// Flashes one renderer. Flashing a renderer that is already flashing
        /// changes its color and extends it to whichever ends later.
        /// </summary>
        public void Start(Renderer renderer, Color color, float seconds)
        {
            var material = MaterialFor(color, renderer is SpriteRenderer);
            if (material == null) return;

            var flash = Find(renderer);
            if (flash == null)
            {
                flash = _pool.Count > 0 ? _pool.Pop() : new Flash();
                flash.Renderer = renderer;
                flash.OriginalMaterials = renderer.sharedMaterials;
                flash.RemainingSeconds = 0f;
                _active.Add(flash);
            }

            flash.RemainingSeconds = Mathf.Max(flash.RemainingSeconds, seconds);
            renderer.sharedMaterials = FilledArray(material, flash.OriginalMaterials.Length);
        }

        public void Tick(float deltaSeconds)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var flash = _active[i];
                flash.RemainingSeconds -= deltaSeconds;
                if (flash.RemainingSeconds <= 0f || flash.Renderer == null) End(i);
            }
        }

        public void StopAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) End(i);
        }

        public void DestroyMaterials()
        {
            foreach (var material in _opaqueMaterials.Values) Object.Destroy(material);
            foreach (var material in _spriteMaterials.Values) Object.Destroy(material);
            _opaqueMaterials.Clear();
            _spriteMaterials.Clear();
        }

        Flash Find(Renderer renderer)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Renderer == renderer) return _active[i];
            }

            return null;
        }

        void End(int index)
        {
            var flash = _active[index];
            if (flash.Renderer != null) flash.Renderer.sharedMaterials = flash.OriginalMaterials;

            flash.Renderer = null;
            flash.OriginalMaterials = null;
            _active.RemoveAt(index);
            _pool.Push(flash);
        }

        Material MaterialFor(Color color, bool isSprite)
        {
            var cache = isSprite ? _spriteMaterials : _opaqueMaterials;
            if (cache.TryGetValue(color, out var cached)) return cached;

            var shader = ResolveShader();
            if (shader == null) return null;

            var material = new Material(shader)
            {
                name = isSprite ? "Juice Flash (Sprite)" : "Juice Flash",
                hideFlags = HideFlags.DontSave
            };
            material.SetColor(FlashColorId, color);

            // Sprites keep their silhouette through alpha and sort as transparent;
            // meshes draw solid so they occlude exactly like the material they replace.
            if (isSprite)
            {
                material.SetFloat(ZWriteId, 0f);
                material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
                material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                material.renderQueue = TransparentRenderQueue;
            }
            else
            {
                material.SetFloat(ZWriteId, 1f);
                material.SetFloat(SrcBlendId, (float)BlendMode.One);
                material.SetFloat(DstBlendId, (float)BlendMode.Zero);
                material.renderQueue = OpaqueRenderQueue;
            }

            cache.Add(color, material);
            return material;
        }

        Shader ResolveShader()
        {
            if (_shaderResolved) return _shader;

            _shaderResolved = true;
            _shader = Resources.Load<Shader>(ShaderResourcePath);
            if (_shader == null)
                Debug.LogWarning($"[Essential.Juice] Flash shader missing from Resources/{ShaderResourcePath}. Flashes are disabled.");

            return _shader;
        }

        static Material[] FilledArray(Material material, int length)
        {
            // A renderer with no material slots still needs one to show the flash.
            var materials = new Material[Mathf.Max(1, length)];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            return materials;
        }
    }
}
