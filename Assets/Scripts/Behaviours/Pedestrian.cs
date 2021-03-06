﻿using System.IO;
using System.Linq;
using System.Collections.Generic;
using SanAndreasUnity.Utilities;
using SanAndreasUnity.Importing.Archive;
using SanAndreasUnity.Importing.Conversion;
using SanAndreasUnity.Importing.Items;
using SanAndreasUnity.Importing.Items.Definitions;
using SanAndreasUnity.Importing.Animation;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SanAndreasUnity.Behaviours
{
    using Anim = SanAndreasUnity.Importing.Conversion.Animation;

    [ExecuteInEditMode]
    public class Pedestrian : MonoBehaviour
    {
        private int _curPedestrianId;
        private AnimGroup _curAnimGroup = AnimGroup.None;
        private AnimIndex _curAnim = AnimIndex.None;

        private UnityEngine.Animation _anim;

        private FrameContainer _frames;
        private Frame _root;

        private readonly Dictionary<string, Anim> _loadedAnims
            = new Dictionary<string,Anim>();

        public PedestrianDef Definition { get; private set; }

        public int PedestrianId = 7;

        public AnimGroup AnimGroup = AnimGroup.WalkCycle;
        public AnimIndex AnimIndex = AnimIndex.Idle;

        public bool IsInVehicle { get; set; }

        public Vector3 VehicleParentOffset { get; set; }

        public bool Walking
        {
            set
            {
                AnimGroup = AnimGroup.WalkCycle;
                AnimIndex = value ? AnimIndex.Walk : AnimIndex.Idle;
            }
            get
            {
                return AnimGroup == AnimGroup.WalkCycle
                    && (AnimIndex == AnimIndex.Walk || Running);
            }
        }

        public bool Running
        {
            set
            {
                AnimGroup = AnimGroup.WalkCycle;
                AnimIndex = value ? AnimIndex.Run : AnimIndex.Walk;
            }
            get
            {
                return AnimGroup == AnimGroup.WalkCycle
                    && (AnimIndex == AnimIndex.Run || AnimIndex == AnimIndex.Panicked);
            }
        }

        public float Speed { get; private set; }

        public Vector3 Position
        {
            get { return transform.localPosition; }
            set { transform.localPosition = value; }
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            var trans = _root.transform;

            if (IsInVehicle)
            {
                Speed = 0.0f;
                trans.parent.localPosition = VehicleParentOffset;
            }
            else
            {
                Speed = _root.LocalVelocity.z;
                trans.parent.localPosition = new Vector3(0f, -trans.localPosition.y * .5f, -trans.localPosition.z);
            }
        }

        private void Update()
        {
            if (!Loader.HasLoaded) return;
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying && !EditorApplication.isPaused) return;
#endif

            if (_curPedestrianId != PedestrianId)
            {
                Load(PedestrianId);
            }

            if (_curAnim != AnimIndex || _curAnimGroup != AnimGroup)
            {
                CrossFadeAnim(AnimGroup, AnimIndex, 0.3f, PlayMode.StopAll);
            }
        }

        private void OnValidate()
        {
            if (_frames != null) Update();
        }

        private void Load(int id)
        {
            _curPedestrianId = PedestrianId;

            Definition = Item.GetDefinition<PedestrianDef>(id);
            if (Definition == null) return;

            LoadModel(Definition.ModelName, Definition.TextureDictionaryName);

            _curAnim = AnimIndex.None;
            _curAnimGroup = AnimGroup.None;

            _anim = gameObject.GetComponent<UnityEngine.Animation>();

            if (_anim == null) {
                _anim = gameObject.AddComponent<UnityEngine.Animation>();
            }

            LoadAnim(AnimGroup.WalkCycle, AnimIndex.Walk);
            LoadAnim(AnimGroup.WalkCycle, AnimIndex.Run);
            LoadAnim(AnimGroup.WalkCycle, AnimIndex.Panicked);
            LoadAnim(AnimGroup.WalkCycle, AnimIndex.Idle);
            LoadAnim(AnimGroup.WalkCycle, AnimIndex.RoadCross);
            LoadAnim(AnimGroup.WalkCycle, AnimIndex.WalkStart);

            LoadAnim(AnimGroup.Car, AnimIndex.Sit);
            LoadAnim(AnimGroup.Car, AnimIndex.DriveLeft);
            LoadAnim(AnimGroup.Car, AnimIndex.DriveRight);
            LoadAnim(AnimGroup.Car, AnimIndex.GetInLeft);
            LoadAnim(AnimGroup.Car, AnimIndex.GetInRight);
            LoadAnim(AnimGroup.Car, AnimIndex.GetOutLeft);
            LoadAnim(AnimGroup.Car, AnimIndex.GetOutRight);
        }

        private void LoadModel(string modelName, params string[] txds)
        {
            if (_frames != null)
            {
                Destroy(_frames.Root.gameObject);
                Destroy(_frames);
                _loadedAnims.Clear();
            }

            var geoms = Geometry.Load(modelName, txds);
            _frames = geoms.AttachFrames(transform, MaterialFlags.Default);

            _root = _frames.GetByName("Root");
        }

        public AnimationState PlayAnim(AnimGroup group, AnimIndex anim, PlayMode playMode)
        {
            var animState = LoadAnim(group, anim);

            _curAnimGroup = AnimGroup = group;
            _curAnim = AnimIndex = anim;

            _anim.Play(animState.name, playMode);

            return animState;
        }

        public AnimationState CrossFadeAnim(AnimGroup group, AnimIndex anim, float duration, PlayMode playMode)
        {
            var animState = LoadAnim(group, anim);

            _curAnimGroup = AnimGroup = group;
            _curAnim = AnimIndex = anim;

            _anim.CrossFade(animState.name, duration, playMode);

            return animState;
        }

        public AnimationState CrossFadeAnimQueued(AnimGroup group, AnimIndex anim, float duration, QueueMode queueMode, PlayMode playMode)
        {
            var animState = LoadAnim(group, anim);

            _curAnimGroup = AnimGroup = group;
            _curAnim = AnimIndex = anim;

            _anim.CrossFadeQueued(animState.name, duration, queueMode, playMode);

            return animState;
        }

        public Anim GetAnim(AnimGroup group, AnimIndex anim)
        {
            var animGroup = AnimationGroup.Get(Definition.AnimGroupName, group);

            Anim result;
            return _loadedAnims.TryGetValue(animGroup[anim], out result) ? result : null;
        }

        private AnimationState LoadAnim(AnimGroup group, AnimIndex anim)
        {
            if (anim == AnimIndex.None) {
                return null;
            }

            var animGroup = AnimationGroup.Get(Definition.AnimGroupName, group);
            var animName = animGroup[anim];

            AnimationState state;

            if (!_loadedAnims.ContainsKey(animName)) {
                var clip = Importing.Conversion.Animation.Load(animGroup.FileName, animName, _frames);
                _loadedAnims.Add(animName, clip);
                _anim.AddClip(clip.Clip, animName);
                state = _anim[animName];
            } else {
                state = _anim[animName];
            }

            return state;
        }
    }
}
