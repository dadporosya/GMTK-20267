using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class Movement : MonoBehaviour
{

    [Header("movement")]
    public bool canMove;
    [SerializeField] float walkSpeed;
    [SerializeField] float gravity;
    [SerializeField] float smoothing;

    [Header("footstep sounds")]

    [SerializeField] bool stepSoundsEnabled;
    [SerializeField] float magnitudeThreshold;
    [SerializeField] Vector2 stepSoundsDelayRange;
    [SerializeField] Vector2 soundPitchRange;
    [SerializeField] AudioSource stepSoundsSource;

    private float savedWalkspeed;
    private CharacterController controller;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        savedWalkspeed = walkSpeed;
    }

    private void Start()
    {
        StartCoroutine(FootstepSoundsCycle());
    }

    private void Update()
    {
        if (!canMove)
        {
            return;
        }

        float horMovement = Input.GetAxis("Horizontal");
        float verMovement = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horMovement * walkSpeed, gravity, verMovement * walkSpeed);
        movement = Vector3.ClampMagnitude(movement, walkSpeed);
        movement *= Time.deltaTime;
        movement = transform.TransformDirection(movement);

        controller.Move(movement);
    }

    public void MoveTo(Transform to)
    {
        transform.position = to.position;
    }

    public void EnableMovement(bool active)
    {
        canMove = active;
        if (active)
        {
            walkSpeed = savedWalkspeed;
        }
        else
        {
            walkSpeed = 0;
        }
    }

    private IEnumerator FootstepSoundsCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(stepSoundsDelayRange.x, stepSoundsDelayRange.y));
            if (controller.velocity.magnitude > magnitudeThreshold && stepSoundsEnabled && canMove)
            {
                if (Physics.Raycast(transform.position, -transform.up, out RaycastHit ground))
                {
                    if (ground.transform.gameObject.TryGetComponent<SurfaceSound>(out SurfaceSound surface))
                    {
                        stepSoundsSource.pitch = Random.Range(soundPitchRange.x, soundPitchRange.y);
                        stepSoundsSource.PlayOneShot(surface.GetSound());
                    }
                }
            }
        }
    }
}
