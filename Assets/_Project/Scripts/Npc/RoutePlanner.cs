using System;
using System.Collections.Generic;
using EyeSky.Security;

namespace EyeSky.Npc
{
    /// <summary>Elige la secuencia de puertas entre dos salas. Cada persona pone su coste por puerta.</summary>
    public static class RoutePlanner
    {
        public static List<BuildingLayout.DoorDef> Plan(BuildingLayout layout, string from, string to,
            Func<BuildingLayout.DoorDef, int> doorCost)
        {
            var result = new List<BuildingLayout.DoorDef>();
            if (from == null || to == null || from == to)
                return result;

            var distance = new Dictionary<string, int> { [from] = 0 };
            var cameFrom = new Dictionary<string, BuildingLayout.DoorDef>();
            var pending = new List<string> { from };

            while (pending.Count > 0)
            {
                pending.Sort((a, b) => distance[a].CompareTo(distance[b]));
                var room = pending[0];
                pending.RemoveAt(0);
                if (room == to)
                    break;

                foreach (var door in layout.doors)
                {
                    if (door.roomA != room && door.roomB != room)
                        continue;
                    var next = door.OtherRoom(room);
                    var cost = distance[room] + doorCost(door);
                    if (distance.TryGetValue(next, out var known) && known <= cost)
                        continue;
                    distance[next] = cost;
                    cameFrom[next] = door;
                    if (!pending.Contains(next))
                        pending.Add(next);
                }
            }

            if (!cameFrom.ContainsKey(to))
                return result;

            var cursor = to;
            while (cursor != from)
            {
                var door = cameFrom[cursor];
                result.Insert(0, door);
                cursor = door.OtherRoom(cursor);
            }
            return result;
        }
    }
}
