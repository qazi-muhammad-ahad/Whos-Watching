using UnityEngine;

namespace WhosWatching
{
    public sealed class CharacterPresentation : MonoBehaviour
    {
        public Animator Animator;
        public bool Floating;
        [Range(0,2)] public float MotionAmount = 1;
        NetworkPlayer player;
        bool supported;
        Vector3 restPosition, restScale;
        Quaternion restRotation;
        float speed, phase, expression = 1;
        float airBlend, landing;
        bool wasAirborne;
        bool captured;
        Transform leftArm, rightArm;
        uint punchSequence, hitSequence, respawnSequence;
        float punch, hit, spawn;
        void Start() { player = GetComponentInParent<NetworkPlayer>(); if (!Animator) Animator = GetComponent<Animator>(); CapturePose(); }
        void CapturePose()
        {
            if (captured) return;
            captured = true; restPosition = transform.localPosition; restRotation = transform.localRotation; restScale = transform.localScale;
        }
        void Update()
        {
            if (!Animator || !player || !player.IsSpawned) return;
            Animator.SetFloat("Speed", player.AnimationSpeed.Value, 0.12f, Time.deltaTime);
            Animator.SetBool("Airborne",player.Airborne.Value);
            bool next = player.Gaze.Value.Allies > 0 && TeamLobby.GameplayAllowed;
            if (next && !supported) Animator.SetTrigger("Buff");
            supported = next;
        }
        void LateUpdate()
        {
            if (!player || !player.IsSpawned) return;
            // Exponential smoothing is frame-rate independent and eases both starts and stops.
            float blend = 1 - Mathf.Exp(-8 * Time.deltaTime);
            speed = Mathf.Lerp(speed, TeamLobby.GameplayAllowed ? player.AnimationSpeed.Value : 0, blend);
            bool dancing = Animator && (Animator.GetCurrentAnimatorStateInfo(0).IsName("Buff") ||
                (Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).IsName("Buff")));
            expression = Mathf.Lerp(expression, dancing ? 0 : 1, blend);
            bool airborne = player.Airborne.Value;
            if (wasAirborne && !airborne) landing = 1;
            wasAirborne = airborne;
            airBlend = Mathf.Lerp(airBlend,airborne ? 1 : 0,blend);
            landing = Mathf.Max(0,landing-Time.deltaTime*4);
            phase = Mathf.Repeat(phase + Time.deltaTime * Mathf.Lerp(Floating ? 0.65f : 0.8f, Floating ? 1.05f : 1.8f, Mathf.Clamp01(speed / 5)) * Mathf.PI * 2, Mathf.PI * 2);
            SampleMotion(phase,speed,expression*(1-airBlend));
            ApplyJumpShape(airBlend,landing);
            ApplyActions();
        }
        void ApplyActions()
        {
            if (!rightArm)
                foreach (var bone in GetComponentsInChildren<Transform>()) {
                    if (bone.name == "Arm.R") rightArm = bone;
                    if (bone.name == "Arm.L") leftArm = bone;
                }
            if (punchSequence != player.PunchSequence.Value) { punchSequence = player.PunchSequence.Value; punch = 1; }
            if (hitSequence != player.HitSequence.Value) { hitSequence = player.HitSequence.Value; hit = 1; }
            if (respawnSequence != player.RespawnSequence.Value) { respawnSequence = player.RespawnSequence.Value; spawn = 1; }
            punch = Mathf.MoveTowards(punch,0,Time.deltaTime*3.5f);
            hit = Mathf.MoveTowards(hit,0,Time.deltaTime*3);
            spawn = Mathf.MoveTowards(spawn,0,Time.deltaTime*2);
            bool carrying = ArenaGameplay.Instance && ArenaGameplay.Instance.Carrying(player) >= 0;
            float working = player.Working.Value ? Mathf.Sin(Time.time * 15 * player.Gaze.Value.Work) : 0;
            float strike = Mathf.Sin(punch * Mathf.PI);
            if (rightArm) rightArm.localRotation *= Quaternion.Euler(-65 * strike - (carrying ? 35 : 0) + working * 22, 0, -20 * strike);
            if (leftArm) leftArm.localRotation *= Quaternion.Euler(-(carrying ? 35 : 0) - working * 22, 0, 0);
            transform.localRotation = Quaternion.Euler(-hit*18 + working*3, strike*15, hit*Mathf.Sin(Time.time*35)*10) * transform.localRotation;
            transform.localPosition += Vector3.forward * (strike*0.14f);
            transform.localScale *= 1 - spawn*0.45f;
        }
        // Same deterministic sampler used for visual QA; changes only the visual child.
        public void SampleMotion(float cycle, float metersPerSecond, float weight = 1)
        {
            CapturePose();
            float moving = Mathf.SmoothStep(0,1,Mathf.Clamp01(metersPerSecond/5));
            float amount = MotionAmount * weight;
            float bob = Floating ? Mathf.Sin(cycle) * Mathf.Lerp(0.012f,0.045f,moving) : (1-Mathf.Cos(cycle*2))*0.018f*moving;
            float sway = Mathf.Sin(cycle) * (Floating ? 0.025f : 0.014f) * moving;
            float roll = Mathf.Sin(cycle) * (Floating ? 4.5f : 3.5f) * moving;
            float pitch = (Mathf.Sin(cycle*2)*1.5f + (Floating ? 3 : 4))*moving;
            transform.localPosition = restPosition + new Vector3(sway,bob,0)*amount;
            transform.localRotation = Quaternion.Euler(pitch*amount,0,roll*amount)*restRotation;
            // Imported Devil axes are rotated; stretch along upright, not FBX local Y.
            Vector3 up = Quaternion.Inverse(restRotation)*Vector3.up;
            up = new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z));
            float stretch = Mathf.Sin(cycle*2) * (Floating ? 0.012f : 0.025f) * moving * amount;
            transform.localScale = Vector3.Scale(restScale, Vector3.one*(1-stretch*0.5f)+up*(stretch*1.5f));
        }
        void OnDisable()
        {
            if (!captured) return;
            transform.localPosition=restPosition; transform.localRotation=restRotation; transform.localScale=restScale;
        }
        public void ApplyJumpShape(float airborneWeight, float landingWeight)
        {
            // The jump's height comes from network physics; this is just a soft visual pose.
            Vector3 up=Quaternion.Inverse(restRotation)*Vector3.up;
            up=new Vector3(Mathf.Abs(up.x),Mathf.Abs(up.y),Mathf.Abs(up.z));
            float shape=0.045f*Mathf.Clamp01(airborneWeight)-0.06f*Mathf.Sin(Mathf.Clamp01(landingWeight)*Mathf.PI);
            transform.localScale=Vector3.Scale(transform.localScale,Vector3.one*(1-shape*0.5f)+up*(shape*1.5f));
        }
    }
}
