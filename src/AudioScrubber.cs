#define ENV_DEVELOPMENT
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace everlaster
{
    sealed class AudioScrubber : MVRScript
    {
        UnityEventsListener _uiListener;

        public override void InitUI()
        {
            base.InitUI();
            if(UITransform != null)
            {
                UITransform.Find("Scroll View").GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f);
                if(_uiListener == null)
                {
                    _uiListener = UITransform.gameObject.AddComponent<UnityEventsListener>();
                    _uiListener.enabledHandlers += () =>
                    {
                        if(_clipTimeFloat != null)
                        {
                            _clipTimeFloat.slider = _clipTimeSlider.slider;
                        }
                    };
                    _uiListener.disabledHandlers += () =>
                    {
                        if(_clipTimeFloat != null)
                        {
                            _clipTimeFloat.slider = null;
                        }
                    };
                }
            }
        }

        AudioSourceControl _audioSourceControl;
        AudioSource _audioSource;
        JSONStorableStringChooser _scrubberChooser;
        JSONStorableFloat _clipTimeFloat;
        JSONStorableFloat _clipTimeNormalizedFloat;
        JSONStorableBool _syncClipNameToUISliderBool;
        JSONStorableBool _showTimestampsBool;
        JSONStorableString _infoString;
        UIDynamicSlider _clipTimeSlider;
        Atom _scrubberAtom;
        Text _scrubberText;
        Slider _scrubberSlider;
        string _clipName;
        readonly List<Atom> _uiSliders = new List<Atom>();

        public override void Init()
        {
            try
            {
                string storableId;
                switch(containingAtom.type)
                {
                    case "Person":
                        storableId = "HeadAudioSource";
                        break;
                    case "AudioSource":
                        storableId = "AudioSource";
                        break;
                    case "AptSpeaker":
                        storableId = "AptSpeaker_Import";
                        break;
                    case "RhythmAudioSource":
                        storableId = "RhythmSource";
                        break;
                    default:
                        SuperController.LogError($"Add to a Person, AudioSource, AptSpeaker, or RhythmAudioSource atom, not {containingAtom.type}");
                        return;
                }

                _audioSourceControl = containingAtom.GetStorableByID(storableId) as AudioSourceControl;
                if(_audioSourceControl == null)
                {
                    SuperController.LogError($"AudioSourceControl {storableId} not found on {containingAtom.uid}");
                    return;
                }

                _audioSource = _audioSourceControl.audioSource;
                _uiSliders.AddRange(SuperController.singleton.GetAtoms().Where(atom => atom.type == "UISlider"));
                SuperController.singleton.onAtomAddedHandlers += OnAtomAdded;
                SuperController.singleton.onAtomRemovedHandlers += OnAtomRemoved;
                SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRenamed;

                _scrubberChooser = new JSONStorableStringChooser("Scrubber", new List<string>(), "None", "Scrubber");
                _scrubberChooser.setCallbackFunction = OnScrubberSelected;
                _scrubberChooser.representsAtomUid = true;

                _clipTimeFloat = new JSONStorableFloat("Clip time (s)", 0, 0, 60, false);
                _clipTimeFloat.isStorable = false;
                _clipTimeFloat.setCallbackFunction = value =>
                {
                    if(_audioSource != null && _audioSource.clip != null)
                    {
                        _audioSource.time = Mathf.Clamp(value, 0, _audioSource.clip.length - 0.1f);
                    }
                };

                _clipTimeNormalizedFloat = new JSONStorableFloat("Clip time (normalized)", 0, 0, 1);
                _clipTimeNormalizedFloat.isStorable = false;
                _clipTimeNormalizedFloat.setCallbackFunction = value =>
                {
                    if(_audioSource != null && _audioSource.clip != null)
                    {
                        float length = _audioSource.clip.length;
                        _audioSource.time = Mathf.Clamp(length * value, 0, length - 0.1f);
                    }
                };

                _syncClipNameToUISliderBool = new JSONStorableBool("Sync clip name to scrubber", true);
                _showTimestampsBool = new JSONStorableBool("Show timestamps in scrubber", true);
                _showTimestampsBool.setCallbackFunction = value => SyncSliderText();

                _infoString = new JSONStorableString("Info",
                    "Usage:" +
                    "\n\n<b>a)</b> Select a UISlider atom from the scene to use as a scrubber." +
                    "\n\n<b>b)</b> Trigger the <i>Clip time (s)</i> or <i>Clip time (normalized)</i> float parameters." +
                    "\n\n<b>c)</b> Scrub the <i>Clip time (s)</i> slider." +
                    "\n\nEach option controls the current audio clip played on this atom's audio source."
                );

                RebuildUISliderOptions();
                RegisterStringChooser(_scrubberChooser);
                RegisterFloat(_clipTimeFloat);
                RegisterFloat(_clipTimeNormalizedFloat);
                RegisterBool(_syncClipNameToUISliderBool);
                RegisterBool(_showTimestampsBool);

                CreateScrollablePopup(_scrubberChooser).popup.labelText.color = Color.black;
                _clipTimeSlider = CreateSlider(_clipTimeFloat, true);
                _clipTimeSlider.valueFormat = "F1";
                if(_uiListener == null || !_uiListener.isEnabled)
                {
                    _clipTimeFloat.slider = null;
                }

                CreateToggle(_syncClipNameToUISliderBool, true);
                var showTimestampsToggle = CreateToggle(_showTimestampsBool, true);

                var textField = CreateTextField(_infoString);
                textField.height = 500;
                textField.backgroundColor = Color.clear;

                _syncClipNameToUISliderBool.setCallbackFunction = value =>
                {
                    SyncSliderText();
                    showTimestampsToggle.toggle.interactable = value;
                    showTimestampsToggle.textColor = value ? Color.black : Color.gray;
                    if(!value)
                    {
                        _showTimestampsBool.val = false;
                    }
                };

            }
            catch(Exception e)
            {
                SuperController.LogError($"{nameof(AudioScrubber)}.{nameof(Init)}: {e}");
            }
        }

        void OnAtomAdded(Atom atom)
        {
            if(atom.type == "UISlider" && !_uiSliders.Contains(atom))
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
                    SuperController.LogError($"{nameof(OnScrubberSelected)}: UISlider '{option}' not found");
                    _scrubberChooser.valNoCallback = prevOption;
                    return;
                }

                var uiSliderTrigger = uiSlider.GetStorableByID("Trigger") as UISliderTrigger;
                if(uiSliderTrigger != null && uiSliderTrigger.trigger != null)
                {
                    foreach(var action in uiSliderTrigger.trigger.TransitionActions)
                    {
                        if(action.receiverTargetName == _clipTimeFloat.name)
                        {
                            SuperController.LogError($"{nameof(OnScrubberSelected)}: {uiSlider.uid} is already triggering the {_clipTimeFloat.name} parameter");
                            _scrubberChooser.valNoCallback = prevOption;
                            return;
                        }

                        if(action.receiverTargetName == _clipTimeNormalizedFloat.name)
                        {
                            SuperController.LogError($"{nameof(OnScrubberSelected)}: {uiSlider.uid} is already triggering the {_clipTimeNormalizedFloat.name} parameter");
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

                var holderTransform = uiSlider.reParentObject.transform.Find("object/rescaleObject/Canvas/Holder");
                _scrubberSlider = holderTransform.Find("Slider").GetComponent<Slider>();
                _scrubberText = holderTransform.Find("Text").GetComponent<Text>();
                _scrubberAtom = uiSlider;
                _scrubberSlider.onValueChanged.AddListener(OnSceneSliderValueChanged);
                SyncSliderText();
            }
            catch(Exception e)
            {
                SuperController.LogError($"{nameof(OnScrubberSelected)}: {e}");
            }
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

        void OnDestroy()
        {
            if(_uiListener != null)
            {
                DestroyImmediate(_uiListener);
            }

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
