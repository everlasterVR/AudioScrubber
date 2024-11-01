// License: CC BY

namespace everlaster
{
    public class RestartAudioOnUnpause : MVRScript
    {
        AudioSourceControl _audioSourceControl;
        bool _initialized;

        public override void Init()
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

            _initialized = true;
        }

        NamedAudioClip _clipOnPause;

        void OnApplicationPause(bool isPaused)
        {
            if(!_initialized)
            {
                return;
            }

            if(isPaused)
            {
                _clipOnPause = _audioSourceControl.playingClip;
            }
            else
            {
                if(_clipOnPause != null)
                {
                    _audioSourceControl.PlayNow(_clipOnPause);
                }
            }
        }
    }
}
