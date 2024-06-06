using System;
using UnityEngine;

namespace everlaster
{
    sealed class UnityEventsListener : MonoBehaviour
    {
        public bool isEnabled { get; private set; }
        public Action enabledHandlers;
        public Action disabledHandlers;

        void OnEnable()
        {
            isEnabled = true;
            enabledHandlers?.Invoke();
        }

        void OnDisable()
        {
            isEnabled = false;
            disabledHandlers?.Invoke();
        }
    }
}
