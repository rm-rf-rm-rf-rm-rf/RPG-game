﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Movement;
using RPG.Combat;
using RPG.Core;
using RPG.Attributes;
using RPG.Tool;
using UnityEngine.AI;
using System;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace RPG.Control
{

    public class PlayerController : MonoBehaviour
    {
        [SerializeField] CursorMapping[] cursorMappings = null;
        [SerializeField] float maxNavMeshProjectionDistance = 1f;
        [SerializeField] float raycastRadius = 1f;
        [SerializeField][Range(0,1)] float rotateSpeed = 0.1f;
        [SerializeField][Range(0,100)] float stopSpeed = 20f;
        [SerializeField][Range(0,10)] float moveSpeed =2.5f;
        [SerializeField][Range(0,10)] float blockingMoveSpeed =1f;
        [SerializeField][Range(10,30)] float rollSpeed =10f;
        [SerializeField][Range(1,2)] float sprintMultiplier = 1.4f;
        [SerializeField] UnityEvent walkEvent;
        [SerializeField] UnityEvent onAttack;
        [SerializeField] float gravityTemp = 10f;
        [SerializeField] float toGroundDistance = 1f;


        Health health;
        Animator anim = null;
        int layerMask = 0x01ff;
        Fighter fighter = null;
        float attackTime = 0f;
        NavMeshAgent agent = null;
        Rigidbody myRigidBody = null;
        Vector3 moveInput;
        Vector3 moveVelocity;
        bool isAttacking = false;
        private bool isRolling = false;
        private float maxForwardSpeed = 6f;
        float minForwardSpeed = 6f;
        private float walkVoiceInterval = 2.426f;
        float nextWalkVoice = 0f;
        Stamina stamina = null;
        bool canSprint = true;
        bool canRoll = true;
        bool isBlocking = false;
        float rollTime = 0f;
        float moveAnimationSpeedMultiplier = 1.2f;
        float walkSpeed =2.5f;
        float gravity = 0;

        // Start is called before the first frame update
        void Start()
        {
            anim = GetComponent<Animator>();
            health = GetComponent<Health>();
            fighter = GetComponent<Fighter>();
            agent = GetComponent<NavMeshAgent>();
            myRigidBody = GetComponent<Rigidbody>();
            stamina = GetComponent<Stamina>();
            // this.gameObject.GetComponent<NavMeshAgent>().enabled=true;
        }

        [System.Serializable]
        struct CursorMapping
        {
            public CursorType type;
            public Texture2D texture;
            public Vector2 hotspot;
        }

        private CursorMapping GetCursorMapping(CursorType type){
            CursorMapping defaultMapping;
            foreach(CursorMapping mapping in cursorMappings){
                if(mapping.type == CursorType.None){
                    defaultMapping = mapping;
                }
                if(mapping.type == type){
                    return mapping;
                }
            }
            return cursorMappings[0];
        }

        // Update is called once per frame
        void Update()
        {
            // anim.SetFloat("movingSpeed",5f);
            // print("Horizontal: " + Input.GetAxisRaw("Horizontal"));
            // print("Vertical: " + Input.GetAxisRaw("Vertical"));

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                GetComponent<ActionScheduler>().CancelCurrentAction();
            }
            // if (InteractWithUI())
            // {
            //     return;
            // }
            // if (health.IsDead())
            // {
            //     SetCursor(CursorType.None);
            //     return;
            // }
            // if (InteractWithComponent())
            // {
            //     return;
            // }
            // if (!InteractWithMovement())
            // {
            //     SetCursor(CursorType.None);
            //     return;
            // }
        }

        void FixedUpdate() {
            if(health.IsDead()) return;
            if (!IsGrounded())
            {
                gravity = gravityTemp;
            }
            else
            {
                gravity = 0f;
            }
            
            if (Input.GetMouseButton(0)&&!isAttacking&&stamina.HasStaminaLeft())
            {
                onAttack.Invoke();
                stamina.ConsumeStaminaOnce(25f, Stamina.StaminaType.attack);
                attackTime = Util.GetCurrentAnimationTime(attackTime,"attack",anim);
                StartCoroutine("StartAttack");
            }else if(canRoll&&Input.GetKeyDown(KeyCode.Space)&&!isAttacking&&!isRolling&&stamina.HasStaminaLeft()){
                anim.SetTrigger("roll");
                canRoll = false;
                StartCoroutine("StartRoll");
                rollTime = Util.GetCurrentAnimationTime(rollTime,"roll", anim)*.7f;
                stamina.ConsumeStaminaOnce(15f,Stamina.StaminaType.roll);
                moveVelocity = (this.transform.forward ) * rollSpeed;
                Debug.Log("x: "+ myRigidBody.velocity.x + " z: " + myRigidBody.velocity.z);
                // myRigidBody.AddForce(this.transform.forward * rollSpeed * rollMultiplier);
                moveVelocity.y = 0;
                myRigidBody.velocity = moveVelocity;
            }else if(Input.GetKeyUp(KeyCode.LeftShift)){
                anim.SetFloat("sprintMultiplier", 1);
                minForwardSpeed = 6f;
            }
            if(Input.GetMouseButton(1)&&!isAttacking){
                isBlocking = true;
                moveAnimationSpeedMultiplier = 1.2f;
            }
            else{
                isBlocking = false;
            }

            if(!isAttacking&&!isRolling){
                HandleMovement();
            }
            HandleBlocking();

            rollTime = Mathf.Max(rollTime-Time.deltaTime,0);
            if(rollTime==0){
                canRoll = true;
            }
            
            myRigidBody.AddRelativeForce(Vector3.down * gravity);
            maxForwardSpeed = Mathf.Clamp(maxForwardSpeed-Time.deltaTime*stopSpeed,minForwardSpeed,10);
            // print("maxForwardSpeed: " + maxForwardSpeed); 
        }

        private void HandleBlocking()
        {
            if(isBlocking){
                minForwardSpeed = 3f;
                walkSpeed = blockingMoveSpeed;
            }else{
                minForwardSpeed = 6f;
                walkSpeed = moveSpeed;
            }
            anim.SetBool("isBlocking", isBlocking);
        }

        private void HandleMovement()
        {
            if (health.IsDead()) return;
            if (!IsGrounded())
            {
                gravity = gravityTemp;
            }
            else
            {
                gravity = 0f;
            }
            float x = Input.GetAxisRaw("Vertical") == 0 ? Input.GetAxisRaw("Horizontal") * 1.5f : Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Horizontal") == 0 ? Input.GetAxisRaw("Vertical") * 1.5f : Input.GetAxisRaw("Vertical");
            // float x = Input.GetAxisRaw("Vertical");
            // float z = Input.GetAxisRaw("Horizontal");

            moveInput = Vector3.zero;
            moveInput.x = x;
            moveInput.z = z;

            // Vector3 camDir = Camera.main.transform.rotation * moveInput;
            Vector3 targetDirection = GetFacingDirection();

            // if (moveInput != Vector3.zero)
            // {
                // transform.rotation = Quaternion.Slerp(
                //     transform.rotation, 
                //     Quaternion.LookRotation(targetDirection),
                //     Time.deltaTime * rotateSpeed
                // );
            // }
            // moveVelocity = moveInput * moveSpeed;
            walkSpeed = moveSpeed;
            moveInput = targetDirection.normalized;
            moveVelocity = moveInput * walkSpeed;
            moveAnimationSpeedMultiplier = 1.2f;

            if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
            {
                // anim.SetBool("isMoving", true);
                anim.SetFloat("forwardSpeed"
                            , Mathf.Clamp(anim.GetFloat("forwardSpeed")
                                + Mathf.Abs(Input.GetAxisRaw("Horizontal"))
                                + Mathf.Abs(Input.GetAxisRaw("Vertical"))
                            , 0
                            , maxForwardSpeed));
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(moveInput), rotateSpeed);
            }
            else
            {
                // anim.SetBool("isMoving", false);
                anim.SetFloat("forwardSpeed"
                                , Mathf.Clamp(anim.GetFloat("forwardSpeed") - Time.deltaTime * stopSpeed
                                , 0
                                , maxForwardSpeed));
            }
            if (Input.GetKey(KeyCode.LeftShift) && stamina.HasStaminaLeft() && canSprint)
            {
                isBlocking = false;
                moveVelocity = moveVelocity * sprintMultiplier;
                moveAnimationSpeedMultiplier = sprintMultiplier;
                stamina.ConsumeStaminaOnce(.2f, Stamina.StaminaType.sprint);
                maxForwardSpeed = 10f;
                if (Time.time >= nextWalkVoice)
                {

                    walkEvent.Invoke();
                    nextWalkVoice = Time.time + walkVoiceInterval;
                }
            }
            anim.SetFloat("moveAnimationSpeedMultiplier", moveAnimationSpeedMultiplier);
            if (isAttacking)
            {
                anim.SetFloat("forwardSpeed", 0);
                myRigidBody.velocity = new Vector3(0, 0, 0);
            }else{
                myRigidBody.velocity = moveVelocity;
            }
        }

        private Vector3 GetFacingDirection()
        {
            Vector3 camDir = Camera.main.transform.TransformDirection(moveInput);
            Vector3 targetDirection = new Vector3(camDir.x, 0, camDir.z);
            return targetDirection;
        }

        private bool InteractWithComponent()
        {
            RaycastHit[] hits = RaycastAllSorted();
            foreach (RaycastHit hit in hits)
            {
                IRaycastable[] raycastables = hit.transform.GetComponents<IRaycastable>();
                foreach (IRaycastable raycastable in raycastables)
                {
                    if (raycastable.HandleRaycast(this))
                    {
                        SetCursor(raycastable.GetCursorType());
                        return true;
                    }
                }
            }
            return false;

        }

        private bool InteractWithUI()
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                SetCursor(CursorType.UI);
                return true;
            }
            return false;
        }

        private bool InteractWithMovement()
        {
            return MoveToCursor();
        }

        private bool RaycastNavMesh(out Vector3 target){
            target = new Vector3();            
            RaycastHit hit;
            NavMeshHit navMeshHit;
            Debug.DrawRay (GetMouseRay().origin, GetMouseRay().direction * 50000000, Color.red);
            // if(!Physics.Raycast(GetMouseRay(), out hit)) return false;
            if(!Physics.Raycast(GetMouseRay(), out hit,100f, layerMask)) return false;
            if(!NavMesh.SamplePosition(hit.point, out navMeshHit, maxNavMeshProjectionDistance, NavMesh.AllAreas)){
                return false;
            }
            target = navMeshHit.position;

            // NavMeshPath path = new NavMeshPath();
            // if(!NavMesh.CalculatePath(transform.position, target, NavMesh.AllAreas, path)) return false;
            // if(path.status != NavMeshPathStatus.PathComplete) return false;
            // if(GetPathLength(path) > maxNavPathLength) return false;

            return GetComponent<Mover>().CanMoveTo(target);
        }

        

        private bool MoveToCursor()
        {
            RaycastHit hitInfo;
            // bool hasHit = Physics.Raycast(GetMouseRay(), out hitInfo);
            Vector3 target;
            bool hasHit = RaycastNavMesh(out target);
            if (hasHit)
            {
                if(!GetComponent<Mover>().CanMoveTo(target)) return false;

                if (Input.GetMouseButton(0))
                {
                    // GetComponent<Mover>().StartMoveAction(hitInfo.point, 1f);
                    GetComponent<Mover>().StartMoveAction(target, 1f);
                }
                SetCursor(CursorType.Movement);
                return true;
            }
            return false;
        }

        private void SetCursor(CursorType type)
        {
            CursorMapping mapping = GetCursorMapping(type);
            Cursor.SetCursor(mapping.texture, mapping.hotspot, CursorMode.Auto);
        }

        private static Ray GetMouseRay()
        {
            return Camera.main.ScreenPointToRay(Input.mousePosition);
        }

        RaycastHit[] RaycastAllSorted(){
            // get all hits
            RaycastHit[] hits = Physics.SphereCastAll(GetMouseRay(), raycastRadius);
            float[] distances = new float[hits.Length];
            // sort by distance
            for(int i=0;i<hits.Length;i++){
                distances[i] = hits[i].distance;
            }
            //sort the hits
            Array.Sort(distances, hits);
            //return 
            return hits;
        }

        
        public IEnumerator StartAttack(){
            // fighter.EnableTrigger();
            GetComponent<Animator>().ResetTrigger("stopAttack");
            GetComponent<Animator>().SetTrigger("attack");
            isAttacking=true;
            yield return new WaitForSeconds(attackTime/4*3);
            isAttacking=false;
            // fighter.DisableTrigger();
        }

        public IEnumerator StartRoll(){
            isRolling = true;
            yield return new WaitForSeconds(Util.GetCurrentAnimationTime(0,"roll", anim)*0.7f);
            isRolling=false;
        }

        public bool IsBlocking(){
            return isBlocking;
        }

        // public void EnableWeaponTrigger(){
        //     print("Enable weapon trigger");
        //     fighter.EnableTrigger();
        //     // agent.SetDestination(transform.position);
        //     // agent.enabled = false;
        // }

        // public void DisableWeaponTrigger(){
        //     print("Disable weapon trigger");
        //     fighter.DisableTrigger();
        //     // agent.enabled = true;
        // }

        public void SetIsAttacking(){
            isAttacking = true;
        }

        public void ResetIsAttacking(){
            isAttacking = false;
        }

        public void EnableInvulnerable(){
            health.SetInvulnerable(true);
        }

        public void DisableInvulnerable(){
            health.SetInvulnerable(false);
        }

        public void FootL(){

        }

        public void FootR(){

        }

        public bool IsGrounded() {
            return Physics.Raycast(transform.position+(Vector3.up), -Vector3.up, toGroundDistance + 0.1f);
        }
    }
}









