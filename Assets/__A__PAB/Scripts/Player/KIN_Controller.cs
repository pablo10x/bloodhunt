using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;
using KinematicCharacterController.Examples;
using UnityEngine.UI;
using Mirror;

public struct PlayerCharacterInputs
{
    public float MoveAxisForward;
    public float MoveAxisRight;
    public Quaternion CameraRotation;
    public bool JumpDown;
}

public class KIN_Controller : NetworkBehaviour, ICharacterController 
{
    public KinematicCharacterMotor Motor;

    [Header("Testing Variables")] public bool Player_Rotate = false;
    [Header("Stable Movement")] public float MaxStableMoveSpeed = 10f;
    public float StableMovementSharpness = 15;
    public float OrientationSharpness = 10;

    [Header("Air Movement")] public float MaxAirMoveSpeed = 10f;
    public float AirAccelerationSpeed = 5f;
    public float Drag = 0.1f;

    [Header("Jumping")] public bool AllowJumpingWhenSliding = false;
    public float JumpSpeed = 10f;
    public float JumpPreGroundingGraceTime = 0f;
    public float JumpPostGroundingGraceTime = 0f;
    public GameObject _Jump_Botton;
    private bool _isjump = false;

    [Header("Misc")] public bool RotationObstruction;
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    public Transform MeshRoot;
    private Joystick _Joystick;
    private int rightFingerId;
    private float halfScreenWidth;

    [Header("Camera")] 
    public ExampleCharacterCamera OrbitCamera;
    public Transform CameraFollowPoint;
    public float cameraSensitivity = 5f;
    public float ZoomInput = 0;

    [Space] [Header("Vehicle")] public RCC_CarControllerV3 _Vehicle = null;
    public bool IS_Driving = false;
    public Button _UICarEnter;
    public Button _UICarExit;
    public GameObject UI_Vehicle_Panel;
    public GameObject UI_Player_Panel;
    private Transform _Exitlocation;

    [Header("Animation")] public Animator _Animator;

    private Vector3 _moveInputVector;

    private Vector3 _lookInputVector;

    // jumping ---------------
    private bool m_jumpRequested = false;
    private bool m_jumpConsumed = false;
    private bool m_jumpedThisFrame = false;
    private float m_timeSinceJumpRequested = Mathf.Infinity;
    private float m_timeSinceLastAbleToJump = 0f;

    private void Start()
    {
        // assignement
        //joystick
        _Joystick = FindObjectOfType<Joystick>();
        if(!_Joystick) Debug.LogWarning("Joystick Not found");
        _Joystick.gameObject.GetComponent<Image>().enabled = true;
        _Joystick.gameObject.GetComponent<FixedJoystick>().enabled = true;
        OrbitCamera = FindObjectOfType<ExampleCharacterCamera>();
        if(!OrbitCamera) Debug.LogWarning("ORBIT Not found");
        // jump button
        _Jump_Botton = GameObject.Find("JUMP_BUTTON");
        _Jump_Botton.GetComponent<Button>().enabled = true;
        _Jump_Botton.GetComponent<Image>().enabled = true;
        if(!_Jump_Botton) Debug.LogWarning("jump button Not found");
        // Assign to motor
        Motor.CharacterController = this;
        // Camera
        OrbitCamera.SetFollowTransform(CameraFollowPoint);
        OrbitCamera.IgnoredColliders.Clear();
        OrbitCamera.IgnoredColliders.AddRange(gameObject.GetComponentsInChildren<Collider>());
       
        
        //_UICarEnter.gameObject.SetActive(false);
        //_UICarExit.gameObject.SetActive(false);
        //_UICarExit.onClick.AddListener(OnExitCar);
        //_UICarEnter.onClick.AddListener(OnEnterCarCalled);
        //UI_Vehicle_Panel.SetActive(false);
        //Screen Touch 
        halfScreenWidth = Screen.width / 2;
        
        _Jump_Botton.GetComponent<Button>().onClick.AddListener(OnPressJump);
    }

    private void OnExitCar()
    {
        if (!IS_Driving) return;
        UI_Player_Panel.SetActive(true);
        UI_Vehicle_Panel.SetActive(false);
        
        OrbitCamera.gameObject.SetActive(true);
        Renderer[] rend;
        rend = gameObject.GetComponentsInChildren<Renderer>();
        foreach (var ran in rend)
        {
            ran.enabled = true;
        }

        _Vehicle.canControl = false;
        _Vehicle.KillEngine();
        gameObject.transform.parent = null;
        _UICarExit.gameObject.SetActive(false);
        IS_Driving = false;
        transform.position = _Exitlocation.position;
        gameObject.GetComponent<KinematicCharacterMotor>().SetPosition(transform.position, true);
        gameObject.GetComponent<KinematicCharacterMotor>().enabled = true;
    }
    
    private void OnEnterCarCalled()
    {
        if (IS_Driving) return;
        OrbitCamera.gameObject.SetActive(false);
       
        RCC_Settings.Instance.controllerType = RCC_Settings.ControllerType.Mobile;
        _Vehicle.canControl = true;
        UI_Player_Panel.SetActive(false);
        UI_Vehicle_Panel.SetActive(true);
        _Exitlocation = _Vehicle.gameObject.transform;
        gameObject.transform.parent = _Vehicle.gameObject.transform;
        Renderer[] rend;
        rend = gameObject.GetComponentsInChildren<Renderer>();
        foreach (var ran in rend)
        {
            ran.enabled = false;
        }

        gameObject.GetComponent<KinematicCharacterMotor>().enabled = false;
        _UICarExit.gameObject.SetActive(true);
        //gameObject.GetComponent<KinematicCharacterMotor>().SetPosition(_Vehicle.gameObject.transform.position,true);
        if (!_Vehicle.engineRunning) _Vehicle.StartEngine();
        IS_Driving = true;
    }

    public void OnPressJump()
    {
        if (IS_Driving) return;
        if (isLocalPlayer)
        {
            m_timeSinceJumpRequested = 0f;
            m_jumpRequested = true;
        }else
            Debug.LogError("Not local player");
        


        //var t = _Animator.GetCurrentAnimatorClipInfo();
    }


    private void Update()
    {
        if (!IS_Driving)
        {
            if (isLocalPlayer)
            {
                HandleCharacterInput();
                HandleCameraInputMobile();
            }else Debug.LogError("Not Local pllayer");
           
        }
    }

    private void HandleCameraInputMobile()
    {
        OrbitCamera.UpdateWithInput(Time.deltaTime, ZoomInput, GetTouchInput());
    }

    private void HandleCharacterInput()
    {
        //tes
        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();
        ManageJoystickMovement(_Joystick.Vertical, _Joystick.Horizontal, ref characterInputs);
        characterInputs.CameraRotation = OrbitCamera.Transform.rotation;
        this.SetInputs(ref characterInputs);
    }

    private void ManageJoystickMovement(float forward, float side, ref PlayerCharacterInputs input)
    {
        forward = _Joystick.Vertical;
        side = _Joystick.Horizontal;
        /*if (forward >= 0.3f || forward <= -0.3f || side >= 0.3f || side <= -0.3f)
        {
            Debug.Log("Input accepted");
            _Animator.SetFloat("ver", forward);
            _Animator.SetFloat("hor", side);
            input.MoveAxisForward = forward;
            input.MoveAxisRight = side;
            this.MaxStableMoveSpeed = 4;
            //cal speed
            if (forward >= 0.6f || forward <= -0.6)
            {
                //running
                this.MaxStableMoveSpeed = 8;
            }
        }
        else
        {
            _Animator.SetFloat("ver", 0);
            _Animator.SetFloat("hor", 0);
            input.MoveAxisForward = 0;
            input.MoveAxisRight = 0;
        }*/
        _Animator.SetFloat("ver", forward);
        _Animator.SetFloat("hor", side);
        input.MoveAxisForward = forward;
        input.MoveAxisRight = side;
        //TODO: more tweaking
    }

    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        // Clamp input
        Vector3 moveInputVector =
            Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

        // Calculate camera direction and rotation on the character plane
        Vector3 cameraPlanarDirection =
            Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection =
                Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
        }

        //var t;

        Quaternion cameraPlanarRotation;

        cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);


        // Move and look inputs
        _moveInputVector = cameraPlanarRotation * moveInputVector;
        _lookInputVector = cameraPlanarDirection;

        // Jump
        /*if (inputs.JumpDown)
        {
            _timeSinceJumpRequested = 0f;
            _jumpRequested = true;
        }*/
        
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called before the character begins its movement update
    /// </summary>
    public void BeforeCharacterUpdate(float deltaTime)
    {
    }


    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (IS_Driving) return;
        if (!Player_Rotate)
        {
            if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
            {
                // Smoothly interpolate from current to target look direction
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector,
                    1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                // Set the current rotation (which will be used by the KinematicCharacterMotor)
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }
        }
        else
        {
            if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
            {
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _moveInputVector,
                    1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;


                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Vehicle"))
        {
            _UICarEnter.gameObject.SetActive(true);
            _Vehicle = other.gameObject.GetComponentInParent<RCC_CarControllerV3>();
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Vehicle"))
        {
            _UICarEnter.gameObject.SetActive(false);
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (IS_Driving) return;
        Vector3 targetMovementVelocity = Vector3.zero;
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            // Reorient source velocity on current ground slope (this is because we don't want our smoothing to cause any velocity losses in slope changes)
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, Motor.GroundingStatus.GroundNormal) *
                              currentVelocity.magnitude;

            // Calculate target velocity
            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal, inputRight).normalized *
                                      _moveInputVector.magnitude;
            targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

            // Smooth movement Velocity
            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity,
                1 - Mathf.Exp(-StableMovementSharpness * deltaTime));
        }
        else
        {
            // Add move input
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;

                // Prevent climbing on un-stable slopes with air movement
                if (Motor.GroundingStatus.FoundAnyGround)
                {
                    Vector3 perpenticularObstructionNormal = Vector3
                        .Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp)
                        .normalized;
                    targetMovementVelocity =
                        Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                }

                Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, Gravity);
                currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
            }

            // Gravity
            currentVelocity += Gravity * deltaTime;

            // Drag
            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
        }

        // handle jumping
        m_jumpedThisFrame = false;
        m_timeSinceJumpRequested += deltaTime;
        if (m_jumpRequested)
        {
            // See if we actually are allowed to jump
            if (!m_jumpConsumed &&
                ((AllowJumpingWhenSliding
                     ? Motor.GroundingStatus.FoundAnyGround
                     : Motor.GroundingStatus.IsStableOnGround) ||
                 m_timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
            {
                // Calculate jump direction before ungrounding
                Vector3 jumpDirection = Motor.CharacterUp;
                if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                {
                    jumpDirection = Motor.GroundingStatus.GroundNormal;
                }

                // Makes the character skip ground probing/snapping on its next update. 
                // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                Motor.ForceUnground(0.1f);

                // Add to the return velocity and reset jump state
                currentVelocity += (jumpDirection * JumpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                m_jumpRequested = false;
                m_jumpConsumed = true;
                m_jumpedThisFrame = true;
            }
        }
    }


    public void AfterCharacterUpdate(float deltaTime)
    {
        if (IS_Driving) return;
        // Handle jump-related values
        {
            // Handle jumping pre-ground grace period
            if (m_jumpRequested && m_timeSinceJumpRequested > JumpPreGroundingGraceTime)
            {
                m_jumpRequested = false;
                Debug.Log("Request is off");
            }

            // Handle jumping while sliding
            if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
            {
                // If we're on a ground surface, reset jumping values
                if (!m_jumpedThisFrame)
                {
                    m_jumpConsumed = false;
                }

                m_timeSinceLastAbleToJump = 0f;
            }
            else
            {
                // Keep track of time since we were last able to jump (for grace period)
                m_timeSinceLastAbleToJump += deltaTime;
            }
        }
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
    }

    public void PostGroundingUpdate(float deltaTime)
    {
    }

    public void AddVelocity(Vector3 velocity)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    private Vector2 GetTouchInput()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.position.x < halfScreenWidth) return Vector2.zero;
            switch (t.phase)
            {
                case TouchPhase.Began:

                    if (t.position.x > halfScreenWidth && rightFingerId == -1)

                    {
                        rightFingerId = t.fingerId;
                    }

                    break;
                case TouchPhase.Ended:
                {
                    return Vector2.zero;
                }
                case TouchPhase.Canceled:


                    if (t.fingerId == rightFingerId)
                    {
                        rightFingerId = -1;
                    }

                    break;
                case TouchPhase.Moved:

                    // Get input for looking around
                    if (t.fingerId == rightFingerId)
                    {
                        return t.deltaPosition * cameraSensitivity * Time.deltaTime;
                    }


                    break;
                case TouchPhase.Stationary:
                    // Set the look input to zero if the finger is still
                    if (t.fingerId == rightFingerId)
                    {
                        return Vector2.zero;
                    }

                    break;
            }
        }

        return Vector2.zero;
    }
}