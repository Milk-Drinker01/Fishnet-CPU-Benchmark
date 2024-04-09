using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using KinematicCharacterController;
using UnityEngine;

namespace FishnetKCC
{
    public struct KCCNetworkState
    {
        public bool MustUnground;
        public float MustUngroundTime;
        public bool LastMovementIterationFoundAnyGround;

        public bool FoundAnyGround;
        public bool IsStableOnGround;
        public bool SnappingPrevented;
        public Vector3 GroundNormal;
        public Vector3 InnerGroundNormal;
        public Vector3 OuterGroundNormal;
    }

    public struct MoveData : IReplicateData
    {
        public Vector2 DeltaYawPitch;
        public Vector2 Movement;
        public bool Sprint;
        public bool CouchInput;
        public bool JumpDown;

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct ReconcileData : IReconcileData
    {
        public Vector3 Position;
        public Vector3 BaseVelocity;
        public KCCNetworkState KCCData;
        public Vector2 YawPitch;
        public bool Crouching;


        public ReconcileData(Vector3 position, Vector3 baseVelocity, KCCNetworkState netState, Vector2 yawPitch, bool crouching)
        {
            Position = position;
            BaseVelocity = baseVelocity;
            KCCData = netState;
            YawPitch = yawPitch;
            Crouching = crouching;
            _tick = 0;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    //put things that you dont really give a fuck about being networked here
    //but need to be rolled back in the simulation
    public class AdditionalKCCNetworkInfo
    {
        public bool JumpConsumed;
        public Rigidbody AttachedRigidbody;
        public Vector3 AttachedRigidbodyVelocity;
    }

    public class KccPlayerFishnet : NetworkBehaviour
    {
        [SerializeField] private float _sensitivityX = 1f;
        [SerializeField] private float _sensitivityY = 1f;
        [SerializeField] private bool ToggleCrouch = false;

        [SerializeField] private Transform RenderTransform;
        [SerializeField] private Transform CameraTransform;

        public KCCNetworkState KCCState;

        private AdditionalKCCNetworkInfo[] AdditionalStateInfoBuffer;
        private ReconcileData[] SnapshotBuffer;   //we have to store this manually if we want to interpolate snapshots

        public Vector3 BaseVelocity;
        public Vector2 YawPitch;
        public bool Crouching;

        private KinematicCharacterMotor _motor;
        private Locomotion _locomotion;
        private bool _crouching;
        private int _latestSimulatedTick;
        private float _lastTickTime;    //this solution sucks but fuck it
                                        //private bool _IsPredicted;

        private void Awake()
        {
            _motor = GetComponent<KinematicCharacterMotor>();
            _locomotion = GetComponent<Locomotion>();
            InstanceFinder.TimeManager.OnTick += TimeManager_OnTick;
        }

        private void OnDestroy()
        {
            if (InstanceFinder.TimeManager != null)
            {
                InstanceFinder.TimeManager.OnTick -= TimeManager_OnTick;
            }
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        void Start()
        {
            // We disable Settings.AutoSimulation + Settings.Interpolate of KinematicCharacterSystem to essentially handle the simulation ourself
            KinematicCharacterSystem.Settings.AutoSimulation = false;
            KinematicCharacterSystem.Settings.Interpolate = false;
        }

        public override void OnStartServer()
        {
            OnStart();
        }

        public override void OnStartClient()
        {
            OnStart();
        }

        void OnStart()
        {
            InitInfoBuffer();

            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                GetComponentInChildren<Camera>(true).gameObject.SetActive(true);
            }
        }

        void InitSnapshotBuffer()
        {
            SnapshotBuffer = new ReconcileData[PredictionManager.GetMaximumServerReplicates()];
            for (int i = 0; i < SnapshotBuffer.Length; i++)
                SnapshotBuffer[i] = new ReconcileData();
        }

        void InitInfoBuffer()
        {
            AdditionalStateInfoBuffer = new AdditionalKCCNetworkInfo[PredictionManager.GetMaximumServerReplicates()];
            for (int i = 0; i < AdditionalStateInfoBuffer.Length; i++)
                AdditionalStateInfoBuffer[i] = new AdditionalKCCNetworkInfo();
        }

        private void TimeManager_OnTick()
        {
            //Debug.Log("---NEW TICK STARTED---");
            CheckInput(out MoveData md);
            PrepareMove(md);
            if (base.IsServerStarted)
            {
                //Debug.Log(TimeManager.Tick);
                //ReconcileData rd = new ReconcileData(transform.position, Velocity, KCCState, YawPitch, Crouching);
                Reconciliation(SnapshotBuffer[_latestSimulatedTick % SnapshotBuffer.Length]);
            }
        }

        private MoveData clientInput = new MoveData();
        private void Update()
        {
            if (!base.IsOwner)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else if (Cursor.lockState == CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            var camInput = new Vector2(Input.GetAxisRaw("Mouse X") * _sensitivityX, Input.GetAxisRaw("Mouse Y") * -_sensitivityY);
            camInput *= (Cursor.lockState == CursorLockMode.Locked ? 1 : 0);

            clientInput.DeltaYawPitch += camInput;
            clientInput.Movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            clientInput.Sprint = Input.GetKey(KeyCode.LeftShift);

            clientInput.JumpDown |= Input.GetKeyDown(KeyCode.Space);

            if (ToggleCrouch)
            {
                if (Input.GetKeyDown(KeyCode.C))
                    _crouching = !_crouching;
            }
            else
                _crouching = Input.GetKey(KeyCode.C);

            clientInput.CouchInput = _crouching;
        }

        private void CheckInput(out MoveData moveData)
        {
            //clientInput.DeltaYawPitch.x += (Random.Range(30, 90) * Time.fixedDeltaTime);
            moveData = clientInput;

            clientInput = new MoveData(); //reset network input
        }

        public void LateUpdate()
        {
            Render();
        }

        void Render()
        {
            if (_latestSimulatedTick == 0 || SnapshotBuffer == null)
                return;
            int previousTick = (_latestSimulatedTick - 1) % SnapshotBuffer.Length;
            int currentTick = (_latestSimulatedTick) % SnapshotBuffer.Length;

            float timeSinceLastTick = Time.time - _lastTickTime;    //this solution sucks but fuck it
            float alpha = (float)((timeSinceLastTick) / TimeManager.TickDelta);  //returns a value 0-1 between ticks

            if (!IsOwner)
            {
                float yawInterp = Mathf.LerpAngle(SnapshotBuffer[previousTick].YawPitch.x, SnapshotBuffer[currentTick].YawPitch.x, alpha);
                float pitchInterp = Mathf.LerpAngle(SnapshotBuffer[previousTick].YawPitch.y, SnapshotBuffer[currentTick].YawPitch.y, alpha);

                RenderTransform.localRotation = Quaternion.Euler(0, yawInterp, 0);
                CameraTransform.localRotation = Quaternion.Euler(pitchInterp, 0, 0);
            }
            else
            {
                // we want to apply rotations immediately on local client, without waiting for next tick.
                // NOTE: this will cause jittering if u use queued inputs.
#if UNITY_EDITOR
                if (PredictionManager.QueuedInputs != 0)
                    Debug.LogError("Attempting to predict rotation before tick simulation. You cant do this when using Queued Inputs");
#endif
                ReconcileData rd = SnapshotBuffer[_latestSimulatedTick % SnapshotBuffer.Length];
                Vector2 yawPitch = rd.YawPitch + clientInput.DeltaYawPitch;
                RenderTransform.rotation = Quaternion.Euler(0, yawPitch.x, 0);
                CameraTransform.localRotation = Quaternion.Euler(yawPitch.y, 0, 0);
            }

            float height = Crouching ? _locomotion.CrouchedCapsuleHeight : _locomotion.CapsuleStandHeight;
            RenderTransform.localScale = new Vector3(1, height / 2, 1);
        }


        [Reconcile]
        private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            if (IsServerInitialized)
                return;
            transform.position = rd.Position;
            BaseVelocity = rd.BaseVelocity;
            KCCState = rd.KCCData;
            YawPitch = rd.YawPitch;
            Crouching = rd.Crouching;
            _motor.ApplyState(NetworkStateToKCCState(KCCState, (int)rd.GetTick()));

            if (SnapshotBuffer == null)
                InitSnapshotBuffer();
            int SnapShotIndex = (int)rd.GetTick() % SnapshotBuffer.Length;
            //if (rd.Crouching != SnapshotBuffer[SnapShotIndex].Crouching)
            //    Debug.Log("MISPREDICT :(");
            SnapshotBuffer[SnapShotIndex] = rd;
        }

        [Replicate]
        private void PrepareMove(MoveData input, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            YawPitch = ClampAngles(YawPitch + input.DeltaYawPitch);
            LocomotionInputs characterInputs = new LocomotionInputs();
            characterInputs.MoveAxisForward = input.Movement.y;
            characterInputs.MoveAxisRight = input.Movement.x;
            characterInputs.sprint = input.Sprint && characterInputs.MoveAxisForward > 0;
            characterInputs.CameraRotation = Quaternion.Euler(0, YawPitch.x, 0);

            characterInputs.JumpDown = input.JumpDown;

            if (!Crouching && input.CouchInput)
                characterInputs.CrouchDown = true;
            if (Crouching && !input.CouchInput)
                characterInputs.CrouchUp = true;

            Crouching = input.CouchInput;

            _locomotion.SetInputs(ref characterInputs);

            if (IsOwner || IsServerInitialized)
            {
                Simulate();
                BaseVelocity = _motor.BaseVelocity;

                KCCState = KCCStateToNetworkState(_motor.GetState(), (int)input.GetTick());
            }

            if (SnapshotBuffer == null)
                InitSnapshotBuffer();
            ReconcileData rd = new ReconcileData(transform.position, BaseVelocity, KCCState, YawPitch, Crouching);
            int SnapShotIndex = (int)input.GetTick() % SnapshotBuffer.Length;
            SnapshotBuffer[SnapShotIndex] = rd;
            _latestSimulatedTick = (int)input.GetTick();
            _lastTickTime = Time.time;
        }

        public void Simulate()
        {
            _motor.UpdatePhase1((float)base.TimeManager.TickDelta);
            _motor.UpdatePhase2((float)base.TimeManager.TickDelta);
            _motor.Transform.SetPositionAndRotation(_motor.TransientPosition, _motor.TransientRotation);
        }

        private KCCNetworkState KCCStateToNetworkState(KinematicCharacterMotorState state, int tick)
        {
            KCCNetworkState kccNetState = new KCCNetworkState();

            transform.position = state.Position;
            transform.rotation = state.Rotation;
            BaseVelocity = state.BaseVelocity;

            kccNetState.MustUnground = state.MustUnground;
            kccNetState.MustUngroundTime = state.MustUngroundTime;
            kccNetState.LastMovementIterationFoundAnyGround = state.LastMovementIterationFoundAnyGround;

            kccNetState.FoundAnyGround = state.GroundingStatus.FoundAnyGround;
            kccNetState.IsStableOnGround = state.GroundingStatus.IsStableOnGround;
            kccNetState.SnappingPrevented = state.GroundingStatus.SnappingPrevented;
            kccNetState.GroundNormal = state.GroundingStatus.GroundNormal;
            kccNetState.InnerGroundNormal = state.GroundingStatus.InnerGroundNormal;
            kccNetState.OuterGroundNormal = state.GroundingStatus.OuterGroundNormal;

            if (AdditionalStateInfoBuffer == null)
                InitInfoBuffer();
            int bufferPosition = tick % AdditionalStateInfoBuffer.Length;
            AdditionalStateInfoBuffer[bufferPosition].AttachedRigidbody = state.AttachedRigidbody;
            AdditionalStateInfoBuffer[bufferPosition].AttachedRigidbodyVelocity = state.AttachedRigidbodyVelocity;
            _locomotion.GetLocomotionState(AdditionalStateInfoBuffer[bufferPosition]);

            return kccNetState;
        }

        private KinematicCharacterMotorState NetworkStateToKCCState(KCCNetworkState kccNetState, int tick)
        {
            KinematicCharacterMotorState kccState = new KinematicCharacterMotorState();

            kccState.Position = transform.position;
            kccState.Rotation = transform.rotation;
            kccState.BaseVelocity = BaseVelocity;

            kccState.MustUnground = kccNetState.MustUnground;
            kccState.MustUngroundTime = kccNetState.MustUngroundTime;
            kccState.LastMovementIterationFoundAnyGround = kccNetState.LastMovementIterationFoundAnyGround;

            kccState.GroundingStatus = new CharacterTransientGroundingReport()
            {
                FoundAnyGround = kccNetState.FoundAnyGround,
                IsStableOnGround = kccNetState.IsStableOnGround,
                SnappingPrevented = kccNetState.SnappingPrevented,
                GroundNormal = kccNetState.GroundNormal,
                InnerGroundNormal = kccNetState.InnerGroundNormal,
                OuterGroundNormal = kccNetState.OuterGroundNormal
            };

            if (AdditionalStateInfoBuffer == null)
                InitInfoBuffer();
            int bufferPosition = tick % AdditionalStateInfoBuffer.Length;
            kccState.AttachedRigidbody = AdditionalStateInfoBuffer[bufferPosition].AttachedRigidbody;
            kccState.AttachedRigidbodyVelocity = AdditionalStateInfoBuffer[bufferPosition].AttachedRigidbodyVelocity;
            _locomotion.SetLocomotionState(AdditionalStateInfoBuffer[bufferPosition]);

            return kccState;
        }

        private Vector2 ClampAngles(Vector2 _yawPitch)
        {
            _yawPitch.x = ClampAngle(_yawPitch.x, -360, 360);
            _yawPitch.y = ClampAngle(_yawPitch.y, -90, 90);
            return _yawPitch;
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle <= -360F)
                angle += 360F;
            if (angle >= 360F)
                angle -= 360F;
            return Mathf.Clamp(angle, min, max);
        }
    }
}