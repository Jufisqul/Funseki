using UnityEngine;
using UnityEngine.AI;

namespace Funseki.Heroes
{
    // On every hero. The leader moves on its CharacterController (Funseki.Player); the other two follow it on a
    // NavMeshAgent with no collider, so they never block a corridor. A follower walks to its spot behind the leader
    // when the leader is more than followMaxDistance away, runs to catch up, and appears behind the leader
    // when it is too far behind and out of the camera's view or gets stuck out of view.
    [RequireComponent(typeof(CharacterController), typeof(NavMeshAgent))]
    public class HeroUnit : MonoBehaviour
    {
        [SerializeField] HeroData data;
        [SerializeField] HeroSettings settings;

        public HeroData Data => data;
        public bool IsLeader { get; private set; }
        /// <summary>Measured ground speed, m/s (leader or follower).</summary>
        public float Speed { get; private set; }

        CharacterController cc;
        NavMeshAgent agent;
        HeroUnit leader;
        int slot;
        bool moving;
        Vector3 lastPosition, lastDestination;
        float repathAt, stuckTimer, offMeshTime;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = settings.agentRadius;
            agent.height = cc.height;
            agent.stoppingDistance = 0.2f;
            agent.angularSpeed = 540f;
            agent.acceleration = 20f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            lastPosition = transform.position;
        }

        /// <summary>Moves the hero instantly (a loaded save). Call MakeLeader / MakeFollower after it.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            bool ccOn = cc.enabled;
            cc.enabled = false;
            if (agent.enabled) agent.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            lastPosition = position;
            moving = false;
            cc.enabled = ccOn;
        }

        public void MakeLeader()
        {
            IsLeader = true;
            leader = null;
            if (agent.enabled) agent.enabled = false;
            cc.enabled = true;
        }

        /// <summary>Not in the party yet: stands where it was put (no collider, no NavMesh agent) until it joins.</summary>
        public void MakeIdle()
        {
            IsLeader = false;
            leader = null;
            moving = false;
            if (agent.enabled) agent.enabled = false;
            cc.enabled = false;
        }

        // slot 0 = behind on the left, 1 = behind on the right.
        public void MakeFollower(HeroUnit newLeader, int followSlot)
        {
            IsLeader = false;
            leader = newLeader;
            slot = followSlot;
            cc.enabled = false;
            agent.avoidancePriority = 50 + followSlot;
            moving = false;
            stuckTimer = 0f;
            TryEnableAgent();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 delta = transform.position - lastPosition;
                delta.y = 0f;
                Speed = delta.magnitude / dt;
            }
            lastPosition = transform.position;

            if (!IsLeader && leader != null) Follow(dt);
            if (!IsLeader && agent.enabled && agent.isOnNavMesh) SnapToGround();
        }

        void Follow(float dt)
        {
            // The NavMesh appears after the school is loaded and baked; until then the follower waits.
            if (!agent.enabled && !TryEnableAgent()) return;
            // A NavMesh rebuild or a door carving can briefly leave the agent off the mesh; give it a moment
            // before putting it back (re-placing it every frame makes it twitch).
            if (!agent.isOnNavMesh)
            {
                offMeshTime += dt;
                if (offMeshTime > 0.5f) { offMeshTime = 0f; agent.enabled = false; }
                return;
            }
            offMeshTime = 0f;

            Vector3 me = transform.position, lead = leader.transform.position;
            float dist = Vector3.Distance(Flat(me), Flat(lead));
            bool visible = IsOnScreen();

            if (dist > settings.teleportDistance && !visible)
            {
                TeleportBehindLeader();
                return;
            }

            // Start when the leader is out of the 1.5-3 m band (too close = step out of the way), stop at the spot.
            if (dist > settings.followMaxDistance || dist < settings.followMinDistance * 0.5f) moving = true;
            else if (moving && !agent.pathPending && agent.hasPath
                     && agent.remainingDistance <= agent.stoppingDistance + 0.3f) moving = false;

            if (!moving)
            {
                // Standing: no path, no leftover velocity, no avoidance nudges; only turn to face the leader.
                if (agent.hasPath) agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.updateRotation = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                stuckTimer = 0f;
                FaceLeader(dt);
                return;
            }
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            bool hurry = dist > settings.catchUpDistance || leader.Speed > settings.followWalkSpeed + 0.5f;
            agent.speed = hurry ? settings.followRunSpeed : settings.followWalkSpeed;

            Vector3 spot = SlotPosition();
            if (Time.time >= repathAt || (spot - lastDestination).sqrMagnitude > 0.25f)
            {
                repathAt = Time.time + 0.25f;
                lastDestination = spot;
                if (NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
                else if (NavMesh.SamplePosition(lead, out hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }

            // Stuck: wants to move but doesn't (a closed door, a dead end of the NavMesh).
            if (!agent.pathPending && agent.velocity.sqrMagnitude < 0.04f) stuckTimer += dt;
            else stuckTimer = 0f;
            if (stuckTimer > settings.stuckTime)
            {
                stuckTimer = 0f;
                if (!visible) TeleportBehindLeader();
                else if (NavMesh.SamplePosition(lead, out var hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }
        }

        // The baked NavMesh lies a few centimetres above or below the real floor; lift or lower the follower onto it
        // so its feet neither float nor sink.
        void SnapToGround()
        {
            Vector3 nav = agent.nextPosition;
            if (Physics.Raycast(nav + Vector3.up * 0.4f, Vector3.down, out var hit, 0.8f, settings.navMeshLayers, QueryTriggerInteraction.Ignore))
            {
                // Smoothly, and ignoring millimetres, so a standing follower doesn't bob.
                float target = Mathf.Clamp(hit.point.y - nav.y, -0.3f, 0.3f);
                if (Mathf.Abs(target - agent.baseOffset) > 0.01f)
                    agent.baseOffset = Mathf.MoveTowards(agent.baseOffset, target, Time.deltaTime);
            }
        }

        Vector3 SlotPosition()
        {
            var lt = leader.transform;
            Vector3 back = -Vector3.ProjectOnPlane(lt.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -back);
            float side = slot == 0 ? -settings.followSide : settings.followSide;
            return lt.position + back * settings.followBehind + right * side;
        }

        void TeleportBehindLeader()
        {
            if (!NavMesh.SamplePosition(SlotPosition(), out var hit, 3f, NavMesh.AllAreas)
                && !NavMesh.SamplePosition(leader.transform.position, out hit, 3f, NavMesh.AllAreas)) return;
            agent.Warp(hit.position);
            Vector3 look = Flat(leader.transform.forward);
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            lastPosition = transform.position;
            moving = false;
        }

        void FaceLeader(float dt)
        {
            Vector3 to = Flat(leader.transform.position - transform.position);
            if (to.sqrMagnitude < 0.01f) return;
            var target = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, settings.idleTurnSpeed * dt);
        }

        bool TryEnableAgent()
        {
            if (IsLeader) return false;
            if (!NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) return false;
            agent.enabled = true;
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        bool IsOnScreen()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up * 1f);
            return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
