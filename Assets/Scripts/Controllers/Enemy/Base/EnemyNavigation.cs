using System.Collections.Generic;
using UnityEngine;

namespace BioBreach.Controller.Enemy.Base
{
    public class EnemyNavigation
    {
        List<Vector3> _path = new();
        int _current;

        const float WAYPOINT_REACH_SQ = 0.7f * 0.7f;

        const float REPATH_INTERVAL = 0.4f;
        float _repathTimer;

        const float STEP_HEIGHT = 1.2f;
        const float AGENT_HEIGHT = 1.8f;
        const float AGENT_RADIUS = 0.45f;

        Dictionary<Vector3Int, float> _heightCache = new();

        public Vector3? CurrentTarget { get; private set; }

        public void Tick(Vector3 ownerPos, Vector3 goalPos, float agentRadius, LayerMask obstacleMask)
        {
            if (HasDirectPath(ownerPos, goalPos, obstacleMask))
            {
                _path.Clear();
                CurrentTarget = goalPos;
                return;
            }

            _repathTimer += Time.deltaTime;

            if (_repathTimer >= REPATH_INTERVAL)
            {
                _repathTimer = 0f;
                RequestPath(ownerPos, goalPos, obstacleMask);
            }

            if (_path.Count == 0)
            {
                CurrentTarget = goalPos;
                return;
            }

            if ((ownerPos - _path[_current]).sqrMagnitude <= WAYPOINT_REACH_SQ)
                Advance();

            CurrentTarget = _path[_current];
        }

        bool HasDirectPath(Vector3 start, Vector3 end, LayerMask mask)
        {
            Vector3 dir = end - start;
            float dist = dir.magnitude;

            if (dist < 0.01f)
                return true;

            dir /= dist;

            Vector3 foot = start + Vector3.up * 0.2f;
            Vector3 head = start + Vector3.up * (AGENT_HEIGHT - 0.2f);

            bool footBlocked = Physics.Raycast(foot, dir, dist, mask);
            bool headBlocked = Physics.Raycast(head, dir, dist, mask);

            return !(footBlocked && headBlocked);
        }

        void RequestPath(Vector3 start, Vector3 goal, LayerMask mask)
        {
            var path = AStar(start, goal, mask);

            if (path != null && path.Count > 0)
            {
                Smooth(path, mask);
                _path = path;
                _current = 0;
            }
        }

        List<Vector3> AStar(Vector3 start, Vector3 goal, LayerMask mask)
        {
            const int MAX_NODES = 4096;

            Vector3Int startCell = WorldToCell(start);
            Vector3Int goalCell  = WorldToCell(goal);

            if (!SampleGround(startCell, out float startY))
                return null;

            var open  = new MinHeap<Node>();
            var came  = new Dictionary<Vector3Int, Vector3Int>();
            var g     = new Dictionary<Vector3Int, float>();
            var hmap  = new Dictionary<Vector3Int, float>();

            g[startCell] = 0;
            hmap[startCell] = startY;

            open.Push(new Node(startCell, Heuristic(startCell, goalCell), startY));

            int explored = 0;

            while (open.Count > 0 && explored < MAX_NODES)
            {
                var cur = open.Pop();
                explored++;

                if (cur.cell == goalCell)
                    return Reconstruct(came, hmap, cur.cell);

                foreach (var n in Neighbors(cur.cell))
                {
                    if (!SampleGround(n, out float y))
                        continue;

                    float heightDiff = Mathf.Abs(y - cur.height);
                    if (heightDiff > STEP_HEIGHT)
                        continue;

                    Vector3 pos = new Vector3(n.x, y, n.z);

                    if (Blocked(pos, mask))
                        continue;

                    float cost = g[cur.cell] + Vector2Int.Distance(
                        new Vector2Int(cur.cell.x, cur.cell.z),
                        new Vector2Int(n.x, n.z)
                    );

                    if (cost < g.GetValueOrDefault(n, float.MaxValue))
                    {
                        came[n] = cur.cell;
                        g[n] = cost;
                        hmap[n] = y;

                        float f = cost + Heuristic(n, goalCell);
                        open.Push(new Node(n, f, y));
                    }
                }
            }

            return null;
        }

        bool SampleGround(Vector3Int cell, out float y)
        {
            if (_heightCache.TryGetValue(cell, out y))
                return true;

            Vector3 origin = new Vector3(cell.x, 10f, cell.z);

            if (Physics.Raycast(origin, Vector3.down, out var hit, 20f))
            {
                y = hit.point.y;
                _heightCache[cell] = y;
                return true;
            }

            y = 0;
            return false;
        }

        bool Blocked(Vector3 pos, LayerMask mask)
        {
            Vector3 bottom = pos + Vector3.up * 0.1f;
            Vector3 top    = pos + Vector3.up * AGENT_HEIGHT;

            return Physics.CheckCapsule(bottom, top, AGENT_RADIUS, mask);
        }

        void Smooth(List<Vector3> path, LayerMask mask)
        {
            if (path.Count < 3)
                return;

            int i = 0;

            while (i < path.Count - 2)
            {
                if (HasDirectPath(path[i], path[i + 2], mask))
                    path.RemoveAt(i + 1);
                else
                    i++;
            }
        }

        void Advance()
        {
            if (_current < _path.Count - 1)
                _current++;
        }

        static Vector3Int WorldToCell(Vector3 p)
        {
            return new Vector3Int(
                Mathf.RoundToInt(p.x),
                0,
                Mathf.RoundToInt(p.z)
            );
        }

        static float Heuristic(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z);
        }

        static IEnumerable<Vector3Int> Neighbors(Vector3Int c)
        {
            yield return new Vector3Int(c.x + 1, 0, c.z);
            yield return new Vector3Int(c.x - 1, 0, c.z);
            yield return new Vector3Int(c.x, 0, c.z + 1);
            yield return new Vector3Int(c.x, 0, c.z - 1);

            yield return new Vector3Int(c.x + 1, 0, c.z + 1);
            yield return new Vector3Int(c.x + 1, 0, c.z - 1);
            yield return new Vector3Int(c.x - 1, 0, c.z + 1);
            yield return new Vector3Int(c.x - 1, 0, c.z - 1);
        }

        List<Vector3> Reconstruct(
            Dictionary<Vector3Int, Vector3Int> came,
            Dictionary<Vector3Int, float> height,
            Vector3Int cur)
        {
            var path = new List<Vector3>();

            while (came.TryGetValue(cur, out var prev))
            {
                float y = height[cur];
                path.Add(new Vector3(cur.x, y, cur.z));
                cur = prev;
            }

            path.Reverse();
            return path;
        }

        struct Node : System.IComparable<Node>
        {
            public Vector3Int cell;
            public float f;
            public float height;

            public Node(Vector3Int c, float fScore, float h)
            {
                cell = c;
                f = fScore;
                height = h;
            }

            public int CompareTo(Node o) => f.CompareTo(o.f);
        }

        class MinHeap<T> where T : System.IComparable<T>
        {
            readonly List<T> data = new();

            public int Count => data.Count;

            public void Push(T item)
            {
                data.Add(item);
                int i = data.Count - 1;

                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (data[p].CompareTo(data[i]) <= 0) break;

                    (data[p], data[i]) = (data[i], data[p]);
                    i = p;
                }
            }

            public T Pop()
            {
                T top = data[0];
                int last = data.Count - 1;

                data[0] = data[last];
                data.RemoveAt(last);

                int i = 0;

                while (true)
                {
                    int l = i * 2 + 1;
                    int r = i * 2 + 2;
                    int s = i;

                    if (l < data.Count && data[l].CompareTo(data[s]) < 0) s = l;
                    if (r < data.Count && data[r].CompareTo(data[s]) < 0) s = r;

                    if (s == i) break;

                    (data[i], data[s]) = (data[s], data[i]);
                    i = s;
                }

                return top;
            }
        }
    }
}