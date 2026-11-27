using System;
using System.Collections.Generic;

using UnityEngine;

namespace ResortEmpire.Buildings
{
    internal sealed class LockedBuildingVisuals : IDisposable
    {
        private readonly Shader _shader = Resources.Load<Shader>("LockedBuildingGrayscale");
        private readonly Dictionary<Transform, Dictionary<Renderer, Material[]>> _originals = new();
        private readonly Dictionary<Material, Material> _materials = new();
        private readonly Dictionary<Texture, RenderTexture> _textures = new();
        private Material _filter;


        internal void SetLocked(Transform model, bool locked)
        {
            if (model == null || _shader == null)
                return;

            if (!locked)
            {
                if (_originals.Remove(model, out var originals))
                    RestoreMaterials(originals);

                return;
            }

            if (_originals.ContainsKey(model))
                return;

            var materialsByRenderer = new Dictionary<Renderer, Material[]>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] original = renderer.sharedMaterials;
                materialsByRenderer.Add(renderer, original);
                renderer.sharedMaterials = Array.ConvertAll(original, GetGrayMaterial);
            }

            _originals.Add(model, materialsByRenderer);
        }


        private Material GetGrayMaterial(Material original)
        {
            if (original == null)
                return null;

            if (_materials.TryGetValue(original, out Material gray))
                return gray;

            gray = new Material(original) { name = original.name + " (Locked)" };
            Desaturate(gray, "_BaseMap", "_BaseColor");
            Desaturate(gray, "_EmissionMap", "_EmissionColor");
            _materials.Add(original, gray);
            return gray;
        }


        private void Desaturate(Material material, string map, string tint)
        {
            if (material.HasProperty(tint))
            {
                Color color = material.GetColor(tint);
                float gray = Vector3.Dot(new Vector3(color.r, color.g, color.b), new Vector3(0.2126f, 0.7152f, 0.0722f));
                const float lockedBrightness = 0.85f;
                gray *= lockedBrightness;
                material.SetColor(tint, new Color(gray, gray, gray, color.a));
            }

            if (material.HasProperty(map) && material.GetTexture(map) is Texture source)
                material.SetTexture(map, GetGrayTexture(source));
        }


        private RenderTexture GetGrayTexture(Texture source)
        {
            if (_textures.TryGetValue(source, out RenderTexture gray))
                return gray;

            // Keep the temporary atlas at a size suitable for the island's zoomed-out view.
            const int maxSize = 1024;
            float scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(source.width, source.height));
            gray = new RenderTexture(Mathf.Max(1, Mathf.RoundToInt(source.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(source.height * scale)), 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                useMipMap = source.mipmapCount > 1,
                autoGenerateMips = source.mipmapCount > 1,
                filterMode = source.filterMode,
                wrapModeU = source.wrapModeU,
                wrapModeV = source.wrapModeV,
                anisoLevel = source.anisoLevel
            };

            _filter ??= new Material(_shader);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, gray, _filter);
            RenderTexture.active = previous;
            _textures.Add(source, gray);
            return gray;
        }


        private static void RestoreMaterials(Dictionary<Renderer, Material[]> originals)
        {
            foreach (var entry in originals)
            {
                if (entry.Key != null)
                    entry.Key.sharedMaterials = entry.Value;
            }
        }


        public void Dispose()
        {
            foreach (var originals in _originals.Values)
                RestoreMaterials(originals);
            foreach (Material material in _materials.Values)
                UnityEngine.Object.Destroy(material);
            foreach (RenderTexture texture in _textures.Values)
                UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(_filter);
        }
    }
}
