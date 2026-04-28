using System;
using System.Linq;
using UnityEngine;
using Utilities;

[System.Serializable]
public class AxleInfo {
    public WheelCollider leftWheel;
    public WheelCollider rightWheel;
    public bool motor;
    public bool steering;
    public WheelFrictionCurve originalForwardFriction;
    public WheelFrictionCurve originalSidewaysFriction;
}

public class KartController : MonoBehaviour
{
    [Header("AxleInformation")]
    [SerializeField] AxleInfo[] _axleInfos;

    [Header("Motor Attributes")]
    [SerializeField] float _maxMotorTorque = 3000f;
    [SerializeField] float _maxSpeed;

    [Header("Steering Attributes")]
    [SerializeField] float _maxSteeringAngle = 30f;
    [SerializeField] AnimationCurve _turnCurve;
    [SerializeField] float _turnStrength = 1500f;

    [Header("Braking and Drifting Attributes")]
    [SerializeField] float _brakeTorque = 10000f;
    [SerializeField] float _driftSteerMultiplier = 1.5f; // Change in steering during a drift

    [Header("Physics")]
    [SerializeField] Transform _centerOfMass;
    [SerializeField] float _downForce = 100f;
    [SerializeField] float _gravity = Physics.gravity.y;
    [SerializeField] float _lateralGScale = 10f; // Scaling factor for lateral G forces;

    [Header("Banking")]
    [SerializeField] float _maxBankAngle = 5f;
    [SerializeField] float _bankSpeed = 2f;

    [Header("Refs")]
    [SerializeField] InputReader _input;
    Rigidbody _rb;

    Vector3 _kartVelocity;
    float _driftVelocity;
    float _brakeVelocity;

    RaycastHit hit;

    const float _thresholdSpeed = 10f;
    const float _centerOfMassOffset = -0.5f;
    Vector3 _originalCenterOfMass;

    public bool IsGrounded = true;
    public Vector3 Velocity => _kartVelocity;
    public float MaxSpeed => _maxSpeed;


    private void Start() {
        _rb = GetComponent<Rigidbody>();
        _input.Enable();

        _rb.centerOfMass = _centerOfMass.localPosition;
        _originalCenterOfMass = _centerOfMass.localPosition;

        foreach(var axleInfo in _axleInfos) {
            axleInfo.originalForwardFriction = axleInfo.leftWheel.forwardFriction;
            axleInfo.originalSidewaysFriction = axleInfo.leftWheel.sidewaysFriction;
        }
    }



    private void FixedUpdate() {
        Vector2 vectorInput = _input.Move.normalized;
        float verticalInput = vectorInput.y;
        float horizontalInput = vectorInput.x;

        float motor = _maxMotorTorque * verticalInput;
        float steering = _maxSteeringAngle * horizontalInput;

        UpdateAxles(motor, steering);
        UpdateBanking(horizontalInput);

        _kartVelocity = transform.InverseTransformDirection(_rb.linearVelocity);

        if (IsGrounded) {
            HandleGroundedMovement(verticalInput, horizontalInput);
        } else {
            HandleAirborneMovement(verticalInput, horizontalInput);
        }
    }

    private void HandleGroundedMovement(float verticalInput, float horizontalInput) {
        //Turn logic
        if(Mathf.Abs(verticalInput) > 0.1f ||  Mathf.Abs(_kartVelocity.z) > 1) {
            float turnMultiplier = Mathf.Clamp01(_turnCurve.Evaluate(_kartVelocity.magnitude / _maxSpeed));
            _rb.AddTorque(Vector3.up * horizontalInput * Mathf.Sign(_kartVelocity.z) * _turnStrength * 100f * turnMultiplier);
        }

        //Acceleration Logic
        if (!_input.IsBraking) {
            float targetSpeed = verticalInput * _maxSpeed;
            Vector3 forwardWithoutY = transform.forward.With(y: 0).normalized;
            _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, forwardWithoutY * targetSpeed, Time.deltaTime);
        }

        //Downforce - always push the cart down
        float speedFactor = Mathf.Clamp01(_rb.linearVelocity.magnitude / _maxSpeed);
        float lateralG = Mathf.Abs(Vector3.Dot(_rb.linearVelocity, transform.right));
        float downForceFactor = Mathf.Max(speedFactor, lateralG / _lateralGScale);
        _rb.AddForce(-transform.up * _downForce * _rb.mass * downForceFactor);

        //Shift Center of Mass
        float speed = _rb.linearVelocity.magnitude;
        Vector3 centerOfMassAdjustment = (speed > _thresholdSpeed)
            ? new Vector3(0, 0, Mathf.Abs(verticalInput) > 0.1f ? Mathf.Sign(verticalInput) * _centerOfMassOffset : 0f)
            : Vector3.zero;

        _rb.centerOfMass = _originalCenterOfMass + centerOfMassAdjustment;
    }

    private void UpdateBanking(float horizontalInput) {
        //Bank the kart in the opposite direction of the turn
        float targetBankAngle = horizontalInput * -_maxBankAngle;
        Vector3 currentEuler = transform.localEulerAngles;
        currentEuler.z = Mathf.LerpAngle(currentEuler.z, targetBankAngle, Time.deltaTime * _bankSpeed);
        transform.localEulerAngles = currentEuler;
    }

    private void HandleAirborneMovement(float verticalInput, float horizontalInput) {
        //Apply gravity to the Kart While its airborne
        _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, _rb.linearVelocity + Vector3.down * _gravity, Time.deltaTime * _gravity);
    }

    private void UpdateAxles(float motor, float steering) {
       foreach(AxleInfo axleInfo in _axleInfos) {
            HandleSteering(steering, axleInfo);
            HandleMotor(motor, axleInfo);
            HandleBrakesAndDrift(axleInfo);
            UpdateWheelVisuals(axleInfo.leftWheel);
            UpdateWheelVisuals(axleInfo.rightWheel);
        }
    }
    private void HandleMotor(float motor, AxleInfo axleInfo) {
        if (axleInfo.motor) {
            axleInfo.leftWheel.motorTorque = motor;
            axleInfo.rightWheel.motorTorque = motor;
        }
    }
    private void HandleSteering(float steering, AxleInfo axleInfo) {
        if (axleInfo.steering) {
            axleInfo.leftWheel.steerAngle = steering;
            axleInfo.rightWheel.steerAngle = steering;
        }
    }
    private void HandleBrakesAndDrift(AxleInfo axleInfo) {
        if (axleInfo.motor) {
            if (_input.IsBraking) {
                _rb.constraints = RigidbodyConstraints.FreezeRotationX;

                float newZ = Mathf.SmoothDamp(_rb.linearVelocity.z, 0, ref _brakeVelocity, 1f);
                _rb.linearVelocity = _rb.linearVelocity.With(z: newZ);

                axleInfo.leftWheel.brakeTorque = _brakeTorque;
                axleInfo.rightWheel.brakeTorque = _brakeTorque;
                ApplyDriftFriction(axleInfo.leftWheel);
                ApplyDriftFriction(axleInfo.rightWheel);
            } else {
                _rb.constraints = RigidbodyConstraints.None;

                axleInfo.leftWheel.brakeTorque = 0f;
                axleInfo.rightWheel.brakeTorque = 0f;
                ResetDriftFriction(axleInfo.leftWheel);
                ResetDriftFriction(axleInfo.rightWheel);

            }
        }
    }

    private void ResetDriftFriction(WheelCollider wheel) {
        AxleInfo axleInfo = _axleInfos.FirstOrDefault(axle => axle.leftWheel == wheel || axle.rightWheel == wheel);
        if (axleInfo == null) return;

        wheel.forwardFriction = axleInfo.originalForwardFriction;
        wheel.sidewaysFriction = axleInfo.originalSidewaysFriction;
    }

    private void ApplyDriftFriction(WheelCollider wheel) {
        if(wheel.GetGroundHit(out var hit)) {
            wheel.forwardFriction = UpdateFriction(wheel.forwardFriction);
            wheel.sidewaysFriction = UpdateFriction(wheel.sidewaysFriction);
            IsGrounded = true;
        }
    }

    private WheelFrictionCurve UpdateFriction(WheelFrictionCurve friction) {
        friction.stiffness = _input.IsBraking ? Mathf.SmoothDamp(friction.stiffness, .5f, ref _driftVelocity, Time.deltaTime * 2f) : 1f;
        return friction;
    }

    private void UpdateWheelVisuals(WheelCollider collider) {
        if (collider.transform.childCount == 0) return;

        Transform wheelVisual = collider.transform.GetChild(0);

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        wheelVisual.position = position;
        wheelVisual.rotation = rotation;
    }

}
