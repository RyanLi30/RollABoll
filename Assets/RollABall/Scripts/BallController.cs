using UnityEngine;
using UnityEngine.InputSystem;

namespace Ryan.RollABall
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BallController : MonoBehaviour
    {
        [SerializeField] private float acceleration = 32f;
        [SerializeField] private float maximumSpeed = 8f;
        [SerializeField] private float jumpSpeed = 7.5f;
        [SerializeField] private bool threeLaneMode;
        [SerializeField] private float laneSpacing = 4f;
        [SerializeField] private float springMaximumSpeed = 26f;
        [SerializeField] private bool autoRun;
        private float speedMultiplier = 1f;
        private float speedEffectUntil = -1f;
        private float runStartsAt;
        public bool AutoRun => autoRun || threeLaneMode;
        public const float NormalJumpGravityScale = 4.5f;
        public float RunStartsIn => Mathf.Max(0, runStartsAt - Time.time);
        public float SpeedMultiplier => Time.time < speedEffectUntil ? speedMultiplier : 1f;
        public float SpeedEffectRemaining => Mathf.Max(0, speedEffectUntil - Time.time);
        public float TargetForwardSpeed => maximumSpeed * SpeedMultiplier;
        public float CurrentForwardSpeed => body == null ? 0 : Mathf.Max(0, body.linearVelocity.z);
        public float MaximumSpeed => maximumSpeed;
        public float JumpSpeed => jumpSpeed;
        public int CurrentLane { get; private set; }
        private Rigidbody body;
        private Vector2 movement;
        private Vector3 spawn;
        private float jumpRequestedUntil = -1f;
        private float lastGrounded = -1f;
        private float springUntil = -1f;
        private float springStarted = -1f;
        private float springSteeringTarget;
        private float springTimeScale = 1f;
        private bool normalJumpInFlight;
        public bool CanMove { get; set; } = true;
        public bool IsGrounded { get; private set; }
        public int Falls { get; private set; }
        public int CheckpointNumber { get; private set; }
        public int Bounces { get; private set; }
        public bool SpringInFlight => Time.time < springUntil;
        public Vector3 RespawnPosition => spawn;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            spawn = transform.position;
            body.linearDamping = 0;
            autoRun = AutoRun;
            runStartsAt = Time.time;
            if (AutoRun) body.linearVelocity = new Vector3(0, body.linearVelocity.y, maximumSpeed);
            CurrentLane = Mathf.Clamp(Mathf.RoundToInt(spawn.x / laneSpacing), -1, 1);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            movement = Vector2.zero;
            if (keyboard == null || !CanMove) return;
            movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                       - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                       - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            movement = Vector2.ClampMagnitude(movement, 1);
            if (threeLaneMode)
            {
                if (!SpringInFlight && (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame))
                    CurrentLane = Mathf.Max(-1, CurrentLane - 1);
                if (!SpringInFlight && (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame))
                    CurrentLane = Mathf.Min(1, CurrentLane + 1);
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                // Forward speed stays constant while changing lanes.
                movement.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            if (keyboard.spaceKey.wasPressedThisFrame) jumpRequestedUntil = Time.time + 0.15f;
        }

        private void FixedUpdate()
        {
            if (!CanMove) return;
            IsGrounded = body.linearVelocity.y <= 0.25f
                && Physics.Raycast(body.position, Vector3.down, out var ground, 0.79f, ~0, QueryTriggerInteraction.Ignore)
                && ground.collider.attachedRigidbody != body;
            if (IsGrounded)
            {
                lastGrounded = Time.time;
                normalJumpInFlight = false;
            }
            if (IsGrounded && Time.time > springStarted + 0.2f && SpringInFlight)
            {
                springUntil = -1;
                var landed = body.linearVelocity;
                body.linearVelocity = new Vector3(0, landed.y, autoRun ? landed.z : 0);
            }
            if (!SpringInFlight && jumpRequestedUntil >= Time.time && lastGrounded >= Time.time - 0.1f)
            {
                var velocity = body.linearVelocity;
                body.linearVelocity = new Vector3(velocity.x, jumpSpeed, velocity.z);
                jumpRequestedUntil = -1;
                lastGrounded = -1;
                IsGrounded = false;
                normalJumpInFlight = true;
            }
            // Time-scale the entire spring arc so boost changes speed without overshooting.
            if (SpringInFlight)
                body.AddForce(Physics.gravity * (springTimeScale * springTimeScale - 1f), ForceMode.Acceleration);
            else
            {
                if (normalJumpInFlight)
                    body.AddForce(Physics.gravity * (NormalJumpGravityScale - 1f), ForceMode.Acceleration);
                else
                {
                    // Rolling off an edge is a fall, never an automatic jump.
                    if (body.linearVelocity.y > 0)
                        body.linearVelocity = new Vector3(body.linearVelocity.x, 0, body.linearVelocity.z);
                    if (!IsGrounded)
                        body.AddForce(Physics.gravity * 19f, ForceMode.Acceleration);
                }
            }
            if (!SpringInFlight && !threeLaneMode)
                body.AddForce(new Vector3(movement.x, 0, movement.y) * acceleration * (IsGrounded ? 1f : 0.55f), ForceMode.Acceleration);
            var current = body.linearVelocity;
            var horizontal = new Vector3(current.x, 0, current.z);
            if (threeLaneMode)
            {
                // Lane steering remains available during a spring jump. Keep forward
                // flight independent so a sideways correction cannot shorten the jump.
                if (SpringInFlight)
                {
                    // Read held input, including keys already held before touching the board.
                    // Releasing a key keeps the adjusted landing target instead of undoing it.
                    springSteeringTarget = Mathf.Clamp(springSteeringTarget + movement.x * 12f * springTimeScale * Time.fixedDeltaTime, -laneSpacing, laneSpacing);
                    CurrentLane = Mathf.Clamp(Mathf.RoundToInt(springSteeringTarget / laneSpacing), -1, 1);
                    horizontal.x = Mathf.Clamp((springSteeringTarget - body.position.x) * 16f * springTimeScale, -18f * springTimeScale, 18f * springTimeScale);
                    // Keep launch speed through the gap instead of braking in the air.
                }
                else
                {
                    horizontal.x = Mathf.Clamp((CurrentLane * laneSpacing - body.position.x) * 12f, -18f, 18f);
                    float target = AutoRun ? TargetForwardSpeed
                        : movement.y * maximumSpeed * (movement.y < 0 ? 0.65f : 1f);
                    horizontal.z = Mathf.MoveTowards(horizontal.z, target, acceleration * Time.fixedDeltaTime);
                }
            }
            else
            {
                horizontal *= 1f / (1f + (IsGrounded ? 3.2f : 0.35f) * Time.fixedDeltaTime);
                horizontal = Vector3.ClampMagnitude(horizontal, SpringInFlight ? springMaximumSpeed : maximumSpeed);
            }
            body.linearVelocity = new Vector3(horizontal.x, current.y, horizontal.z);
            if (body.position.y < -8f) Respawn();
        }

        public bool LaunchFromSpring(Vector3 destination, float upwardSpeed)
        {
            if (!CanMove || SpringInFlight || body.linearVelocity.y > 0.5f) return false;
            float gravity = -Physics.gravity.y;
            float discriminant = upwardSpeed * upwardSpeed - 2 * gravity * (destination.y - body.position.y);
            if (gravity <= 0 || discriminant <= 0) return false;
            float speedScale = AutoRun ? SpeedMultiplier : 1f;
            float flight = (upwardSpeed + Mathf.Sqrt(discriminant)) / gravity / speedScale;
            var displacement = destination - body.position;
            displacement.y = 0;
            // Scale horizontal velocity, upward velocity and gravity together.
            var launch = displacement / flight;
            if (launch.magnitude > springMaximumSpeed * speedScale) return false;
            springTimeScale = speedScale;
            body.linearVelocity = new Vector3(launch.x, upwardSpeed * speedScale, launch.z);
            normalJumpInFlight = false;
            springStarted = Time.time;
            springUntil = Time.time + flight + 0.15f / speedScale;
            jumpRequestedUntil = -1;
            lastGrounded = -1;
            IsGrounded = false;
            Bounces++;
            springSteeringTarget = Mathf.Clamp(body.position.x, -laneSpacing, laneSpacing);
            if (threeLaneMode) CurrentLane = Mathf.Clamp(Mathf.RoundToInt(destination.x / laneSpacing), -1, 1);
            return true;
        }

        public void ConfigureThreeLanes(float forwardSpeed)
        {
            threeLaneMode = true;
            maximumSpeed = forwardSpeed;
            jumpSpeed = 5.5f;
            acceleration = 70f;
            springMaximumSpeed = 26f;
        }

        public void ConfigureAutoRun(float forwardSpeed)
        {
            ConfigureThreeLanes(forwardSpeed);
            autoRun = true;
            jumpSpeed = 12f;
            springMaximumSpeed = 40f;
        }

        public void ApplySpeedStrip(float multiplier, float duration)
        {
            if (!CanMove || !autoRun) return;
            // Latest strip wins. Repeated contact never multiplies an existing boost.
            speedMultiplier = Mathf.Clamp(multiplier, 0.72f, 2.5f);
            // Boost lasts until a slow strip or a run reset; it never stacks.
            speedEffectUntil = speedMultiplier > 1f ? float.PositiveInfinity : Time.time + Mathf.Max(0, duration);
        }

        private void ResetRunState()
        {
            speedMultiplier = 1f;
            speedEffectUntil = -1f;
            runStartsAt = Time.time;
            normalJumpInFlight = false;
            if (AutoRun) body.linearVelocity = new Vector3(0, body.linearVelocity.y, maximumSpeed);
        }

        public void ReturnToCourse(Vector3 position, int section)
        {
            spawn = position;
            CheckpointNumber = section;
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            springUntil = -1;
            lastGrounded = -1;
            jumpRequestedUntil = -1;
            ResetRunState();
            CurrentLane = Mathf.Clamp(Mathf.RoundToInt(position.x / laneSpacing), -1, 1);
        }

        public void SetCheckpoint(Vector3 position, int number)
        {
            if (number <= CheckpointNumber) return;
            spawn = position;
            CheckpointNumber = number;
        }

        private void Respawn()
        {
            Falls++;
            body.position = spawn;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            jumpRequestedUntil = -1;
            lastGrounded = -1;
            springUntil = -1;
            ResetRunState();
            CurrentLane = Mathf.Clamp(Mathf.RoundToInt(spawn.x / laneSpacing), -1, 1);
        }

        public void Stop()
        {
            CanMove = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (CanMove && other.TryGetComponent<CollectibleCube>(out var cube)) cube.Collect();
        }
    }
}
