﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace DM
{
    public class ControlManager : NetworkBehaviour
    {
        [Header("Initialize")]
        public GameObject activeModel;  // defines the current active model.
                                        // public string[] randomAttacks;  // array of normal attacks in string.


        [Header("Inputs")]
        public float vertical;  // stores vertical input.
        public float horizontal; // stores horizontal input.
        public float moveAmount;    //shows the amount of movement from 0 to 1.
        public Vector3 moveDir;     //stores the moving vector value of main character.

        [Header("Stats")]
        public float moveSpeed = 3.5f;  //speed of running
        public float sprintSpeed = 5f;  //speed of sprinting(double time of running)
        public float rotateSpeed = 5;   //speed of character's turning around
        public float jumpForce = 600f;  //how high you can jump value.


        [Header("States")]
        public bool onGround;   //shows you are on ground or not.
        public bool sprint;     //shows you are sprinting or not.
        [HideInInspector] public bool jump;       //stores whether you jump or not
        public bool canMove;    //shows you can move or not
        bool isStationary;
        [HideInInspector] public bool roll;       //stores whether you roll or not

        float fixedDelta;        //stores Time.fixedDeltaTime
        float delta;
        Animator anim;      //for caching Animator component
        [HideInInspector] public Rigidbody rigid;     //for caching Rigidbody component
        CameraManager camManager;   //for caching CameraManager script
        AudioManager audioManager;
        PickUpThrow pickUpThrow;   //for caching CameraManager script


        void Start()
        {
            SetupAnimator();
            rigid = GetComponent<Rigidbody>();

            if (!isLocalPlayer)
                return;

            camManager = CameraManager.singleton;
            camManager.Init(this.transform);

            pickUpThrow = gameObject.GetComponent<PickUpThrow>();
            audioManager = GameObject.Find("AudioManager").GetComponent<AudioManager>();
        }

        void SetupAnimator()
        {
            if (!activeModel)
            {
                anim = GetComponentInChildren<Animator>();//Find animator component in the children hierarchy.
                if (!anim)
                    Debug.Log("No model");
                else
                    activeModel = anim.gameObject; //save this gameobject as active model.
            }

            if (!anim)
                anim = activeModel.GetComponent<Animator>();
        }

        void FixedUpdate() //Since this is physics based controller, you have to use FixedUpdate.
        {
            if (!isLocalPlayer)
                return;

            fixedDelta = Time.fixedDeltaTime;    //storing the last frame updated time.             

            FixedTick(fixedDelta);   //update anything related to character moving.
            camManager.FixedTick(fixedDelta);     //update anything related to camera moving.       
        }

        void Update()
        {
            if (!isLocalPlayer)
                return;

            GetInput();     //getting control input from keyboard or joypad
            UpdateStates();   //Updating anything related to character's actions.

            if (onGround && !roll && sprint)
                CmdPlayFootstepSound();
        }

        [Command]
        void CmdPlayFootstepSound() => RpcPlayFootstepSound();

        [ClientRpc]
        void RpcPlayFootstepSound()
        {
            isStationary = vertical == 0 && horizontal == 0;
            if (!isStationary && audioManager.footstep && !(audioManager.footstep.isPlaying))
                audioManager.footstep.Play();
        }

        void GetInput() //getting various inputs from keyboard or joypad.
        {
            vertical = Input.GetAxis("Vertical");    //for getting vertical input.
            horizontal = Input.GetAxis("Horizontal");    //for getting horizontal input.
            sprint = Input.GetButton("SprintInput");     //for getting sprint input.
            jump = Input.GetButtonDown("Jump");      //for getting jump input.
            roll = Input.GetButtonDown("RollInput");     //for getting roll input.
        }


        void UpdateStates() //updates character's various actions.
        {
            canMove = anim.GetBool("canMove");   //getting bool value from Animator's parameter named "canMove".          

            if (jump)   //I clicked jump, left mouse button or B key in the joypad.
            {
                if (onGround && canMove) //do jump only when you are on ground and you can move.
                {
                    anim.CrossFade("falling", 0.1f); //play "falling" animation in 0.1 second as cross fade method.
                    rigid.AddForce(0, jumpForce, 0);  //Adding force to Y axis for jumping up.                  
                }
            }

            if (roll && onGround && !pickUpThrow.isPicker)    //I clicked for roll. middle mouse button or Y key in the joypad.
                GetComponent<NetworkAnimator>().SetTrigger("roll");

            float targetSpeed = moveSpeed;  //set run speed as target speed.

            if (sprint)
                targetSpeed = sprintSpeed;    //set sprint speed as target speed.            

            //mixing camera rotation value to the character moving value.
            Vector3 v = vertical * camManager.transform.forward;
            Vector3 h = horizontal * camManager.transform.right;

            //multiplying target speed and move amount.
            moveDir = ((v + h).normalized) * (targetSpeed * moveAmount);

            //This is for isolating y velocity from the character control. 
            moveDir.y = rigid.velocity.y;

            //This is for limiting values from 0 to 1.
            float m = Mathf.Abs(horizontal) + Mathf.Abs(vertical);
            moveAmount = Mathf.Clamp01(m);
        }

        void FixedTick(float d)
        {
            if (!isLocalPlayer)
                return;

            float pDelta = d;

            if (onGround && canMove)
                rigid.velocity = moveDir;  //This controls the character movement.                  

            //This can control character's rotation.
            if (canMove)
            {
                Vector3 targetDir = moveDir;
                targetDir.y = 0;
                if (targetDir == Vector3.zero)
                    targetDir = transform.forward;

                Quaternion tr = Quaternion.LookRotation(targetDir);
                Quaternion targetRotation = Quaternion.Slerp(transform.rotation, tr, pDelta * moveAmount * rotateSpeed);
                transform.rotation = targetRotation;
            }

            HandleMovementAnimations(); //update character's animations.
        }

        void HandleMovementAnimations()
        {
            anim.SetBool("sprint", sprint);   //syncing sprint bool value to animator's "Sprint" value.           
            if (moveAmount == 0)
                anim.SetBool("sprint", false);

            anim.SetFloat("vertical", moveAmount, 0.2f, fixedDelta); //syncing moveAmount value to animator's "vertical" value.
        }

        //These mecanic detects whether you are on ground or not.

        private void OnTriggerStay(Collider collision)
        {
            if (collision.gameObject.tag == "Ground")
            {
                onGround = true;
                anim.SetBool("onGround", true);
            }
        }

        //These mecanic detects whether you are on ground or not.
        private void OnTriggerExit(Collider collision)
        {
            if (collision.gameObject.tag == "Ground")
            {
                onGround = false;
                anim.SetBool("onGround", false);
            }
        }
    }
}


