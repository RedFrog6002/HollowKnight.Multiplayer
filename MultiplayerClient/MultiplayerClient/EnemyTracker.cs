using System;
using UnityEngine;

namespace MultiplayerClient
{
    public class EnemyTracker : MonoBehaviour
    {
        private tk2dSpriteAnimator _anim;
        private HealthManager _hm;
        public int enemyId;

        private void Awake()
        {
            _anim = GetComponent<tk2dSpriteAnimator>();
            _hm = GetComponent<HealthManager>();
        }

        private void Start()
        {
            foreach (byte toClient in SessionManager.Instance.Players.Keys)
            {
                ClientSend.SyncEnemy(toClient, gameObject.name, enemyId);
            }

            if (_anim != null)
            {
                foreach (tk2dSpriteAnimationClip clip in _anim.Library.clips)
                {
                    if (clip.frames.Length > 0)
                    {
                        tk2dSpriteAnimationFrame frame0 = clip.frames[0];
                        frame0.triggerEvent = true;
                        frame0.eventInfo = clip.name;
                        clip.frames[0] = frame0;
                    }
                }
                
                _anim.AnimationEventTriggered = AnimationEventDelegate;
            }

            if (_hm != null)
            {
                _hm.hp *= 2;
            }
        }
        
        private Vector3 _storedPosition = Vector3.zero;
        private Vector3 _storedScale = Vector3.zero;
        private void FixedUpdate()
        {
            Vector3 pos = transform.position;
            if (pos != _storedPosition)
            {
                foreach (byte player in SessionManager.Instance.Players.Keys)
                    ClientSend.EnemyPosition(player, pos, enemyId);
                _storedPosition = pos;
            }

            Vector3 scale = transform.localScale;
            if (scale != _storedScale)
            {
                foreach (byte player in SessionManager.Instance.Players.Keys)
                    ClientSend.EnemyScale(player, scale, enemyId);
                _storedScale = scale;
            }
        }
        
        private string _storedClip;
        private void AnimationEventDelegate(tk2dSpriteAnimator anim, tk2dSpriteAnimationClip clip, int frameNum)
        {
            if (clip.wrapMode == tk2dSpriteAnimationClip.WrapMode.Loop && clip.name != _storedClip ||
                clip.wrapMode == tk2dSpriteAnimationClip.WrapMode.LoopSection && clip.name != _storedClip ||
                clip.wrapMode == tk2dSpriteAnimationClip.WrapMode.Once)
            {
                _storedClip = clip.name;
                tk2dSpriteAnimationFrame frame = clip.GetFrame(frameNum);

                string clipName = frame.eventInfo;
                foreach (byte player in SessionManager.Instance.Players.Keys)
                    ClientSend.EnemyAnimation(player, clipName, enemyId);
            }
        }
    }
}