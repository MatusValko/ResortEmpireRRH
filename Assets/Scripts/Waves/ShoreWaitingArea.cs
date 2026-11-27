using System.Collections.Generic;

using UnityEngine;

namespace ResortEmpire.Waves
{
    internal sealed class ShoreWaitingArea
    {
        internal sealed class Spot
        {
            internal readonly Vector3 Position;
            internal readonly Vector3 Access;
            internal readonly Vector3 SideOffset;
            internal bool InUse;

            internal Spot(Vector3 position, Vector3 access, Vector3 sideOffset)
            {
                Position = position;
                Access = access;
                SideOffset = sideOffset;
            }
        }


        internal const float WanderRadius = 0.15f;
        private const float GuestRadius = 0.17f;
        private const float PathClearance = 0.60f;
        private const float SpotClearance = 2f * (WanderRadius + GuestRadius);

        private readonly Transform _centre;
        private readonly Vector2 _size;
        private readonly Transform _road;
        private readonly Transform _shore;
        private readonly Transform _resort;
        private readonly List<Spot> _spots = new();
        private readonly List<Bounds> _obstacles = new();


        internal ShoreWaitingArea(Transform centre, Vector2 size, Transform road, Transform shore, Transform resort)
        {
            _centre = centre;
            _size = size;
            _road = road;
            _shore = shore;
            _resort = resort;
        }


        internal void Generate(int limit)
        {
            _spots.Clear();
            _obstacles.Clear();
            CacheObstacles(_resort.Find("Environment"));
            CacheObstacles(_resort.Find("Buildings"));

            Vector2 extent = _size * 0.5f - Vector2.one * (WanderRadius + GuestRadius);
            if (extent.x <= 0f || extent.y <= 0f)
                return;

            for (int i = 0; i < 200 && _spots.Count < limit; i++)
            {
                Vector3 offset = new(Random.Range(-extent.x, extent.x), 0f, Random.Range(-extent.y, extent.y));
                Vector3 home = _centre.position + _centre.rotation * offset;
                Vector3 access = _shore.position;
                Vector3 sideOffset = Vector3.zero;
                if (_road != null)
                {
                    Vector3 local = _road.InverseTransformPoint(home);
                    float entry = Mathf.Clamp(local.z, -0.5f, 0.5f);
                    Vector3 roadCentre = _road.TransformPoint(0f, 0.5f, entry);
                    Vector3 side = home - roadCentre;
                    side.y = 0f;
                    if (side.sqrMagnitude < PathClearance * PathClearance)
                        continue;

                    sideOffset = _road.right * (local.x < 0f ? -0.30f : 0.30f);
                    access = roadCentre + sideOffset;
                }

                if (IsSeparated(home, access) && IsClear(home, WanderRadius + GuestRadius)
                    && IsConnectorClear(access, home))
                    _spots.Add(new Spot(home, access, sideOffset));
            }
        }


        internal Spot Reserve()
        {
            foreach (Spot spot in _spots)
            {
                if (spot.InUse)
                    continue;

                spot.InUse = true;
                return spot;
            }

            return null;
        }


        private void CacheObstacles(Transform root)
        {
            if (root == null)
                return;

            foreach (MeshRenderer mesh in root.GetComponentsInChildren<MeshRenderer>())
            {
                Bounds bounds = mesh.bounds;
                if (mesh.enabled && bounds.max.y > _centre.position.y + 0.10f
                    && bounds.min.y < _centre.position.y + 0.54f)
                    _obstacles.Add(bounds);
            }
        }


        private bool IsSeparated(Vector3 home, Vector3 access)
        {
            foreach (Spot spot in _spots)
            {
                if ((home - spot.Position).sqrMagnitude < SpotClearance * SpotClearance
                    || DistanceFromSegment(home, spot.Access, spot.Position) < SpotClearance
                    || DistanceFromSegment(spot.Position, access, home) < SpotClearance)
                    return false;
            }

            return true;
        }


        private static float DistanceFromSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 span = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, span) / Mathf.Max(0.0001f, span.sqrMagnitude));
            return Vector3.Distance(point, a + t * span);
        }


        private bool IsConnectorClear(Vector3 from, Vector3 to)
        {
            int steps = Mathf.CeilToInt(Vector3.Distance(from, to) / GuestRadius);
            for (int i = 1; i <= steps; i++)
            {
                if (!IsClear(Vector3.Lerp(from, to, (float)i / steps), GuestRadius))
                    return false;
            }

            return true;
        }


        private bool IsClear(Vector3 point, float radius)
        {
            if (!Physics.Raycast(point + Vector3.up * 0.12f, Vector3.down, out RaycastHit ground, 0.30f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f
                || Physics.CheckCapsule(point + Vector3.up * (radius + 0.10f), point + Vector3.up * (radius + 0.28f),
                    radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;

            foreach (Bounds obstacle in _obstacles)
            {
                Vector3 sample = new(point.x, obstacle.center.y, point.z);
                if (obstacle.SqrDistance(sample) < radius * radius)
                    return false;
            }

            return true;
        }
    }
}
