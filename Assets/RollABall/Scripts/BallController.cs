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
        public bool AutoRun => autoRun;
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
            runStartsAt = Time.time + (autoRun ? 1.8f : 0);
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
            if (IsGrounded) lastGrounded = Time.time;
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
                    springSteeringTarget = Mathf.Clamp(springSteeringTarget + movement.x * 12f * Time.fixedDeltaTime, -laneSpacing, laneSpacing);
                    CurrentLane = Mathf.Clamp(Mathf.RoundToInt(springSteeringTarget / laneSpacing), -1, 1);
                    horizontal.x = Mathf.Clamp((springSteeringTarget - body.position.x) * 16f, -18f, 18f);
                    horizontal.z *= 1f / (1f + 0.35f * Time.fixedDeltaTime);
                }
                else
                {
                    horizontal.x = Mathf.Clamp((CurrentLane * laneSpacing - body.position.x) * 12f, -18f, 18f);
                    float target = autoRun ? (Time.time >= runStartsAt ? TargetForwardSpeed : 0)
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
            float flight = (upwardSpeed + Mathf.Sqrt(discriminant)) / gravity;
            var displacement = destination - body.position;
            displacement.y = 0;
            // Compensate for the same horizontal air drag used by FixedUpdate.
            var launch = displacement * (0.35f / (1f - Mathf.Exp(-0.35f * flight)));
            if (launch.magnitude > springMaximumSpeed) return false;
            body.linearVelocity = new Vector3(launch.x, upwardSpeed, launch.z);
            springStarted = Time.time;
            springUntil = Time.time + flight + 0.15f;
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
            jumpSpeed = 6.5f;
            springMaximumSpeed = 40f;
        }

        public void ApplySpeedStrip(float multiplier, float duration)
        {
            if (!CanMove || !autoRun) return;
            // Latest strip wins. Repeated contact never multiplies an existing boost.
            speedMultiplier = Mathf.Clamp(multiplier, 0.72f, 1.4f);
            speedEffectUntil = Time.time + Mathf.Max(0, duration);
        }

        private void ResetRunState()
        {
            speedMultiplier = 1f;
            speedEffectUntil = -1f;
            runStartsAt = Time.time + (autoRun ? 0.8f : 0);
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
