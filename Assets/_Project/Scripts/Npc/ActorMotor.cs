using System;
using System.Collections.Generic;
using EyeSky.Security;
using UnityEngine;
using UnityEngine.AI;

namespace EyeSky.Npc
{
    /// <summary>
    /// Mueve a una persona por el edificio sala a sala, parandose en cada puerta cerrada.
    /// No decide que hacer con la puerta: avisa con BlockedDoor y espera a Resume.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ActorMotor : MonoBehaviour
    {
        const float DoorApproach = 0.7f;
        const float ArriveDistance = 0.2f;

        public enum MotorStatus
        {
            Idle,
            Moving,
            Blocked,
        }

        struct Leg
        {
            public Vector3 Point;
            public BuildingLayout.DoorDef DoorAhead;
            public BuildingLayout.DoorDef DoorCrossed;
        }

        readonly List<Leg> _legs = new();
        int _legIndex;
        NavMeshAgent _agent;

        public MotorStatus Status { get; private set; } = MotorStatus.Idle;
        public BuildingLayout.DoorDef BlockedDoor { get; private set; }
        public string CurrentRoom { get; private set; }

        /// <summary>Se dispara al acabar de cruzar una puerta.</summary>
        public event Action<BuildingLayout.DoorDef> DoorCrossed;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
        }

        void Start()
        {
            UpdateRoom();
        }

        public void SetSpeed(float speed) => _agent.speed = speed;

        public void Warp(Vector3 position)
        {
            Stop();
            _agent.Warp(position);
            UpdateRoom();
        }

        public void Stop()
        {
            _legs.Clear();
            _legIndex = 0;
            BlockedDoor = null;
            Status = MotorStatus.Idle;
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
                _agent.ResetPath();
        }

        /// <param name="doorCost">Lo que le cuesta a esta persona pasar por cada puerta.</param>
        public void Go(Vector3 destination, Func<BuildingLayout.DoorDef, int> doorCost)
        {
            var layout = NpcWorld.Instance.layout;
            UpdateRoom();
            var targetRoom = layout.RoomIdAt(destination) ?? CurrentRoom;

            _legs.Clear();
            _legIndex = 0;
            BlockedDoor = null;

            var from = CurrentRoom;
            foreach (var door in RoutePlanner.Plan(layout, from, targetRoom, doorCost))
            {
                var next = door.OtherRoom(from);
                _legs.Add(new Leg { Point = layout.DoorSide(door, from, DoorApproach), DoorAhead = door });
                _legs.Add(new Leg { Point = layout.DoorSide(door, next, DoorApproach), DoorCrossed = door });
                from = next;
            }
            _legs.Add(new Leg { Point = destination });

            Status = MotorStatus.Moving;
            SetLeg();
        }

        /// <summary>Sigue andando despues de que la puerta se haya abierto.</summary>
        public void Resume()
        {
            if (Status != MotorStatus.Blocked)
                return;
            BlockedDoor = null;
            Status = MotorStatus.Moving;
            NextLeg();
        }

        void SetLeg()
        {
            var point = _legs[_legIndex].Point;
            if (NavMesh.SamplePosition(point, out var hit, 1.5f, NavMesh.AllAreas))
                point = hit.position;
            _agent.SetDestination(point);
        }

        void NextLeg()
        {
            _legIndex++;
            if (_legIndex >= _legs.Count)
            {
                _legs.Clear();
                Status = MotorStatus.Idle;
                return;
            }
            SetLeg();
        }

        void Update()
        {
            UpdateRoom();
            if (Status != MotorStatus.Moving || _agent.pathPending)
                return;

            var arrived = _agent.remainingDistance <= ArriveDistance || _agent.pathStatus == NavMeshPathStatus.PathInvalid;
            if (!arrived)
                return;

            var leg = _legs[_legIndex];
            if (leg.DoorCrossed != null)
                DoorCrossed?.Invoke(leg.DoorCrossed);

            if (leg.DoorAhead != null && !NpcWorld.Instance.doors.IsOpen(leg.DoorAhead.id))
            {
                BlockedDoor = leg.DoorAhead;
                Status = MotorStatus.Blocked;
                return;
            }
            NextLeg();
        }

        void UpdateRoom()
        {
            var world = NpcWorld.Instance;
            if (world == null)
                return;
            var room = world.layout.RoomIdAt(transform.position);
            if (room != null)
                CurrentRoom = room;
        }
    }
}
