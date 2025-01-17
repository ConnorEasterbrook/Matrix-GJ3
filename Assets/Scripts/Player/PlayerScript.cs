using PlayerControllers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerScript : MonoBehaviour
{
    // Basic Variables
    private Camera playerCamera;
    private Rigidbody playerRigidbody;
    private GameObject playerChild; // Intended to select an empty gameobject child of the player that contains the main camera. Player > child > camera

    [Header("Camera Movement")]
    public float mouseSensitivity = 10.0f;
    [Tooltip("Length of camera movement smoothing. Lower values = sharper stops. 0.1f offers a realistic feel.")]
    [Range(0.0f, 0.4f)] public float cameraRotationSmoothTime = 0.1f;
    [Tooltip("Control the camera tilt range. X = Up. Y = Down. +-40 = A good range.")]
    public Vector2 cameraTiltRange = new Vector2(-40.0f, 40.0f); // Control how far player can look (up, down)
    private float cameraPan; // Looking left and right
    private float cameraPanSmooth; // For smoothing the pan movement
    private float cameraPanSmoothVelocity; // Pan smoothing speed
    private float cameraTilt; // Looking up and down
    private float cameraTiltSmooth; // For smoothing the tilt movement
    private float cameraTiltSmoothVelocity; // Tilt smoothing speed
    private Quaternion panRotation; // For rotating the camera left and right
    private Vector3 oldCameraPos;

    [Header("Player Movement")]
    [Tooltip("Walking speed. 5.0f feels good for shooter-like movement.")]
    [Range(2, 8)] public float walkSpeed = 5.0f;
    [Tooltip("Smooths player movement. Lower values = sharper stops. 0.1f feels cinematic.")]
    [Range(0.0f, 0.4f)]
    public float movementSmoothTime = 0.1f;
    [Tooltip("Jump height. 7.5f feels good for arcade-like jumping (10.0f gravity). 10.0 for realistic jumping (20.0f gravity)")]
    public float jumpForce = 8.0f;
    [Tooltip("Amount of gravity. 10.0f feels good for arcade-like gravity. 20.0f for realistic gravity.")]
    public float gravityForce = 4.0f;
    public float fallingVelocity = 0.0f; // Keep track of falling speed
    public Vector3 velocity;
    private Vector3 currentVelocity;

    // COLLISION
    private Collider playerCollider;
    private float yCollisionBounds = 0.0f; // Variable used in raycast to check if grounded
    private float lastGroundedTime = 0.0f; // Keep track of when last grounded

    [Header("Misc")]
    public bool noLockCursor = false;
    public bool disablePlayer = false;
    public static bool playerDisabled = false;

    // Start is called before the first frame update
    void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        playerRigidbody = GetComponent<Rigidbody>();
        playerChild = transform.GetChild(0).gameObject;

        if (!noLockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        playerCollider = GetComponent<Collider>();
        yCollisionBounds = playerCollider.bounds.extents.y;

        oldCameraPos = playerCamera.transform.localPosition;
    }

    void Update()
    {
        playerDisabled = disablePlayer;

        if (disablePlayer || PauseMenu.GameIsPaused)
        {
            return;
        }

        CalculateCameraMovement();
    }

    void FixedUpdate()
    {
        if (disablePlayer)
        {
            return;
        }

        transform.localRotation = panRotation;
        playerCamera.transform.localEulerAngles = Vector3.right * cameraTiltSmooth;

        FixedGravity();
        CalculatePlayerMovement();

        Vector3 localMove = transform.TransformDirection(velocity); // Final calculation
        playerRigidbody.MovePosition(playerRigidbody.position + localMove * Time.fixedDeltaTime);
    }

    private void CalculateCameraMovement()
    {
        // Get mouse movement
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        // Stop camera from swinging down on game start
        float mouseMagnitude = Mathf.Sqrt(mouseX * mouseX + mouseY * mouseY);
        if (mouseMagnitude > 5)
        {
            mouseX = 0;
            mouseY = 0;
        }

        cameraPan += mouseX * (mouseSensitivity * 100) * Time.deltaTime; // Move camera left & right
        cameraTilt -= mouseY * (mouseSensitivity * 100) * Time.deltaTime; // Move camera up & down

        // Clamp the camera pitch so that the there is a limit when looking up & down
        cameraTilt = Mathf.Clamp(cameraTilt, cameraTiltRange.x, cameraTiltRange.y);

        // Smooth camera movement
        cameraTiltSmooth = Mathf.SmoothDampAngle
        (
            cameraTiltSmooth,
            cameraTilt,
            ref cameraTiltSmoothVelocity,
            cameraRotationSmoothTime
        );
        cameraPanSmooth = Mathf.SmoothDampAngle
        (
            cameraPanSmooth,
            cameraPan,
            ref cameraPanSmoothVelocity,
            cameraRotationSmoothTime
        );

        // Get cameraPanSmooth float to work with rigidbody rotation by making it a Quaternion
        panRotation = Quaternion.Euler(0.0f, cameraPanSmooth, 0.0f);
    }

    private void CalculatePlayerMovement()
    {
        // Create a new Vector2 variable that takes in our movement inputs
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // Normalize the Vector2 input variable and make it a Vector3. Then transform the input to move in world space.
        Vector3 inputDirection = new Vector3(input.x, 0, input.y).normalized;

        // Get and convert the direction of childObject for correct movement direction
        float facingDirection = playerChild.transform.localEulerAngles.y;
        Quaternion facingDirectionEuler = Quaternion.Euler(0.0f, facingDirection, 0.0f);

        // Create a new Vector3 that takes in our world movement and current speed to then use in a movement smoothing calculation
        Vector3 targetVelocity = facingDirectionEuler * inputDirection * walkSpeed;
        velocity = Vector3.SmoothDamp(velocity, targetVelocity, ref currentVelocity, movementSmoothTime); // ref currentVelocity because function needs to set a currentVelocity

        // Establish falling speed. Increase as the falling duration grows
        fallingVelocity -= gravityForce * Time.deltaTime;

        // Set velocity to match the recorded movement from previous movement sections
        velocity = new Vector3(velocity.x, fallingVelocity, velocity.z);
    }

    private void FixedGravity()
    {
        playerRigidbody.AddForce(-transform.up * playerRigidbody.mass * gravityForce);

        // Establish falling speed. Increase as the falling duration grows
        fallingVelocity -= gravityForce * Time.deltaTime;

        // Check for jump input and if true, check that the character isn't jumping or falling. Then jump
        if (Input.GetKey(KeyCode.Space) && CheckGrounded())
        {
            fallingVelocity = jumpForce;
        }
        else if (CheckGrounded()) // If there is collision below the player (ground)
        {
            lastGroundedTime = Time.time; // Set lastGroundedTime to the current time
            fallingVelocity = 0; // Stop fallingVelocity
        }
    }

    private bool CheckGrounded()
    {
        return Physics.Raycast(transform.position, -transform.up, yCollisionBounds);
    }
}


