using UnityEngine;

namespace WhosWatching
{
    public static class GazeVisibility
    {
        // Fixed samples inside the standard 2m x 0.8m capsule. Cosmetic size never enlarges gameplay visibility.
        // Multiple heights/sides allow a visible head or body edge even when the chest is covered.
        static readonly Vector3[] Samples = CreateSamples();
        static Vector3[] CreateSamples()
        {
            var result = new System.Collections.Generic.List<Vector3> {
                new Vector3(0,0.3f,0), new Vector3(0,0.95f,0), new Vector3(0,-0.95f,0),
                new Vector3(0,0.85f,0), new Vector3(0,-0.85f,0)
            };
            foreach (float y in new[] { -0.6f, 0f, 0.6f })
            {
                result.Add(new Vector3(0,y,0));
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    result.Add(new Vector3(Mathf.Cos(angle)*0.37f,y,Mathf.Sin(angle)*0.37f));
                }
            }
            return result.ToArray();
        }
        public static bool CanSeeBody(Transform observer, Vector3 eye, Quaternion aim, Transform target,
            float verticalFov, float aspect, float range, int mask)
        {
            var inverse = Quaternion.Inverse(aim);
            foreach (var sample in Samples)
            {
                Vector3 delta = target.TransformPoint(sample) - eye;
                if (!GazeRules.InViewport(inverse * delta, verticalFov, aspect, range)) continue;
                // The SAME sample must be on screen and unobstructed. A visible bounds corner alone is insufficient.
                var hits = Physics.RaycastAll(eye, delta.normalized, delta.magnitude + 0.01f, mask, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                Transform first = null;
                foreach (var hit in hits)
                {
                    if (hit.transform.IsChildOf(observer) || hit.distance >= nearest) continue;
                    nearest = hit.distance; first = hit.transform;
                }
                if (first && first.IsChildOf(target)) return true;
            }
            return false;
        }
    }
}
