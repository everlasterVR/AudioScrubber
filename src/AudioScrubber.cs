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
        JSONStorableBool _syncClipNameToUISlider;
        JSONStorableString _infoString;
        UIDynamicSlider _clipTimeSlider;
        Atom _scrubberAtom;
        Slider _scrubberSlider;
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

                _syncClipNameToUISlider = new JSONStorableBool("Sync clip name to Scrubber", true);
                _syncClipNameToUISlider.setCallbackFunction = _ => SyncSliderText();

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
                RegisterBool(_syncClipNameToUISlider);

                CreateScrollablePopup(_scrubberChooser).popup.labelText.color = Color.black;
                _clipTimeSlider = CreateSlider(_clipTimeFloat, true);
                _clipTimeSlider.valueFormat = "F1";
                if(_uiListener == null || !_uiListener.isEnabled)
                {
                    _clipTimeFloat.slider = null;
                }

                CreateToggle(_syncClipNameToUISlider, true);

                var textField = CreateTextField(_infoString);
                textField.height = 500;
                textField.backgroundColor = Color.clear;

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

                var holderTransform = uiSlider.reParentObject.transform.Find("object/rescaleObject/Canvas/Holder");
                var scrubberSlider = holderTransform.Find("Slider").GetComponent<Slider>();
                if(scrubberSlider == null)
                {
                    SuperController.LogError($"{nameof(OnScrubberSelected)}: Slider component not found on UISlider '{option}'");
                    _scrubberChooser.valNoCallback = prevOption;
                    return;
                }

                if(_scrubberAtom != null)
                {
                    _scrubberSlider.onValueChanged.RemoveListener(OnSceneSliderValueChanged);
                }

                scrubberSlider.onValueChanged.AddListener(OnSceneSliderValueChanged);
                _scrubberAtom = uiSlider;
                _scrubberSlider = scrubberSlider;
                SyncSliderText();
            }
            catch(Exception e)
            {
                SuperController.LogError($"{nameof(OnScrubberSelected)}: {e}");
            }
        }

        void SyncSliderText()
        {
            if(_audioSource == null || _scrubberAtom == null || !_syncClipNameToUISlider.val)
            {
                return;
            }

            if(_audioSource.clip != null)
            {
                _scrubberAtom.GetStorableByID("Text").SetStringParamValue("text", $"Clip: {_audioSource.clip.name}");
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
                    SyncSliderText();
                }
            }
            else
            {
                if(clip != _prevClip)
                {
                    _clipTimeFloat.max = clip.length;
                    SyncSliderText();
                }

                _preventCircularCallback = true;
                _clipTimeFloat.valNoCallback = Mathf.Min(_audioSource.time, clip.length);
                float normalized = Mathf.InverseLerp(0, clip.length, _audioSource.time);
                if(_scrubberSlider != null)
                {
                    _scrubberSlider.normalizedValue = normalized;
                    _clipTimeNormalizedFloat.valNoCallback = normalized;
                }

                _preventCircularCallback = false;
            }

            _prevClip = clip;
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
