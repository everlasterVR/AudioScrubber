#define VAM_GT_1_22
#define ENV_DEVELOPMENT
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace everlaster
{
    sealed class AudioScrubber : Script
    {
        public override bool ShouldIgnore() => false;
        protected override bool useVersioning => true;
        public override string className => nameof(AudioScrubber);

        protected override void OnUIEnabled()
        {
            if(_clipTimeFloat != null)
            {
                _clipTimeFloat.slider = _clipTimeSlider.slider;
            }
        }

        protected override void OnUIDisabled()
        {
            if(_clipTimeFloat != null)
            {
                _clipTimeFloat.slider = null;
            }
        }

        protected override void CreateUI()
        {
            var uiDynamicPopup = CreateScrollablePopup(_scrubberChooser);
            uiDynamicPopup.popup.labelText.color = Color.black;
            uiDynamicPopup.popup.selectColor = Colors.paleBlue;
            popups.Add(uiDynamicPopup.popup);

            _clipTimeSlider = CreateSlider(_clipTimeFloat, true);
            _clipTimeSlider.valueFormat = "F1";
            CreateToggle(_syncClipNameToUISliderBool, true);
            _showTimestampsToggle = CreateToggle(_showTimestampsBool, true);

            var infoString = new JSONStorableString("Info",
                "Usage:" +
                "\n\n<b>a)</b> Select a UISlider atom from the scene to use as a scrubber." +
                "\n\n<b>b)</b> Trigger the <i>Clip time (s)</i> or <i>Clip time (normalized)</i> float parameters." +
                "\n\n<b>c)</b> Scrub the <i>Clip time (s)</i> slider." +
                "\n\nEach option controls the current audio clip played on this atom's audio source."
            );
            var textField = CreateTextField(infoString);
            textField.height = 500;
            textField.backgroundColor = Color.clear;
            textField.DisableScroll();
        }

        AudioSourceControl _audioSourceControl;
        AudioSource _audioSource;
        StorableStringChooser _scrubberChooser;
        StorableFloat _clipTimeFloat;
        StorableFloat _clipTimeNormalizedFloat;
        StorableBool _syncClipNameToUISliderBool;
        StorableBool _showTimestampsBool;
        UIDynamicSlider _clipTimeSlider;
        UIDynamicToggle _showTimestampsToggle;
        Atom _scrubberAtom;
        Text _scrubberText;
        Slider _scrubberSlider;
        string _clipName;
        readonly List<Atom> _uiSliders = new List<Atom>();

        protected override void OnInit()
        {
            string storableId;
            var typeToStorableId = new Dictionary<string, string>
            {
                { AtomType.PERSON, "HeadAudioSource" },
                { AtomType.AUDIO_SOURCE, "AudioSource" },
                { AtomType.APT_SPEAKER, "AptSpeaker_Import" },
                { AtomType.RHYTHM_AUDIO_SOURCE, "RhythmSource" },
            };

            if(!typeToStorableId.TryGetValue(containingAtom.type, out storableId))
            {
                string atomTypesString = typeToStorableId.Keys.Select(AtomType.Inflect).ToPrettyString(", ").ReplaceLast(", ", ", or ");
                OnInitError($"Plugin must be added to {atomTypesString} atom.");
                return;
            }

            _audioSourceControl = containingAtom.GetStorableByID(storableId) as AudioSourceControl;
            if(_audioSourceControl == null)
            {
                OnInitError($"AudioSourceControl {storableId} not found on {containingAtom.uid}");
                return;
            }

            _audioSource = _audioSourceControl.audioSource;
            _uiSliders.AddRange(SuperController.singleton.GetAtoms().Where(atom => atom.type == AtomType.UI_SLIDER));
            SuperController.singleton.onAtomAddedHandlers += OnAtomAdded;
            SuperController.singleton.onAtomRemovedHandlers += OnAtomRemoved;
            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRenamed;

            _scrubberChooser = new StorableStringChooser("Scrubber", new List<string>(), "None");
            _scrubberChooser.SetCallback(OnScrubberSelected);
            _scrubberChooser.representsAtomUid = true;

            _clipTimeFloat = new StorableFloat("Clip time (s)", 0, 0, 60, false);
            _clipTimeFloat.isStorable = false;
            _clipTimeFloat.SetCallback(value =>
            {
                if(_audioSource != null && _audioSource.clip != null)
                {
                    _audioSource.time = Mathf.Clamp(value, 0, _audioSource.clip.length - 0.1f);
                }
            });

            _clipTimeNormalizedFloat = new StorableFloat("Clip time (normalized)", 0, 0, 1);
            _clipTimeNormalizedFloat.isStorable = false;
            _clipTimeNormalizedFloat.SetCallback(value =>
            {
                if(_audioSource != null && _audioSource.clip != null)
                {
                    float length = _audioSource.clip.length;
                    _audioSource.time = Mathf.Clamp(length * value, 0, length - 0.1f);
                }
            });

            _syncClipNameToUISliderBool = new StorableBool("Sync clip name to scrubber", true);
            _syncClipNameToUISliderBool.SetCallback(value =>
            {
                SyncSliderText();
                _showTimestampsToggle.toggle.interactable = value;
                _showTimestampsToggle.textColor = value ? Color.black : Color.gray;
                if(!value)
                {
                    _showTimestampsBool.val = false;
                }
            });

            _showTimestampsBool = new StorableBool("Show timestamps in scrubber", true);
            _showTimestampsBool.SetCallback(SyncSliderText);

            RebuildUISliderOptions();
            RegisterStringChooser(_scrubberChooser);
            RegisterFloat(_clipTimeFloat);
            RegisterFloat(_clipTimeNormalizedFloat);
            RegisterBool(_syncClipNameToUISliderBool);
            RegisterBool(_showTimestampsBool);

            initialized = true;
        }

        void OnAtomAdded(Atom atom)
        {
            if(atom.type == AtomType.UI_SLIDER && !_uiSliders.Contains(atom))
            {
                _uiSliders.Add(atom);
            }

            RebuildUISliderOptions();
        }

        void OnAtomRemoved(Atom atom)
        {
            if(_uiSliders.Contains(atom))
            {
                _uiSliders.Remove(atom);
            }

            RebuildUISliderOptions();
            if(_scrubberAtom == atom)
            {
                _scrubberChooser.val = string.Empty;
            }
        }

        void OnAtomRenamed(string oldUid, string newUid)
        {
            RebuildUISliderOptions();
            if(_scrubberChooser.val == oldUid)
            {
                _scrubberChooser.valNoCallback = newUid;
            }
        }

        void RebuildUISliderOptions()
        {
            var options = new List<string> { string.Empty };
            var displayOptions = new List<string> { "None" };
            options.AddRange(_uiSliders.Select(atom => atom.uid));
            displayOptions.AddRange(_uiSliders.Select(atom => atom.uid));
            _scrubberChooser.choices = options;
            _scrubberChooser.displayChoices = displayOptions;
        }

        void OnScrubberSelected(string option)
        {
            try
            {
                if(option == string.Empty)
                {
                    if(_scrubberAtom != null)
                    {
                        _scrubberSlider.onValueChanged.RemoveListener(OnSceneSliderValueChanged);
                        _scrubberSlider = null;
                        _scrubberText = null;
                        _scrubberAtom = null;
                    }

                    return;
                }

                string prevOption = _scrubberAtom != null ? _scrubberAtom.uid : string.Empty;
                var uiSlider = _uiSliders.Find(atom => atom.uid == option);
                if(uiSlider == null)
                {
                    logBuilder.Error($"{nameof(OnScrubberSelected)}: UISlider '{option}' not found");
                    _scrubberChooser.valNoCallback = prevOption;
                    return;
                }

                var uiSliderTrigger = uiSlider.GetStorableByID("Trigger") as UISliderTrigger;
                if(uiSliderTrigger != null && uiSliderTrigger.trigger != null)
                {
                    foreach(string targetName in GetTransitionActionNames(uiSliderTrigger))
                    {
                        if(
                            IsAlreadyTriggering(uiSlider.uid, targetName, _clipTimeFloat.name) ||
                            IsAlreadyTriggering(uiSlider.uid, targetName, _clipTimeNormalizedFloat.name)
                        )
                        {
                            _scrubberChooser.valNoCallback = prevOption;
                            return;
                        }
                    }
                }

                var prevSlider = _scrubberSlider;
                if(prevSlider != null)
                {
                    prevSlider.onValueChanged.RemoveListener(OnSceneSliderValueChanged);
                }

                var canvasTransform = uiSlider.reParentObject.transform.Find("object/rescaleObject/Canvas");
                _scrubberSlider = canvasTransform.GetComponentInChildren<Slider>();
                _scrubberText = canvasTransform.GetComponentInChildren<Text>();
                _scrubberAtom = uiSlider;
                _scrubberSlider.onValueChanged.AddListener(OnSceneSliderValueChanged);
                SyncSliderText();
            }
            catch(Exception e)
            {
                logBuilder.Exception(e);
            }
        }

        static IEnumerable<string> GetTransitionActionNames(UISliderTrigger uiSliderTrigger)
        {
            #if VAM_GT_1_22
            {
                return uiSliderTrigger.trigger.TransitionActions?.Select(action => action.receiverTargetName) ?? Enumerable.Empty<string>();
            }
            #else
            {
                var json = uiSliderTrigger.trigger.GetJSON();
                var transitionActions = json["transitionActions"] as JSONArray;
                var names = new List<string>();
                if(transitionActions != null)
                {
                    foreach(JSONNode action in transitionActions)
                    {
                        var asObject = action as JSONClass;
                        if(asObject != null && asObject.HasKey("receiverTargetName"))
                        {
                            names.Add(asObject["receiverTargetName"].Value);
                        }
                    }
                }

                return names;
            }
            #endif
        }

        bool IsAlreadyTriggering(string sliderUid, string targetName, string floatParamName)
        {
            if(targetName == floatParamName)
            {
                logBuilder.Error($"{nameof(OnScrubberSelected)}: {sliderUid} is already triggering the {floatParamName} parameter", false);
                return true;
            }

            return false;
        }

        void SyncSliderText()
        {
            if(_audioSource == null || _scrubberAtom == null || !_syncClipNameToUISliderBool.val)
            {
                return;
            }

            if(_audioSource.clip != null)
            {
                _clipName = _audioSource.clip.name;
                _scrubberAtom.GetStorableByID("Text").SetStringParamValue("text", _clipName);
                _scrubberText.text = _clipName; // gets rid of timestamps when _showTimestampsBool set to false
                UpdateClipLengthTimestamp(_audioSource.clip);
            }
        }

        bool _preventCircularCallback;

        void OnSceneSliderValueChanged(float value)
        {
            if(_preventCircularCallback)
            {
                return;
            }

            if(_audioSource != null && _audioSource.clip != null)
            {
                float length = _audioSource.clip.length;
                float normalizedValue = Mathf.InverseLerp(_scrubberSlider.minValue, _scrubberSlider.maxValue, value);
                _audioSource.time = Mathf.Clamp(length * normalizedValue, 0, length - 0.1f);
            }
        }

        AudioClip _prevClip;
        string _clipLengthTimestamp;

        void Update()
        {
            if(_audioSource == null)
            {
                return;
            }

            var clip = _audioSource.clip;
            if(clip == null)
            {
                if(_prevClip != null)
                {
                    _clipTimeFloat.valNoCallback = 0;
                    _clipTimeNormalizedFloat.valNoCallback = 0;
                    if(_scrubberSlider != null)
                    {
                        _preventCircularCallback = true;
                        _scrubberSlider.normalizedValue = 0;
                        if(_syncClipNameToUISliderBool.val && _showTimestampsBool.val)
                        {
                            UpdateTimestamps(0);
                        }

                        _preventCircularCallback = false;
                    }

                    _clipLengthTimestamp = string.Empty;
                }
            }
            else
            {
                float time = _audioSource.time;
                float length = clip.length;

                if(clip != _prevClip)
                {
                    _clipTimeFloat.valNoCallback = 0;
                    _clipTimeFloat.max = length;
                    SyncSliderText();
                }

                _clipTimeFloat.valNoCallback = Mathf.Min(time, length);
                float normalized = Mathf.InverseLerp(0, length, time);
                if(_scrubberSlider != null)
                {
                    _preventCircularCallback = true;
                    _scrubberSlider.normalizedValue = normalized;
                    _clipTimeNormalizedFloat.valNoCallback = normalized;
                    if(_syncClipNameToUISliderBool.val && _showTimestampsBool.val)
                    {
                        UpdateTimestamps(time);
                    }

                    _preventCircularCallback = false;
                }
            }

            _prevClip = clip;
        }

        void UpdateClipLengthTimestamp(AudioClip clip) =>
            _clipLengthTimestamp = clip == null
                ? string.Empty
                : $"{(int) (clip.length / 60):D2}:{(int) (clip.length % 60):D2}";

        int _prevMin;
        int _prevSec;

        void UpdateTimestamps(float time)
        {
            int min = (int) time / 60;
            int sec = (int) time % 60;
            if(_prevMin == min && _prevSec == sec)
            {
                return;
            }

            _prevMin = min;
            _prevSec = sec;
            _scrubberText.text = $"{_clipName} [{min:D2}:{sec:D2} / {_clipLengthTimestamp}]";
        }

        public override void RestoreFromJSON(
            JSONClass jc,
            bool restorePhysical = true,
            bool restoreAppearance = true,
            JSONArray presetAtoms = null,
            bool setMissingToDefault = true
        )
        {
            if(containingAtom.containingSubScene != null)
            {
                string subsceneUid = containingAtom.uid.Replace(containingAtom.uidWithoutSubScenePath, "");

                /* Ensure loading a SubScene file sets the correct value to JSONStorableStringChooser.
                 * - Assumes the targeted scrubber atom is in the same SubScene as the containing atom.
                 * - The stored value will already start with the SubScene UID if loading e.g. a scene file rather than a subscene.
                 */
                if(jc.HasKey(_scrubberChooser.name) && !jc[_scrubberChooser.name].Value.StartsWith(subsceneUid))
                {
                    subScenePrefix = subsceneUid;
                }
            }

            base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);
            subScenePrefix = null;
        }

        protected override void DoDestroy()
        {
            if(_scrubberSlider != null)
            {
                _scrubberSlider.onValueChanged.RemoveListener(OnSceneSliderValueChanged);
            }

            SuperController.singleton.onAtomAddedHandlers -= OnAtomAdded;
            SuperController.singleton.onAtomRemovedHandlers -= OnAtomRemoved;
            SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRenamed;
        }
    }
}
